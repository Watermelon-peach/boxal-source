using Boxal.Util;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game
{
    /// <summary>
    /// 궁극기(광폭화) 게이지 관리. 박스몬 처치로 게이지를 채우고, 가득 차 발동하면
    /// 2페이즈로 진행한다:
    ///  ① 대시 페이즈(상수 1초): 위로 대시 + 궤도 회전속도 10배(상수)
    ///  ② 지속 페이즈(궁극기 지속시간): 궤도 회전속도에 속도배수 적용
    /// 발동 내내 무적. 런 한정(세이브 없음).
    ///
    /// 핵심: 스탯을 저장/복원하지 않고 "배수 레이어"(Orbit.speedMult)로 얹는다.
    /// 베이스 rotationSpeed는 업그레이드 소유라 발동 중 레벨업이 들어와도 오염되지 않는다.
    /// </summary>
    public class UltimateManager : Singleton<UltimateManager>
    {
        #region Variables
        [Header("UI (UltGauge 자식)")]
        [Tooltip("충전량 표시 (fillAmount 0~1)")]
        [SerializeField] private Image ultFill;
        [Tooltip("남은 지속시간 표시 (fillAmount, 평시 0 → 발동 시 1→0)")]
        [SerializeField] private Image durationFill;

        [Header("충전")]
        [Tooltip("박스몬 1처치당 차는 게이지(0~1). 0.04 = 25킬 만충")]
        [SerializeField] private float chargePerKill = 0.04f;
        [Tooltip("초당 자동 충전량(0~1). 처치가 뜸한 보스전 등에서도 게이지가 조금씩 찬다. 0.01 = 100초에 만충")]
        [SerializeField] private float chargePerSecond = 0.01f;

        [Header("발동 · 대시 페이즈(상수)")]
        [Tooltip("대시 + 초고속 회전이 유지되는 시간(초)")]
        [SerializeField] private float dashDuration = 1f;
        [Tooltip("대시 페이즈 회전속도(초당 바퀴 수). 5 = 1초에 5바퀴")]
        [SerializeField] private float dashRevolutionsPerSecond = 5f;

        [Header("발동 · 지속 페이즈")]
        [Tooltip("지속시간(초). 지속시간 업그레이드가 여기에 가산된다.")]
        [SerializeField] private float durationSeconds = 5f;
        [Tooltip("지속 동안 궤도 회전속도 배수")]
        [SerializeField] private float speedMultiplier = 10f;

        private float charge = 0f;
        // "지금 쓸 수 있다" 소리를 게이지가 처음 가득 찬 순간 1회만 울리기 위한 엣지 추적.
        private bool readyAnnounced;
        private float efficiencyMult = 1f;
        /// <summary>오토차지(시간충전) 전용 배수. 킬차지(efficiencyMult)와 분리된 별도 레이어(Legendary용).</summary>
        private float autoChargeEfficiencyMult = 1f;
        private float durationBonus = 0f;
        private Coroutine berserkRoutine;
        #endregion

        #region Properties
        /// <summary>게이지가 가득 차 발동 가능한지.</summary>
        public bool IsReady => charge >= 1f;
        /// <summary>발동 중인지.</summary>
        public bool IsActive => berserkRoutine != null;
        #endregion

        #region Unity Event Methods
        private void Update()
        {
            // 시간 경과 자동 충전 — 처치가 뜸한 보스전에서도 게이지가 조금씩 찬다.
            // 발동 중/만충/게임오버가 아닐 때만. timeScale=0(업글 선택·인트로) 중엔 deltaTime=0이라 자연히 멈춤.
            if (IsActive || IsReady)
                return;
            if (!GameManager.InstanceExist || GameManager.Instance.IsGameOver)
                return;

            charge = Mathf.Clamp01(charge + chargePerSecond * autoChargeEfficiencyMult * Time.deltaTime);
            UpdateChargeUI();
            AnnounceReadyIfNeeded();
        }
        #endregion

        #region Custom Methods
        /// <summary>박스몬 처치 시 게이지 충전 (Boxmon에서 호출).</summary>
        public void AddCharge()
        {
            charge = Mathf.Clamp01(charge + chargePerKill * efficiencyMult);
            UpdateChargeUI();
            AnnounceReadyIfNeeded();
        }

        /// <summary>게이지가 이번에 처음 가득 찼으면 준비 완료음을 1회 재생한다.</summary>
        private void AnnounceReadyIfNeeded()
        {
            if (IsReady && !readyAnnounced)
            {
                readyAnnounced = true;
                SoundManager.Instance?.PlaySfx(SoundId.UltReady);
            }
        }

        /// <summary>업그레이드: 킬차지 효율 증가 (add 가산 후 multiply 승산). Common.</summary>
        public void AddEfficiency(float multiply = 1f, float add = 0f)
        {
            efficiencyMult = (efficiencyMult + add) * multiply;
        }

        /// <summary>업그레이드: 오토차지(시간충전) 전용 효율 증가. 킬차지와 분리된 레이어. Legendary.</summary>
        public void AddAutoChargeEfficiency(float multiply = 1f, float add = 0f)
        {
            autoChargeEfficiencyMult = (autoChargeEfficiencyMult + add) * multiply;
        }

        /// <summary>업그레이드: 지속시간 증가(초). Rare.</summary>
        public void AddDuration(float addSeconds)
        {
            durationBonus += addSeconds;
        }

        /// <summary>게이지가 가득 찼으면 발동. 버튼/키 입력에서 호출.</summary>
        public void TryActivate()
        {
            if (!IsReady || IsActive)
                return;
            if (GameManager.Instance.IsGameOver)
                return;

            charge = 0f;
            readyAnnounced = false;
            UpdateChargeUI();
            SoundManager.Instance?.PlaySfx(SoundId.UltActivate);
            HapticManager.Play(HapticType.Heavy);
            berserkRoutine = StartCoroutine(Berserk());
        }

        private IEnumerator Berserk()
        {
            Player player = Player.Instance;
            player.ForceInvulnerable = true;
            player.SetBerserkVisual(true); // 몸체 색 FF393B + 트레일 켜기

            // 전체 활성 시간 = 대시(상수) + 지속(+업글보너스). DurationFill은 이 전체를 1→0으로 표시.
            float total = dashDuration + durationSeconds + durationBonus;
            float elapsed = 0f;

            // ① 대시 페이즈: 위로 대시 + 초당 5바퀴 절대 회전(1800°/s)
            player.Orbit.SetSpeedOverride(dashRevolutionsPerSecond * 360f);
            player.DashUp(dashDuration);
            player.Orbit.SpinBurst();
            while (elapsed < dashDuration)
            {
                SetDurationUI(1f - elapsed / total);
                elapsed += Time.deltaTime; // timeScale=0(업글 선택) 중엔 자연히 멈춤
                yield return null;
            }

            // ② 지속 페이즈: 오버라이드 해제 후 회전속도 배수
            player.Orbit.ClearSpeedOverride();
            player.Orbit.SetSpeedMultiplier(speedMultiplier);
            while (elapsed < total)
            {
                SetDurationUI(1f - elapsed / total);
                elapsed += Time.deltaTime;
                yield return null;
            }

            SoundManager.Instance?.PlaySfx(SoundId.UltEnd);
            EndBerserk();
        }

        private void EndBerserk()
        {
            Player player = Player.Instance;
            player.Orbit.ClearSpeedOverride();
            player.Orbit.SetSpeedMultiplier(1f);
            player.ForceInvulnerable = false;
            player.SetBerserkVisual(false); // 몸체 색/트레일 원복
            SetDurationUI(0f);
            berserkRoutine = null;
        }

        /// <summary>게임 시작/재시작 시 게이지·효율·지속·발동상태 초기화.</summary>
        public void ResetUlt()
        {
            if (berserkRoutine != null)
            {
                StopCoroutine(berserkRoutine);
                EndBerserk();
            }
            charge = 0f;
            readyAnnounced = false;
            efficiencyMult = 1f;
            autoChargeEfficiencyMult = 1f;
            durationBonus = 0f;
            UpdateChargeUI();
            SetDurationUI(0f);
        }

        private void UpdateChargeUI()
        {
            if (ultFill != null)
                ultFill.fillAmount = charge;

            // 게이지 만충 & 미발동 시 DurationFill도 가득 채워 "발동 가능"을 강조.
            // 발동 중에는 코루틴이 DurationFill(남은시간)을 관리하므로 건드리지 않는다.
            if (!IsActive)
                SetDurationUI(IsReady ? 1f : 0f);
        }

        private void SetDurationUI(float v)
        {
            if (durationFill != null)
                durationFill.fillAmount = Mathf.Clamp01(v);
        }
        #endregion
    }
}
