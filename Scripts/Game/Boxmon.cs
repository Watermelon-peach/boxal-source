using Boxal.Util;
using Boxal.Game.Audio;
using MoreMountains.Feedbacks;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Boxal.Game
{
    public class Boxmon : MonoBehaviour
    {
        #region Variables
        [Header("대미지처리")]
        public TextMeshPro hpTmp;
        public Material boxMat;
        [SerializeField] private MMF_Player ftPlayer;
        [SerializeField] private Transform ftTransform;

        [Header("파괴 연출")]
        public GameObject originalObject;
        public GameObject demolished;
        [SerializeField] private float despawnDelay = 2f;

        [Header("*밸런싱용 임시 직렬화")]
        private long maxHp = 1;
        
        //TODO: 테스트 인풋 대미지
        //[SerializeField] private long tempDmg = 99;
        private long currentHp = 0;


        private Color startColor;
        [SerializeField] private Color targetColor;
        private Color currentColor;

        [Header("보스 비주얼")]
        [Tooltip("일반 보스 크기 배율(기본 박스몬 대비)")]
        [SerializeField] private float bossScaleMultiplier = 1.5f;
        [Tooltip("왕보스 크기 배율(기본 박스몬 대비)")]
        [SerializeField] private float kingBossScaleMultiplier = 3f;
        [Tooltip("왕보스 전용 색 (#5A6DFF)")]
        [SerializeField] private Color kingBossColor = new Color(0.353f, 0.427f, 1f);
        private Vector3 defaultScale;
        private Color defaultStartColor;

        private Renderer rend;
        private MaterialPropertyBlock mpb;
        private MMF_Player feelPlayer;

        private Coroutine blinkRoutine;
        private float blinkDuration = 0.05f;
        //참조
        public Rigidbody rb;
        private Collider originalCollider;
        private int weaponLayer;
        #endregion

        #region Properties
        public long MaxHp
        { 
            get { return maxHp; }
            set { maxHp = value; } 
        }

        public bool IsDead { get; private set; }
        /// <summary>보스 개체인지. 처치 시 Legendary 보상 지급 트리거로 쓰인다.</summary>
        public bool IsBoss { get; set; }
        /// <summary>왕보스 개체인지. 처치 시 점수 10배·풀힐, 크기/색 차별화. (IsBoss도 함께 true)</summary>
        public bool IsKingBoss { get; set; }

        public Transform[] Transforms { get; private set; }
        public Rigidbody[] Rigidbodies { get; private set; }

        //라운드관리
        //public bool IsClearPoint { get; set; }
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            startColor = boxMat.color;
            originalCollider = GetComponent<Collider>();
            rb = GetComponent<Rigidbody>();
            rend = originalObject.GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();
            feelPlayer = GetComponent<MMF_Player>();

            Transforms = GetComponentsInChildren<Transform>(true);
            Rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            weaponLayer = LayerMask.NameToLayer("Weapon");

            defaultScale = transform.localScale;
            defaultStartColor = startColor;
        }


        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != weaponLayer) return;
            TakeDamage(Player.Instance.ConsumeAttackDamage());
        }

        #endregion

        #region Custom Methods

        public void TakeDamage(long dmg)
        {
            if (IsDead) return;
            if(currentHp - dmg <= 0)
            {
                SoundManager.Instance?.PlaySfx(SoundId.BoxmonBreak);
                StartCoroutine(BreakBox());
                return;
            }
            //대미지 처리
            currentHp -= dmg;
            SoundManager.Instance?.PlaySfx(SoundId.BoxmonHit);
            //Fx (오브젝트 색, 숫자 표시)
            MMF_FloatingText floatingTextFeedback = ftPlayer.GetFeedbackOfType<MMF_FloatingText>();
            floatingTextFeedback.Value = $"-{NumberUtil.FormatNumber(dmg)}";
            
            if (blinkRoutine != null)
                StopCoroutine(blinkRoutine);

            blinkRoutine = StartCoroutine(Blink());

            ftPlayer?.PlayFeedbacks(ftTransform.position);


            UpdateHpText();
            feelPlayer?.PlayFeedbacks();
            UpdateColor(dmg);
        }

        private IEnumerator Blink()
        {
            rend.GetPropertyBlock(mpb);
            mpb.SetFloat("_UseEmission", 1f);
            rend.SetPropertyBlock(mpb);

            yield return new WaitForSeconds(blinkDuration);

            rend.GetPropertyBlock(mpb);
            mpb.SetFloat("_UseEmission", 0f);
            rend.SetPropertyBlock(mpb);
        }

        public void ResetBox()
        {
            currentHp = MaxHp;

            IsDead = false;
            IsBoss = false;
            IsKingBoss = false;
            // 풀 재사용 대비: 크기/색을 기본 몹 상태로 되돌린다(등급은 ConfigureTier가 다시 적용)
            transform.localScale = defaultScale;
            startColor = defaultStartColor;
            hpTmp.enabled = true;
            originalCollider.enabled = true;
            currentColor = startColor;
            UpdateHpText();
            UpdateColor();
        }

        /// <summary>보스 등급별 크기/색을 적용한다. SpawnManager가 ResetBox 직후 호출.
        /// 일반몹=기본, 일반보스=1.5배(색 유지), 왕보스=3배+전용 색(#5A6DFF).</summary>
        public void ConfigureTier(bool isBoss, bool isKingBoss)
        {
            IsBoss = isBoss;
            IsKingBoss = isKingBoss;

            float scaleMul = isKingBoss ? kingBossScaleMultiplier : (isBoss ? bossScaleMultiplier : 1f);
            transform.localScale = defaultScale * scaleMul;

            // 왕보스만 전용 색, 일반보스/일반몹은 기본 색 유지
            startColor = isKingBoss ? kingBossColor : defaultStartColor;
            currentColor = startColor;
            UpdateColor();
        }

        //막타대미지 고려해야됨 (처형처리 비슷하게)
        private void UpdateColor(long dmg = 0)
        {
            float t = (float)currentHp / (MaxHp - dmg);
            currentColor = Color.Lerp(targetColor, startColor, t);

            //mpb 사용
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", currentColor);
            rend.SetPropertyBlock(mpb);
        }

        private void UpdateHpText()
        {
            //UI 업데이트
            hpTmp.text = NumberUtil.FormatNumber(currentHp);
        }

        private IEnumerator BreakBox()
        {
            IsDead = true;
            //Debug.Log("rb 리스트 remove 시도: " + SpawnManager.Instance.aliveRbs.Contains(rb));
            SpawnManager.Instance.aliveBoxmons.Remove(this);

            // 이번 판 처치 수 집계 + 점수(현재 라운드 티어 데미지) + 궁극기 게이지 충전
            if (GameManager.InstanceExist)
            {
                GameManager.Instance.RegisterKill();
                if (RoundManager.InstanceExist)
                {
                    long points = RoundManager.Instance.EnemyContactDamage;
                    if (IsKingBoss)
                        points *= 10; // 왕보스 처치 점수 10배
                    GameManager.Instance.AddPoints(points);
                }
            }
            // 왕보스 처치 시 풀힐(상한만큼 채우기에 충분한 값)
            if (IsKingBoss && Player.InstanceExist)
                Player.Instance.AddLife(Player.Instance.maxLife);
            if (UltimateManager.InstanceExist)
                UltimateManager.Instance.AddCharge();
            // 보스 보상을 레벨업 카드보다 먼저 제시(둘이 겹칠 때 보상 우선). timeScale=0.
            if (IsBoss && BossRewardManager.InstanceExist)
                BossRewardManager.Instance.OfferReward(IsKingBoss);
            // 처치 경험치 → 레벨업 시 카드. 보스 보상이 떠 있으면 UpgradeManager가 확정 후로 미룬다.
            if (LevelManager.InstanceExist)
                LevelManager.Instance.AddKillXp();


            //파편화연출
            //1. rb끄기(Kinematic) 2. 콜라이더 끄기 3. Hp 패널 / original 끄기 4. 파편 키기
            rb.isKinematic = true;
            originalCollider.enabled = false;

            hpTmp.enabled = false;
            originalObject.SetActive(false);
            demolished.SetActive(true);

            feelPlayer.StopFeedbacks();
            feelPlayer.RestoreInitialValues();



            yield return new WaitForSeconds(despawnDelay);
            SpawnManager.Instance.Despawn(this);
        }
        #endregion
    }

}
