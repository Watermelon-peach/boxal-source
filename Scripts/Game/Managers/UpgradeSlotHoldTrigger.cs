using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Boxal.Game.UI
{
    /// <summary>
    /// UI 요소를 일정 시간 누르고 있으면(롱프레스) 콜백을 부르고, 떼면 해제 콜백을 부른다.
    /// 퍼즈의 업그레이드 슬롯에서 설명 툴팁을 띄우는 데 쓴다.
    /// 슬롯마다 하나씩 필요해 <see cref="UpgradeHistoryUI"/>가 수집 시점에 자동으로 붙인다.
    /// </summary>
    /// <remarks>
    /// ★ScrollRect 안에서 쓰이므로 스크롤 드래그와 구분해야 한다. 목록을 넘기려고 끌었을 뿐인데
    /// 툴팁이 뜨면 안 되므로, 누르는 동안 <see cref="PointerEventData.dragging"/>을 지켜보다
    /// 드래그가 시작되면 취소한다(EventSystem이 같은 PointerEventData 인스턴스를 갱신하므로
    /// 눌린 뒤에 읽어도 현재 상태가 나온다).
    ///
    /// ★홀드 시간은 unscaled 시간으로 잰다 — 퍼즈 중에는 timeScale=0이라 스케일 시간이 흐르지 않는다.
    ///
    /// ★대상 오브젝트에 raycastTarget이 켜진 Graphic(Image 등)이 있어야 포인터 이벤트가 들어온다.
    /// </remarks>
    public class UpgradeSlotHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        #region Variables
        private float holdSeconds = 0.4f;
        private Action held;
        private Action released;
        private Coroutine holdRoutine;
        #endregion

        #region Unity Event Methods
        /// <summary>슬롯이 꺼지거나(빈 칸 처리) 퍼즈 패널이 닫히면 눌린 상태가 그대로 남지 않게 정리한다.</summary>
        private void OnDisable()
        {
            if (holdRoutine != null)
            {
                StopCoroutine(holdRoutine);
                holdRoutine = null;
            }
            released?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (holdRoutine != null)
                StopCoroutine(holdRoutine);
            holdRoutine = StartCoroutine(HoldRoutine(eventData));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (holdRoutine != null)
            {
                StopCoroutine(holdRoutine);
                holdRoutine = null;
            }
            released?.Invoke();
        }
        #endregion

        #region Custom Methods
        /// <summary>홀드 시간과 콜백을 지정한다(수집 시 1회).</summary>
        public void Configure(float seconds, Action onHeld, Action onReleased)
        {
            holdSeconds = seconds;
            held = onHeld;
            released = onReleased;
        }

        private IEnumerator HoldRoutine(PointerEventData eventData)
        {
            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                // 스크롤이 시작되면 롱프레스가 아니라 목록 넘기기다.
                if (eventData != null && eventData.dragging)
                {
                    holdRoutine = null;
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            holdRoutine = null;
            held?.Invoke();
        }
        #endregion
    }
}
