using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>궤도 무기 회전 속도 증가 (타격 빈도/커버리지 축).</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/회전 속도 증가", fileName = "Upgrade_OrbitSpeed")]
    public class OrbitSpeedUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Tooltip("시작 회전속도 대비 가산 비율(0.1 = 매번 +10%p, 복리 아님). 10회면 +100%p = 2배.")]
        [Min(0f)] public float addPercent = 0.1f;

        public override void Apply()
        {
            Orbit orbit = Player.Instance.Orbit;
            // 베이스(시작) 속도의 addPercent만큼을 가산 → 복리 없이 선형 증가
            orbit.rotationSpeed += orbit.DefaultRotationSpeed * addPercent;
        }
    }
}
