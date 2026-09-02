using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>
    /// 궤도 무기 +N (DPS + 생존 축). 무기 상한(maxLife)을 함께 올려 실제로 늘어나게 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/궤도 무기 추가", fileName = "Upgrade_WeaponCount")]
    public class WeaponCountUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Min(1)] public int amount = 1;

        public override void Apply()
        {
            Player p = Player.Instance;
            // 상한(생명/무기)을 먼저 올린 뒤 AddLife로 생명·무기를 함께 +amount
            // (무기만 늘려 life와 어긋나면 무기가 남았는데 게임오버되는 버그 발생)
            p.maxLife += amount;
            p.Orbit.RaiseMaxWeaponCount(amount);
            p.AddLife(amount);
        }
    }
}
