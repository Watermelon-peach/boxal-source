using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Boxal.Util
{
    /// <summary>
    /// 붙어 있는 오브젝트에서 손가락(마우스)을 뗀 순간을 알린다.
    /// Unity <see cref="UnityEngine.UI.Slider"/>는 "조작이 끝났다"는 이벤트가 없고
    /// onValueChanged가 드래그 중 프레임마다 발생한다. 저장·미리듣기처럼 한 번만 해야 하는 일은
    /// 이 릴레이의 <see cref="Released"/>로 받는다.
    /// IEndDragHandler가 아니라 PointerUp을 쓰는 이유: 트랙을 탭만 해도(드래그 없이) 값이 바뀌는데
    /// 그때는 드래그 이벤트가 오지 않는다. PointerUp은 두 경우 모두 온다.
    /// 씬에서 직접 붙일 필요 없이 사용하는 쪽에서 런타임에 AddComponent 한다.
    /// </summary>
    public class SliderReleaseRelay : MonoBehaviour, IPointerUpHandler
    {
        /// <summary>포인터를 뗀 순간 발생.</summary>
        public event Action Released;

        public void OnPointerUp(PointerEventData eventData)
        {
            Released?.Invoke();
        }
    }
}
