using Boxal.Game.Audio;
using System;
using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 차징점프 클래스
    /// </summary>
    public class ChargeJump : MonoBehaviour
    {
        #region Variables
        public Rigidbody rb;

        public float minForce = 5f;
        public float maxForce = 15f;
        public float maxChargeTime = 2f;

        public float fallMultiplier = 2.5f;

        private float chargeTime;
        private bool isCharging;

        [Header("게이지UI")]
        [SerializeField] private ImgsFillDynamic imgsFill;
        private CanvasGroup group;
        #endregion

        #region Events
        /// <summary>
        /// 충전 후 실제로 점프가 발생했을 때(누른 채 땅에 있다가 뗀 경우).
        /// 첫 판 조작 안내(<see cref="UI.TutorialHintUI"/>)가 구독한다.
        /// static이라 씬 참조가 필요 없다.
        /// </summary>
        public static event Action Jumped;

        // Enter Play Mode에서 도메인 리로드를 끄면 static 구독이 세션 간 살아남는다
        // (UpgradeHistory/HapticManager와 같은 이유의 초기화).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => Jumped = null;
        #endregion


        #region Unity Event Methods
        private void Awake()
        {
            group = imgsFill.GetComponent<CanvasGroup>();
        }
        void Update()
        {
            if (isCharging)
            {
                chargeTime += Time.deltaTime;
                chargeTime = Mathf.Clamp(chargeTime, 0f, maxChargeTime);
                // Image fillAmount 구현
                float ratio = chargeTime / maxChargeTime;
                imgsFill.SetValue(ratio,true);
            }
        }

        void FixedUpdate()
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            }
        }
        #endregion

        #region Custom Methods
        public void StartCharge()
        {
            isCharging = true;
            chargeTime = 0f;
            group.alpha = 1f;
            SoundManager.Instance?.PlayTracked(SoundId.Charge);
        }

        public void ReleaseJump()
        {
            group.alpha = 0;
            SoundManager.Instance?.StopLoop(SoundId.Charge);
            if (!isCharging|| !Player.Instance.IsGrounded)
                return;

            imgsFill.SetValue(0, true);
            isCharging = false;

            float ratio = chargeTime / maxChargeTime;
            float force = Mathf.Lerp(minForce, maxForce, ratio);

            rb.AddForce(Vector3.up * force, ForceMode.Impulse);
            SoundManager.Instance?.PlaySfx(SoundId.Jump);
            Jumped?.Invoke();
        }
        #endregion

    }

}