using System;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 팝업 공용 Dim 제어. PopUps 루트에 부착하고, 자식 팝업 중 하나라도 켜져 있으면 Dim을 켠다.
    /// 각 팝업이 자기 Dim을 들고 있던 것을 하나로 합친 것이라, 팝업 스크립트들은 이 존재를 몰라도 된다
    /// (활성 상태에서 파생시키므로 Show/Hide 경로를 빠뜨려 Dim이 남는 사고가 없다).
    ///
    /// ★Dim은 SafeArea 밖에 있어야 한다. 안에 두면 노치/제스처바 영역만 어두워지지 않아 띠가 남는다.
    /// ★Dim Image의 CanvasRenderer에서 Cull Transparent Mesh를 꺼둘 것. 켜져 있으면 알파 0일 때
    ///   메시가 컬링되고, GraphicRaycaster는 컬링된 그래픽을 건너뛰므로 입력 차단까지 같이 사라진다
    ///   (퍼즈처럼 "안 보이지만 입력은 막는" 용도가 통째로 무력화된다).
    /// </summary>
    public class PopupDimController : MonoBehaviour
    {
        #region Types
        /// <summary>팝업별 Dim 농도 예외. 목록에 없으면 defaultAlpha를 쓴다.</summary>
        [Serializable]
        public struct AlphaOverride
        {
            public GameObject popup;
            [Range(0f, 1f)] public float alpha;
        }
        #endregion

        #region Variables
        [Tooltip("공용 Dim의 Image. PopUps의 첫 자식이어야 팝업들 뒤에 깔린다.")]
        [SerializeField] private Image dim;

        [Tooltip("예외 목록에 없는 팝업에 적용할 기본 농도.")]
        [Range(0f, 1f)][SerializeField] private float defaultAlpha = 0.745f;

        [Tooltip("농도를 다르게 가져갈 팝업. 퍼즈는 0(안 보이지만 입력은 막힘).")]
        [SerializeField] private AlphaOverride[] alphaOverrides;

        private GameObject lastOpenPopup;
        #endregion

        #region Unity Event Methods
        private void LateUpdate()
        {
            GameObject open = FindOpenPopup();
            if (open == lastOpenPopup)
                return;

            lastOpenPopup = open;
            Apply(open);
        }
        #endregion

        #region Custom Methods
        /// <summary>Dim을 제외한 자식 중 첫 번째로 켜져 있는 것. 팝업은 동시에 하나만 뜨는 설계다.</summary>
        private GameObject FindOpenPopup()
        {
            Transform dimTransform = dim != null ? dim.transform : null;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == dimTransform)
                    continue;
                if (child.gameObject.activeSelf)
                    return child.gameObject;
            }
            return null;
        }

        private void Apply(GameObject open)
        {
            if (dim == null)
                return;

            if (open == null)
            {
                dim.gameObject.SetActive(false);
                return;
            }

            Color color = dim.color;
            color.a = ResolveAlpha(open);
            dim.color = color;
            dim.gameObject.SetActive(true);
        }

        private float ResolveAlpha(GameObject open)
        {
            if (alphaOverrides != null)
            {
                for (int i = 0; i < alphaOverrides.Length; i++)
                {
                    if (alphaOverrides[i].popup == open)
                        return alphaOverrides[i].alpha;
                }
            }
            return defaultAlpha;
        }
        #endregion
    }
}
