using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈 MainPanel의 골드 리소스바(ResourceBars/ResourceBar_Golds) 표시.
    /// 보유 골드를 그리고, + 버튼으로 상점 페이지로 이동한다.
    /// </summary>
    /// <remarks>
    /// 게임 내 텍스트는 TMP 한글 폰트가 없어 영문 + ASCII로만 쓴다.
    /// 골드는 시간에 따라 변하지 않으므로 <see cref="Gold.Changed"/>를 받을 때만 다시 그린다(틱 없음).
    /// </remarks>
    public class GoldBarUI : MonoBehaviour
    {
        #region Variables
        [Header("Text")]
        [Tooltip("보유 골드 텍스트(Text_Value).")]
        [SerializeField] private TextMeshProUGUI valueText;
        [Tooltip("켜면 \"999.9K\"처럼 줄여서, 끄면 \"999,900\"처럼 전부 표기한다. " +
                 "상점 가격과 비교하기 좋은 건 전체 표기 쪽이다.")]
        [SerializeField] private bool abbreviate = true;

        [Header("Shop")]
        [Tooltip("+ 버튼(Button_Add). 상점 페이지로 이동한다.")]
        [SerializeField] private Button addButton;
        [Tooltip("이동할 페이저. 비워두면 + 버튼이 비활성으로 남는다(상점 패널을 만들기 전 상태).")]
        [SerializeField] private UiPager pager;
        [Tooltip("상점 페이지 인덱스. 페이저를 비워두면 무시된다.")]
        [SerializeField] private int shopPageIndex = 3;

        [Tooltip("페이저가 없을 때 + 버튼을 비활성(회색)으로 표시할지. " +
                 "끄면 눌러도 아무 일이 없는 장식용 버튼으로 남는다 — 상점 안의 골드바처럼 " +
                 "이동할 곳이 없는 게 당연한 자리에서 쓴다.")]
        [SerializeField] private bool dimAddButtonWithoutPager = true;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (addButton != null)
                addButton.onClick.AddListener(OnAddClicked);
        }

        private void OnEnable()
        {
            // static 이벤트라 반드시 짝을 맞춰 해제해야 한다.
            Gold.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Gold.Changed -= Refresh;
        }
        #endregion

        #region Custom Methods
        /// <summary>보유 골드를 표시에 반영한다.</summary>
        public void Refresh()
        {
            if (valueText != null)
            {
                long gold = Gold.Balance;
                valueText.text = abbreviate ? NumberUtil.FormatNumber(gold) : NumberUtil.FormatComma(gold);
            }

            // 상점이 아직 없으면 눌러도 갈 데가 없다. 죽은 버튼으로 두느니 비활성으로 표시한다.
            // 단 상점 안의 골드바처럼 "갈 데가 없는 게 정상"인 자리에서는 회색이 오히려 고장으로 보이므로,
            // dimAddButtonWithoutPager를 꺼서 평소 모습 그대로 두고 눌러도 아무 일이 없게 한다.
            if (addButton != null && dimAddButtonWithoutPager)
                addButton.interactable = pager != null;
        }

        private void OnAddClicked()
        {
            if (pager != null)
                pager.GoToPage(shopPageIndex);
        }
        #endregion
    }
}
