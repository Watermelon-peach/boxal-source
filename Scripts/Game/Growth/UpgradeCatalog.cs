using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>
    /// 업그레이드 전체 목록. 홈의 해금 패널처럼 <b>PlayScene 밖에서</b> 목록이 필요할 때 쓴다.
    /// </summary>
    /// <remarks>
    /// 추첨 풀은 여전히 <c>UpgradeManager.pool</c>(PlayScene에 직렬화)이 갖고 있다. 홈 씬에서는
    /// 그걸 볼 수 없어서 표시용 목록을 애셋으로 따로 둔 것이다.
    /// <para/>
    /// ★두 목록이 갈라져 있으므로 업그레이드를 새로 추가하면 <b>양쪽 다</b> 넣어야 한다.
    /// 빠뜨려도 게임은 정상 동작하고 해금 패널에서 안 보이거나(카탈로그 누락)
    /// 패널에만 보이고 안 나오는(풀 누락) 차이만 생긴다.
    /// </remarks>
    [CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "Boxal/Upgrade Catalog")]
    public class UpgradeCatalog : ScriptableObject
    {
        [Tooltip("해금 패널에 표시할 업그레이드 전체. 표시 순서는 정렬 옵션이 정한다.")]
        [SerializeField] private List<UpgradeSO> entries = new List<UpgradeSO>();

        public IReadOnlyList<UpgradeSO> Entries => entries;

        /// <summary>
        /// 해금 순서(unlockKills 오름차순 → 등급 → 이름)로 정렬한 목록.
        /// 처음부터 해금된 것(0)이 앞에, 멀리 있는 것이 뒤에 온다.
        /// </summary>
        public List<UpgradeSO> GetSortedByUnlock()
        {
            var list = new List<UpgradeSO>();
            foreach (UpgradeSO u in entries)
            {
                if (u != null)
                    list.Add(u);
            }

            list.Sort((a, b) =>
            {
                int byKills = a.unlockKills.CompareTo(b.unlockKills);
                if (byKills != 0)
                    return byKills;
                int byGrade = a.grade.CompareTo(b.grade);
                if (byGrade != 0)
                    return byGrade;
                return string.CompareOrdinal(a.displayName, b.displayName);
            });
            return list;
        }

        /// <summary>
        /// 아직 안 해금된 것 중 가장 가까운 하나. 전부 해금됐으면 null.
        /// "다음 해금까지 N킬" 표시에 쓴다.
        /// </summary>
        public UpgradeSO GetNextLocked()
        {
            UpgradeSO best = null;
            foreach (UpgradeSO u in entries)
            {
                if (u == null || u.IsUnlocked)
                    continue;
                if (best == null || u.unlockKills < best.unlockKills)
                    best = u;
            }
            return best;
        }

        /// <summary>해금된 개수 / 전체 개수.</summary>
        public void GetProgress(out int unlocked, out int total)
        {
            unlocked = 0;
            total = 0;
            foreach (UpgradeSO u in entries)
            {
                if (u == null)
                    continue;
                total++;
                if (u.IsUnlocked)
                    unlocked++;
            }
        }
    }
}
