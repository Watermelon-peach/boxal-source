using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>
    /// 업그레이드 자동 선택. 켜면 레벨업 카드를 보여준 뒤 제한시간이 지나면 최선 후보로 자동 확정된다.
    /// 카운트다운 중에 토글을 끄면 취소되므로, 자동이 켜져 있어도 원하면 직접 고를 수 있다.
    /// 실제 카운트다운과 표시는 <see cref="UI.UpgradeCardUI"/>가 맡고, 여기서는 설정값과 선택 규칙만 둔다.
    /// </summary>
    /// <remarks>
    /// 왜 자동 선택이 손해가 아닌가 — 밸런스 시뮬(Tools/BalanceSim) 결과, 최적 선택이
    /// 고정 우선순위 리스트 하나로 완전히 표현된다(랜덤 픽 대비 R60 생존 3%→30%,
    /// 궁을 보스전에 아끼면 20%→84%). 즉 카드 선택은 매번 고민할 결정이 아니라
    /// 조회 테이블이었고, 모르고 고르면 손해만 보는 구조였다. 그 테이블을 여기로 옮긴다.
    ///
    /// 값 자체는 <see cref="UpgradeSO.autoPriority"/>에 데이터로 두어 .asset에서 튜닝한다
    /// (풀에 새 업그레이드를 추가해도 이 클래스는 손대지 않는다).
    ///
    /// 볼륨·진동과 달리 <b>저장하지 않는다</b> — 앱을 켤 때마다 항상 꺼진 상태로 시작한다.
    /// 자동 선택은 켜두면 성장 선택이 통째로 사라지는 설정이라, 지난 판에 켰던 것이
    /// 조용히 이어져 "내가 안 골랐는데 넘어가네" 상태가 되지 않게 한다.
    /// 대신 앱 실행 중에는 유지되므로(홈에서 켜고 플레이 씬으로 넘어가도 살아 있음)
    /// 씬 싱글턴이 아니라 static으로 둔다.
    /// </remarks>
    public static class AutoSelectManager
    {
        /// <summary>자동 선택 사용 여부. 설정 패널의 토글이 이 값을 읽고 쓴다.
        /// 앱 실행 단위 설정이라 디스크에 저장되지 않는다.</summary>
        public static bool Enabled { get; set; }

        /// <summary>앱 시작 시 항상 off로 되돌린다.
        /// Enter Play Mode에서 도메인 리로드를 끄면 static이 세션 간 살아남으므로 명시적으로 끈다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Enabled = false;
        }

        /// <summary>후보 중 자동 선택이 고를 인덱스를 반환한다. 후보가 없으면 -1.</summary>
        /// <remarks>
        /// 평시엔 <see cref="UpgradeSO.autoPriority"/>가 가장 높은 것을 고른다.
        /// 단 다음 피격 한 방에 죽는 상태(<see cref="Player.IsLethalState"/>)면
        /// <see cref="UpgradeSO.autoEmergencyPick"/>이 붙은 후보(회복류)를 최우선으로 집는다.
        /// 시뮬의 greedy 정책이 "목숨이 위험하면 heal 우선"을 두던 것과 같은 규칙이다.
        /// </remarks>
        public static int PickBest(IReadOnlyList<UpgradeSO> choices)
        {
            if (choices == null || choices.Count == 0)
                return -1;

            bool lethal = Player.InstanceExist && Player.Instance.IsLethalState;

            int best = -1;
            int bestPriority = 0;
            bool bestIsEmergency = false;

            for (int i = 0; i < choices.Count; i++)
            {
                UpgradeSO up = choices[i];
                if (up == null)
                    continue;

                bool emergency = lethal && up.autoEmergencyPick;

                // 위급 후보는 평시 우선순위와 무관하게 항상 앞선다.
                if (best < 0
                    || (emergency && !bestIsEmergency)
                    || (emergency == bestIsEmergency && up.autoPriority > bestPriority))
                {
                    best = i;
                    bestPriority = up.autoPriority;
                    bestIsEmergency = emergency;
                }
            }

            // 전부 null인 방어적 경우에도 유효한 인덱스를 돌려준다.
            return best >= 0 ? best : 0;
        }
    }
}
