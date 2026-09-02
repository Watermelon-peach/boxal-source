using System;
using UnityEngine;

namespace Boxal.Game
{
    /// <summary>상점에서 파는 영구 레벨 업그레이드의 종류.</summary>
    public enum ShopUpgradeId
    {
        /// <summary>처치당 획득 골드 +1/레벨.</summary>
        GoldPerKill = 0,
        /// <summary>시작 공격력 +1/레벨.</summary>
        StartAttack = 1,
        /// <summary>획득 포인트 +5%/레벨.</summary>
        PointBonus = 2,
    }

    /// <summary>
    /// 골드로 사는 영구 성장. 런을 넘겨 유지되며 레벨마다 값이 오르고 비용도 오른다.
    /// </summary>
    /// <remarks>
    /// 튜닝값은 아래 표 하나에 모여 있다. 저장 키는 <see cref="ShopUpgradeId"/> 이름이라
    /// enum 이름을 바꾸면 기존 저장이 초기화된다(순서/값 변경은 안전, 이름 변경만 주의).
    /// <para/>
    /// <b>레벨 상한은 <see cref="ShopUpgradeId.StartAttack"/>에만 있다.</b> 골드·포인트는 무한이다
    /// (상점을 점수 늘리는 엔드컨텐츠로 두기로 한 결정). 상한 유무로 갈리는 곳이 여럿이라
    /// 새로 다루는 코드는 <see cref="GetMaxLevel"/>/<see cref="HasMaxLevel"/>을 거칠 것 —
    /// 공통 상수(MaxLevel)는 더 이상 없다.
    /// </remarks>
    public static class ShopUpgrades
    {
        #region Tuning
        /// <summary>
        /// <see cref="ShopUpgradeId.StartAttack"/>의 최대 레벨. <b>상한이 있는 건 이것 하나뿐이다.</b>
        /// </summary>
        /// <remarks>
        /// 골드·포인트는 숫자만 커지는 축이라 무한히 올려도 게임이 안 망가지지만, 시작 공격력은
        /// 기본값이 1이라 레벨당 +1이 곧 배수다(Lv50이면 51배). 전투가 통째로 무의미해지므로 여기만 막는다.
        /// </remarks>
        public const int StartAttackMaxLevel = 10;

        /// <summary>
        /// 가격의 상한. 상한 없는 업그레이드의 레벨이 아주 높아지면 <c>baseCost * growth^level</c>이
        /// long 범위를 넘는데, 넘긴 채로 캐스팅하면 <b>음수</b>가 된다. 음수는 "최대 레벨"을 뜻하는
        /// -1과 구분이 안 돼서 UI가 MAX로 오인한다. 그 전에 잘라낸다.
        /// </summary>
        private const long CostCeiling = 9_000_000_000_000_000_000L;

        /// <summary>처치당 기본 골드(레벨 0일 때).</summary>
        public const int BaseGoldPerKill = 1;

        /// <summary>레벨당 처치 골드 증가량(기획서의 m).</summary>
        public const int GoldPerKillStep = 1;

        /// <summary>레벨당 시작 공격력 증가량.</summary>
        public const int StartAttackStep = 1;

        /// <summary>레벨당 획득 포인트 증가율.</summary>
        public const float PointBonusStep = 0.05f;

        // 비용 = baseCost x growth^(현재 레벨). 10단위로 반올림해서 표시가 지저분해지지 않게 한다.
        //
        // growth가 상한 유무로 갈린다.
        //   무한 축(골드·포인트) = 1.18. 골드 수급은 레벨당 +1로 선형인데 가격이 지수라 둘은 반드시
        //     벌어진다. growth가 높으면 그 지점이 금방 와서 "레벨이 안 오르는" 구간이 시작된다.
        //     1.55였을 때는 Lv15~20이 사실상 천장이었다(200판 굴려도 3종 합쳐 39레벨).
        //     1.18로 낮춰 천장을 Lv55~60까지 밀었다 — 상점을 엔드컨텐츠로 두기로 한 결정에 맞춘 값이다.
        //   상한 축(공격력) = 1.80 그대로. 레벨이 10에서 멈추므로 위의 천장 문제가 아예 없고,
        //     "가장 비싼 전투력 강화"라는 원래 의도(골드 배수부터 올리는 게 정석)를 유지해야 한다.
        //     여기까지 1.18로 내리면 10레벨 총액이 66,740 -> 3,530골드가 되어 너무 싸진다.
        private const float GoldPerKillBaseCost = 100f;
        private const float GoldPerKillGrowth = 1.18f;

        private const float StartAttackBaseCost = 150f;
        private const float StartAttackGrowth = 1.80f;

        private const float PointBonusBaseCost = 120f;
        private const float PointBonusGrowth = 1.18f;
        #endregion

        #region Events
        /// <summary>레벨이 바뀌었을 때. 상점 UI가 구독한다.</summary>
        public static event Action Changed;
        #endregion

        #region Properties
        /// <summary>처치 1회당 지급되는 골드.</summary>
        public static int GoldPerKill => BaseGoldPerKill + GetLevel(ShopUpgradeId.GoldPerKill) * GoldPerKillStep;

        /// <summary>시작 공격력에 더해지는 보너스.</summary>
        public static int StartAttackBonus => GetLevel(ShopUpgradeId.StartAttack) * StartAttackStep;

        /// <summary>획득 포인트에 곱해지는 배수(레벨 0이면 1.0).</summary>
        public static float PointMultiplier => 1f + GetLevel(ShopUpgradeId.PointBonus) * PointBonusStep;
        #endregion

        #region Custom Methods
        /// <summary>
        /// 이 업그레이드의 레벨 상한. 상한이 없으면 <see cref="int.MaxValue"/>를 돌려준다
        /// (그래야 clamp·비교가 특별 취급 없이 그대로 동작한다). 상한 유무는 <see cref="HasMaxLevel"/>로 볼 것.
        /// </summary>
        public static int GetMaxLevel(ShopUpgradeId id) =>
            id == ShopUpgradeId.StartAttack ? StartAttackMaxLevel : int.MaxValue;

        /// <summary>레벨 상한이 있는지. UI가 "Lv 3 / 10"과 "Lv 3"을 가르는 기준이다.</summary>
        public static bool HasMaxLevel(ShopUpgradeId id) => GetMaxLevel(id) != int.MaxValue;

        public static int GetLevel(ShopUpgradeId id) =>
            Mathf.Clamp(PlayerStats.GetShopLevel(id.ToString()), 0, GetMaxLevel(id));

        public static bool IsMaxed(ShopUpgradeId id) => GetLevel(id) >= GetMaxLevel(id);

        /// <summary>
        /// 다음 레벨의 가격. 이미 최대 레벨이면 -1(가격이 없다는 뜻).
        /// </summary>
        public static long GetNextCost(ShopUpgradeId id)
        {
            int level = GetLevel(id);
            if (level >= GetMaxLevel(id))
                return -1;

            GetCostCurve(id, out float baseCost, out float growth);
            double raw = baseCost * Math.Pow(growth, level);
            // 상한 없는 축은 레벨이 계속 오르므로 long을 넘길 수 있다(CostCeiling 주석 참고).
            if (double.IsNaN(raw) || raw >= CostCeiling)
                return CostCeiling;
            // 10단위 반올림. 최소 10골드는 되게 막아 둔다.
            long rounded = (long)(Math.Round(raw / 10.0) * 10.0);
            return rounded < 10 ? 10 : rounded;
        }

        /// <summary>지금 살 수 있는지(최대 레벨이 아니고 골드가 충분한지).</summary>
        public static bool CanPurchase(ShopUpgradeId id)
        {
            long cost = GetNextCost(id);
            return cost >= 0 && Gold.Balance >= cost;
        }

        /// <summary>
        /// 한 레벨 구매한다. 최대 레벨이거나 골드가 모자라면 아무것도 하지 않고 false.
        /// </summary>
        public static bool TryPurchase(ShopUpgradeId id)
        {
            long cost = GetNextCost(id);
            if (cost < 0)
                return false;
            if (!Gold.TrySpend(cost))
                return false;

            PlayerStats.SetShopLevel(id.ToString(), GetLevel(id) + 1);
            Changed?.Invoke();
            return true;
        }

        /// <summary>이 레벨에서 다음 레벨로 갈 때 늘어나는 효과를 사람이 읽을 문구로.</summary>
        public static string GetEffectLabel(ShopUpgradeId id)
        {
            switch (id)
            {
                case ShopUpgradeId.GoldPerKill:
                    return $"Gold +{GoldPerKillStep} per kill";
                case ShopUpgradeId.StartAttack:
                    return $"Start ATK +{StartAttackStep}";
                case ShopUpgradeId.PointBonus:
                    return $"Points +{Mathf.RoundToInt(PointBonusStep * 100f)}%";
                default:
                    return string.Empty;
            }
        }

        /// <summary>현재 레벨에서의 누적 효과를 사람이 읽을 문구로(상점의 "지금 값" 표시용).</summary>
        public static string GetCurrentValueLabel(ShopUpgradeId id)
        {
            switch (id)
            {
                case ShopUpgradeId.GoldPerKill:
                    return $"{GoldPerKill} / kill";
                case ShopUpgradeId.StartAttack:
                    return $"ATK {1 + StartAttackBonus}";
                case ShopUpgradeId.PointBonus:
                    return $"+{Mathf.RoundToInt((PointMultiplier - 1f) * 100f)}%";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 지금 적용 중인 수치(숫자만). 상점 카드의 "999 +999" 표기 왼쪽 값이다.
        /// </summary>
        /// <remarks>
        /// <see cref="GetCurrentValueLabel"/>은 "+15%"처럼 단위가 붙은 문장이라 그 양식에 못 쓴다.
        /// 포인트는 <b>퍼센트 값</b>을 돌려준다(Lv3이면 15). 단위 표기는 UI의 포맷 문자열이 정한다.
        /// </remarks>
        public static int GetCurrentValue(ShopUpgradeId id)
        {
            switch (id)
            {
                case ShopUpgradeId.GoldPerKill:
                    return GoldPerKill;
                case ShopUpgradeId.StartAttack:
                    return 1 + StartAttackBonus;
                case ShopUpgradeId.PointBonus:
                    return Mathf.RoundToInt((PointMultiplier - 1f) * 100f);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 한 레벨 올릴 때 늘어나는 수치(숫자만). "999 +999" 표기 오른쪽 값이며
        /// <see cref="GetCurrentValue"/>와 단위가 같다.
        /// </summary>
        public static int GetStepValue(ShopUpgradeId id)
        {
            switch (id)
            {
                case ShopUpgradeId.GoldPerKill:
                    return GoldPerKillStep;
                case ShopUpgradeId.StartAttack:
                    return StartAttackStep;
                case ShopUpgradeId.PointBonus:
                    return Mathf.RoundToInt(PointBonusStep * 100f);
                default:
                    return 0;
            }
        }

        /// <summary>디버그/테스트용. 모든 레벨을 0으로 되돌린다.</summary>
        public static void ResetAll()
        {
            foreach (ShopUpgradeId id in Enum.GetValues(typeof(ShopUpgradeId)))
                PlayerStats.SetShopLevel(id.ToString(), 0);
            Changed?.Invoke();
        }

        private static void GetCostCurve(ShopUpgradeId id, out float baseCost, out float growth)
        {
            switch (id)
            {
                case ShopUpgradeId.StartAttack:
                    baseCost = StartAttackBaseCost;
                    growth = StartAttackGrowth;
                    break;
                case ShopUpgradeId.PointBonus:
                    baseCost = PointBonusBaseCost;
                    growth = PointBonusGrowth;
                    break;
                default:
                    baseCost = GoldPerKillBaseCost;
                    growth = GoldPerKillGrowth;
                    break;
            }
        }
        #endregion
    }
}
