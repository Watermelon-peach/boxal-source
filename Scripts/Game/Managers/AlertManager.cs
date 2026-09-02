using System.Collections;
using Boxal.Util;
using Boxal.Game.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 인게임 경고 연출을 담당한다.
    /// 1) LowHP: 다음 접촉 피격 한 방에 죽는 "한방컷" 상태면 경고를 즉시 켜고, 벗어나면 즉시 끈다.
    /// 2) Boss: 보스 등장 시 카메라를 보스로 옮겨 3초 홀드하며 경고를 싸인 곡선으로 점멸한다(인트로 연출과 유사).
    /// </summary>
    public class AlertManager : Singleton<AlertManager>
    {
        #region Variables
        [Header("한방컷 경고 (LowHP)")]
        [Tooltip("한방컷 상태에서 Alpha=1, 아니면 Alpha=0으로 즉시 토글되는 CanvasGroup")]
        [SerializeField] private CanvasGroup lowHpGroup;

        [Header("보스 경고 (Boss)")]
        [Tooltip("보스 등장 시 점멸하는 CanvasGroup")]
        [SerializeField] private CanvasGroup bossGroup;
        [Tooltip("보스 등장 카메라 홀드 시간(초). 인트로와 동일하게 실시간 기준.")]
        [SerializeField] private float bossHoldDuration = 3f;
        [Tooltip("홀드 동안 점멸 횟수(싸인 곡선의 봉우리 개수)")]
        [SerializeField] private int bossBlinkCount = 3;
        [Tooltip("보스 경고 동안 게임을 정지(timeScale=0)할지. 인트로 연출과 동일하게 기본 true.")]
        [SerializeField] private bool freezeDuringBossAlert = true;

        private bool bossAlertActive;
        // 한방컷 상태 진입 순간에만 경고음을 1회 울리기 위한 엣지 추적.
        private bool wasLethal;
        #endregion

        #region Unity Event Methods
        protected override void Awake()
        {
            base.Awake();
            if (lowHpGroup != null) lowHpGroup.alpha = 0f;
            if (bossGroup != null) bossGroup.alpha = 0f;
        }

        private void Update()
        {
            UpdateLowHpAlert();
        }
        #endregion

        #region Custom Methods
        /// <summary>플레이어가 한방컷 상태면 LowHP 경고 Alpha를 즉시 1, 아니면 즉시 0으로 세팅한다.</summary>
        private void UpdateLowHpAlert()
        {
            if (lowHpGroup == null) return;

            bool lethal = GameManager.InstanceExist && !GameManager.Instance.IsGameOver
                          && Player.InstanceExist && Player.Instance.IsLethalState;

            float target = lethal ? 1f : 0f;
            if (!Mathf.Approximately(lowHpGroup.alpha, target))
                lowHpGroup.alpha = target; // 즉시 반영(페이드 없음)

            // 상태 진입(false→true) 순간에만 단발 경고음. 루프는 후반 라운드에서 상시 켜져 위험(Sound_Design.md).
            if (lethal && !wasLethal)
                SoundManager.Instance?.PlaySfx(SoundId.LowHpAlert);
            wasLethal = lethal;
        }

        /// <summary>보스 등장 연출: 카메라 추적 타겟을 보스로 옮기고 홀드 시간 동안 경고를 점멸한 뒤 플레이어로 복귀한다.</summary>
        public void AlertBoss(Transform boss)
        {
            if (boss == null || bossAlertActive) return;
            StartCoroutine(BossAlertRoutine(boss));
        }

        private IEnumerator BossAlertRoutine(Transform boss)
        {
            bossAlertActive = true;

            GameManager gm = GameManager.InstanceExist ? GameManager.Instance : null;
            CinemachineCamera cam = gm != null ? gm.cineCam : null;
            Transform playerTr = Player.InstanceExist ? Player.Instance.transform : null;

            // 인트로 루틴과 동일하게 정지 + 퍼즈 버튼 잠금
            if (freezeDuringBossAlert)
            {
                Time.timeScale = 0f;
                if (gm != null) gm.SetPauseButtonInteractable(false);
            }

            // 추적 타겟을 보스로 이동
            if (cam != null) cam.Follow = boss;

            // 홀드 동안 싸인 곡선으로 점멸 (timeScale=0이므로 unscaled 시간 사용)
            float t = 0f;
            float duration = Mathf.Max(0.01f, bossHoldDuration);
            while (t < duration)
            {
                if (bossGroup != null)
                    bossGroup.alpha = Mathf.Abs(Mathf.Sin(Mathf.PI * bossBlinkCount * (t / duration)));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (bossGroup != null) bossGroup.alpha = 0f;

            // 카메라 복귀 + 게임 재개
            if (cam != null && playerTr != null) cam.Follow = playerTr;
            if (freezeDuringBossAlert)
            {
                Time.timeScale = 1f;
                if (gm != null) gm.SetPauseButtonInteractable(true);
            }

            bossAlertActive = false;
        }
        #endregion
    }
}
