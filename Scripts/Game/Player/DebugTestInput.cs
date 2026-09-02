using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace Boxal.Game
{
    /// <summary>
    /// 에디터 테스트용 키보드 입력. 버튼 대신 키보드로 점프/막기를 테스트한다.
    /// - 오른쪽 화살표: 차징점프 (누르는 동안 충전, 떼면 점프)
    /// - 왼쪽 화살표: 막기(패링)
    /// 빌드에는 포함되지 않는다(#if UNITY_EDITOR).
    /// </summary>
    public class DebugTestInput : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("참조 (비우면 씬에서 자동 탐색)")]
        [SerializeField] private ChargeJump jump;
        [SerializeField] private Parrying parry;
        [Tooltip("궁극기 버튼(UltGauge). 아래 방향키가 이 버튼을 누른 것처럼 동작한다.")]
        [SerializeField] private UnityEngine.UI.Button ultButton;

        private void Awake()
        {
            if (jump == null) jump = FindAnyObjectByType<ChargeJump>(FindObjectsInactive.Include);
            if (parry == null) parry = FindAnyObjectByType<Parrying>(FindObjectsInactive.Include);
            if (ultButton == null)
            {
                GameObject go = GameObject.Find("UI/MainPlayCanvas/PlayerActionInputs/UltGauge");
                if (go != null) ultButton = go.GetComponent<UnityEngine.UI.Button>();
            }
        }

        private void Update()
        {
            // Active Input Handling이 "Input System Package (New)"라 레거시 Input 클래스는 예외를 던진다.
            // 키보드가 없는 환경(디바이스 시뮬레이터 등)에서는 Keyboard.current가 null이다.
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            // 오른쪽 화살표 = 차징점프
            if (jump != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) jump.StartCharge();
                if (kb.rightArrowKey.wasReleasedThisFrame) jump.ReleaseJump();
            }

            // 왼쪽 화살표 = 막기
            if (parry != null && kb.leftArrowKey.wasPressedThisFrame) parry.OnParry();

            // 아래 화살표 = 궁극기 버튼 누른 것처럼 (버튼 onClick 호출)
            if (kb.downArrowKey.wasPressedThisFrame)
            {
                if (ultButton != null) ultButton.onClick.Invoke();
                else if (UltimateManager.InstanceExist) UltimateManager.Instance.TryActivate();
            }

            // 업그레이드 선택 (임시 — 4단계 카드 UI로 대체 예정)
            if (UpgradeManager.InstanceExist && UpgradeManager.Instance.IsChoosing)
            {
                if (kb.digit1Key.wasPressedThisFrame) UpgradeManager.Instance.SelectChoice(0);
                else if (kb.digit2Key.wasPressedThisFrame) UpgradeManager.Instance.SelectChoice(1);
                else if (kb.digit3Key.wasPressedThisFrame) UpgradeManager.Instance.SelectChoice(2);
            }
        }
#endif
    }
}
