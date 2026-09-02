using Boxal.Game.Feedback;
using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 상점 아이템 한 칸(<c>Items/Item_*</c>). 레벨·가격·현재 수치를 그리고, 구매 버튼을
    /// 꾹 누르면 살 수 있는 만큼 연속으로 레벨을 올린다.
    /// </summary>
    /// <remarks>
    /// 표시 문구는 전부 인스펙터의 포맷 문자열이 정한다(씬에 적어둔 샘플 텍스트가 기본값이다).
    /// 아이콘·이름(<c>Icon/DisplayName</c>)은 손으로 채운 값을 그대로 두고 건드리지 않는다.
    /// <para/>
    /// 자식은 이름으로 수집한다. 규격:
    /// <code>
    /// Item_*                    (이 컴포넌트가 붙는 루트)
    ///   ├ BuyButton   (Button)  구매. 홀드 반복은 ShopBuyHoldTrigger가 자동으로 붙는다
    ///   │   ├ Level   (TMP)     "Lv. 3"
    ///   │   ├ Cost
    ///   │   │   └ Text_Cost (TMP)  "1,240" / 최대 레벨이면 "MAX"
    ///   │   └ Value   (TMP)     "3 &lt;color=#70FF70&gt;+1"
    ///   └ Icon
    ///       └ DisplayName (TMP) 고정 텍스트 — 건드리지 않음
    /// </code>
    /// <para/>
    /// ★<see cref="ShopUpgrades.Changed"/>와 <see cref="Gold.Changed"/>를 직접 구독한다.
    /// 구매 한 번에 레벨과 잔액이 같이 바뀌므로 둘 다 필요하다(가격이 매 레벨 갱신되는 것도 이 경로다).
    /// static 이벤트라 반드시 짝을 맞춰 해제한다.
    /// </remarks>
    public class ShopItemUI : MonoBehaviour
    {
        #region Variables
        [Header("대상")]
        [Tooltip("이 칸이 파는 업그레이드.")]
        [SerializeField] private ShopUpgradeId upgradeId = ShopUpgradeId.GoldPerKill;

        [Header("표기 (씬의 샘플 텍스트와 같은 양식)")]
        [Tooltip("레벨. {0}=현재 레벨.")]
        [SerializeField] private string levelFormat = "Lv. {0}";

        [Tooltip("현재 수치와 증가분. {0}=지금 적용 중인 값, {1}=한 레벨 올릴 때 오르는 값.")]
        [SerializeField] private string valueFormat = "{0} <color=#70FF70>+{1}";

        [Tooltip("최대 레벨일 때의 수치 표기. 더 오를 게 없으니 증가분을 빼고 쓴다. {0}=지금 값.")]
        [SerializeField] private string valueMaxedFormat = "{0}";

        [Tooltip("최대 레벨일 때 가격 자리에 쓸 문구. 실제로 뜨는 건 StartAttack뿐이다.")]
        [SerializeField] private string maxedCostText = "MAX";

        [Header("색")]
        [Tooltip("레벨업 가능할 때의 레벨 텍스트 색.")]
        [SerializeField] private Color levelColorAffordable = new Color32(0xFF, 0xEA, 0x75, 0xFF);

        [Tooltip("골드가 모자랄 때의 레벨 텍스트 색.")]
        [SerializeField] private Color levelColorUnaffordable = new Color32(0xFF, 0x70, 0x70, 0xFF);

        [Tooltip("가격 텍스트 기본색.")]
        [SerializeField] private Color costColorNormal = Color.white;

        [Tooltip("골드가 모자랄 때의 가격 텍스트 색.")]
        [SerializeField] private Color costColorUnaffordable = new Color32(0x4B, 0x4B, 0x4B, 0xFF);

        [Header("홀드 연속 구매")]
        [Tooltip("누른 뒤 두 번째 구매까지의 대기(초). 짧으면 한 번만 사려다 여러 번 사게 된다.")]
        [SerializeField] private float holdFirstDelay = 0.35f;
        [Tooltip("반복 시작 간격(초).")]
        [SerializeField] private float holdInterval = 0.15f;
        [Tooltip("가장 빨라졌을 때의 간격(초). 드르륵 느낌의 상한이다.")]
        [SerializeField] private float holdMinInterval = 0.045f;
        [Tooltip("한 번 살 때마다 간격에 곱하는 값(<1이면 점점 빨라진다).")]
        [Range(0.1f, 1f)][SerializeField] private float holdAccel = 0.85f;

        [Tooltip("한 레벨 올릴 때마다 울리는 진동. 연속으로 사면 드르륵으로 이어진다.")]
        [SerializeField] private HapticType purchaseHaptic = HapticType.Selection;

        private Button buyButton;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI costText;
        private TextMeshProUGUI valueText;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            CollectViews();
        }

        private void OnEnable()
        {
            ShopUpgrades.Changed += Refresh;
            Gold.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            ShopUpgrades.Changed -= Refresh;
            Gold.Changed -= Refresh;
        }
        #endregion

        #region Custom Methods
        private void CollectViews()
        {
            Transform buyTf = transform.Find("BuyButton");
            if (buyTf == null)
            {
                Debug.LogWarning($"[ShopItemUI] '{name}'에 BuyButton이 없다. 자식 이름을 확인할 것.", this);
                return;
            }

            buyButton = buyTf.GetComponent<Button>();
            levelText = FindText(buyTf, "Level");
            costText = FindText(buyTf, "Cost/Text_Cost");
            valueText = FindText(buyTf, "Value");

            // 구매 입력은 이 트리거가 전부 가져간다(Button.onClick은 쓰지 않는다 — 겹치면 두 번 산다).
            // 칸마다 하나씩 필요해서, 손으로 붙이다 빠뜨리지 않게 여기서 붙인다.
            ShopBuyHoldTrigger trigger = buyTf.GetComponent<ShopBuyHoldTrigger>();
            if (trigger == null)
                trigger = buyTf.gameObject.AddComponent<ShopBuyHoldTrigger>();
            trigger.Configure(holdFirstDelay, holdInterval, holdMinInterval, holdAccel, TryBuyOnce);
        }

        private TextMeshProUGUI FindText(Transform parent, string path)
        {
            Transform tf = parent.Find(path);
            if (tf == null)
            {
                Debug.LogWarning($"[ShopItemUI] '{name}'에서 '{path}'를 찾지 못했다.", this);
                return null;
            }
            return tf.GetComponent<TextMeshProUGUI>();
        }

        /// <summary>한 레벨 산다. 살 수 없으면 false — 홀드 반복은 이걸 보고 멈춘다.</summary>
        private bool TryBuyOnce()
        {
            if (!ShopUpgrades.TryPurchase(upgradeId))
                return false;

            HapticManager.Play(purchaseHaptic);
            // 표시 갱신은 TryPurchase가 쏘는 Changed 이벤트를 타고 들어온다.
            return true;
        }

        /// <summary>레벨·가격·수치와 색을 현재 상태에 맞춰 다시 그린다.</summary>
        public void Refresh()
        {
            int level = ShopUpgrades.GetLevel(upgradeId);
            bool maxed = ShopUpgrades.IsMaxed(upgradeId);
            bool affordable = ShopUpgrades.CanPurchase(upgradeId);

            if (levelText != null)
            {
                levelText.text = string.Format(levelFormat, level);
                // 최대 레벨은 "못 산다"가 아니라 "다 했다"라서, 골드 부족의 빨강을 쓰지 않는다.
                levelText.color = (affordable || maxed) ? levelColorAffordable : levelColorUnaffordable;
            }

            if (costText != null)
            {
                costText.text = maxed ? maxedCostText : NumberUtil.FormatComma(ShopUpgrades.GetNextCost(upgradeId));
                costText.color = (affordable || maxed) ? costColorNormal : costColorUnaffordable;
            }

            if (valueText != null)
            {
                int current = ShopUpgrades.GetCurrentValue(upgradeId);
                valueText.text = maxed
                    ? string.Format(valueMaxedFormat, current)
                    : string.Format(valueFormat, current, ShopUpgrades.GetStepValue(upgradeId));
            }

            // 버튼 자체의 색 전환(비활성 표시)에 쓴다. 실제 구매 차단은 TryBuyOnce가 한다 —
            // interactable=false여도 이 오브젝트의 포인터 핸들러는 계속 이벤트를 받기 때문이다.
            if (buyButton != null)
                buyButton.interactable = affordable;
        }
        #endregion
    }
}
