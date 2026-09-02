using System;
using System.Collections;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game
{
    public class Parrying : MonoBehaviour
    {
        #region Variables
        public Image icon;

        [Header("쿨다운")]
        [SerializeField] private float parryCoolDown = 0.5f;
        [SerializeField] private float power = 5f;

        [Header("판정")]
        [Tooltip("막기 활성 윈도우(초). 누른 뒤 이 시간 동안 범위에 적이 들어오면 막힘")]
        [SerializeField] private float parryWindow = 0.2f;
        [Tooltip("막기 감지 반경 (데미지 판정과 분리된 전용 반경)")]
        [SerializeField] private float parryRadius = 1.5f;
        [Tooltip("감지 구의 중심을 플레이어 기준 위로 올리는 오프셋")]
        [SerializeField] private float parryUpOffset = 0.5f;

        private bool isCoolDown = false;
        private LayerMask enemyLayer;
        #endregion

        #region Events
        /// <summary>
        /// 막기가 실제로 성공했을 때(윈도우 안에 적이 범위에 들어와 튕겨낸 경우).
        /// 첫 판 조작 안내(<see cref="UI.TutorialHintUI"/>)가 구독한다.
        /// static이라 씬 참조가 필요 없다.
        /// </summary>
        public static event Action Parried;

        // Enter Play Mode에서 도메인 리로드를 끄면 static 구독이 세션 간 살아남는다
        // (UpgradeHistory/HapticManager와 같은 이유의 초기화).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => Parried = null;
        #endregion

        #region Unity Event Methods
        private void Awake()
        {
            enemyLayer = LayerMask.GetMask("Enemy");
        }
        #endregion

        #region Custom Method
        public void OnParry()
        {
            if (isCoolDown)
                return;

            StartCoroutine(ParryWindow());
            StartCoroutine(CoolDown());
        }

        /// <summary>누른 순간의 점 판정 대신, 짧은 윈도우 동안 범위에 들어오는 적을 막는다.</summary>
        private IEnumerator ParryWindow()
        {
            float timer = 0f;
            while (timer <= parryWindow)
            {
                if (IsEnemyInRange())
                {
                    LaunchAllEnemies();
                    Player.Instance.Orbit.SpinBurst(); // 성공 연출: 궤도 한 바퀴 휘리릭
                    SoundManager.Instance?.PlaySfx(SoundId.Parry);
                    HapticManager.Play(HapticType.Rigid); // 짧고 날카롭게 — 실력으로 따낸 순간
                    Parried?.Invoke();
                    yield break; // 성공 시 윈도우 종료
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>플레이어 위쪽 구 범위 안에 적이 있는지.</summary>
        private bool IsEnemyInRange()
        {
            Vector3 center = Player.Instance.transform.position + Vector3.up * parryUpOffset;
            return Physics.CheckSphere(center, parryRadius, enemyLayer);
        }

        /// <summary>살아있는 적 전체를 위로 띄운다 (군중제어).</summary>
        private void LaunchAllEnemies()
        {
            foreach (Boxmon boxmon in SpawnManager.Instance.aliveBoxmons)
            {
                Vector3 lv = boxmon.rb.linearVelocity;
                boxmon.rb.linearVelocity = new Vector3(lv.x, power, lv.z);
            }
        }

        private IEnumerator CoolDown()
        {
            isCoolDown = true;
            //TODO: UI 연결
            float timer = 0f;
            while (timer <= parryCoolDown)
            {
                timer += Time.deltaTime;
                icon.fillAmount = timer / parryCoolDown;
                yield return null;
            }
            isCoolDown = false;
        }
        #endregion
    }

}
