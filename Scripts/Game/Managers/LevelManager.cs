using Boxal.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Boxal.Game
{
    /// <summary>
    /// 플레이어 경험치/레벨 관리. 적 처치로 XP를 모아 레벨업하면 업그레이드 선택을 띄운다.
    /// 런 한정(세이브 없음).
    /// </summary>
    public class LevelManager : Singleton<LevelManager>
    {
        #region Variables
        [Header("UI")]
        [Tooltip("경험치 표시 바 (LevelSlider)")]
        [SerializeField] private Slider xpBar;
        [Tooltip("현재 레벨 텍스트 (Badge/Text)")]
        [SerializeField] private TextMeshProUGUI levelText;
        [Tooltip("경험치 수치 텍스트 (LevelSlider/Bg/Text) — 현재/필요 표기")]
        [SerializeField] private TextMeshProUGUI xpText;

        [Header("경험치 곡선")]
        [SerializeField] private int xpPerKill = 1;
        [SerializeField] private int baseXpToLevel = 5;
        [Tooltip("레벨당 필요 경험치 증가 배수")]
        [SerializeField] private float xpGrowth = 1.3f;

        private int level = 1;
        private float currentXp = 0f;
        private int xpToNext = 5;
        /// <summary>경험치 획득 배수(Legendary 등). 곱연산 누적.</summary>
        private float xpMult = 1f;
        #endregion

        #region Properties
        public int Level => level;
        #endregion

        #region Custom Methods
        /// <summary>게임 시작/재시작 시 레벨 상태 초기화.</summary>
        public void ResetLevel()
        {
            level = 1;
            currentXp = 0f;
            xpToNext = baseXpToLevel;
            xpMult = 1f;
            UpdateUI();
        }

        /// <summary>적 처치 보상 경험치.</summary>
        public void AddKillXp()
        {
            AddXp(xpPerKill * xpMult);
        }

        /// <summary>업그레이드: 경험치 획득 배수 증가 (곱연산). Legendary.</summary>
        public void AddXpMult(float multiplier)
        {
            xpMult *= multiplier;
        }

        public void AddXp(float amount)
        {
            currentXp += amount;
            // 고정 XP라 한 번에 다중 레벨업은 사실상 없지만 방어적으로 루프 처리
            while (currentXp >= xpToNext)
            {
                currentXp -= xpToNext;
                LevelUp();
            }
            UpdateUI();
        }

        private void LevelUp()
        {
            level++;
            xpToNext = Mathf.CeilToInt(baseXpToLevel * Mathf.Pow(xpGrowth, level - 1));
            UpgradeManager.Instance.OfferChoices();
        }

        private void UpdateUI()
        {
            if (xpBar != null)
                xpBar.value = xpToNext > 0 ? currentXp / xpToNext : 0f;
            if (levelText != null)
                levelText.text = level.ToString();
            if (xpText != null)
                xpText.text = $"{currentXp:0.0}/{xpToNext} xp";
        }
        #endregion
    }
}
