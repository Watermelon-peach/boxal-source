using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 홈 화면 전용 종료 확인 팝업. 지금은 홈에서 뒤로가기를 누르면 확인 없이
    /// 안드로이드 기본 동작(즉시 종료)으로 이어지는데, 이를 막기 위한 팝업이다.
    /// 항상 활성인 HomeCanvas에 부착한다(팝업 자신에 두면 비활성 상태에서 동작하지 않는다).
    /// </summary>
    public class ExitConfirmPopupUI : MonoBehaviour
    {
        #region Variables
        [Tooltip("팝업 루트(Dim + 패널을 포함한 전체화면 오브젝트). 평소 비활성.")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button quitButton;

        [Tooltip("떠 있는 동안은 뒤로가기를 무시해야 하는 강제 팝업(닉네임 최초 입력). 없으면 비워도 됨.")]
        [SerializeField] private NicknamePopupUI nicknamePopup;
        #endregion

        #region Properties
        private bool IsShowing => popupRoot != null && popupRoot.activeSelf;

        /// <summary>지금 종료 확인창을 띄워도 되는지. 나갈 방법이 없어야 하는 강제 팝업 위에는 띄우지 않는다.</summary>
        private bool CanShow => !(nicknamePopup != null && nicknamePopup.IsShowing);
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
            if (quitButton != null)
                quitButton.onClick.AddListener(Application.Quit);
        }

        private void Start()
        {
            Hide();
        }

        private void Update()
        {
            if (!WasBackPressedThisFrame())
                return;

            if (IsShowing)
                Hide();
            else if (CanShow)
                Show();
        }
        #endregion

        #region Custom Methods
        /// <summary>
        /// 안드로이드 뒤로가기 키가 눌렸는지. Input System은 안드로이드 백 버튼을 키보드 Escape로 전달한다
        /// (에디터에서도 Esc로 같은 동작을 확인할 수 있다).
        /// </summary>
        private static bool WasBackPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }

        public void Show()
        {
            if (popupRoot != null)
                popupRoot.SetActive(true);
        }

        public void Hide()
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
        }
        #endregion
    }
}
