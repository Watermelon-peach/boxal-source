using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 누르는 동안 콜백을 <b>반복</b>해서 부르는 트리거. 상점의 구매 버튼에 붙어
    /// "꾹 누르면 레벨이 드르륵 올라가는" 조작을 만든다.
    /// 아이템마다 하나씩 필요해 <see cref="ShopItemUI"/>가 수집 시점에 자동으로 붙인다.
    /// </summary>
    /// <remarks>
    /// ★<see cref="Button"/>의 onClick을 쓰지 않고 이 트리거가 입력을 전부 가져간다.
    /// 둘 다 쓰면 한 번 탭할 때 onClick과 첫 반복이 겹쳐 두 번 구매된다.
    /// <para/>
    /// ★버튼이 페이저(<see cref="Boxal.Util.UiPager"/>) 안에 있어서, 눌린 채로 좌우로 끌면
    /// 페이지를 넘기려는 동작이다. <see cref="PointerEventData.dragging"/>이 서면 반복을 멈춘다
    /// (<see cref="UpgradeSlotHoldTrigger"/>가 스크롤과 롱프레스를 가르는 것과 같은 방식).
    /// <para/>
    /// ★간격을 점점 좁혀서(first → interval → minInterval) 오래 누를수록 빨라지게 한다.
    /// 처음부터 빠르면 한 번만 사려던 사람이 실수로 여러 레벨을 사게 된다.
    /// </remarks>
    public class ShopBuyHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        #region Variables
        private float firstDelay = 0.35f;
        private float interval = 0.15f;
        private float minInterval = 0.045f;
        private float accel = 0.85f;

        /// <summary>한 번 구매를 시도한다. 더 살 수 없으면 false를 돌려주고, 그러면 반복을 멈춘다.</summary>
        private Func<bool> tryBuy;

        private Coroutine repeatRoutine;
        #endregion

        #region Unity Event Methods
        /// <summary>패널이 꺼지거나 오브젝트가 비활성화되면 눌린 상태가 남지 않게 정리한다.</summary>
        private void OnDisable()
        {
            StopRepeat();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StopRepeat();
            repeatRoutine = StartCoroutine(RepeatRoutine(eventData));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopRepeat();
        }

        /// <summary>손가락이 버튼 밖으로 나가면 멈춘다(누른 채 화면을 쓸어 넘기는 경우).</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            StopRepeat();
        }
        #endregion

        #region Custom Methods
        /// <summary>반복 간격과 콜백을 지정한다(수집 시 1회).</summary>
        public void Configure(float firstDelay, float interval, float minInterval, float accel, Func<bool> tryBuy)
        {
            this.firstDelay = firstDelay;
            this.interval = interval;
            this.minInterval = minInterval;
            this.accel = Mathf.Clamp(accel, 0.1f, 1f);
            this.tryBuy = tryBuy;
        }

        private void StopRepeat()
        {
            if (repeatRoutine != null)
            {
                StopCoroutine(repeatRoutine);
                repeatRoutine = null;
            }
        }

        private IEnumerator RepeatRoutine(PointerEventData eventData)
        {
            // 첫 한 번은 누르는 즉시. 탭 한 번 = 한 레벨이라는 게 기본 동작이다.
            if (tryBuy == null || !tryBuy())
            {
                repeatRoutine = null;
                yield break;
            }

            float wait = firstDelay;
            float currentInterval = interval;

            while (true)
            {
                float elapsed = 0f;
                while (elapsed < wait)
                {
                    // 페이지를 넘기려고 끌기 시작했으면 구매가 아니다.
                    if (eventData != null && eventData.dragging)
                    {
                        repeatRoutine = null;
                        yield break;
                    }
                    // 홈은 timeScale의 영향을 받지 않는 게 안전하다(UiPager와 같은 방침).
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!tryBuy())
                    break; // 골드가 떨어졌거나 최대 레벨 — 더 반복할 이유가 없다

                wait = currentInterval;
                currentInterval = Mathf.Max(minInterval, currentInterval * accel);
            }

            repeatRoutine = null;
        }
        #endregion
    }
}
