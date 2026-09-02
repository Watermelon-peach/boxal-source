using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Boxal.Util
{
    /// <summary>
    /// 좌우 슬라이드 페이저(탭 버튼 + 손가락 스와이프 둘 다 지원).
    /// 각 페이지는 앵커로 (i,0)~(i+1,1)에 배치돼 있어 화면비와 무관하게 정확히 뷰포트 1개 너비를 갖는다
    /// (앵커는 부모 비율이라 1을 넘겨도 된다). 페이저는 container를 x축으로 밀어서 페이지를 전환한다.
    /// Viewport에 부착하고 RectMask2D로 클리핑할 것.
    /// </summary>
    public class UiPager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Variables
        [Header("Refs")]
        [Tooltip("클리핑 영역(자기 자신인 경우가 많음). 페이지 너비 기준이 된다.")]
        [SerializeField] private RectTransform viewport;
        [Tooltip("페이지들을 담고 실제로 움직이는 컨테이너. 뷰포트와 같은 크기(Stretch, offset 0)여야 한다.")]
        [SerializeField] private RectTransform container;

        [Header("Pages")]
        [SerializeField] private int pageCount = 3;
        [Tooltip("시작 페이지. Main이 가운데면 1.")]
        [SerializeField] private int defaultPage = 1;

        [Header("Feel")]
        [Tooltip("페이지 전환 트윈 시간(초).")]
        [SerializeField] private float tweenDuration = 0.25f;
        [Tooltip("이 비율 이상 끌면 다음 페이지로 넘어간다(뷰포트 너비 대비).")]
        [Range(0.05f, 0.5f)][SerializeField] private float swipeDistanceRatio = 0.2f;
        [Tooltip("이 속도 이상으로 튕기면 거리와 무관하게 넘어간다(초당 canvas 단위).")]
        [SerializeField] private float flickVelocity = 800f;
        [Tooltip("양 끝에서 더 끌 때의 저항(0=고무줄 없음, 1=저항 없음).")]
        [Range(0f, 1f)][SerializeField] private float edgeResistance = 0.35f;

        private Canvas canvas;
        private Coroutine tweenRoutine;
        private float dragVelocity;
        private float lastViewportWidth;
        #endregion

        #region Properties
        /// <summary>현재 페이지 인덱스.</summary>
        public int CurrentPage { get; private set; } = -1;

        /// <summary>페이지가 실제로 바뀔 때 발생(탭 하이라이트/지연 로딩 훅용).</summary>
        public event Action<int> PageChanged;

        private float PageWidth => viewport != null ? viewport.rect.width : 0f;
        private float ScaleFactor => canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (viewport == null)
                viewport = transform as RectTransform;
        }

        private void Start()
        {
            SetPage(defaultPage, instant: true);
            lastViewportWidth = PageWidth;
        }

        private void Update()
        {
            // 회전/해상도/SafeArea 변경으로 뷰포트 너비가 바뀌면 현재 페이지에 다시 스냅한다.
            // (페이지 너비는 앵커가 알아서 맞추지만, 컨테이너 위치는 픽셀이라 재계산이 필요)
            if (!Mathf.Approximately(PageWidth, lastViewportWidth))
            {
                lastViewportWidth = PageWidth;
                if (tweenRoutine == null)
                    SnapInstant(CurrentPage);
            }
        }
        #endregion

        #region Custom Methods
        /// <summary>탭 버튼에서 호출(인스펙터 OnClick에 연결 가능).</summary>
        public void GoToPage(int index)
        {
            SetPage(index, instant: false);
        }

        private void SetPage(int index, bool instant)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, pageCount - 1));
            bool changed = index != CurrentPage;
            CurrentPage = index;

            if (instant)
            {
                SnapInstant(index);
            }
            else
            {
                StopTween();
                tweenRoutine = StartCoroutine(TweenTo(-index * PageWidth));
            }

            if (changed)
                PageChanged?.Invoke(index);
        }

        private void SnapInstant(int index)
        {
            if (container == null)
                return;
            StopTween();
            container.anchoredPosition = new Vector2(-index * PageWidth, container.anchoredPosition.y);
        }

        private IEnumerator TweenTo(float targetX)
        {
            float startX = container.anchoredPosition.x;
            float t = 0f;
            while (t < tweenDuration)
            {
                // 홈은 timeScale 영향을 안 받는 게 안전(퍼즈/연출과 무관하게 동작).
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / tweenDuration);
                k = 1f - (1f - k) * (1f - k); // ease-out quad
                container.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, k), container.anchoredPosition.y);
                yield return null;
            }
            container.anchoredPosition = new Vector2(targetX, container.anchoredPosition.y);
            tweenRoutine = null;
        }

        private void StopTween()
        {
            if (tweenRoutine != null)
            {
                StopCoroutine(tweenRoutine);
                tweenRoutine = null;
            }
        }
        #endregion

        #region Drag Handlers
        public void OnBeginDrag(PointerEventData eventData)
        {
            StopTween();
            dragVelocity = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (container == null || PageWidth <= 0f)
                return;

            // eventData.delta는 스크린 픽셀 → canvas 단위로 변환해야 anchoredPosition과 단위가 맞는다.
            float dx = eventData.delta.x / ScaleFactor;
            float x = container.anchoredPosition.x + dx;

            // 양 끝을 넘어가면 저항을 줘서 고무줄처럼 (넘어갈 수 없다는 걸 체감시킴)
            float min = -(pageCount - 1) * PageWidth;
            const float max = 0f;
            if (x > max)
                x = max + (x - max) * edgeResistance;
            else if (x < min)
                x = min + (x - min) * edgeResistance;

            container.anchoredPosition = new Vector2(x, container.anchoredPosition.y);

            if (Time.unscaledDeltaTime > 0f)
                dragVelocity = dx / Time.unscaledDeltaTime;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            float w = PageWidth;
            if (w <= 0f)
                return;

            // 현재 페이지 기준으로 얼마나 끌었는지(왼쪽으로 끌면 음수).
            float offset = container.anchoredPosition.x + CurrentPage * w;
            int target = CurrentPage;

            if (Mathf.Abs(dragVelocity) > flickVelocity)
                target = CurrentPage - (int)Mathf.Sign(dragVelocity);       // 빠른 플릭
            else if (Mathf.Abs(offset) > w * swipeDistanceRatio)
                target = CurrentPage - (int)Mathf.Sign(offset);            // 충분히 끌었음

            SetPage(target, instant: false);
        }
        #endregion
    }
}
