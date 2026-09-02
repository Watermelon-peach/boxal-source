using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>즉시 회복 (생존 축, 손실 누적 완화). 생명 = 무기이므로 AddLife로 무기도 회복.</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/즉시 회복", fileName = "Upgrade_Heal")]
    public class HealUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Min(1)] public int amount = 2;
        [Tooltip("체크하면 amount 무시하고 현재 상한까지 즉시 전부 회복 (Legendary용)")]
        public bool fullHeal = false;

        public override void Apply()
        {
            Player p = Player.Instance;
            if (fullHeal)
                p.AddLife(p.maxLife); // 상한만큼 채우기에 항상 충분한 값
            else
                p.AddLife(amount);
        }
    }
}
