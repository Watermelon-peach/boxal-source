using System;
using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>
    /// 이번 런에서 획득한 업그레이드를 종류별로 집계한다(획득 순서 유지, 중복은 횟수로).
    /// 퍼즈 화면의 획득 목록(<see cref="UI.UpgradeHistoryUI"/>)이 이 값을 읽는다.
    /// </summary>
    /// <remarks>
    /// 레벨업 3택1(<see cref="UpgradeManager"/>)과 보스 보상 Legendary(<see cref="BossRewardManager"/>)가
    /// 서로 다른 풀이라, 양쪽이 공통으로 기록할 자리가 필요해 별도로 둔다.
    /// 런 한정 데이터라 세이브하지 않으며, 재시작 시 <see cref="Clear"/>로 비운다.
    ///
    /// Enter Play Mode에서 도메인 리로드를 끄면 static 필드가 세션 간 살아남으므로,
    /// 진입 시점에 한 번 초기화해 이전 판의 목록이 남지 않게 한다(HapticManager와 같은 방식).
    /// </remarks>
    public static class UpgradeHistory
    {
        /// <summary>업그레이드 1종과 그 획득 횟수.</summary>
        public struct Entry
        {
            public UpgradeSO upgrade;
            public int count;
        }

        private static readonly List<Entry> entries = new List<Entry>();

        /// <summary>획득 순서대로의 목록. 같은 업그레이드는 한 항목에 합쳐진다.</summary>
        public static IReadOnlyList<Entry> Entries => entries;

        /// <summary>목록이 바뀌었을 때(획득/초기화) 발생. UI 갱신용.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            entries.Clear();
            Changed = null; // 구독은 각 UI의 Start에서 다시 걸린다
        }

        /// <summary>업그레이드 1회 획득을 기록한다. 이미 가진 것이면 횟수만 올린다.</summary>
        public static void Record(UpgradeSO upgrade)
        {
            if (upgrade == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].upgrade != upgrade)
                    continue;

                Entry existing = entries[i];
                existing.count++;
                entries[i] = existing; // 구조체라 되넣어야 반영된다
                Changed?.Invoke();
                return;
            }

            entries.Add(new Entry { upgrade = upgrade, count = 1 });
            Changed?.Invoke();
        }

        /// <summary>재시작 시 목록을 비운다.</summary>
        public static void Clear()
        {
            entries.Clear();
            Changed?.Invoke();
        }
    }
}
