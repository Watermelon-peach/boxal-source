using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    using Boxal.Game.Leaderboard;

    /// <summary>
    /// 최초 1회 닉네임 입력 팝업(강제). 닫기/건너뛰기 수단이 없고, 서버가 이름을 승인해야만 닫힌다.
    /// 항상 활성인 HomeCanvas에 부착한다(팝업 자신에 두면 비활성 상태에서 동작하지 않는다).
    ///
    /// 강제 팝업이라 실패 처리가 핵심이다. 사용자가 고쳐야 하는 실패(이름 규칙 위반)는 팝업을 유지한 채
    /// 규칙을 보여주고, 사용자 잘못이 아닌 실패(오프라인)는 재시도를 안내한다.
    /// 어느 경우든 무반응으로 두지 않는다 — 나갈 방법이 없는 팝업에서 무반응은 갇힌 것과 같기 때문이다.
    /// </summary>
    public class NicknamePopupUI : MonoBehaviour
    {
        #region Constants
        /// <summary>입력 상한. 씬의 TMP_InputField Character Limit과 같은 값을 코드에도 둔다(인스펙터 설정이 바뀌어도 방어).</summary>
        public const int MaxNameLength = 10;

        /// <summary>영문/숫자만 허용. UGS는 공백을 거부하며, 나머지 서버측 규칙은 응답으로만 알 수 있다.</summary>
        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9]+$");

        private const string MsgRule = "Letters & numbers only, max 10.";
        private const string MsgOffline = "No connection. Please try again.";
        private const string MsgUnknown = "Something went wrong. Please try again.";
        #endregion

        #region Variables
        [Header("Refs")]
        [Tooltip("팝업 루트(Dim + 패널을 포함한 전체화면 오브젝트). 평소 비활성.")]
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private TMP_InputField inputField;
        [Tooltip("에러 발생 시에만 메시지를 표시한다. 평소에는 빈 문자열.")]
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private Button confirmButton;

        /// <summary>닉네임이 서버에 확정 반영된 뒤 발생. UserInfo 등 표시 갱신용.</summary>
        public event Action NameConfirmed;

        private bool isSubmitting;
        #endregion

        #region Properties
        /// <summary>강제 팝업이 떠 있는 동안은 뒤로가기로 앱 종료 확인창을 띄우면 안 된다(나갈 방법이 없어야 함).</summary>
        public bool IsShowing => popupRoot != null && popupRoot.activeSelf;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
        }
        #endregion

        #region Custom Methods
        /// <summary>닉네임이 아직 없으면 팝업을 띄운다.</summary>
        public void ShowIfNeeded()
        {
            if (!PlayerStats.HasPlayerName)
                Show();
        }

        public void Show()
        {
            if (popupRoot == null)
                return;

            if (inputField != null)
                inputField.text = string.Empty;
            SetError(null);
            SetInteractable(true);

            popupRoot.SetActive(true);

            // 팝업을 켠 뒤에 포커스를 줘야 한다(비활성 상태에서는 선택이 먹지 않는다).
            if (inputField != null)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        public void Hide()
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
        }

        private async void OnConfirm()
        {
            if (isSubmitting)
                return;

            string raw = inputField != null ? inputField.text : string.Empty;
            string name = raw.Trim();

            // 1차 검증: 입력칸 설정(Alphanumeric/10자)이 이미 막지만, 인스펙터 값이 바뀌어도 규칙이 유지되도록 코드에서도 확인한다.
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || !NamePattern.IsMatch(name))
            {
                SetError(MsgRule);
                return;
            }

            if (!LeaderboardManager.InstanceExist)
            {
                SetError(MsgOffline);
                return;
            }

            isSubmitting = true;
            SetInteractable(false);
            SetError(null);

            NameSubmitResult result = await LeaderboardManager.Instance.SetPlayerNameAsync(name);

            isSubmitting = false;
            SetInteractable(true);

            if (result.Success)
            {
                Hide();
                NameConfirmed?.Invoke();
                return;
            }

            // 실패 사유별 안내. 팝업은 유지되고 버튼은 다시 살아나므로 그대로 재시도할 수 있다.
            switch (result.Error)
            {
                case NameSubmitError.InvalidName:
                    SetError(MsgRule);
                    break;
                case NameSubmitError.Offline:
                    SetError(MsgOffline);
                    break;
                default:
                    SetError(MsgUnknown);
                    break;
            }

            if (inputField != null)
                inputField.ActivateInputField();
        }

        /// <summary>에러 메시지 표시. null/빈 문자열이면 아무것도 보여주지 않는다.</summary>
        private void SetError(string message)
        {
            if (errorText != null)
                errorText.text = string.IsNullOrEmpty(message) ? string.Empty : message;
        }

        /// <summary>제출 중 중복 입력을 막는다.</summary>
        private void SetInteractable(bool value)
        {
            if (confirmButton != null)
                confirmButton.interactable = value;
            if (inputField != null)
                inputField.interactable = value;
        }
        #endregion
    }
}
