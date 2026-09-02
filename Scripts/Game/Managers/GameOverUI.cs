using Boxal.Util;
using Boxal.Game.Leaderboard;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Boxal.Game.UI
{
    /// <summary>
    /// 게임오버 패널 표시 및 결과 데이터 연결.
    /// GameManager가 Show/Hide를 호출한다. 항상 활성인 호스트(MainPlayCanvas)에 부착하고
    /// panel 참조로 GameOver 패널을 토글한다(UpgradeCardUI와 동일 패턴).
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        private static readonly int IsNewRecordHash = Animator.StringToHash("IsNewRecord");

        #region Variables
        [SerializeField] private GameObject panel;

        [Header("Final Round")]
        [Tooltip("도달 라운드 표시 (FinalRound)")]
        [SerializeField] private TextMeshProUGUI finalRoundText;
        [Tooltip("신기록 강조 애니메이터 (NewRecord). IsNewRecord bool 파라미터를 토글한다.")]
        [SerializeField] private Animator newRecordAnimator;

        [Header("Scores")]
        [Tooltip("최고 기록 (Best) — \"Round 00\" 포맷")]
        [SerializeField] private TextMeshProUGUI bestText;
        [Tooltip("생존 시간 (Time) — \"min : sec\" 포맷")]
        [SerializeField] private TextMeshProUGUI timeText;
        [Tooltip("처치 수 (Kill) — \"누적 + 이번판!\" 포맷")]
        [SerializeField] private TextMeshProUGUI killText;
        [Tooltip("이번 판 점수 (ScorePanel/FinalRound/Points) — 전체 쉼표 표기")]
        [SerializeField] private TextMeshProUGUI pointsText;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [Tooltip("홈(메인) 화면 이동 버튼.")]
        [SerializeField] private Button homeButton;
        [Tooltip("이동할 홈 씬 이름. Build Settings에 등록돼 있어야 함.")]
        [SerializeField] private string homeSceneName = "Home";
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestart);
            if (homeButton != null)
                homeButton.onClick.AddListener(OnHome);

            // 게임오버는 timeScale=0으로 게임을 세운다. Animator는 기본이 스케일 시간이라
            // 그대로 두면 신기록 애니메이션이 첫 프레임에서 얼어붙는다.
            if (newRecordAnimator != null)
                newRecordAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        private void Start()
        {
            Hide();
        }
        #endregion

        #region Custom Methods
        /// <summary>
        /// 게임오버 결과를 표시한다.
        /// finalRound = 도달 라운드, runKills = 이번 판 처치 수, runSeconds = 생존 시간(초), runPoints = 이번 판 점수.
        /// 신기록(New Record) 판정은 점수 기준.
        /// </summary>
        public void Show(int finalRound, int runKills, float runSeconds, long runPoints)
        {
            // 저장 반영 전 값 캡처 (Kill 표시의 a = 이번 판 이전까지의 누적 처치 수)
            int prevTotalKills = PlayerStats.TotalKills;
            bool isNewRecord = PlayerStats.TrySubmitScore(runPoints); // 신기록은 점수 기준
            PlayerStats.TrySubmitRound(finalRound);                   // 최고 라운드 기록도 갱신
            int bestRound = PlayerStats.BestRound;
            PlayerStats.AddKills(runKills);

            // 이번 판에 번 골드를 계정에 넘긴다(판중에는 GameManager가 들고만 있었다).
            if (GameManager.InstanceExist)
                Gold.Add(GameManager.Instance.RunGold);

            // 온라인 리더보드에 이번 판 점수를 제출(서버가 최고값만 유지). 매니저가 없으면 무시.
            // fire-and-forget: 게임오버 UI 표시를 네트워크 응답으로 막지 않는다.
            if (LeaderboardManager.InstanceExist)
                _ = LeaderboardManager.Instance.SubmitScoreAsync(runPoints);

            if (panel != null)
                panel.SetActive(true);

            if (finalRoundText != null)
                finalRoundText.text = $"Round {finalRound}";
            if (bestText != null)
                bestText.text = $"Round {bestRound}";
            if (timeText != null)
                timeText.text = NumberUtil.FormatMinSec(runSeconds);
            if (killText != null)
                killText.text = $"{prevTotalKills} <color=#ff6c6c>+ {runKills}!</color>";
            if (pointsText != null)
                pointsText.text = NumberUtil.FormatComma(runPoints) + " Points"; // 게임오버는 전체 표기

            // 신기록일 때만 NewRecord 오브젝트를 켜서 애니메이션을 재생한다. 오브젝트를 직접 토글해야,
            // 블링크 중 패널이 꺼져 애니메이션이 얼어붙은 시각 상태가 다음 사이클에 남는 버그를 막는다
            // (idle 상태 IsBest에 클립이 없어 스스로 리셋하지 못하기 때문).
            if (newRecordAnimator != null)
            {
                newRecordAnimator.gameObject.SetActive(isNewRecord);
                if (isNewRecord)
                {
                    newRecordAnimator.SetBool(IsNewRecordHash, true);
                    SoundManager.Instance?.PlaySfx(SoundId.NewRecord);
                }
            }

            // 게임오버 햅틱은 여기서 한 번만 — 신기록이면 축하, 아니면 실패.
            // (GameManager.GameOver에서도 울리면 두 개가 연달아 나가 뭉개진다)
            HapticManager.Play(isNewRecord ? HapticType.Success : HapticType.Failure);
        }

        /// <summary>패널을 숨긴다. NewRecord는 Show가 SetActive로 매번 재설정하므로 여기선 건드리지 않는다.</summary>
        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        private void OnRestart()
        {
            GameManager.Instance.OnGameStart();
        }

        private void OnHome()
        {
            // timeScale 정규화는 SceneFader가 로드 직전에 수행한다(연출 중에도 정지 상태를 유지해야 하므로).
            SceneFader.LoadScene(homeSceneName);
        }
        #endregion
    }
}
