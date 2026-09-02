using UnityEngine;
using Boxal.Util;
using System.Collections;
using Boxal.Game.UI;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;

namespace Boxal.Game
{
    public class RoundManager : Singleton<RoundManager>
    {
        #region Variables
       

        private uint roundCount = 0;

        /// <summary>현재(도달한) 라운드 번호. 게임오버 결과 표시용.</summary>
        public int CurrentRound => (int)roundCount;

        /// <summary>박스몬 접촉 시 플레이어가 받는 데미지. 보스 티어(damageStepRounds 라운드마다)에 비례해 1씩 증가.
        /// 예) 5단위면 1~5라운드=1, 6~10=2, 11~15=3 ...</summary>
        public int EnemyContactDamage => Mathf.Max(1, ((int)roundCount - 1) / Mathf.Max(1, damageStepRounds) + 1);

        [SerializeField] private float roundDuration = 30f;
        [SerializeField] private int enemiesPerRound = 5;
        [Tooltip("보스 HP = 라운드 번호 × 이 값 (밸런싱 레버). 일반 몹은 라운드 번호 HP.")]
        [SerializeField] private float bossHpMultiplier = 7f;
        [Tooltip("이 라운드 간격마다 왕보스 등장 (20 = 20/40/60...). 5의 배수 권장.")]
        [SerializeField] private int kingBossInterval = 20;
        [Tooltip("왕보스 HP = 라운드 번호 × 이 값. 30초 내 못 잡으면 게임오버(하드 게이트).")]
        [SerializeField] private float kingBossHpMultiplier = 30f;
        private bool currentRoundIsKingBoss;
        [Tooltip("이 라운드 수마다 박스몬 접촉 데미지가 1씩 증가 (5 = 1~5라운드:1, 6~10:2 ...)")]
        [SerializeField] private int damageStepRounds = 5;
        [Header("라운드 타이머")]
        [SerializeField] private AnimationCurve curve;
        private Color startColor;
        [SerializeField] private Color targetColor = Color.red;
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            startColor = UiManager.Instance.timerUi.color;
        }
        #endregion

        #region Custom Methods
        private IEnumerator RoundTimer()
        {
            float timer = 0f;
            while (timer <= roundDuration)
            {
                if (GameManager.Instance.IsGameOver)
                {

                    yield break;
                }

                timer += Time.deltaTime;
                float t = curve.Evaluate(timer / roundDuration);
                UiManager.Instance.timerUi.text = ((int)(roundDuration - timer + 1)).ToString();
                UiManager.Instance.timerUi.color = Color.Lerp(startColor, targetColor, t);
                float scale = Mathf.Lerp(1f, 2f, t);
                UiManager.Instance.timerUi.transform.localScale = Vector3.one * scale;

                //타이머끝나기 전에 클리어 시 바로 탈출
                if (SpawnManager.Instance.currentEnemies == 0)
                {
                    StartRound();
                    yield break;
                }

                yield return null;
            }
            // ★위 IsGameOver 검사는 루프 안에만 있어서, 타이머 만료로 루프를 빠져나온 경로에는 걸리지 않는다.
            // 여기서 다시 확인하지 않으면 라운드 종료 직전에 죽었을 때 게임오버 패널 뒤에서 StartRound가
            // 돌아 다음 라운드가 시작된다(라운드 시작음 + 새 몹 스폰).
            if (GameManager.Instance.IsGameOver)
                yield break;

            // 타이머 만료: 왕보스 라운드였는데 여기 도달 = 왕보스 미처치(처치 시 위에서 조기 탈출) → 게임오버
            // ★단, currentEnemies는 디스폰 연출(despawnDelay 2초) 후에야 감소하므로 그것만 믿으면
            // 28~30초 사이에 처치해도 "미처치"로 오판해 억울한 게임오버가 난다. 처치 즉시 제거되는
            // aliveBoxmons에서 왕보스 생존 여부를 직접 확인한다.
            if (currentRoundIsKingBoss)
            {
                bool kingAlive = SpawnManager.Instance.aliveBoxmons
                    .Exists(b => b != null && b.IsKingBoss);
                if (kingAlive)
                {
                    if (!GameManager.Instance.IsGameOver)
                        GameManager.Instance.GameOver();
                    yield break;
                }
                // 왕보스는 이미 처치됨(디스폰 대기 중) → 클리어로 처리
            }
            StartRound();

        }
        public void StartRound()
        {
            roundCount++;
            ShowRoundCount();
            SoundManager.Instance?.PlaySfx(SoundId.RoundStart);
            currentRoundIsKingBoss = false;

            // 왕보스 라운드(간격 우선 판정) → 30초 하드 게이트
            if (kingBossInterval > 0 && roundCount % kingBossInterval == 0)
            {
                currentRoundIsKingBoss = true;
                Boxmon kingBoss = SpawnManager.Instance.Spawn((long)(roundCount * kingBossHpMultiplier), isBoss: true, isKingBoss: true);
                if (AlertManager.InstanceExist)
                    AlertManager.Instance.AlertBoss(kingBoss.transform);
                SoundManager.Instance?.PlaySfx(SoundId.KingBossAlert);
                HapticManager.Play(HapticType.Warning);
            }
            //5라운드마다 일반 보스 스폰 (일반 보스는 경고 없음, 왕보스만 경고)
            else if (roundCount % 5 == 0)
            {
                SpawnManager.Instance.Spawn((long)(roundCount * bossHpMultiplier), isBoss: true);
            }
            else
            {
                for (int i = 0; i < enemiesPerRound; i++)
                {
                    SpawnManager.Instance.Spawn(roundCount);
                }
            }

            // 왕보스 라운드에만 전용 BGM, 끝나면 다음 라운드 시작 시 기본 곡으로 복귀한다.
            // PlayBgm은 같은 트랙이 이미 돌고 있으면 무시하므로 매 라운드 호출해도 곡이 재시작되지 않는다.
            SoundManager.Instance?.PlayBgm(currentRoundIsKingBoss ? BgmId.KingBoss : BgmId.Play);

            StartCoroutine(RoundTimer());
        }
        /// <summary>진행 중인 타이머를 멈추고 라운드 수(점수)와 타이머 UI를 초기화한다. 게임 재시작용.</summary>
        public void ResetRound()
        {
            StopAllCoroutines();
            roundCount = 0;
            currentRoundIsKingBoss = false;
            UiManager.Instance.timerUi.transform.localScale = Vector3.one;
            UiManager.Instance.timerUi.color = startColor;
        }

        private void ShowRoundCount()
        {
            UiManager.Instance.roundUi.text = "Round " + roundCount.ToString();
            // 현재 라운드의 박스몬 접촉 데미지(맞으면 목숨=무기를 이만큼 잃음)를 HUD에 예고
            if (UiManager.Instance.dmgUi != null)
                UiManager.Instance.dmgUi.text = "DMG " + EnemyContactDamage.ToString();
        }

        #endregion
    }

}
