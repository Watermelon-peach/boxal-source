using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Util
{
    /// <summary>
    /// 기본 Toggle을 "스위치" 스타일로 연출한다. 상태(bool)는 Toggle이 그대로 관리하고,
    /// 이 스크립트는 값이 바뀔 때 손잡이(Handle)를 좌/우로 슬라이드하고
    /// 배경 색/스프라이트를 전환한다.
    ///
    /// 사용법:
    /// 1) Toggle이 붙은 GameObject에 이 컴포넌트를 추가(Toggle은 RequireComponent로 자동).
    /// 2) Toggle의 Transition을 None으로 두면 기본 체크마크 연출과 겹치지 않는다.
    /// 3) handle/background 참조와 on/off 위치·색(또는 스프라이트)을 인스펙터에서 지정.
    ///
    /// 퍼즈 패널 등 timeScale=0 상태에서도 동작하도록 unscaled 시간을 사용한다.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class SwitchToggle : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("좌우로 움직이는 손잡이. RectTransform의 anchoredPosition.x를 보간한다.")]
        [SerializeField] private RectTransform handle;
        [Tooltip("손잡이 이미지. on/off 스프라이트 교체 대상(선택, handle과 같은 오브젝트의 Image).")]
        [SerializeField] private Image handleImage;
        [Tooltip("트랙 배경 이미지. 색 틴트/스프라이트 교체 대상(선택).")]
        [SerializeField] private Image background;

        [Header("손잡이 위치 (anchoredPosition.x)")]
        [SerializeField] private float onPosX = 20f;
        [SerializeField] private float offPosX = -20f;

        [Header("색 전환 (background 필요)")]
        [SerializeField] private bool useColorTransition = true;
        [SerializeField] private Color onColor = new Color(0.30f, 0.78f, 0.40f);
        [SerializeField] private Color offColor = new Color(0.55f, 0.55f, 0.55f);

        [Header("배경 스프라이트 전환 (선택, background 필요)")]
        [Tooltip("지정하면 on/off에 따라 background.sprite를 교체한다. 색 전환과 함께 써도 된다.")]
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;

        [Header("손잡이 스프라이트 전환 (선택, handleImage 필요)")]
        [Tooltip("지정하면 on/off에 따라 handleImage.sprite를 교체한다.")]
        [SerializeField] private Sprite handleOnSprite;
        [SerializeField] private Sprite handleOffSprite;

        [Header("애니메이션")]
        [Tooltip("전환에 걸리는 시간(초). 0이면 즉시.")]
        [SerializeField] private float duration = 0.15f;

        private Toggle toggle;
        private Coroutine anim;

        /// <summary>현재 on 상태(Toggle.isOn과 동일).</summary>
        public bool IsOn => toggle != null && toggle.isOn;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnEnable()
        {
            // 활성화 시 현재 상태를 애니메이션 없이 즉시 반영(초기 표시/재활성 대비).
            if (toggle != null) ApplyImmediate(toggle.isOn);
        }

        private void OnDestroy()
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(bool isOn)
        {
            if (!isActiveAndEnabled || duration <= 0f)
            {
                ApplyImmediate(isOn);
                return;
            }
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Animate(isOn));
        }

        /// <summary>보간 없이 목표 상태를 즉시 반영한다.</summary>
        private void ApplyImmediate(bool isOn)
        {
            if (handle != null)
                handle.anchoredPosition = new Vector2(isOn ? onPosX : offPosX, handle.anchoredPosition.y);
            ApplySprites(isOn);
            if (background != null && useColorTransition)
                background.color = isOn ? onColor : offColor;
        }

        private IEnumerator Animate(bool isOn)
        {
            float startX = handle != null ? handle.anchoredPosition.x : 0f;
            float targetX = isOn ? onPosX : offPosX;
            Color startColor = background != null ? background.color : Color.white;
            Color targetColor = isOn ? onColor : offColor;

            // 스프라이트는 전환 시작 시점에 바로 교체(슬라이드 중간에 바뀌면 어색).
            ApplySprites(isOn);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // timeScale=0(퍼즈)에서도 진행
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                if (handle != null)
                    handle.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, p), handle.anchoredPosition.y);
                if (background != null && useColorTransition)
                    background.color = Color.Lerp(startColor, targetColor, p);
                yield return null;
            }
            ApplyImmediate(isOn); // 마무리 스냅
            anim = null;
        }

        private void ApplySprites(bool isOn)
        {
            if (background != null)
            {
                Sprite bg = isOn ? onSprite : offSprite;
                if (bg != null) background.sprite = bg;
            }
            if (handleImage != null)
            {
                Sprite hs = isOn ? handleOnSprite : handleOffSprite;
                if (hs != null) handleImage.sprite = hs;
            }
        }

        /// <summary>코드에서 상태를 세팅한다. animate=false면 즉시 반영(초기화용).</summary>
        public void SetOn(bool isOn, bool animate = true)
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            if (animate)
            {
                toggle.isOn = isOn; // onValueChanged 경유로 애니메이션
            }
            else
            {
                toggle.SetIsOnWithoutNotify(isOn);
                ApplyImmediate(isOn);
            }
        }
    }
}
