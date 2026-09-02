using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>경험치 획득량 증가 (성장 속도 축). 곱연산 배수, 복리로 누적.</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/경험치 증가", fileName = "Upgrade_Xp")]
    public class XpUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Tooltip("경험치 배수. 1.2 = 20% 증가")]
        [Min(1f)] public float multiplyXp = 1.2f;

        public override void Apply()
        {
            LevelManager.Instance.AddXpMult(multiplyXp);
        }
    }
}
