using Boxal.Game.Growth;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 퍼즈 화면의 "이번 판에 고른 업그레이드" 목록. 같은 업그레이드를 또 고르면
    /// 새 슬롯을 쓰지 않고 그 슬롯의 횟수만 올린다.
    /// 표시는 등급이 높은 순(Legendary → Rare → Common), 같은 등급 안에서는 획득 순서다.
    /// 슬롯을 길게 누르면 그 업그레이드의 이름·설명이 고정 위치 툴팁으로 뜬다.
    ///
    /// 슬롯 뷰는 slotsContainer의 자식들에서 자동 수집한다. 각 슬롯 구조:
    ///   Slot_Upgrade (빈 칸 배경, 항상 활성)
    ///     └ CardFrame (등급 프레임 Image, 기본 비활성 = 아직 안 채워진 칸)
    ///         ├ Icon  (업그레이드 아이콘 Image)
    ///         └ Count (획득 횟수 TMP)
    /// </summary>
    /// <remarks>
    /// 퍼즈 패널은 닫혀 있는 동안 비활성이라, 이 컴포넌트는 항상 활성인 호스트(MainPlayCanvas)에
    /// 붙이고 slotsContainer 참조로 접근한다(PauseUI/UpgradeCardUI와 동일 패턴).
    /// 비활성 오브젝트의 sprite/text를 바꿔두는 것은 문제없고, 다음에 열릴 때 그대로 보인다.
    /// </remarks>
    public class UpgradeHistoryUI : MonoBehaviour
    {
        private class Slot
        {
            public GameObject frame;
            public Image frameImage;
            public Image icon;
            public TextMeshProUGUI countText;
            /// <summary>이 슬롯이 지금 보여주는 업그레이드(빈 칸이면 null). 툴팁이 읽는다.</summary>
            public UpgradeSO upgrade;
        }

        #region Variables
        [Tooltip("슬롯들의 부모 (ScrollRect의 Content). 자식 하나가 슬롯 하나다.")]
        [SerializeField] private Transform slotsContainer;
        [Tooltip("등급별 카드 프레임 스프라이트. 인덱스 = UpgradeGrade (0:Common, 1:Rare, 2:Legendary)")]
        [SerializeField] private Sprite[] gradeFrames;

        [Header("횟수 표시")]
        [Tooltip("획득 횟수 표시 형식. {0}에 횟수가 들어간다.")]
        [SerializeField] private string countFormat = "x{0}";
        [Tooltip("체크하면 1회 획득한 업그레이드는 횟수를 표시하지 않는다(중복만 눈에 띄게).")]
        [SerializeField] private bool hideCountAtOne = true;

        [Header("툴팁 (슬롯 홀드)")]
        [Tooltip("슬롯을 길게 누르면 뜨는 설명 패널. 평소 비활성. " +
                 "PopUps 바로 아래가 아니라 퍼즈 패널 안에 두어야 한다 — " +
                 "PopUps의 직계 자식은 PopupDimController가 팝업으로 보고 Dim을 건드린다.")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI tooltipNameText;
        [SerializeField] private TextMeshProUGUI tooltipDescText;
        [Tooltip("툴팁이 뜨기까지 눌러야 하는 시간(초).")]
        [SerializeField] private float holdSeconds = 0.4f;

        private readonly List<Slot> slots = new List<Slot>();
        // 표시 순서(= UpgradeHistory.Entries의 인덱스). 매 갱신마다 다시 만든다.
        private readonly List<int> displayOrder = new List<int>();
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            CollectSlots();
        }

        private void Start()
        {
            UpgradeHistory.Changed += Refresh;
            HideTooltip();
            Refresh(); // 이미 쌓인 게 있으면(씬 재진입 등) 즉시 반영
        }

        private void OnDestroy()
        {
            UpgradeHistory.Changed -= Refresh;
        }
        #endregion

        #region Custom Methods
        private void CollectSlots()
        {
            slots.Clear();
            if (slotsContainer == null)
                return;

            foreach (Transform child in slotsContainer)
            {
                Transform frameTf = child.Find("CardFrame");
                if (frameTf == null)
                    continue; // 슬롯 규격이 아닌 자식(구분선 등)은 건너뛴다

                Transform iconTf = frameTf.Find("Icon");
                Transform countTf = frameTf.Find("Count");
                Slot slot = new Slot
                {
                    frame = frameTf.gameObject,
                    frameImage = frameTf.GetComponent<Image>(),
                    icon = iconTf != null ? iconTf.GetComponent<Image>() : null,
                    countText = countTf != null ? countTf.GetComponent<TextMeshProUGUI>() : null
                };
                slots.Add(slot);

                // 홀드 감지는 슬롯마다 컴포넌트가 필요하다. 16칸을 손으로 붙이면 빠뜨리기 쉬워
                // 여기서 없으면 붙인다(자식을 이름으로 찾아 수집하는 위 방식과 같은 결).
                // 대상은 CardFrame — 빈 칸은 이 오브젝트가 꺼져 있어 애초에 이벤트를 받지 않는다.
                UpgradeSlotHoldTrigger trigger = frameTf.GetComponent<UpgradeSlotHoldTrigger>();
                if (trigger == null)
                    trigger = frameTf.gameObject.AddComponent<UpgradeSlotHoldTrigger>();
                trigger.Configure(holdSeconds, () => ShowTooltip(slot), HideTooltip);
            }
        }

        /// <summary>슬롯을 길게 누르면 그 업그레이드의 이름·설명을 띄운다.</summary>
        private void ShowTooltip(Slot slot)
        {
            if (slot == null || slot.upgrade == null)
                return;

            if (tooltipNameText != null)
                tooltipNameText.text = slot.upgrade.displayName;
            if (tooltipDescText != null)
                tooltipDescText.text = slot.upgrade.description;
            if (tooltipPanel != null)
                tooltipPanel.SetActive(true);
        }

        private void HideTooltip()
        {
            if (tooltipPanel != null)
                tooltipPanel.SetActive(false);
        }

        /// <summary>등급이 높은 순(Legendary → Rare → Common)으로 표시 순서를 만든다.
        /// 같은 등급 안에서는 획득 순서를 유지한다(인덱스를 동점 기준으로 써서 안정 정렬).</summary>
        private void BuildDisplayOrder(IReadOnlyList<UpgradeHistory.Entry> entries)
        {
            displayOrder.Clear();
            for (int i = 0; i < entries.Count; i++)
                displayOrder.Add(i);

            displayOrder.Sort((a, b) =>
            {
                int gradeA = (int)entries[a].upgrade.grade;
                int gradeB = (int)entries[b].upgrade.grade;
                if (gradeA != gradeB)
                    return gradeB - gradeA;  // 등급 내림차순
                return a - b;                // 같은 등급이면 먼저 얻은 것이 앞
            });
        }

        /// <summary>획득 목록을 슬롯에 다시 그린다. 남는 슬롯의 CardFrame은 꺼서 빈 칸으로 남긴다.</summary>
        public void Refresh()
        {
            IReadOnlyList<UpgradeHistory.Entry> entries = UpgradeHistory.Entries;
            BuildDisplayOrder(entries);

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                bool filled = i < displayOrder.Count;

                if (slot.frame != null)
                    slot.frame.SetActive(filled);
                if (!filled)
                {
                    slot.upgrade = null;
                    continue;
                }

                UpgradeHistory.Entry entry = entries[displayOrder[i]];
                UpgradeSO up = entry.upgrade;
                slot.upgrade = up; // 툴팁이 읽는다

                if (slot.frameImage != null)
                {
                    int g = (int)up.grade;
                    if (gradeFrames != null && g >= 0 && g < gradeFrames.Length && gradeFrames[g] != null)
                        slot.frameImage.sprite = gradeFrames[g];
                }
                if (slot.icon != null)
                {
                    slot.icon.sprite = up.icon;
                    slot.icon.enabled = up.icon != null;
                }
                if (slot.countText != null)
                {
                    bool showCount = !(hideCountAtOne && entry.count <= 1);
                    slot.countText.text = showCount ? string.Format(countFormat, entry.count) : string.Empty;
                }
            }
        }
        #endregion
    }
}
