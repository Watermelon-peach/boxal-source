using UnityEngine;
using UnityEngine.EventSystems;

namespace Boxal.Game
{
    /// <summary>
    /// 점프버튼 이벤트 트리거 클래스
    /// </summary
    public class JumpButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public ChargeJump jump;

        public void OnPointerDown(PointerEventData eventData)
        {
            jump.StartCharge();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            jump.ReleaseJump();
        }
    }

}
