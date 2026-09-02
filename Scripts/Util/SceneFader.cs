using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boxal.Util
{
    /// <summary>
    /// 씬 진입 시 검정에서 페이드 인, 씬 전환 시 페이드 아웃 후 로드한다.
    /// 씬마다 프리팹을 하나씩 배치하는 방식(DontDestroyOnLoad 아님) — 전환 시 이전 씬의 인스턴스는
    /// 파괴되고 새 씬의 인스턴스가 Start에서 페이드 인을 이어받는다.
    ///
    /// Boxal은 퍼즈/카드 선택/인트로/보스 경고에서 timeScale을 0으로 만들기 때문에
    /// 이 연출은 <b>전부 unscaled 시간</b>으로 동작해야 한다. scaled 시간을 쓰면
    /// 퍼즈 중(홈 버튼이 퍼즈 패널에 있다) 페이드 루프가 진행되지 않아 씬 전환이 영영 끝나지 않는다.
    /// </summary>
    public class SceneFader : Singleton<SceneFader>
    {
        #region Constants
        /// <summary>페이드 진행에 반영할 프레임당 최대 시간(초). 로딩 히치 한 프레임이 페이드를 통째로 삼키는 것을 막는다.</summary>
        private const float MaxDeltaStep = 1f / 30f;
        #endregion

        #region Variables
        [Header("Refs")]
        [Tooltip("화면 전체를 덮는 검정 Image.")]
        [SerializeField] private Image img;

        [Header("Fade")]
        [Tooltip("알파 곡선. x=진행도(0~1), y=알파(0=투명, 1=검정).")]
        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("씬을 떠날 때(투명→검정) 시간(초). 짧을수록 반응이 빠르게 느껴진다.")]
        [SerializeField] private float fadeOutDuration = 0.3f;
        [Tooltip("씬에 들어올 때(검정→투명) 시간(초). 도착은 여유 있게 느껴지도록 아웃보다 길게 잡는다.")]
        [SerializeField] private float fadeInDuration = 0.6f;

        [Header("Behaviour")]
        [Tooltip("씬 전환 직전 timeScale을 1로 되돌린다. 퍼즈(timeScale=0) 상태에서 씬을 로드하면 새 씬이 멈춘 채로 시작된다.")]
        [SerializeField] private bool normalizeTimeScaleOnLoad = true;

        /// <summary>현재 실행 중인 페이드. 새 페이드를 시작할 때 중단해 알파를 두 코루틴이 동시에 쓰는 것을 막는다.</summary>
        private Coroutine currentFade;

        /// <summary>씬 전환이 시작됐는지. 버튼 연타로 LoadScene이 중복 호출되는 것을 막는다.</summary>
        private bool isLoading;
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            // 씬 진입: 검정에서 시작해 걷어낸다.
            SetAlpha(1f);
            FadeStart();
        }
        #endregion

        #region Static API
        /// <summary>
        /// 페이드 아웃 후 씬을 로드한다. 페이더가 없는 씬에서도 이동이 깨지지 않도록
        /// 인스턴스가 없으면 즉시 로드로 대체한다(연출만 빠지고 동작은 동일).
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;

            if (InstanceExist)
            {
                Instance.FadeTo(sceneName);
                return;
            }

            // 폴백: 퍼즈 상태(timeScale=0)에서 넘어가면 다음 씬이 멈춘 채 시작되므로 정규화한다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
        #endregion

        #region Custom Methods
        /// <summary>페이드 인(검정 → 투명). 씬 진입 연출.</summary>
        public void FadeStart(float delayTime = 0f)
        {
            Run(FadeInRoutine(delayTime));
        }

        /// <summary>씬 이동 없이 깜빡인다(투명 → 검정 → 투명).</summary>
        public void Blink(float delayTime = 0f)
        {
            Run(BlinkRoutine(delayTime));
        }

        /// <summary>페이드 아웃 후 씬 이름으로 이동.</summary>
        public void FadeTo(string sceneName)
        {
            if (isLoading || string.IsNullOrEmpty(sceneName))
                return;
            isLoading = true;
            Run(FadeOutAndLoad(sceneName, -1));
        }

        /// <summary>페이드 아웃 후 빌드 인덱스로 이동.</summary>
        public void FadeTo(int buildIndex)
        {
            if (isLoading || buildIndex < 0)
                return;
            isLoading = true;
            Run(FadeOutAndLoad(null, buildIndex));
        }

        /// <summary>진행 중인 페이드를 중단하고 새 페이드를 시작한다(알파 동시 기록 방지).</summary>
        private void Run(IEnumerator routine)
        {
            if (currentFade != null)
                StopCoroutine(currentFade);
            currentFade = StartCoroutine(routine);
        }

        private IEnumerator FadeInRoutine(float delayTime)
        {
            if (delayTime > 0f)
                yield return new WaitForSecondsRealtime(delayTime);

            SetBlocking(true);
            yield return Fade(1f, 0f, fadeInDuration);
            // 페이드가 끝나면 반드시 입력을 통과시켜야 한다.
            // 전체화면 Image는 알파가 0이어도 raycastTarget이 켜져 있으면 모든 클릭을 계속 가로챈다.
            SetBlocking(false);
        }

        private IEnumerator BlinkRoutine(float delayTime)
        {
            SetBlocking(true);
            yield return Fade(0f, 1f, fadeOutDuration);
            yield return FadeInRoutine(delayTime);
        }

        private IEnumerator FadeOutAndLoad(string sceneName, int buildIndex)
        {
            SetBlocking(true);
            yield return Fade(0f, 1f, fadeOutDuration);

            if (normalizeTimeScaleOnLoad)
                Time.timeScale = 1f;

            if (!string.IsNullOrEmpty(sceneName))
                SceneManager.LoadScene(sceneName);
            else
                SceneManager.LoadScene(buildIndex);
        }

        /// <summary>알파를 from에서 to로 duration 동안 보간한다(unscaled).</summary>
        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 씬 로드/도메인 리로드 직후 프레임은 그 히치 시간이 통째로 deltaTime에 들어온다(실측 1.97초).
                // 그대로 누적하면 짧은 페이드가 1~2프레임 만에 끝나 하드컷처럼 보이므로 한 프레임 증가분을 제한한다.
                // 프레임이 느릴 때 페이드가 실제 시간보다 길어지지만, 연출이 보이지 않는 것보다 낫다.
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxDeltaStep);
                float progress = Mathf.Clamp01(elapsed / duration);
                // 커브는 "전환 진행도"만 나타내고 방향은 Lerp가 처리한다.
                // 진행도(x)를 뒤집는 방식은 비대칭 커브를 거꾸로 읽어 이징이 반대로 뒤바뀐다
                // (ease-in 커브가 페이드 인에서 ease-out이 되어, 앞부분에서 알파가 급락해 연출이 거의 안 보였다).
                SetAlpha(Mathf.Lerp(from, to, curve.Evaluate(progress)));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (img != null)
                img.color = new Color(0f, 0f, 0f, a);
        }

        private void SetBlocking(bool value)
        {
            if (img != null)
                img.raycastTarget = value;
        }
        #endregion
    }
}
