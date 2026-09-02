using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 디버그 캔버스의 Reset 버튼용 래퍼. static <see cref="PlayerStats"/>는
    /// 인스펙터 onClick에 직접 바인딩할 수 없어, 인스턴스 메서드로 감싼다.
    /// </summary>
    public class DebugStatsReset : MonoBehaviour
    {
        /// <summary>저장된 모든 통계(최고기록/누적처치)를 초기화한다. Reset 버튼 onClick에서 호출.</summary>
        public void ResetStats()
        {
            PlayerStats.ClearAll();
            Debug.Log("[DebugStatsReset] PlayerStats cleared (BestRound / TotalKills reset to 0).");
        }
    }
}
