using UnityEngine.Events;
using UnityEngine.UI;

namespace Boxal.Util
{
    /// <summary>
    /// 설정용 Toggle을 현재 값에 맞춰 초기화하고 변경 콜백을 연결한다.
    /// </summary>
    /// <remarks>
    /// ★씬의 토글에는 <see cref="SwitchToggle"/>(손잡이 슬라이드 연출)이 붙어 있다. 이 연출은
    /// onValueChanged로만 갱신되고 OnEnable(=Start보다 먼저)에서 한 번 반영되므로,
    /// <see cref="Toggle.SetIsOnWithoutNotify"/>만 부르면 값과 스위치 위치가 어긋난 채 남는다.
    /// 설정 토글이 여러 화면(홈 설정 / 퍼즈 팝업)에 있어 이 처리를 한곳에 모은다.
    /// </remarks>
    public static class ToggleBinding
    {
        /// <summary>토글을 현재 값으로 맞추고(이벤트 없이) 변경 리스너를 건다.</summary>
        public static void Bind(Toggle toggle, bool currentValue, UnityAction<bool> onChanged)
        {
            if (toggle == null)
                return;

            SetWithoutNotify(toggle, currentValue);
            if (onChanged != null)
                toggle.onValueChanged.AddListener(onChanged);
        }

        /// <summary>이벤트를 발생시키지 않고 값과 연출 상태를 함께 맞춘다(다시 열 때 재동기화용).</summary>
        public static void SetWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle == null)
                return;

            var switchToggle = toggle.GetComponent<SwitchToggle>();
            if (switchToggle != null)
                switchToggle.SetOn(value, animate: false); // 값 + 손잡이 위치를 함께 반영
            else
                toggle.SetIsOnWithoutNotify(value);
        }
    }
}
