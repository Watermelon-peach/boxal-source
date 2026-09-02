using Boxal.Util;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using System.Collections;
using UnityEngine;

namespace Boxal.Game
{
    public class Player : Singleton<Player>
    {
        #region Variables
        public int maxLife = 6;
        private int life = 0;
        private bool isDead = false;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 1.1f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private float enemyCheckDistance = 1.1f;

        [Header("무적시간")]
        [SerializeField] private float invulnerabilityPeriod = 3f;
        [SerializeField] private float blinkingInterval = 0.25f;
        private bool isInvulnerable = false;
        private Renderer rend;

        [Header("궁극기 대시")]
        [Tooltip("궁극기 발동 시 위로 대시하는 속도")]
        [SerializeField] private float ultDashSpeed = 12f;

        [Header("연출 오브젝트")]
        [Tooltip("보호막 활성 시 켜지는 오브젝트(Player/Shield)")]
        [SerializeField] private GameObject shieldObject;
        [Tooltip("궁극기(광폭화) 중 켜지는 트레일(Player/VFX_Trail_Fire)")]
        [SerializeField] private GameObject berserkTrail;

        private Rigidbody rb;
        private int defaultMaxLife;

        // 궁극기 중 몸체 색 전환용
        private Material bodyMat;
        private Color defaultBodyColor;
        private static readonly Color BerserkColor = new Color(1f, 57f / 255f, 59f / 255f); // FF393B
        private const string BodyColorProp = "_Color";
        #endregion

        #region Properties
        public bool IsGrounded { get; private set; }
        public bool IsEnemyDetected { get; private set; }
        /// <summary>베이스 공격력. 실수로 유지해 ×배수 업그레이드(예: ×1.5)가 소수까지 정확히 반영된다.
        /// 실제 데미지 정수화는 ConsumeAttackDamage가 담당.</summary>
        public double Atk { get; set; }

        /// <summary>궁극기 등 일시 배수 레이어(기본 1). 베이스 Atk는 업그레이드 소유라 건드리지 않는다.</summary>
        public float AtkMult { get; set; } = 1f;
        /// <summary>현재 화력(베이스 Atk × 일시 배수). 실수 — 표시/참조용. 실제 타격 데미지는 ConsumeAttackDamage.</summary>
        public double EffectiveAtk => Atk * AtkMult;

        // 데미지를 정수로 주되 소수부를 누적한다. 예) 화력 1.5 → 데미지 1,2,1,2... 로 번갈아 적용돼
        // 장기적으로 실제 화력과 정확히 일치한다(반올림/올림 없이 손실 0).
        private double dmgCarry = 0.0;

        /// <summary>한 번의 무기 타격이 주는 데미지(정수). EffectiveAtk의 소수부를 캐리에 누적해
        /// 다음 타격으로 이월하므로, 여러 번 때리면 평균이 EffectiveAtk에 수렴한다.</summary>
        public long ConsumeAttackDamage()
        {
            dmgCarry += EffectiveAtk;
            long dmg = (long)(dmgCarry + 1e-9); // 부동소수 오차로 정수 경계에서 1 손실 방지
            dmgCarry -= dmg;
            return dmg;
        }
        /// <summary>궁극기 등으로 강제 무적(기존 무적 깜빡임과 별개로 데미지만 무효).</summary>
        public bool ForceInvulnerable { get; set; }
        /// <summary>Legendary 보호막 등. true면 다음 피격 1회를 완전히 무효화하고 자동 소진된다.
        /// 값이 바뀌면 씬의 Shield 오브젝트 표시도 함께 토글한다.</summary>
        public bool HasShield
        {
            get => hasShield;
            set
            {
                hasShield = value;
                if (shieldObject != null)
                    shieldObject.SetActive(value);
            }
        }
        private bool hasShield;

        public Orbit Orbit { get; private set; }
        public Rigidbody detectedEnemyRb { get; private set; }

        /// <summary>다음 접촉 피격 한 방에 사망하는 "한방컷" 상태인지. 보호막이 있으면 1회 흡수되고,
        /// 궁극기 강제 무적 중이면 데미지를 받지 않으므로 둘 다 false다. LowHP 경고 UI가 매 프레임 참조한다.</summary>
        public bool IsLethalState
        {
            get
            {
                if (isDead || HasShield || ForceInvulnerable) return false;
                int dmg = RoundManager.InstanceExist ? RoundManager.Instance.EnemyContactDamage : 1;
                return life <= dmg;
            }
        }
        #endregion

        #region Unity Event Methods
        protected override void Awake()
        {
            base.Awake();
            groundLayer = LayerMask.GetMask("Ground");
            enemyLayer = LayerMask.GetMask("Enemy");

            rb = GetComponent<Rigidbody>();
            Orbit = GetComponent<Orbit>();

            rend = GetComponent<Renderer>();
            defaultMaxLife = maxLife;

            // 궁극기 색 전환용 머티리얼 인스턴스 + 원래 색 캐싱
            bodyMat = rend.material; // 인스턴스화(플레이어 단일 오브젝트라 부담 없음)
            if (bodyMat.HasProperty(BodyColorProp))
                defaultBodyColor = bodyMat.GetColor(BodyColorProp);
        }

        private void Update()
        {
            GroundCheck();
            EnemyCheck();
            //Debug.Log(IsGrounded);
            if (IsEnemyDetected && IsGrounded && !isInvulnerable && !ForceInvulnerable)
            {
                int dmg = RoundManager.InstanceExist ? RoundManager.Instance.EnemyContactDamage : 1;
                TakeDamage(dmg);
            }
                
        }
        #endregion

        #region Custom Methods
        public void PlayerInitSettings()
        {
            isDead = false;

            // 무적 깜빡임은 WaitForSeconds(스케일 시간) 기반이라 게임오버 정지(timeScale=0) 중에 얼어붙는다.
            // 끊고 상태를 되돌리지 않으면 다음 판을 잔여 무적 + 깜빡이다 꺼진 몸으로 시작하게 된다.
            StopAllCoroutines();
            isInvulnerable = false;
            if (rend != null)
                rend.enabled = true;

            // 재시작 대비: 성장값/생명/무기 초기화 후 시작값 세팅 (누적 방지)
            maxLife = defaultMaxLife;
            life = 0;
            Orbit.ResetWeapons();
            // 시작 시 상한만큼 가득 채운다 → 1렙 최대 무기 수 = 시작 무기 수
            // (무기+1 업그레이드로 maxLife/상한이 오르면 그만큼만 확장)
            AddLife(defaultMaxLife);
            // 상점의 '최초 공격력' 레벨만큼 더한 값으로 시작한다.
            Atk = 1.0 + ShopUpgrades.StartAttackBonus;
            dmgCarry = 0.0; // 재시작 시 이월된 소수부 초기화
            // 궁극기 배수 레이어 초기화 (재시작 대비)
            AtkMult = 1f;
            ForceInvulnerable = false;
            HasShield = false;
            SetBerserkVisual(false); // 재시작 시 궁극기 연출 원복(안전)
        }

        /// <summary>궁극기(광폭화) 연출 on/off: 몸체 색을 FF393B로 바꾸고 트레일을 켠다(off면 원복).</summary>
        public void SetBerserkVisual(bool on)
        {
            if (bodyMat != null && bodyMat.HasProperty(BodyColorProp))
                bodyMat.SetColor(BodyColorProp, on ? BerserkColor : defaultBodyColor);
            if (berserkTrail != null)
                berserkTrail.SetActive(on);
        }

        /// <summary>궁극기 발동: 일정 시간 위로 대시(회피/연출). 물리 속도를 유지해 밀어올린다.</summary>
        public void DashUp(float duration)
        {
            StartCoroutine(DashUpRoutine(duration));
        }

        private IEnumerator DashUpRoutine(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                Vector3 v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(v.x, ultDashSpeed, v.z);
                t += Time.deltaTime;
                yield return null;
            }
        }
        
        //무적타임
        private IEnumerator StartInvulnerabilityPeriod()
        {
            isInvulnerable = true;
            float timer = 0f;
            bool isVisible = true;
            while (timer <= invulnerabilityPeriod)
            {
                isVisible = !isVisible;
                rend.enabled = isVisible;
                timer += blinkingInterval;
                yield return new WaitForSeconds(blinkingInterval);
            }
            rend.enabled = true;
            isInvulnerable = false;
        }

        private void EnemyCheck()
        {
            RaycastHit hit;

            IsEnemyDetected = Physics.Raycast(transform.position, Vector3.up,out hit, enemyCheckDistance, enemyLayer);
            if (IsEnemyDetected)
            {
                detectedEnemyRb = hit.collider.attachedRigidbody;
            }
            else
            {
                detectedEnemyRb = null;
            }
        }

        private void GroundCheck()
        {
            IsGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
            //Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.red);
        }

        public void TakeDamage(int amount)
        {
            if (isDead)
                return;

            // 게임오버(왕보스 타임아웃 등) 이후에는 피해를 받지 않는다.
            // Update와 Physics.Raycast는 timeScale=0에서도 그대로 도는 탓에, 막지 않으면
            // 정지된 게임오버 화면에서 한 번 더 맞고 피격음·햅틱까지 울린다.
            if (GameManager.InstanceExist && GameManager.Instance.IsGameOver)
                return;

            if (HasShield)
            {
                HasShield = false;
                StartCoroutine(StartInvulnerabilityPeriod());
                return;
            }

            //Debug.Log("life left" + life);
            life -= amount;

            //Sfx,Vfx,Ui 업데이트
            //...
            SoundManager.Instance?.PlaySfx(SoundId.PlayerHit);
            HapticManager.Play(HapticType.Heavy); // 무기(=생명)를 잃는 순간
            Orbit.RemoveWeapon(amount);
            if (life <= 0)
            {
                Die();
                return;
            }
            StartCoroutine(StartInvulnerabilityPeriod());
        }

        private void Die()
        {
            Debug.Log("죽음!");
            isDead = true;
            GameManager.Instance.GameOver();
        }

        public void AddLife(int amount = 1)
        {
            if (isDead)
                return;

            //서순 반대로 + else 안넣었다가 엉망잔칭됐었음
            if (life + amount >= maxLife) life = maxLife;
            else life += amount;

            Orbit.AddWeapon(amount);
            //Ui 업데이트
            //...
        }
        #endregion
    }

}
