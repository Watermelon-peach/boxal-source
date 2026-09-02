using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 퍼즈 패널 표시/숨김 및 현재 진행 상황(Best/Time/Kill) 표시.
    /// 항상 활성인 호스트(MainPlayCanvas)에 부착하고 panel 참조로 토글한다
    /// (GameOverUI/UpgradeCardUI와 동일 패턴).
    ///
    /// 포기(Quit Game)는 홈으로 바로 나가지 않고 확인 팝업을 거쳐 게임오버로 이어진다.
    /// 퍼즈 → Quit Game → 확인 팝업 → Quit → GameOver(점수 반영) → 결과 화면.
    /// 홈 이동은 결과 화면의 홈 버튼이 담당한다.
    /// </summary>
    public class PauseUI : MonoBehaviour
    {
        #region Variables
        [SerializeField] private GameObject panel;

        [Header("Buttons")]
        [Tooltip("MainPlayCanvas의 퍼즈 진입 버튼. 퍼즈 중엔 재입력 방지를 위해 비활성화한다.")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button closeButton;
        [Tooltip("이번 판 포기 버튼. 바로 나가지 않고 확인 팝업을 띄운다.")]
        [SerializeField] private Button quitGameButton;

        [Header("Quit Confirm")]
        [Tooltip("종료 확인 팝업 루트(PopUps 아래). 평소 비활성. " +
                 "확인하면 지금까지의 점수로 게임오버 처리되고 결과 화면으로 이어진다.")]
        [SerializeField] private GameObject quitConfirmPopup;
        [Tooltip("확인(포기) 버튼.")]
        [SerializeField] private Button quitConfirmButton;
        [Tooltip("취소 버튼. 퍼즈 화면으로 돌아간다.")]
        [SerializeField] private Button quitCancelButton;

        [Header("Scores (현재 진행 상황 실시간 표시)")]
        [SerializeField] private TextMeshProUGUI bestText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI killText;

        [Header("Settings Toggles (선택)")]
        [Tooltip("사운드 on/off. 켜짐 = 소리 남. 볼륨 슬라이더 값은 건드리지 않고 SoundManager.Muted만 뒤집는다. " +
                 "이 음소거는 이번 플레이 한정이라 홈으로 나가면 자동 해제된다.")]
        [SerializeField] private Toggle soundToggle;
        [Tooltip("진동 on/off. HapticManager.Enabled와 연결된다.")]
        [SerializeField] private Toggle hapticToggle;
        #endregion

        #region Properties
        /// <summary>
        /// 퍼즈 진입 버튼의 RectTransform. 업그레이드 획득 연출(<see cref="UpgradeFlyFx"/>)이
        /// 도착 지점으로 쓴다. 새 참조를 만들지 않고 이미 배선된 버튼을 그대로 빌려주는 것이라
        /// 씬 파일이 바뀌지 않는다.
        /// </summary>
        public RectTransform PauseButtonRect =>
            pauseButton != null ? pauseButton.transform as RectTransform : null;

        /// <summary>소리가 나는 상태인지(= 음소거가 아님). SoundManager가 없으면 켜진 것으로 본다.</summary>
        private static bool SoundOn => !(SoundManager.InstanceExist && SoundManager.Instance.Muted);

        /// <summary>퍼즈 패널이 떠 있는지.</summary>
        private bool IsPanelOpen => panel != null && panel.activeSelf;

        /// <summary>종료 확인 팝업이 떠 있는지.</summary>
        private bool IsQuitConfirmOpen => quitConfirmPopup != null && quitConfirmPopup.activeSelf;

        /// <summary>
        /// 지금 퍼즈를 걸어도 되는지. 뒤로가기 키는 버튼과 달리 화면을 가린 패널을 무시하고 들어오므로
        /// 여기서 직접 막아야 한다.
        /// ★특히 레벨업 카드/보스 보상이 떠 있을 때가 위험하다. 그 둘은 이미 timeScale=0으로
        /// 게임을 세워둔 상태라, 그 위에 퍼즈를 열었다 닫으면 Hide()가 timeScale을 1로 되돌려
        /// 선택 중인데 게임이 다시 굴러간다.
        /// </summary>
        private bool CanPause
        {
            get
            {
                if (panel == null)
                    return false;
                if (GameManager.InstanceExist && GameManager.Instance.IsGameOver)
                    return false;
                // 인트로 연출 등에서 퍼즈 버튼을 잠가둔 구간은 뒤로가기도 동일하게 막는다.
                if (pauseButton != null && !pauseButton.interactable)
                    return false;
                if (UpgradeManager.InstanceExist && UpgradeManager.Instance.IsChoosing)
                    return false;
                if (BossRewardManager.InstanceExist && BossRewardManager.Instance.IsOffering)
                    return false;
                return true;
            }
        }
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (pauseButton != null)
                pauseButton.onClick.AddListener(Show);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (quitGameButton != null)
                quitGameButton.onClick.AddListener(ShowQuitConfirm);
            if (quitConfirmButton != null)
                quitConfirmButton.onClick.AddListener(ConfirmQuit);
            if (quitCancelButton != null)
                quitCancelButton.onClick.AddListener(CancelQuit);
        }

        private void Start()
        {
            // 토글 연결은 Start에서 한다. SoundManager는 홈에서 넘어온 DontDestroyOnLoad 인스턴스라
            // Awake 시점에는 아직 없을 수 있다.
            SetupToggles();
            if (quitConfirmPopup != null)
                quitConfirmPopup.SetActive(false);
            Hide();
        }

        private void Update()
        {
            if (!WasBackPressedThisFrame())
                return;

            // 확인 팝업이 떠 있으면 뒤로가기는 "취소"다(게임 재개로 새지 않게 먼저 잡는다).
            if (IsQuitConfirmOpen)
            {
                CancelQuit();
                return;
            }

            // 안드로이드 관례: 열려 있으면 닫고, 닫혀 있으면 연다.
            if (IsPanelOpen)
                Hide();
            else if (CanPause)
                Show();
        }
        #endregion

        #region Custom Methods
        /// <summary>
        /// 안드로이드 뒤로가기 키가 눌렸는지. Input System은 안드로이드 백 버튼을 키보드 Escape로 전달한다
        /// (에디터에서도 Esc로 같은 동작을 확인할 수 있다).
        /// 키 입력이 한 번도 없었으면 Keyboard 디바이스가 없을 수 있어 null을 확인한다.
        /// </summary>
        private static bool WasBackPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }

        private void SetupToggles()
        {
            ToggleBinding.Bind(soundToggle, SoundOn, value =>
            {
                if (SoundManager.InstanceExist)
                    SoundManager.Instance.Muted = !value; // 토글 켜짐 = 소리 남
            });

            ToggleBinding.Bind(hapticToggle, HapticManager.Enabled, value =>
            {
                HapticManager.Enabled = value;
                if (value)
                    HapticManager.Play(HapticType.Selection); // 켠 직후 1회(실기기에서만 느껴진다)
            });
        }

        /// <summary>퍼즈 패널을 열고 게임을 정지한다.</summary>
        public void Show()
        {
            if (panel != null)
                panel.SetActive(true);
            if (pauseButton != null)
                pauseButton.interactable = false;

            // 홈 설정 패널에서 값이 바뀐 뒤 들어올 수 있으므로 열 때마다 현재 값으로 맞춘다.
            ToggleBinding.SetWithoutNotify(soundToggle, SoundOn);
            ToggleBinding.SetWithoutNotify(hapticToggle, HapticManager.Enabled);

            if (bestText != null)
                bestText.text = $"Round {PlayerStats.BestRound}";
            if (timeText != null)
                timeText.text = NumberUtil.FormatMinSec(GameManager.Instance.RunElapsedSeconds);
            if (killText != null)
                killText.text = GameManager.Instance.RunKills.ToString();

            Time.timeScale = 0f;
        }

        /// <summary>퍼즈 패널을 닫고 게임을 재개한다.</summary>
        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
            if (pauseButton != null)
                pauseButton.interactable = true;

            Time.timeScale = 1f;
        }

        /// <summary>패널 표시만 켜고 끈다. <see cref="Hide"/>와 달리 timeScale과 퍼즈 버튼은 건드리지 않는다
        /// — 확인 팝업으로 오갈 때 게임이 다시 흐르면 안 되기 때문.</summary>
        private void SetPanelVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        /// <summary>종료 확인 팝업을 띄운다.
        /// ★퍼즈 패널은 잠시 감춘다 — 공용 Dim(<see cref="PopupDimController"/>)이 "동시에 뜨는 팝업은 하나"를
        /// 전제로 첫 번째 활성 자식의 농도를 쓰는데, 퍼즈는 농도 0이라 겹쳐 두면 확인창 뒤가 안 어두워진다.</summary>
        private void ShowQuitConfirm()
        {
            SetPanelVisible(false);
            if (quitConfirmPopup != null)
                quitConfirmPopup.SetActive(true);
        }

        /// <summary>종료를 취소하고 퍼즈 화면으로 돌아간다.</summary>
        private void CancelQuit()
        {
            if (quitConfirmPopup != null)
                quitConfirmPopup.SetActive(false);
            SetPanelVisible(true);
        }

        /// <summary>이번 판을 포기한다. 홈으로 바로 나가지 않고 <see cref="GameManager.GameOver"/>를 태워
        /// 지금까지의 라운드·처치·점수가 결과와 기록에 반영되게 한다
        /// (라운드가 끝나지 않아 스스로는 죽지 않는 상황에서도 성적을 남기기 위함).
        /// 결과 화면에서 재시작/홈으로 이어진다.</summary>
        private void ConfirmQuit()
        {
            if (quitConfirmPopup != null)
                quitConfirmPopup.SetActive(false);
            SetPanelVisible(false);

            // GameOver가 timeScale=0 유지, 퍼즈 버튼 잠금, 결과 패널 표시까지 처리한다(재진입도 막혀 있다).
            if (GameManager.InstanceExist)
                GameManager.Instance.GameOver();
        }

        /// <summary>퍼즈 버튼 자체를 켜고 끈다. 인트로 카메라 연출 등 퍼즈가 불가능해야 하는 구간에서 사용.</summary>
        public void SetPauseButtonInteractable(bool interactable)
        {
            if (pauseButton != null)
                pauseButton.interactable = interactable;
        }
        #endregion
    }
}
