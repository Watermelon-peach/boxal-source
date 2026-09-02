using UnityEngine;

namespace Boxal.Game.Growth
{
    /// <summary>궁극기(광폭화) 지속시간 증가. (Rare 권장)</summary>
    [CreateAssetMenu(menuName = "Boxal/Upgrades/궁극기 지속시간", fileName = "Upgrade_UltDuration")]
    public class UltDurationUpgradeSO : UpgradeSO
    {
        [Header("효과")]
        [Tooltip("지속시간 증가(초)")]
        [Min(0f)] public float addSeconds = 1f;

        public override void Apply()
        {
            UltimateManager.Instance.AddDuration(addSeconds);
        }
    }
}
