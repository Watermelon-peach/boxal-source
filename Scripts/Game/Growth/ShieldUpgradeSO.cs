using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>1회 피해 무효화 보호막 부여 (생존 축, Legendary 전용).</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/보호막", fileName = "Upgrade_Shield")]
    public class ShieldUpgradeSO : UpgradeSO
    {
        public override void Apply()
        {
            Player.Instance.HasShield = true;
        }

        /// <summary>이미 보호막을 보유 중이면 무의미하므로 제시하지 않는다.</summary>
        protected override bool CanOfferCore()
        {
            Player p = Player.Instance;
            return p == null || !p.HasShield;
        }
    }
}
