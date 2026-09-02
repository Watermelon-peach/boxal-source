using Boxal.Game.Growth;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 레벨업에서 업그레이드를 고르면 그 아이콘이 화면 중앙에 잠깐 떴다가
    /// 퍼즈 버튼으로 날아가며 작아지고 사라지는 획득 연출.
    /// </summary>
    /// <remarks>
    /// 목적이 두 가지다. (a) 방금 고른 걸 한 번 더 각인시키고,
    /// (b) <b>퍼즈를 누르면 고른 업그레이드를 다시 볼 수 있다</b>는 걸 암시한다
    /// (퍼즈의 획득 목록 = <see cref="UpgradeHistoryUI"/>). 그래서 도착 지점이 퍼즈 버튼이다.
    /// <para/>
    /// <b>씬에 배치하지 않는다.</b> 첫 호출 때 캔버스 아래에 스스로를 만들고, 그 뒤로는
    /// 껐다 켜며 재사용한다(판당 생성 1회). 씬 파일을 건드리지 않으려는 의도이기도 하다 —
    /// 이 프로젝트는 아트/레이아웃을 사용자가 잡으므로 스크립트가 씬에 오브젝트를 남기지 않는다.
    /// 나중에 테두리·글로우 같은 걸 붙이고 싶으면 <see cref="Create"/> 하나만
    /// 프리팹 <c>Instantiate</c>로 바꾸면 되고 나머지는 그대로 쓸 수 있다.
    /// <para/>
    /// <b>전부 unscaled 시간으로 돈다.</b> <see cref="UpgradeManager.SelectChoice"/>는 대기 중인
    /// 레벨업이 더 있으면 <c>timeScale=0</c>을 유지한 채 다음 카드를 바로 띄운다. scaled 시간을 쓰면
    /// 그 구간에서 연출이 그대로 얼어붙는다(SceneFader와 같은 규칙).
    /// </remarks>
    public class UpgradeFlyFx : MonoBehaviour
    {
        #region Tuning
        /// <summary>아이콘 한 변의 길이(캔버스 기준 해상도 단위).</summary>
        private const float IconSize = 180f;

        /// <summary>중앙에 튀어나오는 시간.</summary>
        private const float PopDuration = 0.16f;
        /// <summary>중앙에서 멈춰 있는 시간. 각인이 목적이라 이동보다 이 값이 중요하다.</summary>
        private const float HoldDuration = 0.34f;
        /// <summary>퍼즈 버튼까지 날아가는 시간.</summary>
        private const float FlyDuration = 0.45f;

        /// <summary>도착 시점의 크기 배율. 0에 가까울수록 "빨려들어가는" 느낌이 강해진다.</summary>
        private const float EndScale = 0.3f;
        /// <summary>이동 경로가 위로 부푸는 정도(캔버스 단위). 0이면 직선.</summary>
        private const float ArcHeight = 130f;

        /// <summary>
        /// 한 프레임에 반영할 최대 시간(초). 로딩 히치 한 프레임이 연출을 통째로 삼키는 걸 막는다.
        /// SceneFader의 같은 이름 상수와 같은 이유의 장치다.
        /// </summary>
        private const float MaxDeltaStep = 1f / 30f;

        /// <summary>팝인 크기 곡선. 0.6배에서 시작해 살짝 넘겼다가(1.12) 제자리로 앉는다.</summary>
        private static readonly AnimationCurve PopCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.62f, 1.12f), new Keyframe(1f, 1f));
        #endregion

        #region Variables
        /// <summary>현재 인스턴스. 씬이 바뀌면 파괴되고, 다음 호출에서 다시 만들어진다.</summary>
        private static UpgradeFlyFx instance;

        private RectTransform rect;
        private Image image;
        /// <summary>좌표 계산 기준이 되는 루트 캔버스.</summary>
        private RectTransform canvasRect;
        /// <summary>도착 지점(퍼즈 버튼).</summary>
        private RectTransform target;
        private Coroutine playing;
        #endregion

        #region Static API
        /// <summary>
        /// 획득 연출을 재생한다. 아이콘이 없는 업그레이드나 퍼즈 버튼을 못 찾은 상황에서는
        /// 조용히 아무것도 하지 않는다(연출만 빠지고 게임 동작은 그대로).
        /// </summary>
        public static void Play(UpgradeSO upgrade)
        {
            if (upgrade == null || upgrade.icon == null)
                return;

            UpgradeFlyFx fx = GetOrCreate();
            if (fx == null)
                return;

            fx.Begin(upgrade.icon);
        }

        /// <summary>재생 중인 연출을 즉시 중단한다(재시작 시 이전 판의 아이콘이 남지 않도록).</summary>
        public static void Stop()
        {
            if (instance == null)
                return;
            if (instance.playing != null)
            {
                instance.StopCoroutine(instance.playing);
                instance.playing = null;
            }
            instance.gameObject.SetActive(false);
        }

        /// <summary>인스턴스를 재사용하거나 처음 한 번 만든다. 만들 수 없으면 null.</summary>
        private static UpgradeFlyFx GetOrCreate()
        {
            // 씬 전환으로 파괴됐으면 Unity의 가짜 null에 걸려 여기서 다시 만들어진다.
            if (instance != null)
                return instance;

            // 도착 지점은 PauseUI가 이미 배선해 둔 퍼즈 버튼을 그대로 빌려 쓴다
            // (새 SerializeField를 만들면 씬 파일이 바뀌므로).
            PauseUI pause = FindAnyObjectByType<PauseUI>(FindObjectsInactive.Include);
            RectTransform targetRect = pause != null ? pause.PauseButtonRect : null;
            if (targetRect == null)
                return null;

            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            instance = Create(canvas.rootCanvas.transform as RectTransform, targetRect);
            return instance;
        }

        /// <summary>
        /// 연출용 오브젝트를 만든다. 프리팹으로 바꾸고 싶으면 이 메서드만 <c>Instantiate</c>로 교체하면 된다.
        /// </summary>
        private static UpgradeFlyFx Create(RectTransform parent, RectTransform targetRect)
        {
            if (parent == null)
                return null;

            var go = new GameObject("UpgradeFlyFx", typeof(RectTransform), typeof(Image));
            var fx = go.AddComponent<UpgradeFlyFx>();

            fx.rect = (RectTransform)go.transform;
            fx.rect.SetParent(parent, false);
            // 중앙 앵커 + 중앙 피벗이라 anchoredPosition이 곧 "캔버스 중앙으로부터의 거리"가 된다.
            fx.rect.anchorMin = fx.rect.anchorMax = fx.rect.pivot = new Vector2(0.5f, 0.5f);
            fx.rect.sizeDelta = new Vector2(IconSize, IconSize);

            fx.image = go.GetComponent<Image>();
            fx.image.raycastTarget = false; // 날아가는 동안 점프 입력을 가리면 안 된다
            fx.image.preserveAspect = true;

            fx.canvasRect = parent;
            fx.target = targetRect;

            go.SetActive(false);
            return fx;
        }
        #endregion

        #region Custom Methods
        private void Begin(Sprite icon)
        {
            if (playing != null)
                StopCoroutine(playing);

            image.sprite = icon;
            gameObject.SetActive(true);
            // 레벨업이 연달아 나면 다음 카드 패널이 같은 프레임에 열린다. 그 위로 보이게 올린다.
            rect.SetAsLastSibling();
            playing = StartCoroutine(Routine());
        }

        private IEnumerator Routine()
        {
            Vector2 from = Vector2.zero; // 화면 중앙
            Vector2 to = target != null
                ? (Vector2)canvasRect.InverseTransformPoint(target.position)
                : Vector2.zero;

            // 1) 등장 — 중앙에서 팝인
            rect.anchoredPosition = from;
            for (float t = 0f; t < PopDuration; t += Step())
            {
                float k = Mathf.Clamp01(t / PopDuration);
                SetVisual(PopCurve.Evaluate(k), k);
                yield return null;
            }
            SetVisual(1f, 1f);

            // 2) 각인 — 잠깐 멈춰 둔다
            yield return new WaitForSecondsRealtime(HoldDuration);

            // 3) 퍼즈 버튼으로 이동 + 축소 + 소멸
            for (float t = 0f; t < FlyDuration; t += Step())
            {
                float k = Mathf.Clamp01(t / FlyDuration);
                float ease = k * k; // 가속 — 버튼으로 빨려들어가는 느낌
                Vector2 pos = Vector2.Lerp(from, to, ease);
                pos.y += ArcHeight * Mathf.Sin(ease * Mathf.PI); // 완만한 포물선
                rect.anchoredPosition = pos;
                SetVisual(Mathf.Lerp(1f, EndScale, ease), 1f - ease);
                yield return null;
            }

            playing = null;
            gameObject.SetActive(false);
        }

        private void SetVisual(float scale, float alpha)
        {
            rect.localScale = new Vector3(scale, scale, 1f);
            Color c = image.color;
            c.a = Mathf.Clamp01(alpha);
            image.color = c;
        }

        /// <summary>이번 프레임에 반영할 시간. 히치가 나도 연출이 통째로 건너뛰지 않게 잘라낸다.</summary>
        private static float Step() => Mathf.Min(Time.unscaledDeltaTime, MaxDeltaStep);
        #endregion
    }
}
