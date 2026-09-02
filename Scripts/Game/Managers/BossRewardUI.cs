using Boxal.Game.Growth;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 보스 처치 보상(Legendary) 패널. BossRewardManager가 제시한 단일 보상을 표시하고,
    /// 화면 아무 곳이나 터치하거나 제한시간이 지나면 보상을 확정(ClaimReward)한다.
    /// 항상 활성인 호스트(MainPlayCanvas)에 부착하고 panel 참조로 토글한다
    /// (GameOverUI/UpgradeCardUI와 동일 패턴).
    ///
    /// panel은 timeScale=0에서 표시되므로 카운트다운은 unscaled 시간을 쓴다.
    /// panel 루트에 raycastTarget인 Image가 있어야 터치 감지가 동작한다.
    /// </summary>
    public class BossRewardUI : MonoBehaviour, IPointerClickHandler
    {
        #region Variables
        [SerializeField] private GameObject panel;

        [Header("보상 표시")]
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI displayNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [Tooltip("자동 닫힘까지 남은 시간 표시")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("설정")]
        [Tooltip("자동으로 닫히기까지의 시간(초)")]
        [SerializeField] private float autoCloseSeconds = 5f;
        [Tooltip("패널이 뜨고 이 시간 동안은 터치 입력을 막는다(정신없이 플레이 중 즉시 스킵 방지)")]
        [SerializeField] private float inputBlockSeconds = 1f;

        private Coroutine countdownRoutine;
        private float openedUnscaledTime;
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            if (BossRewardManager.InstanceExist)
            {
                BossRewardManager.Instance.RewardOffered += Show;
                BossRewardManager.Instance.RewardResolved += Hide;
            }
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (BossRewardManager.InstanceExist)
            {
                BossRewardManager.Instance.RewardOffered -= Show;
                BossRewardManager.Instance.RewardResolved -= Hide;
            }
        }

        /// <summary>패널 위 아무 곳이나 터치(에디터에선 클릭)하면 보상 확정.
        /// EventSystem이 마우스/터치를 포인터 이벤트로 통합하므로 모바일 탭도 여기로 들어온다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // 뜨자마자 실수로 스킵되는 것 방지: 최초 inputBlockSeconds 동안은 무시.
            if (Time.unscaledTime - openedUnscaledTime < inputBlockSeconds)
                return;
            Claim();
        }
        #endregion

        #region Custom Methods
        private void Show(UpgradeSO reward)
        {
            if (panel != null)
                panel.SetActive(true);

            if (icon != null)
            {
                icon.sprite = reward.icon;
                icon.enabled = reward.icon != null;
            }
            if (displayNameText != null)
                displayNameText.text = reward.displayName;
            if (descriptionText != null)
                descriptionText.text = reward.description;

            openedUnscaledTime = Time.unscaledTime;
            if (countdownRoutine != null)
                StopCoroutine(countdownRoutine);
            countdownRoutine = StartCoroutine(Countdown());
        }

        private IEnumerator Countdown()
        {
            float remaining = autoCloseSeconds;
            while (remaining > 0f)
            {
                if (timerText != null)
                    timerText.text = Mathf.CeilToInt(remaining).ToString();
                remaining -= Time.unscaledDeltaTime; // timeScale=0에서도 진행
                yield return null;
            }
            countdownRoutine = null;
            Claim();
        }

        /// <summary>보상 확정(터치 또는 시간초과 공통 진입점). 중복 호출 방지 포함.</summary>
        private void Claim()
        {
            if (!BossRewardManager.InstanceExist || !BossRewardManager.Instance.IsOffering)
                return;
            // ClaimReward가 RewardResolved를 발생시켜 Hide로 이어진다.
            BossRewardManager.Instance.ClaimReward();
        }

        private void Hide()
        {
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }
            if (panel != null)
                panel.SetActive(false);
        }
        #endregion
    }
}
