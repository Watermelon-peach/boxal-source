using Boxal.Util;
using TMPro;
using UnityEngine;

namespace Boxal.Game.UI
{
    /// <summary>
    /// MainPanel의 최고기록 배지(BestBadge/Record). 로컬 최고 기록을 "R{라운드} · {점수}" 포맷으로 표시한다.
    /// 샘플 포맷: "R12 · 34,500". BestBadge 또는 Record에 부착하고 recordText를 연결할 것.
    /// </summary>
    public class BestBadgeUI : MonoBehaviour
    {
        [Tooltip("기록 텍스트 (BestBadge/Record)")]
        [SerializeField] private TextMeshProUGUI recordText;
        [Tooltip("활성화될 때 자동 갱신.")]
        [SerializeField] private bool refreshOnEnable = true;

        private void OnEnable()
        {
            if (refreshOnEnable)
                Refresh();
        }

        /// <summary>로컬 최고 기록을 배지에 반영한다.</summary>
        public void Refresh()
        {
            if (recordText == null)
                return;
            int round = PlayerStats.BestRound;
            long score = PlayerStats.BestScore;
            recordText.text = $"R{round} · {NumberUtil.FormatNumber(score)}";
        }
    }
}
