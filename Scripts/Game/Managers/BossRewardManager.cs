using Boxal.Game.Growth;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using Boxal.Util;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 보스 처치 보상(Legendary) 관리. UpgradeManager의 3택1 풀과 완전히 분리된
    /// 전용 풀에서 가중치 랜덤으로 1개만 뽑아 선택지 없이 확정 제시한다.
    /// 보스 처치 시 Boxmon.BreakBox()가 OfferReward()를 호출한다.
    /// </summary>
    public class BossRewardManager : Singleton<BossRewardManager>
    {
        #region Variables
        [SerializeField] private List<UpgradeSO> legendaryPool = new List<UpgradeSO>();
        #endregion

        #region Events
        /// <summary>보상 제시됨 (UI 표시용).</summary>
        public event Action<UpgradeSO> RewardOffered;
        /// <summary>보상 확정/해제됨 (UI 숨김용).</summary>
        public event Action RewardResolved;
        #endregion

        #region Properties
        /// <summary>제시 대기 중인 보상(없으면 null).</summary>
        public UpgradeSO CurrentReward { get; private set; }
        /// <summary>보상 확인 대기 중인지.</summary>
        public bool IsOffering => CurrentReward != null;
        #endregion

        #region Custom Methods
        /// <summary>보스 처치 시 호출. 게임을 멈추고 후보 1개를 추첨해 제시한다.
        /// 왕보스는 처치 시 이미 풀힐되므로 완전회복 Legendary는 후보에서 제외한다.</summary>
        public void OfferReward(bool isKingBoss = false)
        {
            UpgradeSO picked = DrawReward(isKingBoss);
            if (picked == null)
                return; // 제시 가능한 후보 없음 (방어)

            CurrentReward = picked;
            Time.timeScale = 0f;
            SoundManager.Instance?.PlaySfx(SoundId.BossRewardOffer);
            RewardOffered?.Invoke(picked);
        }

        /// <summary>확인 버튼에서 호출. 효과 적용 후 게임 재개.</summary>
        public void ClaimReward()
        {
            if (CurrentReward == null)
                return;

            SoundManager.Instance?.PlaySfx(SoundId.RewardClaim);
            HapticManager.Play(HapticType.Success);
            CurrentReward.Apply();
            UpgradeHistory.Record(CurrentReward); // 퍼즈의 획득 목록용
            CurrentReward = null;
            Time.timeScale = 1f;
            RewardResolved?.Invoke();
        }

        /// <summary>게임 재시작 시 대기 상태 정리.</summary>
        public void ResetRewards()
        {
            CurrentReward = null;
            RewardResolved?.Invoke(); // 재시작 중 패널 열려있으면 숨김
        }

        private UpgradeSO DrawReward(bool isKingBoss)
        {
            List<UpgradeSO> candidates = new List<UpgradeSO>();
            float totalWeight = 0f;
            foreach (UpgradeSO u in legendaryPool)
            {
                if (u == null || !u.CanOffer())
                    continue;
                // 왕보스는 처치 시 자동 풀힐 → 완전회복 Legendary는 중복(꽝)이라 제외
                if (isKingBoss && u is HealUpgradeSO heal && heal.fullHeal)
                    continue;
                candidates.Add(u);
                totalWeight += Mathf.Max(0f, u.weight);
            }

            if (candidates.Count == 0)
                return null;
            if (totalWeight <= 0f)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];

            float r = UnityEngine.Random.value * totalWeight;
            foreach (UpgradeSO u in candidates)
            {
                r -= Mathf.Max(0f, u.weight);
                if (r <= 0f)
                    return u;
            }
            return candidates[candidates.Count - 1];
        }
        #endregion
    }
}
