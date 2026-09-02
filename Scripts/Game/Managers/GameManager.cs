using Boxal.Util;
using Boxal.Game.UI;
using Boxal.Game.Audio;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Boxal.Game
{
    public class GameManager : Singleton<GameManager>
    {
        #region Variables
        public CinemachineCamera cineCam;

        public Transform spawnPoint;
        private Transform origin;

        [SerializeField] private GameOverUI gameOverUI;
        [SerializeField] private PauseUI pauseUI;

        // 이번 판(런) 통계
        private float runStartTime;

        /// <summary>인트로가 끝난 뒤 튜토리얼을 띄워야 하는지(첫 판에만 true).</summary>
        private bool runTutorialAfterIntro;
        #endregion

        #region Properties
        public bool IsGameOver { get; private set; }

        /// <summary>이번 판에서 처치한 박스몬 수.</summary>
        public int RunKills { get; private set; }

        /// <summary>이번 판 점수(처치한 박스몬의 티어 데미지 누적). 상점 포인트 배수가 적용된 값이다.</summary>
        public long RunPoints { get; private set; }

        /// <summary>
        /// 이번 판에 번 골드. 판중에는 여기 쌓아 두고 게임오버에서 한 번만 계정에 넘긴다
        /// (처치마다 저장하면 PlayerPrefs.Save()가 초당 여러 번 돌아 기기에서 끊긴다).
        /// </summary>
        public long RunGold { get; private set; }

        /// <summary>이번 판 경과 시간(초). Time.time 기반이라 timeScale=0(인트로/업글 선택/퍼즈) 구간은 제외된다.</summary>
        public float RunElapsedSeconds => Time.time - runStartTime;
        #endregion

        #region Unity Event Methods
        protected override void Awake()
        {
            base.Awake();
            origin = Player.Instance.transform;
        }

        private void Start()
        {
            // PlayScene 진입 시 자동으로 게임 시작 (Home의 PLAY 버튼 → LoadScene("PlayScene") → 여기서 시작).
            // 단, 한 프레임 미뤄서 다른 컴포넌트의 Start()가 모두 끝난 뒤 시작한다.
            // (PauseUI.Start()가 Hide()로 timeScale=1을 세팅하는 등 Start 순서 충돌로 인트로 정지가 덮이는 것을 방지)
            IsGameOver = true;
            StartCoroutine(StartGameDeferred());
        }

        private IEnumerator StartGameDeferred()
        {
            yield return null; // 모든 Start() 완료 보장
            OnGameStart();
        }
        #endregion

        #region Custom Methods
        public void OnGameStart()
        {
            // 진행 중이던 게임을 정리하고 처음부터 다시 시작 (게임오버 여부 무관)
            StopAllCoroutines();
            Time.timeScale = 1f;
            SpawnManager.Instance.ResetSpawns();
            RoundManager.Instance.ResetRound();
            UpgradeManager.Instance.ResetUpgrades();
            LevelManager.Instance.ResetLevel();
            if (UltimateManager.InstanceExist)
                UltimateManager.Instance.ResetUlt();
            if (BossRewardManager.InstanceExist)
                BossRewardManager.Instance.ResetRewards();

            IsGameOver = false;
            SoundManager.Instance?.PlayBgm(BgmId.Play);
            // 런 통계 초기화
            RunKills = 0;
            RunGold = 0;
            if (UiManager.InstanceExist)
                UiManager.Instance.SetGold(0);
            RunPoints = 0;
            if (UiManager.InstanceExist)
                UiManager.Instance.SetPoints(0);
            runStartTime = Time.time;
            if (gameOverUI != null)
                gameOverUI.Hide();
            //초반 체력 세팅
            Player.Instance.PlayerInitSettings();

            // 첫 판 튜토리얼이 뜰 상황이면 라운드 시작을 튜토리얼이 끝날 때까지 미룬다.
            // 미루지 않으면 안내를 읽는 동안 무기가 몹을 깨서 XP가 쌓이고, 레벨업 카드가
            // 안내 위로 튀어나온다(Boxmon.BreakBox가 처치 시 킬·점수·XP를 한 번에 준다).
            runTutorialAfterIntro = TutorialHintUI.WillRun;
            if (!runTutorialAfterIntro)
                RoundManager.Instance.StartRound();

            //카메라 연출
            StartCoroutine(CameraWork());
        }
        
        private IEnumerator CameraWork()
        {
            Time.timeScale = 0f;
            if (pauseUI != null)
                pauseUI.SetPauseButtonInteractable(false);
            cineCam.Follow = spawnPoint;
            yield return new WaitForSecondsRealtime(3f);
            cineCam.Follow = origin;
            Time.timeScale = 1f;
            if (pauseUI != null)
                pauseUI.SetPauseButtonInteractable(true);

            // 조작이 실제로 가능해진 시점 — 첫 판이면 여기서 튜토리얼을 띄우고,
            // 끝나면(Start 버튼) 그때 1라운드를 시작한다.
            if (runTutorialAfterIntro)
            {
                runTutorialAfterIntro = false;
                TutorialHintUI.Begin(RoundManager.Instance.StartRound);
            }
        }

        /// <summary>퍼즈 버튼 상호작용 잠금/해제. 인트로·보스 경고 등 연출 정지 구간에서 재사용한다.</summary>
        public void SetPauseButtonInteractable(bool interactable)
        {
            if (pauseUI != null)
                pauseUI.SetPauseButtonInteractable(interactable);
        }

        public void GameOver()
        {
            // 재진입 차단. 왕보스 타임아웃 게임오버는 플레이어를 죽이지 않으므로, 그 뒤에도 살아 있는
            // 플레이어가 왕보스에 깔려 Player.Die()가 여기를 한 번 더 부른다(게임오버가 두 번 뜨던 버그).
            // 두 번 들어오면 GameOverUI.Show가 다시 돌아 누적 처치 수와 리더보드 제출까지 중복된다.
            if (IsGameOver)
                return;

            Debug.Log("게임오버!");
            IsGameOver = true;

            // 게임을 정지한다. 멈추지 않으면 패널 뒤에서 박스몬이 계속 떨어지고 플레이어가 계속 맞는다.
            Time.timeScale = 0f;
            // 정지 중에 퍼즈를 열었다 닫으면 PauseUI.Hide()가 timeScale을 1로 되돌려 다시 굴러간다.
            SetPauseButtonInteractable(false);

            SoundManager.Instance?.PlaySfx(SoundId.GameOver);
            // 게임오버 햅틱은 GameOverUI.Show에서 재생한다 — 신기록 여부에 따라 Success/Failure로
            // 갈리는데, 여기서도 울리면 둘이 연달아 나가 뭉개진다(햅틱은 단일 채널).

            if (gameOverUI != null)
                gameOverUI.Show(RoundManager.Instance.CurrentRound, RunKills, RunElapsedSeconds, RunPoints);
        }

        /// <summary>박스몬 처치 시 이번 판 처치 수와 획득 골드를 누적한다(Boxmon에서 호출).</summary>
        public void RegisterKill()
        {
            RunKills++;
            RunGold += ShopUpgrades.GoldPerKill;
            if (UiManager.InstanceExist)
                UiManager.Instance.SetGold(RunGold);
        }

        /// <summary>박스몬 처치 시 점수를 누적하고 HUD를 갱신한다(Boxmon에서 호출).</summary>
        public void AddPoints(long amount)
        {
            if (amount <= 0)
                return;
            // 상점의 포인트 배수는 여기서 한 번만 적용한다(모든 획득 경로가 이 함수를 지난다).
            amount = (long)System.Math.Round(amount * ShopUpgrades.PointMultiplier);
            if (amount <= 0)
                amount = 1;
            RunPoints += amount;
            if (UiManager.InstanceExist)
                UiManager.Instance.SetPoints(RunPoints);
        }
        #endregion
    }

}
