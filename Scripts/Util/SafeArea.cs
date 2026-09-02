using UnityEngine;

namespace Boxal.Util
{
    /// <summary>
    /// 이 RectTransform을 기기의 Safe Area(노치·펀치홀·홈 인디케이터·둥근 모서리를 제외한
    /// 실제 표시 영역)에 맞춰 인셋한다. UI를 이 오브젝트의 자식으로 넣으면 자동으로 안쪽에 배치된다.
    /// Screen.safeArea는 픽셀 Rect라, 화면 크기로 정규화해 anchorMin/Max에 반영한다.
    /// 화면 회전·해상도 변경을 감지해 갱신한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [Tooltip("가로축(좌우) 인셋 적용 여부. 배경처럼 화면을 꽉 채워야 하는 요소엔 이 컴포넌트를 쓰지 말 것.")]
        [SerializeField] private bool conformX = true;
        [Tooltip("세로축(상하) 인셋 적용 여부.")]
        [SerializeField] private bool conformY = true;

        private RectTransform rectTransform;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int lastScreenSize = new Vector2Int(0, 0);
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            // 회전/해상도/멀티태스킹 창 변경 등으로 safeArea가 바뀌면 다시 적용.
            if (Screen.safeArea != lastSafeArea
                || Screen.width != lastScreenSize.x
                || Screen.height != lastScreenSize.y
                || Screen.orientation != lastOrientation)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (rectTransform == null)
                return;

            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;

            // 화면 크기가 0이면(초기화 전) 스킵.
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;
            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // 축별로 인셋을 켜고 끌 수 있게(예: 상하만 노치 회피, 좌우는 꽉 채우기).
            if (!conformX)
            {
                anchorMin.x = 0f;
                anchorMax.x = 1f;
            }
            if (!conformY)
            {
                anchorMin.y = 0f;
                anchorMax.y = 1f;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            // offset을 0으로 두면 anchor에 딱 맞게 늘어난다.
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
