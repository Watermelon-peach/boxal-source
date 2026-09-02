using Boxal.Game;
using UnityEditor;
using UnityEngine;

namespace Boxal.Util.EditorTools
{
    /// <summary>
    /// 저장된 진행도를 손으로 조작하는 에디터 전용 메뉴. 상점·해금 패널처럼 <b>누적 진행도가 있어야
    /// 화면이 채워지는</b> UI를 테스트할 때 쓴다(그냥 플레이하면 첫 업그레이드 하나 사는 데도 두 판이 든다).
    /// </summary>
    /// <remarks>
    /// Editor 폴더에 있어 플레이어 빌드에는 들어가지 않는다.
    /// 값은 전부 <see cref="PlayerStats"/>를 거치므로 저장 키를 여기서 알 필요가 없다.
    /// <para/>
    /// 플레이 중에 실행해도 된다 — <see cref="Gold.Add"/>가 <see cref="Gold.Changed"/>를 쏘므로
    /// 열려 있는 상점/골드바가 즉시 갱신된다.
    /// </remarks>
    public static class BoxalDebugMenu
    {
        private const long GoldGrant = 100_000L;

        /// <summary>모든 업그레이드가 풀리는 누적 처치 수(가장 늦은 해금이 500킬).</summary>
        private const int UnlockAllKills = 500;

        [MenuItem("Boxal/Debug/Grant 100,000 Gold", priority = 0)]
        private static void GrantGold()
        {
            Gold.Add(GoldGrant);
            Debug.Log($"[BoxalDebug] Gold +{GoldGrant:N0} -> {Gold.Balance:N0}");
        }

        [MenuItem("Boxal/Debug/Unlock All (Total Kills -> 500)", priority = 1)]
        private static void UnlockAll()
        {
            // TotalKills에는 setter가 없다(AddKills만 있다). 모자란 만큼만 더한다.
            int missing = UnlockAllKills - PlayerStats.TotalKills;
            if (missing <= 0)
            {
                Debug.Log($"[BoxalDebug] 이미 {PlayerStats.TotalKills} kills — 전부 해금 상태다.");
                return;
            }
            PlayerStats.AddKills(missing);
            Debug.Log($"[BoxalDebug] Total kills -> {PlayerStats.TotalKills} (모든 업그레이드 해금)");
        }

        [MenuItem("Boxal/Debug/Reset All Stats", priority = 20)]
        private static void ResetAll()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Reset All Stats",
                "최고 기록 / 누적 처치 수 / 골드 / 상점 레벨 / 스태미나를 모두 지운다. 되돌릴 수 없다.",
                "Reset", "Cancel");
            if (!ok)
                return;

            PlayerStats.ClearAll();
            Debug.Log("[BoxalDebug] PlayerStats cleared.");
        }
    }
}
