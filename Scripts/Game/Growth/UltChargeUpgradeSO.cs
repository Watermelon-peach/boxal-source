using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>궁극기 충전 효율 증가. 처치당 차는 게이지량을 늘린다. (Common 권장)</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/궁극기 충전 효율", fileName = "Upgrade_UltCharge")]
    public class UltChargeUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Tooltip("충전 효율 가산")]
        [Min(0f)] public float addEff = 0f;
        [Tooltip("충전 효율 승수. 1이면 미적용")]
        [Min(1f)] public float multiplyEff = 1.2f;
        [Tooltip("체크하면 킬차지는 그대로 두고 오토차지(시간충전)에만 적용 (Legendary용)")]
        public bool autoChargeOnly = false;

        public override void Apply()
        {
            if (autoChargeOnly)
                UltimateManager.Instance.AddAutoChargeEfficiency(multiplyEff, addEff);
            else
                UltimateManager.Instance.AddEfficiency(multiplyEff, addEff);
        }
    }
}
