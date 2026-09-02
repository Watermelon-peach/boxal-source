using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈의 스태미나 충전 팝업(HomeCanvas/RefillStaminaPopup).
    /// 광고 보상으로 스태미나를 받고, 다음 1개 / 풀충전까지 남은 시간을 보여준다.
    /// </summary>
    /// <remarks>
    /// 게임 내 텍스트는 TMP 한글 폰트가 없어 영문 + ASCII로만 쓴다.
    /// 열려 있는 동안에만 1초 틱으로 타이머를 갱신하고, 닫히면 아무 일도 하지 않는다.
    /// </remarks>
    public class RefillStaminaPopupUI : MonoBehaviour
    {
        #region Variables
        [Header("Root")]
        [Tooltip("켜고 끌 팝업 루트(RefillStaminaPopup). 비우면 이 스크립트가 붙은 오브젝트를 쓴다.")]
        [SerializeField] private GameObject root;

        [Header("Ad")]
        [Tooltip("광고 보상 버튼(Button_Ad). 지금은 광고 SDK가 없어 클릭 한 번을 시청 완료로 친다.")]
        [SerializeField] private Button adButton;
        [Tooltip("일일 한도 표시(DailyLimit). \"남은 횟수 / 하루 한도\" 형식. 예) \"5 / 5\"")]
        [SerializeField] private TextMeshProUGUI dailyLimitText;
        [Tooltip("한도를 다 쓰면 광고 버튼을 비활성으로 바꾼다.")]
        [SerializeField] private bool disableAdWhenExhausted = true;

        [Header("Timers")]
        [Tooltip("다음 1개까지 남은 시간(Timer_Next/Timer). \"m : ss\" 형식.")]
        [SerializeField] private TextMeshProUGUI nextTimerText;
        [Tooltip("풀충전까지 남은 시간(Timer_Full/Timer). \"2h 29m\" 형식.")]
        [SerializeField] private TextMeshProUGUI fullTimerText;
        [Tooltip("타이머가 멈춘 상태(가득 참/초과)에 두 타이머 자리에 표시할 문구.")]
        [SerializeField] private string fullLabel = "FULL";

        [Header("Close")]
        [SerializeField] private Button closeButton;

        private float nextTickTime;
        #endregion

        #region Properties
        /// <summary>팝업이 떠 있는지.</summary>
        public bool IsShowing => Target != null && Target.activeSelf;

        private GameObject Target => root != null ? root : gameObject;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (adButton != null)
                adButton.onClick.AddListener(OnAdClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            // static 이벤트라 반드시 짝을 맞춰 해제해야 한다.
            Stamina.Changed += Refresh;
            Refresh();
            nextTickTime = Time.unscaledTime + 1f;
        }

        private void OnDisable()
        {
            Stamina.Changed -= Refresh;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextTickTime)
                return;
            nextTickTime = Time.unscaledTime + 1f;
            RefreshTimers();
        }
        #endregion

        #region Custom Methods
        /// <summary>팝업을 연다.</summary>
        public void Show()
        {
            GameObject target = Target;
            if (target == null)
                return;

            target.SetActive(true);
            // 루트를 따로 지정한 경우 이 스크립트의 OnEnable이 안 불릴 수 있으므로 여기서도 갱신한다.
            Refresh();
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            GameObject target = Target;
            if (target != null)
                target.SetActive(false);
        }

        /// <summary>표시를 현재 상태로 맞춘다.</summary>
        public void Refresh()
        {
            if (dailyLimitText != null)
                dailyLimitText.text = $"{Stamina.AdClaimsRemaining} / {Stamina.AdDailyLimit}";

            if (adButton != null && disableAdWhenExhausted)
                adButton.interactable = Stamina.CanClaimAdReward;

            RefreshTimers();
        }

        private void RefreshTimers()
        {
            // 가득 찼거나 초과면 자연 회복이 멈춰 있어 두 타이머 모두 의미가 없다.
            bool stopped = Stamina.Current >= Stamina.Max;

            if (nextTimerText != null)
            {
                nextTimerText.text = stopped
                    ? fullLabel
                    : NumberUtil.FormatMinSecShort((float)Stamina.TimeUntilNext.TotalSeconds);
            }

            if (fullTimerText != null)
            {
                fullTimerText.text = stopped
                    ? fullLabel
                    : NumberUtil.FormatHourMin((float)Stamina.TimeUntilFull.TotalSeconds);
            }
        }

        /// <summary>
        /// 광고 보상 버튼. 지금은 SDK가 없어 클릭 자체를 시청 완료로 취급한다.
        /// 나중에 광고를 붙이면 여기서 광고를 띄우고 <b>시청 완료 콜백에서</b>
        /// <see cref="Stamina.TryClaimAdReward"/>를 부르는 형태로 바꾸면 된다.
        /// </summary>
        private void OnAdClicked()
        {
            // 성공하면 Stamina.Changed가 표시를 갱신한다. 실패(한도 소진)만 여기서 반영.
            if (!Stamina.TryClaimAdReward())
                Refresh();
        }
        #endregion
    }
}
