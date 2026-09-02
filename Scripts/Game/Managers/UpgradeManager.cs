using Boxal.Game.Growth;
using Boxal.Game.Audio;
using Boxal.Game.Feedback;
using Boxal.Game.UI;
using Boxal.Util;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 업그레이드 풀 보관 + 가중치 추첨 + 선택분 적용. 런 한정 로그라이트.
    /// 레벨업 시 OfferChoices()로 게임을 멈추고 N장을 제시 → SelectChoice()로 적용/재개.
    /// </summary>
    public class UpgradeManager : Singleton<UpgradeManager>
    {
        #region Variables
        [SerializeField] private List<UpgradeSO> pool = new List<UpgradeSO>();
        [SerializeField] private int choiceCount = 3;

        private readonly List<UpgradeSO> currentChoices = new List<UpgradeSO>();
        // 아직 제시하지 못한 레벨업 카드 수(한 번에 여러 레벨업 시 하나씩 순차 제시).
        private int pendingOffers = 0;
        // 보스 보상(Legendary)과 레벨업이 겹칠 때, 보상 확정 후로 카드 제시를 미루는 중인지.
        private bool pendingAfterReward = false;
        #endregion

        #region Events
        /// <summary>선택지 제시됨 (UI 표시용).</summary>
        public event Action<IReadOnlyList<UpgradeSO>> ChoicesOffered;
        /// <summary>선택 확정/해제됨 (UI 숨김용).</summary>
        public event Action ChoiceResolved;
        #endregion

        #region Properties
        /// <summary>선택 대기 중인지.</summary>
        public bool IsChoosing => currentChoices.Count > 0;
        public IReadOnlyList<UpgradeSO> CurrentChoices => currentChoices;
        #endregion

        #region Custom Methods
        /// <summary>레벨업 1회당 호출. 카드를 큐에 쌓고, 지금 띄울 수 있으면 첫 장을 제시한다.
        /// 한 번에 여러 레벨업이 나면(고배수 XP 등) 큐에 누적되어 선택할 때마다 다음 장이 이어진다.</summary>
        public void OfferChoices()
        {
            pendingOffers++;

            // 이미 카드가 떠 있으면 그 선택이 끝난 뒤 SelectChoice에서 다음 장을 이어 제시한다.
            if (IsChoosing)
                return;

            // 보스 보상(Legendary) 패널이 떠 있으면(보스 처치+레벨업 동시) 보상 확정 후로 미룬다.
            // 한 번에 하나의 모달만 띄우고 그동안 timeScale=0을 유지하기 위함.
            if (BossRewardManager.InstanceExist && BossRewardManager.Instance.IsOffering)
            {
                if (!pendingAfterReward)
                {
                    pendingAfterReward = true;
                    BossRewardManager.Instance.RewardResolved += OfferAfterReward;
                }
                return;
            }

            PresentNextIfAny();
        }

        /// <summary>대기 중인 카드를 하나 제시한다. 제시 가능한 후보가 없는 레벨업분은 조용히 소모한다.
        /// 실제로 카드를 띄웠으면 true(게임 정지), 띄울 게 없으면 false.</summary>
        private bool PresentNextIfAny()
        {
            while (pendingOffers > 0)
            {
                DrawChoices();
                pendingOffers--;
                if (currentChoices.Count > 0)
                {
                    Time.timeScale = 0f;
                    SoundManager.Instance?.PlaySfx(SoundId.LevelUp);
                    HapticManager.Play(HapticType.Light);
                    ChoicesOffered?.Invoke(currentChoices);
                    return true;
                }
                // 후보 없음(모든 축 상한 등) → 이 레벨업분은 조용히 소모하고 다음 시도
            }
            return false;
        }

        /// <summary>보스 보상 확정 후 미뤄뒀던 레벨업 카드를 제시한다(RewardResolved 1회 구독).</summary>
        private void OfferAfterReward()
        {
            if (BossRewardManager.InstanceExist)
                BossRewardManager.Instance.RewardResolved -= OfferAfterReward;
            pendingAfterReward = false;
            // 재시작 등으로 게임오버 상태면 제시하지 않는다.
            if (GameManager.InstanceExist && GameManager.Instance.IsGameOver)
                return;
            // 보상 패널이 닫혀 IsOffering=false. 미뤄둔 카드가 없으면 게임 재개.
            if (!PresentNextIfAny())
                Time.timeScale = 1f;
        }

        /// <summary>선택 확정. 효과 적용 후 다음 대기 카드가 있으면 이어서, 없으면 게임 재개.</summary>
        public void SelectChoice(int index)
        {
            if (index < 0 || index >= currentChoices.Count)
                return;

            SoundManager.Instance?.PlaySfx(SoundId.CardSelect);
            HapticManager.Play(HapticType.Selection);

            UpgradeSO picked = currentChoices[index];
            picked.Apply();
            UpgradeHistory.Record(picked);  // 퍼즈의 획득 목록용
            UpgradeFlyFx.Play(picked);      // 획득 연출 — 아이콘이 퍼즈 버튼으로 날아간다
            currentChoices.Clear();
            ChoiceResolved?.Invoke(); // 현재 카드 UI 숨김

            // 남은 레벨업 카드가 있으면 이어서 제시(timeScale=0 유지), 없으면 재개.
            if (!PresentNextIfAny())
                Time.timeScale = 1f;
        }

        /// <summary>게임 재시작 시 선택 대기 상태 정리.</summary>
        public void ResetUpgrades()
        {
            // 보상 대기 구독이 남아있으면 해제(재시작 중 RewardResolved에 반응하지 않도록).
            if (pendingAfterReward && BossRewardManager.InstanceExist)
                BossRewardManager.Instance.RewardResolved -= OfferAfterReward;
            pendingAfterReward = false;
            pendingOffers = 0;

            // 이전 판의 획득 연출이 화면에 남아 있으면 지운다.
            UpgradeFlyFx.Stop();

            // 획득 목록도 런 한정이라 여기서 함께 비운다(GameManager.OnGameStart가 재시작마다 호출).
            UpgradeHistory.Clear();

            currentChoices.Clear();
            ChoiceResolved?.Invoke(); // 재시작 중 패널 열려있으면 숨김
        }

        private void DrawChoices()
        {
            currentChoices.Clear();

            // 제시 가능한 후보만 수집
            List<UpgradeSO> candidates = new List<UpgradeSO>();
            float totalWeight = 0f;
            foreach (UpgradeSO u in pool)
            {
                if (u == null || !u.CanOffer())
                    continue;
                candidates.Add(u);
                totalWeight += Mathf.Max(0f, u.weight);
            }

            int draws = Mathf.Min(choiceCount, candidates.Count);
            for (int n = 0; n < draws; n++)
            {
                UpgradeSO picked = WeightedPick(candidates, totalWeight);
                if (picked == null)
                    break;
                currentChoices.Add(picked);
                totalWeight -= Mathf.Max(0f, picked.weight);
                candidates.Remove(picked); // 한 번의 제시 내 중복 방지
            }
        }

        private UpgradeSO WeightedPick(List<UpgradeSO> candidates, float totalWeight)
        {
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
