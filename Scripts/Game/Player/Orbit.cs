using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 플레이어 주위를 도는 궤도 무기. 무기는 "고정 슬롯"으로 관리한다:
    /// 슬롯 각도 간격은 최대 슬롯 수(maxWeaponCount) 기준으로 고정되고, 피격 시 무기를
    /// 파괴하지 않고 <b>비활성화</b>해 회전하는 "딜 공백"을 만든다. 회복/무기 상한 증가가
    /// 슬롯 채움·추가로 명확히 보이게 하기 위함. 활성 무기 수 = 플레이어 생명(life) 불변식.
    /// </summary>
    public class Orbit : MonoBehaviour
    {
        #region Variables
        public Transform player;
        [Tooltip("모든 슬롯(활성+비활성). Count == maxWeaponCount. 활성 슬롯만 데미지를 준다.")]
        public List<Transform> weapons;
        public float radius = 2f;
        [Tooltip("반경 상한. 반경 업그레이드는 이 값을 향해 수렴한다(후반 효율 감소).")]
        [SerializeField] private float maxRadius = 1.5f;
        public float rotationSpeed = 100f;
        [Tooltip("궁극기 등 일시 회전속도 배수(기본 1). 베이스 rotationSpeed는 업그레이드 소유.")]
        private float speedMult = 1f;
        [Tooltip("절대 회전속도 오버라이드(deg/s). 0 이상이면 rotationSpeed*speedMult 대신 이 값을 쓴다. 음수=미사용.")]
        private float speedOverride = -1f;
        public GameObject weaponPrefab;
        private float currentRotation;
        private int maxWeaponCount = 6;
        private int activeWeaponCount = 0; // 현재 활성(데미지 주는) 무기 수 = 플레이어 생명

        private float defaultRadius;
        private float defaultRotationSpeed;
        private Vector3 defaultWeaponScale = Vector3.one;

        [Header("스핀 버스트 (패링 등 연출)")]
        [Tooltip("한 번에 추가로 도는 각도. 360 = 한 바퀴")]
        [SerializeField] private float spinBurstDegrees = 360f;
        [Tooltip("추가 회전을 소진하는 시간(초). 짧을수록 '휘리릭'")]
        [SerializeField] private float spinBurstDuration = 0.25f;
        private Coroutine spinRoutine;
        #endregion

        #region Properties
        /// <summary>반경이 상한에 사실상 도달했는지(추가 반경 업그레이드 무의미).</summary>
        public bool IsRadiusMaxed => radius >= maxRadius - 0.01f;

        /// <summary>업그레이드 가산의 기준이 되는 시작 회전속도(Start에서 캐싱).</summary>
        public float DefaultRotationSpeed => defaultRotationSpeed;
        #endregion

        #region Unity Event Methods
        private void Start()
        {
            maxWeaponCount = Player.Instance.maxLife;
            defaultRadius = radius;
            defaultRotationSpeed = rotationSpeed;
            if (weaponPrefab != null)
                defaultWeaponScale = weaponPrefab.transform.localScale;
        }

        private void Update()
        {
            float degPerSec = speedOverride >= 0f ? speedOverride : rotationSpeed * speedMult;
            currentRotation += degPerSec * Time.deltaTime;

            int slotCount = weapons.Count;
            if (slotCount == 0)
                return;

            // 각도 간격은 활성 개수가 아니라 전체 슬롯 수 기준 → 비활성 슬롯 자리가 "딜 공백"으로 남는다.
            float angleStep = 360f / slotCount;

            for (int i = 0; i < slotCount; i++)
            {
                Transform weapon = weapons[i];
                if (weapon == null)
                    continue;

                float angle = currentRotation + angleStep * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;

                weapon.localPosition = offset;
                weapon.up = weapon.localPosition.normalized;
            }
        }
        #endregion

        #region Custom Methods
        /// <summary>활성 무기를 amount만큼 켠다(비활성 슬롯을 활성화). 회복/생명 증가 시 Player.AddLife가 호출.</summary>
        public void AddWeapon(int amount)
        {
            activeWeaponCount = Mathf.Clamp(activeWeaponCount + amount, 0, weapons.Count);
            RefreshActiveSlots();
        }

        /// <summary>활성 무기를 amount만큼 끈다(파괴하지 않고 비활성화 → 회전하는 딜 공백). 피격 시 Player가 호출.</summary>
        public void RemoveWeapon(int amount)
        {
            activeWeaponCount = Mathf.Clamp(activeWeaponCount - amount, 0, weapons.Count);
            RefreshActiveSlots();
        }

        /// <summary>인덱스 &lt; activeWeaponCount 인 슬롯만 활성화한다(활성분은 앞쪽에 몰려 공백이 하나의 호로 유지됨).</summary>
        private void RefreshActiveSlots()
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                    continue;
                bool active = i < activeWeaponCount;
                if (weapons[i].gameObject.activeSelf != active)
                    weapons[i].gameObject.SetActive(active);
            }
        }

        /// <summary>비활성 슬롯 오브젝트를 하나 만들어 슬롯 리스트에 추가한다.</summary>
        private void CreateSlot()
        {
            Transform weapon = Instantiate(weaponPrefab, player).transform;
            weapon.localScale = CurrentWeaponScale();
            weapon.gameObject.SetActive(false); // 새 슬롯은 비활성으로 시작(AddWeapon이 켠다)
            weapons.Add(weapon);
        }

        /// <summary>반경을 상한(maxRadius)을 향해 남은 여유의 fraction만큼 좁힌다(수렴형 성장).
        /// 무기 크기도 반경에 비례해 함께 키워 커버리지를 유지한다.</summary>
        public void GrowRadius(float fraction01)
        {
            fraction01 = Mathf.Clamp01(fraction01);
            radius = Mathf.Lerp(radius, maxRadius, fraction01);
            ApplyWeaponScale();
        }

        /// <summary>현재 반경 비율에 맞는 무기 스케일(기본 반경일 때 기본 스케일).</summary>
        private Vector3 CurrentWeaponScale()
        {
            float k = defaultRadius > 0.0001f ? radius / defaultRadius : 1f;
            return defaultWeaponScale * k;
        }

        private void ApplyWeaponScale()
        {
            Vector3 s = CurrentWeaponScale();
            foreach (Transform w in weapons)
                if (w != null)
                    w.localScale = s;
        }

        /// <summary>궤도를 짧은 시간에 spinBurstDegrees만큼 추가로 확 돌린다(연출용).
        /// 기존 연속 회전에 가산되며, 앞부분이 빠른 ease-out으로 "휘리릭" 느낌.</summary>
        /// <summary>궁극기 등 일시 회전속도 배수 설정(1이면 원복).</summary>
        public void SetSpeedMultiplier(float mult)
        {
            speedMult = mult;
        }

        /// <summary>절대 회전속도(deg/s)로 오버라이드. 예: 초당 5바퀴=1800.</summary>
        public void SetSpeedOverride(float degPerSec)
        {
            speedOverride = degPerSec;
        }

        /// <summary>절대 회전속도 오버라이드 해제(다시 rotationSpeed*speedMult 사용).</summary>
        public void ClearSpeedOverride()
        {
            speedOverride = -1f;
        }

        public void SpinBurst()
        {
            if (spinRoutine != null)
                StopCoroutine(spinRoutine);
            spinRoutine = StartCoroutine(SpinBurstRoutine());
        }

        private IEnumerator SpinBurstRoutine()
        {
            float elapsed = 0f;
            float applied = 0f; // 지금까지 가산한 각도
            while (elapsed < spinBurstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / spinBurstDuration);
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad (초반 빠름)
                float target = spinBurstDegrees * eased;
                currentRotation += target - applied; // 이번 프레임 몫만 가산
                applied = target;
                yield return null;
            }
            currentRotation += spinBurstDegrees - applied; // 잔여 보정 (정확히 한 바퀴 보장)
            spinRoutine = null;
        }

        /// <summary>모든 슬롯을 제거·재생성하고 상한/반경/회전속도를 기본값으로 되돌린다. 게임 재시작용.
        /// 최대 슬롯 수만큼 슬롯을 비활성으로 만들어두고, 실제 활성화는 Player.AddLife→AddWeapon이 처리한다.</summary>
        public void ResetWeapons()
        {
            for (int i = weapons.Count - 1; i >= 0; i--)
                if (weapons[i] != null)
                    Destroy(weapons[i].gameObject);
            weapons.Clear();
            activeWeaponCount = 0;

            maxWeaponCount = Player.Instance.maxLife;
            radius = defaultRadius;
            rotationSpeed = defaultRotationSpeed;
            speedMult = 1f;
            speedOverride = -1f;

            for (int i = 0; i < maxWeaponCount; i++)
                CreateSlot();
        }

        /// <summary>무기 상한(슬롯 수)만 올린다(비활성 슬롯 추가, 즉시 활성화 없음). 실제 활성화는 Player.AddLife가
        /// 생명과 함께 처리해 "활성 무기 = 생명" 불변식을 유지한다. 성장(궤도 무기 +N) 업그레이드용.</summary>
        public void RaiseMaxWeaponCount(int amount)
        {
            for (int i = 0; i < amount; i++)
                CreateSlot();
            maxWeaponCount += amount; // == weapons.Count 유지
        }
        #endregion
    }
}
