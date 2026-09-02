using Boxal.Util;
using TMPro;
using UnityEngine;

namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 리더보드 한 줄(RecordCard) 프리팹의 바인딩 컴포넌트.
    /// LeaderboardManager가 가져온 LeaderboardRow 하나를 받아 랭크/이름/점수를 채우고,
    /// 본인 행이면 하이라이트한다. UI는 UGS를 몰라도 되고 이 Bind만 호출하면 된다.
    /// RecordCard 루트에 부착하고, 인스펙터에서 자식 TMP들을 연결할 것.
    /// </summary>
    public class RecordCardUI : MonoBehaviour
    {
        #region Variables
        [Header("Texts")]
        [Tooltip("랭크 표시 (지금 카드의 'Text (TMP)' → RankText로 리네임 권장)")]
        [SerializeField] private TextMeshProUGUI rankText;
        [Tooltip("닉네임 표시 (NameTag)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [Tooltip("점수 표시 (Records)")]
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("Highlight (본인 행)")]
        [Tooltip("현재 플레이어 본인 행일 때 켜지는 오브젝트(테두리/배경 등). 없으면 비워도 됨.")]
        [SerializeField] private GameObject highlightObject;

        [Header("Rank Colors (선택)")]
        [Tooltip("체크 시 1~3등 랭크 텍스트를 금/은/동 색으로 표시.")]
        [SerializeField] private bool useRankColors = true;
        [SerializeField] private Color firstColor = new Color(1f, 0.84f, 0f);      // Gold
        [SerializeField] private Color secondColor = new Color(0.75f, 0.75f, 0.75f); // Silver
        [SerializeField] private Color thirdColor = new Color(0.8f, 0.5f, 0.2f);   // Bronze
        [SerializeField] private Color defaultRankColor = Color.white;
        [Tooltip("순위가 없을 때(오프라인 로컬 최고점 등) 표시.")]
        [SerializeField] private string unrankedLabel = "-";
        #endregion

        #region Custom Methods
        /// <summary>한 줄 데이터를 카드에 반영한다.</summary>
        public void Bind(LeaderboardRow row)
        {
            if (rankText != null)
            {
                rankText.text = row.Rank > 0 ? $"#{row.Rank}" : unrankedLabel;
                if (useRankColors)
                    rankText.color = RankColor(row.Rank);
            }

            if (nameText != null)
                nameText.text = row.DisplayName;

            if (scoreText != null)
                scoreText.text = NumberUtil.FormatComma(row.Score);

            if (highlightObject != null)
                highlightObject.SetActive(row.IsCurrentPlayer);
        }

        private Color RankColor(int rank)
        {
            switch (rank)
            {
                case 1: return firstColor;
                case 2: return secondColor;
                case 3: return thirdColor;
                default: return defaultRankColor;
            }
        }
        #endregion
    }
}
