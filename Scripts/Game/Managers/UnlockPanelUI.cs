using System.Collections;
using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈의 업그레이드 해금 패널. 누적 처치 수와 해금 현황(n / m)을 표시하고, 카드들의 잠금/해금
    /// 상태를 갱신한 뒤, <b>가장 최근에 해금된 카드</b>가 보이도록 스크롤을 맞춘다.
    /// </summary>
    /// <remarks>
    /// "가장 최근에 해금된 카드" = 해금된 것 중 <c>unlockKills</c>가 가장 큰 카드다.
    /// 해금은 누적 처치 수 순서대로 풀리므로, 요구치가 가장 높은 해금 카드가 곧 마지막에 뚫은 것이다.
    /// (씬의 카드 배치도 위로 갈수록 요구치가 높지만, 순서를 나중에 바꿔도 되도록 계산은 값으로 한다.)
    /// <para/>
    /// 아직 하나도 안 풀렸으면 <b>맨 아래</b>로 내린다 — 거기가 가장 가까운 다음 목표라서,
    /// 신규 유저에게 먼 미래의 잠긴 카드부터 보여주는 것보다 낫다.
    /// <para/>
    /// 누적 처치 수는 홈에 있는 동안 변하지 않는다(플레이 씬에서만 는다). 그래서 매 프레임 갱신하지 않고
    /// 시작할 때와 <see cref="HomeManager"/>가 이 페이지로 들어왔다고 알려줄 때만 다시 그린다.
    /// </remarks>
    public class UnlockPanelUI : MonoBehaviour
    {
        #region Variables
        [Header("Refs")]
        [Tooltip("카드 목록 스크롤뷰(PagerFriendlyScrollRect). Content 아래의 UnlockCardUI를 전부 모은다.")]
        [SerializeField] private ScrollRect scrollRect;

        [Tooltip("누적 처치 수 텍스트(Title/CurrentKill).")]
        [SerializeField] private TextMeshProUGUI currentKillText;

        [Tooltip("해금 현황 텍스트(CurrentUnlocked).")]
        [SerializeField] private TextMeshProUGUI currentUnlockedText;

        [Header("표시")]
        [Tooltip("해금 현황 표기. {0}=해금된 수, {1}=전체 수. TMP 한글 폰트가 없어 영문 + ASCII로만 쓸 것.")]
        [SerializeField] private string unlockedFormat = "{0} / {1} Unlocked";

        [Tooltip("켜면 \"999.9K\"처럼 줄여서, 끄면 \"999,900\"처럼 전부 표기한다.")]
        [SerializeField] private bool abbreviateKills = true;

        [Tooltip("최근 해금 카드를 뷰포트의 어디에 맞출지. 0=맨 위, 0.5=가운데, 1=맨 아래. " +
                 "가운데면 바로 위(다음 목표)와 아래(이미 딴 것)가 같이 보인다.")]
        [Range(0f, 1f)][SerializeField] private float targetViewportAnchor = 0.5f;

        private UnlockCardUI[] cards;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            // 비활성 카드도 포함해야 한다(해금 전 Card/Label이 꺼져 있는 것과는 별개로, 루트는 항상 켜져 있지만
            // 패널 전체가 꺼진 채 시작할 수도 있다).
            if (scrollRect != null && scrollRect.content != null)
                cards = scrollRect.content.GetComponentsInChildren<UnlockCardUI>(true);
            else
                cards = GetComponentsInChildren<UnlockCardUI>(true);
        }

        private void Start()
        {
            Refresh();
            // 첫 프레임에는 VerticalLayoutGroup/ContentSizeFitter가 아직 크기를 못 잡아 스크롤 계산이 어긋날 수
            // 있다. 표시는 위에서 이미 맞췄고, 스크롤만 한 프레임 뒤에 다시 잡는다.
            StartCoroutine(ScrollNextFrame());
        }
        #endregion

        #region Custom Methods
        private IEnumerator ScrollNextFrame()
        {
            yield return null;
            ScrollToLatestUnlocked();
        }

        /// <summary>누적 처치 수 표시와 카드 상태를 다시 그리고, 최근 해금 카드로 스크롤한다.</summary>
        public void Refresh()
        {
            if (currentKillText != null)
            {
                long kills = PlayerStats.TotalKills;
                currentKillText.text = abbreviateKills ? NumberUtil.FormatNumber(kills) : NumberUtil.FormatComma(kills);
            }

            int unlocked = 0;
            int total = 0;
            if (cards != null)
            {
                foreach (UnlockCardUI card in cards)
                {
                    if (card == null)
                        continue;
                    card.Refresh();
                    total++;
                    if (card.IsUnlocked)
                        unlocked++;
                }
            }

            // 진행도는 UpgradeCatalog(13개)가 아니라 이 패널의 카드 수로 센다.
            // 카탈로그에는 처음부터 풀려 있는 것(unlockKills 0)도 들어 있어서 "5 / 13"으로 표시하면
            // 화면에 보이는 카드는 8장인데 분모가 13이라 세어보는 사람이 헷갈린다.
            if (currentUnlockedText != null)
                currentUnlockedText.text = string.Format(unlockedFormat, unlocked, total);

            ScrollToLatestUnlocked();
        }

        /// <summary>해금된 것 중 요구치가 가장 높은 카드로 스크롤한다. 하나도 없으면 맨 아래로.</summary>
        public void ScrollToLatestUnlocked()
        {
            if (scrollRect == null || cards == null)
                return;

            UnlockCardUI target = null;
            foreach (UnlockCardUI card in cards)
            {
                if (card == null || !card.IsUnlocked)
                    continue;
                if (target == null || card.UnlockKills > target.UnlockKills)
                    target = card;
            }

            scrollRect.StopMovement(); // 관성이 남아 있으면 맞춰놓은 위치가 다시 흘러간다

            if (target == null)
            {
                scrollRect.verticalNormalizedPosition = 0f; // 아직 아무것도 안 풀림 → 가장 가까운 목표(맨 아래)
                return;
            }

            ScrollTo(target.transform as RectTransform);
        }

        /// <summary>아이템의 중앙이 뷰포트의 <see cref="targetViewportAnchor"/> 위치에 오도록 스크롤한다.</summary>
        private void ScrollTo(RectTransform item)
        {
            RectTransform content = scrollRect.content;
            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.transform as RectTransform;
            if (item == null || content == null || viewport == null)
                return;

            // 레이아웃이 아직 안 잡혔으면 높이가 0이라 계산이 통째로 어긋난다.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            float scrollable = content.rect.height - viewport.rect.height;
            if (scrollable <= 0f)
                return; // 목록이 한 화면에 다 들어옴 — 스크롤할 것이 없다

            // content 위쪽 끝에서 아이템 중앙까지의 거리(아래로 갈수록 커진다).
            // 피벗이 제각각이어도 맞도록 rect.center를 거쳐 좌표를 변환한다.
            Vector3 itemCenterLocal = content.InverseTransformPoint(item.TransformPoint(item.rect.center));
            float itemCenterFromTop = content.rect.yMax - itemCenterLocal.y;

            float scrolled = Mathf.Clamp(itemCenterFromTop - viewport.rect.height * targetViewportAnchor,
                                         0f, scrollable);
            scrollRect.verticalNormalizedPosition = 1f - scrolled / scrollable;
        }
        #endregion
    }
}
