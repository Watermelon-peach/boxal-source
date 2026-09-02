using UnityEngine;

namespace Boxal.Game
{
    /// <summary>
    /// 세션 간 유지되는 플레이어 통계의 단일 저장소. 현재는 PlayerPrefs를 래핑한다.
    /// 차후 리더보드/서버 연동으로 확장할 때 이 클래스 내부만 교체하면 되도록
    /// 호출부는 키/저장방식을 몰라도 되게 캡슐화했다.
    /// </summary>
    public static class PlayerStats
    {
        private const string KEY_BEST_ROUND = "boxal.stats.bestRound";
        private const string KEY_TOTAL_KILLS = "boxal.stats.totalKills";
        private const string KEY_BEST_SCORE = "boxal.stats.bestScore";
        private const string KEY_SYNCED_SCORE = "boxal.stats.syncedScore";
        private const string KEY_PLAYER_NAME = "boxal.player.name";
        private const string KEY_STAMINA = "boxal.stamina.current";
        private const string KEY_STAMINA_ANCHOR = "boxal.stamina.anchorUtc";
        private const string KEY_STAMINA_MAX_SEEN = "boxal.stamina.maxSeenUtc";
        private const string KEY_STAMINA_AD_DATE = "boxal.stamina.adDate";
        private const string KEY_STAMINA_AD_COUNT = "boxal.stamina.adCount";
        private const string KEY_GOLD = "boxal.gold";
        private const string KEY_SHOP_LEVEL_PREFIX = "boxal.shop.level.";
        private const string KEY_TUTORIAL_DONE = "boxal.tutorial.done";

        /// <summary>지금까지 도달한 최고 라운드.</summary>
        public static int BestRound => PlayerPrefs.GetInt(KEY_BEST_ROUND, 0);

        /// <summary>지금까지의 최고 점수. long이라 문자열로 저장(PlayerPrefs는 long 미지원).</summary>
        public static long BestScore
        {
            get
            {
                string s = PlayerPrefs.GetString(KEY_BEST_SCORE, "0");
                return long.TryParse(s, out long v) ? v : 0L;
            }
        }

        /// <summary>
        /// 온라인 리더보드에 반영이 확인된 최고 점수.
        /// <see cref="BestScore"/>보다 작으면 오프라인에서 세운 미전송 기록이 있다는 뜻이며,
        /// 다음에 온라인이 되면 그 차이만큼 올려 보낸다.
        /// </summary>
        public static long SyncedScore
        {
            get
            {
                string s = PlayerPrefs.GetString(KEY_SYNCED_SCORE, "0");
                return long.TryParse(s, out long v) ? v : 0L;
            }
            set
            {
                PlayerPrefs.SetString(KEY_SYNCED_SCORE, value.ToString());
                PlayerPrefs.Save();
            }
        }

        /// <summary>서버에 아직 못 올린 기록이 있는지.</summary>
        public static bool HasUnsyncedScore => BestScore > 0 && BestScore > SyncedScore;

        /// <summary>지금까지 처치한 박스몬 누적 수(모든 판 합산).</summary>
        public static int TotalKills => PlayerPrefs.GetInt(KEY_TOTAL_KILLS, 0);

        /// <summary>리더보드 표시용 닉네임(로컬 저장). 미설정이면 빈 문자열.</summary>
        public static string PlayerName
        {
            get => PlayerPrefs.GetString(KEY_PLAYER_NAME, "");
            set
            {
                PlayerPrefs.SetString(KEY_PLAYER_NAME, value ?? "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>닉네임을 한 번이라도 설정했는지(최초 실행 시 입력 팝업 노출 판단용).</summary>
        public static bool HasPlayerName => !string.IsNullOrEmpty(PlayerPrefs.GetString(KEY_PLAYER_NAME, ""));

        /// <summary>도달 라운드를 최고 기록에 제출한다. 신기록이면 저장하고 true를 반환.</summary>
        public static bool TrySubmitRound(int round)
        {
            if (round <= BestRound)
                return false;
            PlayerPrefs.SetInt(KEY_BEST_ROUND, round);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>이번 판 점수를 최고 점수에 제출한다. 신기록이면 저장하고 true를 반환.</summary>
        public static bool TrySubmitScore(long score)
        {
            if (score <= BestScore)
                return false;
            PlayerPrefs.SetString(KEY_BEST_SCORE, score.ToString());
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>이번 판 처치 수를 누적 처치 수에 더한다.</summary>
        public static void AddKills(int kills)
        {
            if (kills <= 0)
                return;
            PlayerPrefs.SetInt(KEY_TOTAL_KILLS, TotalKills + kills);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 남아 있는 스태미나. 저장만 담당하며 회복 계산은 <see cref="Stamina"/>가 한다.
        /// 최초 실행(키 없음)은 <paramref name="fallback"/>으로 채운다 — 기본값 0을 쓰면
        /// 설치 직후 플레이가 막히고 회복을 기다려야 한다.
        /// </summary>
        public static int GetStamina(int fallback) => PlayerPrefs.GetInt(KEY_STAMINA, fallback);

        public static void SetStamina(int value)
        {
            PlayerPrefs.SetInt(KEY_STAMINA, value);
            PlayerPrefs.Save();
        }

        /// <summary>스태미나 회복 타이머의 기준 시각(UTC ticks). 0이면 미설정.</summary>
        public static long StaminaAnchorUtc
        {
            get => ReadTicks(KEY_STAMINA_ANCHOR);
            set => WriteTicks(KEY_STAMINA_ANCHOR, value);
        }

        /// <summary>
        /// 지금까지 관측한 가장 미래의 시각(UTC ticks). 기기 시계를 과거로 되돌렸는지 판정하는 기준이다.
        /// </summary>
        public static long StaminaMaxSeenUtc
        {
            get => ReadTicks(KEY_STAMINA_MAX_SEEN);
            set => WriteTicks(KEY_STAMINA_MAX_SEEN, value);
        }

        /// <summary>보유 골드. 상점 재화라 long으로 두고 문자열 저장(PlayerPrefs는 long 미지원).</summary>
        public static long Gold
        {
            get => ReadTicks(KEY_GOLD);
            set => WriteTicks(KEY_GOLD, value < 0 ? 0 : value);
        }

        /// <summary>상점 레벨 업그레이드의 현재 레벨. 키는 업그레이드 id로 갈린다.</summary>
        public static int GetShopLevel(string upgradeKey) =>
            PlayerPrefs.GetInt(KEY_SHOP_LEVEL_PREFIX + upgradeKey, 0);

        public static void SetShopLevel(string upgradeKey, int level)
        {
            PlayerPrefs.SetInt(KEY_SHOP_LEVEL_PREFIX + upgradeKey, level < 0 ? 0 : level);
            PlayerPrefs.Save();
        }

        /// <summary>광고 시청 횟수를 세고 있는 날짜(yyyyMMdd). 이 값이 오늘과 다르면 횟수를 리셋한다.</summary>
        public static int StaminaAdDate
        {
            get => PlayerPrefs.GetInt(KEY_STAMINA_AD_DATE, 0);
            set
            {
                PlayerPrefs.SetInt(KEY_STAMINA_AD_DATE, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>오늘 광고 보상을 받은 횟수.</summary>
        public static int StaminaAdCount
        {
            get => PlayerPrefs.GetInt(KEY_STAMINA_AD_COUNT, 0);
            set
            {
                PlayerPrefs.SetInt(KEY_STAMINA_AD_COUNT, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 사전 조작 튜토리얼을 끝까지 마쳤는지. 마지막 Start 버튼을 눌러야 true가 된다
        /// (중간에 앱을 끄면 다음 실행에 다시 뜬다 — 게임 시작 전 화면이라 그게 맞다).
        /// </summary>
        public static bool TutorialCompleted
        {
            get => PlayerPrefs.GetInt(KEY_TUTORIAL_DONE, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KEY_TUTORIAL_DONE, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // PlayerPrefs는 long을 지원하지 않아 문자열로 저장한다(BestScore와 같은 방식).
        private static long ReadTicks(string key)
        {
            string s = PlayerPrefs.GetString(key, "0");
            return long.TryParse(s, out long v) ? v : 0L;
        }

        private static void WriteTicks(string key, long value)
        {
            PlayerPrefs.SetString(key, value.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>저장된 모든 통계를 초기화한다(디버그/테스트용).</summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(KEY_BEST_ROUND);
            PlayerPrefs.DeleteKey(KEY_TOTAL_KILLS);
            PlayerPrefs.DeleteKey(KEY_BEST_SCORE);
            PlayerPrefs.DeleteKey(KEY_SYNCED_SCORE);
            PlayerPrefs.DeleteKey(KEY_PLAYER_NAME);
            PlayerPrefs.DeleteKey(KEY_STAMINA);
            PlayerPrefs.DeleteKey(KEY_STAMINA_ANCHOR);
            PlayerPrefs.DeleteKey(KEY_STAMINA_MAX_SEEN);
            PlayerPrefs.DeleteKey(KEY_STAMINA_AD_DATE);
            PlayerPrefs.DeleteKey(KEY_STAMINA_AD_COUNT);
            PlayerPrefs.DeleteKey(KEY_GOLD);
            PlayerPrefs.DeleteKey(KEY_TUTORIAL_DONE);
            // 상점 레벨은 id별로 키가 갈려 있어 여기서 일괄 삭제할 수 없다. ShopUpgrades.ResetAll()이 담당한다.
            ShopUpgrades.ResetAll();
            PlayerPrefs.Save();
        }
    }
}
