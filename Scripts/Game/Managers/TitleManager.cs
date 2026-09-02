using Boxal.Game.Audio;
using Boxal.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 타이틀(부팅) 씬 컨트롤러. 타이틀 BGM 재생과 "아무 곳이나 터치 → 홈 이동"을 담당한다.
    /// </summary>
    /// <remarks>
    /// 이 씬이 부팅 씬이므로 <see cref="SoundManager"/> 인스턴스도 여기에 있어야 한다
    /// (DontDestroyOnLoad라 홈/플레이로 넘어가도 그대로 따라간다. 이후 씬의 SoundManager는
    /// 싱글톤 중복으로 스스로 파괴되므로 씬마다 하나씩 둬도 문제없다).
    /// 입력은 EventSystem이 아니라 직접 폴링한다 — "화면 아무 데나"가 요구사항이라
    /// 전체화면 버튼을 깔면 로고·애니메이션 위에서 레이캐스트 순서를 신경 써야 하기 때문이다.
    /// 부팅 로딩(예: UGS 초기화)이 필요해지면 여기에 붙이면 된다.
    /// </remarks>
    public class TitleManager : MonoBehaviour
    {
        #region Variables
        [Header("Scene")]
        [Tooltip("터치하면 이동할 씬 이름. Build Settings에 등록돼 있어야 함.")]
        [SerializeField] private string homeSceneName = "Home";

        [Header("Input")]
        [Tooltip("씬 진입 후 이 시간(초) 동안은 입력을 무시한다. 페이드 인이 보이기도 전에 " +
                 "타이틀이 넘어가 버리는 것을 막는다.")]
        [SerializeField] private float inputIgnoreTime = 0.5f;

        /// <summary>이미 이동을 시작했는지. 연타로 사운드가 겹쳐 울리는 것을 막는다.</summary>
        private bool isLeaving;
        private float elapsed;
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            SoundManager.Instance?.PlayBgm(BgmId.Title);
        }

        private void Update()
        {
            if (isLeaving)
                return;

            // 타이틀에는 timeScale을 건드리는 요소가 없지만, 연출용 정지가 생겨도 입력이 죽지 않도록 unscaled로 센다.
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < inputIgnoreTime)
                return;

            if (!WasPressedThisFrame())
                return;

            isLeaving = true;
            SoundManager.Instance?.PlaySfx(SoundId.UiClick); // 클립이 아직 없으면 조용히 무시된다
            SceneFader.LoadScene(homeSceneName);
        }
        #endregion

        #region Custom Methods
        /// <summary>이번 프레임에 터치·클릭·키 입력이 있었는지. 연결되지 않은 장치는 null이므로 건너뛴다.</summary>
        private static bool WasPressedThisFrame()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;
            return false;
        }
        #endregion
    }
}
