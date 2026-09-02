using System;
using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈 MainPanel의 스태미나 리소스바(ResourceBars/ResourceBar_Stamina) 표시.
    /// 보유량("24 / 30")과 다음 회복까지 남은 시간을 그리고, + 버튼으로 광고 보상을 받는다.
    /// </summary>
    /// <remarks>
    /// 게임 내 텍스트는 TMP 한글 폰트가 없어 영문 + ASCII로만 쓴다.
    /// <para/>
    /// 값 변경은 <see cref="Stamina.Changed"/>로 받고, 카운트다운만 1초에 한 번 갱신한다.
    /// 매 프레임 <see cref="Stamina"/>를 조회하면 그만큼 PlayerPrefs를 읽게 된다.
    /// </remarks>
    public class StaminaBarUI : MonoBehaviour
    {
        #region Variables
        [Header("Texts")]
        [Tooltip("보유량 텍스트(Text_Value). 예) \"24 / 30\". 보상으로 최대치를 넘으면 \"38 / 30\"처럼 그대로 넘겨 보여준다.")]
        [SerializeField] private TextMeshProUGUI valueText;
        [Tooltip("풀충전까지 남은 시간(Timer). 가득 찼거나 초과 상태면 fullLabel로 대체된다. 없으면 비워도 됨.")]
        [SerializeField] private TextMeshProUGUI timerText;
        [Tooltip("타이머가 멈춘 상태(가득 참/초과)에 시간 자리에 표시할 문구.")]
        [SerializeField] private string fullLabel = "FULL";

        [Header("Refill")]
        [Tooltip("+ 버튼(Button_Add). 충전 팝업을 연다.")]
        [SerializeField] private Button addButton;
        [Tooltip("충전 팝업(RefillStaminaPopup). 비워두면 + 버튼이 광고 보상을 곧바로 지급한다.")]
        [SerializeField] private RefillStaminaPopupUI refillPopup;
        [Tooltip("팝업이 없을 때만 쓰인다. 일일 한도를 다 쓰면 + 버튼을 비활성으로 바꾼다.")]
        [SerializeField] private bool disableAddWhenExhausted = true;

        private float nextTickTime;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (addButton != null)
                addButton.onClick.AddListener(OnAddClicked);
        }

        private void OnEnable()
        {
            // static 이벤트라 반드시 짝을 맞춰 해제해야 한다(씬 전환 후에도 죽은 오브젝트가 붙잡힌다).
            Stamina.Changed += Refresh;
            // 팝업은 닫혀 있는 동안 OnEnable이 안 돌아 스스로 구독할 수 없다.
            // 바는 홈에서 항상 켜져 있으므로 여기서 받아 대신 열어준다.
            Stamina.NotEnoughForPlay += OnNotEnoughForPlay;
            Refresh();
            nextTickTime = Time.unscaledTime + 1f;
        }

        private void OnDisable()
        {
            Stamina.Changed -= Refresh;
            Stamina.NotEnoughForPlay -= OnNotEnoughForPlay;
        }

        private void Update()
        {
            // 카운트다운만 초 단위로 다시 그린다. 보유량 변경은 이벤트로 즉시 반영된다.
            if (Time.unscaledTime < nextTickTime)
                return;
            nextTickTime = Time.unscaledTime + 1f;
            RefreshTimer();
        }
        #endregion

        #region Custom Methods
        /// <summary>보유량·타이머·버튼 상태를 한꺼번에 갱신한다.</summary>
        public void Refresh()
        {
            if (valueText != null)
                valueText.text = $"{Stamina.Current} / {Stamina.Max}";

            RefreshTimer();

            // 팝업이 있으면 + 는 "팝업 열기"라 한도와 무관하게 항상 눌려야 한다.
            if (addButton != null && refillPopup == null && disableAddWhenExhausted)
                addButton.interactable = Stamina.CanClaimAdReward;
        }

        private void RefreshTimer()
        {
            if (timerText == null)
                return;

            // 다음 1개가 아니라 <b>풀충전까지</b> 남은 시간을 보여준다.
            TimeSpan remain = Stamina.TimeUntilFull;
            if (remain <= TimeSpan.Zero)
            {
                timerText.text = fullLabel;
                return;
            }

            // 표기는 충전 팝업의 FULL 타이머와 같은 "2h 29m" 형식을 쓴다(둘 다 풀충전 기준이라).
            timerText.text = NumberUtil.FormatHourMin((float)remain.TotalSeconds);
        }

        /// <summary>
        /// 광고 보상 버튼. 지금은 SDK가 없어 클릭 자체를 시청 완료로 취급한다.
        /// 나중에 광고를 붙이면 이 자리에서 광고를 띄우고, <b>시청 완료 콜백에서</b>
        /// <see cref="Stamina.TryClaimAdReward"/>를 부르는 형태로 바꾸면 된다.
        /// </summary>
        private void OnAddClicked()
        {
            if (refillPopup != null)
            {
                refillPopup.Show();
                return;
            }

            // 팝업이 없을 때의 폴백: 클릭 한 번을 광고 시청 완료로 친다.
            if (!Stamina.TryClaimAdReward())
                Refresh(); // 한도 소진 직후 버튼 상태를 즉시 반영
        }

        /// <summary>스태미나가 모자라 PLAY가 막혔을 때 충전 팝업을 띄운다.</summary>
        private void OnNotEnoughForPlay()
        {
            if (refillPopup != null)
                refillPopup.Show();
        }
        #endregion
    }
}
