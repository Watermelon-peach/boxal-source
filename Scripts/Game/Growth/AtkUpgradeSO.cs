using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>공격력 증가 (DPS 핵심 축). 합산 후 승산 순으로 적용.</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/공격력 증가", fileName = "Upgrade_Atk")]
    public class AtkUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Min(0)] public int addAtk = 1;
        [Tooltip("승수. 1이면 미적용")]
        [Min(1f)] public float multiplyAtk = 1f;

        public override void Apply()
        {
            Player p = Player.Instance;
            // 공격력은 실수라 정수 캐스팅/반올림 없이 그대로 반영한다.
            // 예) 1 × 1.5 = 1.5 (버림해서 1이 되지 않음). 데미지 정수화는 Player.ConsumeAttackDamage 담당.
            p.Atk = (p.Atk + addAtk) * multiplyAtk;
        }
    }
}
