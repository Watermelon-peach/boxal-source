using Boxal.Game.Growth;
using Boxal.Util;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 레벨업 시 UpgradeManager가 제시한 업그레이드 카드를 패널로 보여주고,
    /// 카드를 누르면 선택을 확정한다. (숫자키 임시 선택을 대체)
    /// 카드 뷰는 cardsContainer의 자식들에서 자동 수집한다
    /// (각 카드: 자신에 Button + Image(등급 배경), 자식 "DisplayName"/"Description"/"Grade" TMP, "Icon" Image).
    ///
    /// 자동 선택이 켜져 있으면 카드를 그대로 보여주되 제한시간 뒤에 <see cref="AutoSelectManager"/>가
    /// 고른 카드로 자동 확정한다. 카운트다운 중에 토글을 끄면 취소되고 평소처럼 직접 고르게 된다
    /// (즉 "자동으로 넘어가기 직전에 개입할 수 있는" 구조다).
    /// </summary>
    /// <remarks>
    /// 패널이 떠 있는 동안은 timeScale=0이므로 카운트다운은 반드시 unscaled 시간을 쓴다
    /// (<see cref="BossRewardUI"/>의 자동 닫힘과 같은 규칙).
    /// </remarks>
    public class UpgradeCardUI : MonoBehaviour
    {
        private class Card
        {
            public GameObject root;
            public Button button;
            public Image background;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI descText;
            public TextMeshProUGUI gradeText;
            public Image icon;
        }

        #region Variables
        [SerializeField] private GameObject panel;
        [Tooltip("카드 GameObject들의 부모 (자식 = 카드)")]
        [SerializeField] private Transform cardsContainer;
        [Tooltip("등급별 카드 배경 스프라이트. 인덱스 = UpgradeGrade (0:Common, 1:Rare, 2:Legendary)")]
        [SerializeField] private Sprite[] gradeBackgrounds;

        [Header("자동 선택")]
        [Tooltip("자동 선택 on/off 토글. 켜면 아래 제한시간 뒤 자동 확정된다. " +
                 "카운트다운 중에 끄면 그 자리에서 취소되고 직접 고를 수 있다.")]
        [SerializeField] private Toggle autoSelectToggle;
        [Tooltip("토글 라벨. 자동이 켜져 있는 동안 남은 시간을 여기에 표시한다. " +
                 "비워두면 토글 자식의 \"Label\"을 자동으로 찾는다.")]
        [SerializeField] private TextMeshProUGUI autoSelectLabel;
        [Tooltip("자동 확정까지의 시간(초).")]
        [SerializeField] private float autoSelectSeconds = 3f;
        [Tooltip("카운트다운 표시 형식. {0}에 남은 초가 들어간다.")]
        [SerializeField] private string autoSelectFormat = "Auto Select ({0}s)";

        private readonly List<Card> cards = new List<Card>();
        private Coroutine autoSelectRoutine;
        // 자동이 꺼져 있을 때 되돌릴 라벨 문구(씬에 적어둔 원본을 그대로 쓴다).
        private string autoSelectIdleLabel;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            CollectCards();
            CollectAutoSelectLabel();
        }

        private void Start()
        {
            if (UpgradeManager.InstanceExist)
            {
                UpgradeManager.Instance.ChoicesOffered += Show;
                UpgradeManager.Instance.ChoiceResolved += Hide;
            }
            SetupAutoSelectToggle();
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (UpgradeManager.InstanceExist)
            {
                UpgradeManager.Instance.ChoicesOffered -= Show;
                UpgradeManager.Instance.ChoiceResolved -= Hide;
            }
        }
        #endregion

        #region Custom Methods
        private void CollectCards()
        {
            cards.Clear();
            if (cardsContainer == null)
                return;

            foreach (Transform child in cardsContainer)
            {
                Transform nameTf = child.Find("DisplayName");
                Transform descTf = child.Find("Description");
                Transform gradeTf = child.Find("Grade");
                Transform iconTf = child.Find("Icon");
                cards.Add(new Card
                {
                    root = child.gameObject,
                    button = child.GetComponent<Button>(),
                    background = child.GetComponent<Image>(),
                    nameText = nameTf != null ? nameTf.GetComponent<TextMeshProUGUI>() : null,
                    descText = descTf != null ? descTf.GetComponent<TextMeshProUGUI>() : null,
                    gradeText = gradeTf != null ? gradeTf.GetComponent<TextMeshProUGUI>() : null,
                    icon = iconTf != null ? iconTf.GetComponent<Image>() : null
                });
            }
        }

        /// <summary>토글 라벨을 확보하고 원래 문구를 기억해둔다(자동을 껐을 때 되돌릴 문구).</summary>
        private void CollectAutoSelectLabel()
        {
            if (autoSelectLabel == null && autoSelectToggle != null)
            {
                Transform labelTf = autoSelectToggle.transform.Find("Label");
                if (labelTf != null)
                    autoSelectLabel = labelTf.GetComponent<TextMeshProUGUI>();
            }
            if (autoSelectLabel != null)
                autoSelectIdleLabel = autoSelectLabel.text;
        }

        /// <summary>자동 선택 토글을 연결한다. 패널이 떠 있는 중에 켜면 그 자리에서 카운트다운이 시작되고,
        /// 끄면 즉시 취소돼 직접 고를 수 있다.</summary>
        private void SetupAutoSelectToggle()
        {
            ToggleBinding.Bind(autoSelectToggle, AutoSelectManager.Enabled, value =>
            {
                AutoSelectManager.Enabled = value;

                if (!value)
                {
                    StopAutoSelect();
                    return;
                }
                // 카드가 떠 있는 동안 켰다면 지금 것부터 카운트다운을 건다.
                if (panel != null && panel.activeSelf)
                    StartAutoSelect();
            });
        }

        private void StartAutoSelect()
        {
            StopAutoSelect();
            autoSelectRoutine = StartCoroutine(AutoSelectCountdown());
        }

        /// <summary>카운트다운을 멈추고 라벨을 원래 문구로 되돌린다.</summary>
        private void StopAutoSelect()
        {
            if (autoSelectRoutine != null)
            {
                StopCoroutine(autoSelectRoutine);
                autoSelectRoutine = null;
            }
            if (autoSelectLabel != null)
                autoSelectLabel.text = autoSelectIdleLabel;
        }

        /// <summary>제한시간이 지나면 <see cref="AutoSelectManager"/>가 고른 카드로 확정한다.
        /// 패널 표시 중에는 timeScale=0이라 unscaled 시간으로 센다.</summary>
        private IEnumerator AutoSelectCountdown()
        {
            float remaining = autoSelectSeconds;
            while (remaining > 0f)
            {
                if (autoSelectLabel != null)
                    autoSelectLabel.text = string.Format(autoSelectFormat, Mathf.CeilToInt(remaining));
                yield return null;
                remaining -= Time.unscaledDeltaTime;
            }
            autoSelectRoutine = null;

            if (!UpgradeManager.InstanceExist)
                yield break;

            // SelectChoice → ChoiceResolved → Hide 순으로 이어져 패널이 닫힌다.
            // 대기 중인 카드가 더 있으면 UpgradeManager가 다음 장을 띄우고, Show가 다시 카운트다운을 건다.
            int index = AutoSelectManager.PickBest(UpgradeManager.Instance.CurrentChoices);
            if (index >= 0)
                UpgradeManager.Instance.SelectChoice(index);
        }

        private void Show(IReadOnlyList<UpgradeSO> choices)
        {
            if (panel != null)
                panel.SetActive(true);

            // 퍼즈 팝업/홈 설정에서 값이 바뀐 뒤 열릴 수 있으므로 열 때마다 현재 값으로 맞춘다.
            ToggleBinding.SetWithoutNotify(autoSelectToggle, AutoSelectManager.Enabled);

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                bool active = i < choices.Count;
                if (card.root != null)
                    card.root.SetActive(active);
                if (!active)
                    continue;

                UpgradeSO up = choices[i];
                if (card.nameText != null)
                    card.nameText.text = up.displayName;
                if (card.descText != null)
                    card.descText.text = up.description;
                if (card.gradeText != null)
                    card.gradeText.text = up.grade.ToString();
                if (card.icon != null)
                {
                    card.icon.sprite = up.icon;
                    card.icon.enabled = up.icon != null;
                }
                if (card.background != null)
                {
                    int g = (int)up.grade;
                    if (gradeBackgrounds != null && g >= 0 && g < gradeBackgrounds.Length && gradeBackgrounds[g] != null)
                        card.background.sprite = gradeBackgrounds[g];
                }

                int index = i; // 클로저 캡처용 지역 복사
                if (card.button != null)
                {
                    card.button.onClick.RemoveAllListeners();
                    card.button.onClick.AddListener(() => UpgradeManager.Instance.SelectChoice(index));
                }
            }

            // 카드를 다 채운 뒤에 건다 — 카운트다운이 만료되면 곧바로 확정으로 이어지기 때문.
            if (AutoSelectManager.Enabled)
                StartAutoSelect();
            else
                StopAutoSelect();
        }

        private void Hide()
        {
            StopAutoSelect();
            if (panel != null)
                panel.SetActive(false);
        }
        #endregion
    }
}
