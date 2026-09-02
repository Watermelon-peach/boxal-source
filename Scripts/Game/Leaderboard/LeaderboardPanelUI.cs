using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 리더보드 패널: LeaderboardManager에서 상위 점수를 받아 RecordCard 클론으로 채운다.
    /// 로딩/빈/오프라인 상태를 statusText로 표시하고, 실패 시 캐시 → 로컬 최고점으로 폴백한다.
    /// UGS는 전혀 모르고 LeaderboardManager 파사드만 호출한다.
    /// </summary>
    public class LeaderboardPanelUI : MonoBehaviour
    {
        #region Variables
        [Header("List")]
        [Tooltip("프리팹화한 RecordCard (RecordCardUI 부착본).")]
        [SerializeField] private RecordCardUI recordCardPrefab;
        [Tooltip("카드가 생성될 부모 (ScrollView/ViewPort/Content).")]
        [SerializeField] private Transform content;
        [Tooltip("가져올 상위 인원 수.")]
        [SerializeField] private int fetchCount = 20;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private string loadingMessage = "Loading...";
        [SerializeField] private string emptyMessage = "No records yet";
        [SerializeField] private string offlineMessage = "Offline";

        [Header("My Rank (선택)")]
        [Tooltip("내 순위를 상단/하단에 고정 표시할 카드(top N 밖이어도 보이게). 없으면 비워도 됨.")]
        [SerializeField] private RecordCardUI myRankCard;

        [Header("Behaviour")]
        [Tooltip("패널이 활성화될 때 자동 새로고침. 페이저(항상 활성) 구조면 끄고 HomeManager에서 Refresh()를 호출할 것.")]
        [SerializeField] private bool autoRefreshOnEnable = true;

        private bool isLoading;
        private bool hasLoadedOnce;
        #endregion

        #region Unity Event Methods
        private void OnEnable()
        {
            if (autoRefreshOnEnable && !hasLoadedOnce)
                Refresh();
        }
        #endregion

        #region Custom Methods
        /// <summary>리더보드를 새로 가져와 채운다. 탭 전환(지연 로딩) 시 HomeManager에서 호출.</summary>
        public async void Refresh()
        {
            if (isLoading)
                return;

            if (!LeaderboardManager.InstanceExist)
            {
                Populate(System.Array.Empty<LeaderboardRow>());
                SetStatus(offlineMessage);
                BindLocalBestToRankBar();
                return;
            }

            isLoading = true;
            SetStatus(loadingMessage);

            var mgr = LeaderboardManager.Instance;
            IReadOnlyList<LeaderboardRow> rows = await mgr.GetTopScoresAsync(fetchCount);

            // 실패/빈 결과면 마지막 캐시로 폴백(오프라인 재진입 시 즉시 표시).
            if (rows.Count == 0 && mgr.CachedTop.Count > 0)
                rows = mgr.CachedTop;

            Populate(rows);

            if (rows.Count > 0)
                SetStatus(null); // 리스트가 있으면 상태문구 숨김
            else
                SetStatus(mgr.IsReady ? emptyMessage : offlineMessage); // 오프라인이어도 최고기록은 MyRankBar로

            if (myRankCard != null)
                await RefreshMyRankBar(mgr);

            hasLoadedOnce = true;
            isLoading = false;
        }

        /// <summary>내 순위 바: 온라인이면 서버 순위, 없으면 로컬 최고점으로 폴백.</summary>
        private async System.Threading.Tasks.Task RefreshMyRankBar(LeaderboardManager mgr)
        {
            if (mgr.IsReady)
            {
                var mine = await mgr.GetMyRankAsync();
                if (mine.HasValue)
                {
                    myRankCard.gameObject.SetActive(true);
                    myRankCard.Bind(mine.Value);
                    return;
                }
            }
            // 온라인 순위가 없으면(오프라인/미기록) 로컬 최고점으로 폴백.
            BindLocalBestToRankBar();
        }

        /// <summary>로컬 최고점을 MyRankBar에 표시(rank 0 → RecordCardUI가 "-"). 기록 없으면 숨김.</summary>
        private void BindLocalBestToRankBar()
        {
            if (myRankCard == null)
                return;

            long best = PlayerStats.BestScore;
            if (best > 0)
            {
                string name = string.IsNullOrEmpty(PlayerStats.PlayerName) ? "You" : PlayerStats.PlayerName;
                myRankCard.gameObject.SetActive(true);
                myRankCard.Bind(new LeaderboardRow(0, name, best, true));
            }
            else
            {
                myRankCard.gameObject.SetActive(false);
            }
        }

        private void Populate(IReadOnlyList<LeaderboardRow> rows)
        {
            ClearRows();
            if (recordCardPrefab == null || content == null)
                return;

            foreach (var row in rows)
            {
                RecordCardUI card = Instantiate(recordCardPrefab, content);
                card.gameObject.SetActive(true);
                card.Bind(row);
            }
        }

        /// <summary>Content의 기존 행을 모두 제거한다(디자인 타임에 남긴 견본 카드 포함).</summary>
        private void ClearRows()
        {
            if (content == null)
                return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        private void SetStatus(string message)
        {
            if (statusText == null)
                return;
            bool show = !string.IsNullOrEmpty(message);
            statusText.gameObject.SetActive(show);
            if (show)
                statusText.text = message;
        }
        #endregion
    }
}
