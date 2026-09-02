using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Boxal.Util
{
    /// <summary>
    /// UiPager 안에 들어가는 세로 스크롤 리스트용 ScrollRect.
    /// 기본 ScrollRect는 자기가 드래그를 먹어버려서, 리스트 위에서 좌우로 스와이프해도
    /// 부모 페이저가 페이지를 못 넘긴다. 이 클래스는 드래그 시작 방향을 보고
    /// 세로면 자기가 처리하고, 가로면 부모(UiPager)에게 넘긴다.
    /// 리더보드 ScrollView의 ScrollRect를 이걸로 교체해서 사용할 것.
    /// </summary>
    public class PagerFriendlyScrollRect : ScrollRect
    {
        private bool routeToParent;

        public override void OnBeginDrag(PointerEventData eventData)
        {
            // delta는 시작 프레임에 불안정할 수 있어 press 지점 대비 이동량으로 방향을 판정한다.
            Vector2 drag = eventData.position - eventData.pressPosition;
            routeToParent = Mathf.Abs(drag.x) > Mathf.Abs(drag.y);

            if (routeToParent)
                DoForParents<IBeginDragHandler>(h => h.OnBeginDrag(eventData));
            else
                base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (routeToParent)
                DoForParents<IDragHandler>(h => h.OnDrag(eventData));
            else
                base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (routeToParent)
                DoForParents<IEndDragHandler>(h => h.OnEndDrag(eventData));
            else
                base.OnEndDrag(eventData);

            routeToParent = false;
        }

        /// <summary>부모 계층을 거슬러 올라가며 해당 핸들러를 가진 컴포넌트에 이벤트를 전달한다.</summary>
        private void DoForParents<T>(Action<T> action) where T : IEventSystemHandler
        {
            Transform parent = transform.parent;
            while (parent != null)
            {
                foreach (var component in parent.GetComponents<Component>())
                {
                    if (component is T handler)
                        action(handler);
                }
                parent = parent.parent;
            }
        }
    }
}
