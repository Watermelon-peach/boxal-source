using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 플레이어 정보 위젯(UserInfo01_Left). 닉네임·순위·백분위 슬라이더를 표시한다.
    /// Home MainPanel과 PlayScene의 동명 오브젝트에 같은 컴포넌트로 재사용한다.
    /// 슬라이더 value = 1 - (rank-1)/total  → 1등이면 1.0(만땅), 꼴찌면 0에 가까움.
    /// Trophy/Profile 아이콘은 티어·프로필 기능 추가 시 확장(지금은 정적).
    /// </summary>
    public class UserInfoUI : MonoBehaviour
    {
        #region Variables
        [Header("Texts")]
        [Tooltip("닉네임 (Bg_Top/Text_Name)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [Tooltip("순위 (Bg_Top/Text_Rank)")]
        [SerializeField] private TextMeshProUGUI rankText;

        [Header("Percentile")]
        [Tooltip("백분위 슬라이더 (Bg_Bottom/Slider). value 0~1")]
        [SerializeField] private Slider rankSlider;

        [Header("Labels")]
        [SerializeField] private string defaultName = "Player";
        [Tooltip("기록이 없을 때 순위 표시.")]
        [SerializeField] private string unrankedLabel = "-";

        [Header("Behaviour")]
        [Tooltip("Start 시 자동 새로고침. PlayScene에선 끄고 게임 시작/오버 때 Refresh() 호출해도 됨.")]
        [SerializeField] private bool refreshOnStart = true;
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            if (refreshOnStart)
                Refresh();
        }
        #endregion

        #region Custom Methods
        /// <summary>닉네임/순위/슬라이더를 최신 상태로 갱신한다.</summary>
        public async void Refresh()
        {
            // 닉네임은 로컬에서 먼저 즉시 표시(네트워크를 안 기다림).
            SetName(PlayerStats.PlayerName);

            if (!LeaderboardManager.InstanceExist)
            {
                ShowUnranked();
                return;
            }

            PlayerStanding? standing = await LeaderboardManager.Instance.GetMyStandingAsync();
            if (!standing.HasValue || standing.Value.Total <= 0)
            {
                ShowUnranked();
                return;
            }

            PlayerStanding s = standing.Value;
            if (!string.IsNullOrEmpty(s.PlayerName))
                SetName(s.PlayerName);

            if (rankText != null)
                rankText.text = $"#{s.Rank}";

            if (rankSlider != null)
                rankSlider.value = s.Total > 1 ? 1f - (float)(s.Rank - 1) / s.Total : 1f;
        }

        private void SetName(string name)
        {
            if (nameText == null)
                return;
            // 로컬 저장 이름/서버 응답 모두 "#1234" 태그가 붙어 있으므로 표시 직전에 떼어낸다.
            string display = LeaderboardRow.StripTag(name);
            nameText.text = string.IsNullOrEmpty(display) ? defaultName : display;
        }

        private void ShowUnranked()
        {
            if (rankText != null)
                rankText.text = unrankedLabel;
            if (rankSlider != null)
                rankSlider.value = 0f;
        }
        #endregion
    }
}
