using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>궤도 반경 증가 (사거리/커버리지 축). 상한을 향해 수렴하는 성장 — 후반 효율 감소.
    /// 무기 크기도 반경에 비례해 함께 커진다(Orbit.GrowRadius).</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/궤도 반경 증가", fileName = "Upgrade_OrbitRadius")]
    public class OrbitRadiusUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Tooltip("상한까지 남은 여유를 이 비율만큼 좁힌다(수렴형). 클수록 초반 성장 큼, 매번 효율 감소")]
        [Range(0f, 1f)] public float growFraction = 0.4f;

        public override void Apply()
        {
            Player.Instance.Orbit.GrowRadius(growFraction);
        }

        /// <summary>반경이 상한에 도달하면 더는 제시하지 않는다(무의미한 픽 방지).</summary>
        protected override bool CanOfferCore()
        {
            Player p = Player.Instance;
            if (p == null || p.Orbit == null)
                return true;
            return !p.Orbit.IsRadiusMaxed;
        }
    }
}
