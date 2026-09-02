using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 플레이어 높이에 따라 스카이박스의 _SpaceBlend(0=맑은하늘, 1=우주)를 구동한다.
    /// RenderSettings.skybox 머티리얼(= Boxal/GradientSpaceSkybox)에 직접 값을 쓴다.
    /// </summary>
    public class SkyboxHeightController : MonoBehaviour
    {
        [Header("높이 매핑")]
        [Tooltip("이 높이 이하이면 완전한 맑은 하늘(blend 0)")]
        [SerializeField] private float groundHeight = 0f;
        [Tooltip("이 높이 이상이면 완전한 우주(blend 1)")]
        [SerializeField] private float spaceHeight = 15f;

        [Header("추적 대상")]
        [Tooltip("비워두면 Player 싱글톤을 자동으로 찾는다")]
        [SerializeField] private Transform target;

        [Tooltip("블렌드 전환 부드러움(클수록 빠름). 0이면 즉시")]
        [SerializeField] private float smoothing = 5f;

        private static readonly int SpaceBlendID = Shader.PropertyToID("_SpaceBlend");
        private Material skyMat;
        private float current;

        private void OnEnable()
        {
            skyMat = RenderSettings.skybox;
            current = 0f;
            ApplyBlend(0f);
        }

        private void OnDisable()
        {
            // 에디터 플레이 종료 시 에셋에 값이 남지 않도록 원복
            ApplyBlend(0f);
        }

        private void Update()
        {
            if (skyMat == null)
                return;

            if (target == null)
            {
                if (Player.InstanceExist)
                    target = Player.Instance.transform;
                else
                    return;
            }

            float y = target.position.y;
            float targetBlend = Mathf.Clamp01(Mathf.InverseLerp(groundHeight, spaceHeight, y));

            if (smoothing <= 0f)
                current = targetBlend;
            else
                current = Mathf.Lerp(current, targetBlend, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));

            ApplyBlend(current);
        }

        private void ApplyBlend(float value)
        {
            if (skyMat != null && skyMat.HasProperty(SpaceBlendID))
                skyMat.SetFloat(SpaceBlendID, value);
        }
    }
}
