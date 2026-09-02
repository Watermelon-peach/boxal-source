using System;

namespace Boxal.Game
{
    /// <summary>
    /// 플레이 횟수를 제한하는 스태미나. 한 판 진입에 <see cref="PlayCost"/>를 쓰고 실시간으로 회복한다.
    /// 저장은 <see cref="PlayerStats"/>에 위임하고, 여기서는 "지금 몇 개인가"를 계산하는 규칙만 갖는다.
    /// </summary>
    /// <remarks>
    /// 회복은 코루틴이 아니라 <b>타임스탬프 역산</b>이다. 앱이 꺼져 있는 동안에도 시간이 흘러야 하므로,
    /// 마지막 정산 시각(anchor)만 저장해 두고 다음에 볼 때 경과 시간으로 몇 개가 찼는지 계산한다.
    /// 그래서 Update가 필요 없고, 조회할 때마다 <see cref="Settle"/>이 알아서 최신화한다.
    /// <para/>
    /// UI는 이 클래스를 직접 읽고 <see cref="Changed"/>를 구독하면 된다. 씬에 둘 오브젝트는 없다.
    /// </remarks>
    public static class Stamina
    {
        #region Tuning
        /// <summary>자연 회복의 상한. 보상/아이템은 이 위로 넘길 수 있다(<see cref="Grant"/>).</summary>
        public const int Max = 30;

        /// <summary>1 회복에 걸리는 시간(초). 5분.</summary>
        public const int RefillSeconds = 5 * 60;

        /// <summary>한 판 진입 비용.</summary>
        public const int PlayCost = 5;

        /// <summary>광고 1회 시청당 회복량.</summary>
        public const int AdRewardAmount = 5;

        /// <summary>하루에 광고로 받을 수 있는 횟수.</summary>
        public const int AdDailyLimit = 5;
        #endregion

        #region Events
        /// <summary>보유량이 바뀌었을 때. HUD가 구독해서 다시 그리면 된다.</summary>
        public static event Action Changed;

        /// <summary>
        /// 스태미나가 모자라 플레이가 막혔을 때. 광고 팝업이 구독해서 스스로 열면 된다.
        /// (HomeManager가 팝업을 참조하지 않으므로 씬 배선이 필요 없다.)
        /// </summary>
        public static event Action NotEnoughForPlay;
        #endregion

        #region Properties
        /// <summary>현재 보유량(조회 시점 기준으로 회복을 정산한 뒤의 값). 보상으로 <see cref="Max"/>를 넘을 수 있다.</summary>
        public static int Current
        {
            get
            {
                Settle();
                return PlayerStats.GetStamina(Max);
            }
        }

        /// <summary>자연 회복이 멈춰 있는 상태인지(가득 찼거나 그 위).</summary>
        public static bool IsFull => Current >= Max;

        /// <summary>한 판 시작할 수 있는지.</summary>
        public static bool CanPlay => Current >= PlayCost;

        /// <summary>
        /// 다음 1개가 찰 때까지 남은 시간. 가득 찼거나 초과 상태면 <see cref="TimeSpan.Zero"/>
        /// (그 동안은 타이머가 돌지 않는다).
        /// </summary>
        public static TimeSpan TimeUntilNext
        {
            get
            {
                Settle();
                if (PlayerStats.GetStamina(Max) >= Max)
                    return TimeSpan.Zero;

                long anchor = PlayerStats.StaminaAnchorUtc;
                double elapsed = (NowUtc() - new DateTime(anchor, DateTimeKind.Utc)).TotalSeconds;
                double remain = RefillSeconds - elapsed;
                return TimeSpan.FromSeconds(remain > 0 ? remain : 0);
            }
        }

        /// <summary>
        /// <see cref="Max"/>까지 다 차는 데 남은 시간. 가득 찼거나 초과 상태면 <see cref="TimeSpan.Zero"/>.
        /// </summary>
        /// <remarks>
        /// "다음 1개까지 남은 시간" + "그 뒤로 더 채워야 하는 개수 x 회복 간격"이다.
        /// 초과분이 있어도 0이며, 초과분이 소모로 <see cref="Max"/> 아래로 내려간 뒤부터 다시 값이 생긴다.
        /// </remarks>
        public static TimeSpan TimeUntilFull
        {
            get
            {
                Settle();
                int current = PlayerStats.GetStamina(Max);
                if (current >= Max)
                    return TimeSpan.Zero;

                long anchor = PlayerStats.StaminaAnchorUtc;
                double elapsed = (NowUtc() - new DateTime(anchor, DateTimeKind.Utc)).TotalSeconds;
                double toNext = RefillSeconds - elapsed;
                if (toNext < 0.0)
                    toNext = 0.0;

                // 다음 1개를 뺀 나머지 개수는 온전히 간격만큼씩 더 걸린다.
                int afterNext = Max - current - 1;
                return TimeSpan.FromSeconds(toNext + (double)afterNext * RefillSeconds);
            }
        }

        /// <summary>오늘 광고로 받은 횟수.</summary>
        public static int AdClaimsToday
        {
            get
            {
                RollOverAdDay();
                return PlayerStats.StaminaAdCount;
            }
        }

        /// <summary>오늘 광고로 더 받을 수 있는 횟수.</summary>
        public static int AdClaimsRemaining
        {
            get
            {
                int remain = AdDailyLimit - AdClaimsToday;
                return remain > 0 ? remain : 0;
            }
        }

        /// <summary>지금 광고 보상을 받을 수 있는지(일일 한도).</summary>
        public static bool CanClaimAdReward => AdClaimsRemaining > 0;
        #endregion

        #region Custom Methods
        /// <summary>
        /// 한 판 분량을 소모한다. 부족하면 아무것도 하지 않고 <see cref="NotEnoughForPlay"/>를 발생시킨 뒤 false.
        /// </summary>
        public static bool TryConsume()
        {
            Settle();
            int current = PlayerStats.GetStamina(Max);
            if (current < PlayCost)
            {
                NotEnoughForPlay?.Invoke();
                return false;
            }

            // 가득 찬(또는 초과) 상태에서는 타이머가 멈춰 있다(anchor를 안 밀었다). 여기서 처음 상한 아래로
            // 내려가므로 지금을 기준으로 회복을 시작한다. 이걸 빼먹으면 오래 쉬었다 한 판 한 순간
            // 그동안 쌓인 경과 시간이 한꺼번에 정산돼 즉시 다시 차버린다.
            if (current >= Max)
                PlayerStats.StaminaAnchorUtc = NowUtc().Ticks;

            PlayerStats.SetStamina(current - PlayCost);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 보상/아이템으로 스태미나를 지급한다. <b><see cref="Max"/>를 넘을 수 있고</b>,
        /// 넘은 동안에는 자연 회복 타이머가 돌지 않는다.
        /// </summary>
        public static void Grant(int amount)
        {
            if (amount <= 0)
                return;

            Settle();
            int current = PlayerStats.GetStamina(Max);

            // 상한 아래에서 지급받아 상한을 넘어서면, 그 시점부터 타이머가 멈춘 것으로 본다.
            // (Settle이 current >= Max일 때 anchor를 계속 현재로 밀어주므로 별도 처리는 필요 없다.)
            PlayerStats.SetStamina(current + amount);
            Changed?.Invoke();
        }

        /// <summary>
        /// 광고 보상을 받는다. 일일 한도를 넘었으면 아무것도 하지 않고 false.
        /// </summary>
        /// <remarks>
        /// 지금은 실제 광고 SDK가 붙어 있지 않다. <b>광고 시청이 끝났다고 가정하고</b> 호출하면 되며,
        /// 나중에 SDK를 붙일 때 "시청 완료 콜백에서 이 함수를 부르는" 형태로 바꾸면 된다.
        /// </remarks>
        public static bool TryClaimAdReward()
        {
            RollOverAdDay();
            if (PlayerStats.StaminaAdCount >= AdDailyLimit)
                return false;

            PlayerStats.StaminaAdCount = PlayerStats.StaminaAdCount + 1;
            Grant(AdRewardAmount);
            return true;
        }

        /// <summary>디버그/테스트용. 가득 채우고 타이머를 정지 상태로 되돌린다.</summary>
        public static void Refill()
        {
            PlayerStats.SetStamina(Max);
            PlayerStats.StaminaAnchorUtc = NowUtc().Ticks;
            Changed?.Invoke();
        }

        /// <summary>
        /// 마지막 정산 이후 흐른 시간만큼 회복을 반영한다. 조회/소모 앞에서 항상 먼저 불린다.
        /// </summary>
        private static void Settle()
        {
            DateTime now = NowUtc();
            int current = PlayerStats.GetStamina(Max);

            long anchorTicks = PlayerStats.StaminaAnchorUtc;
            if (anchorTicks <= 0)
            {
                // 최초 실행. 가득 찬 상태로 시작하고 타이머는 세워 둔다.
                PlayerStats.StaminaAnchorUtc = now.Ticks;
                return;
            }

            if (current >= Max)
            {
                // 상한 이상이면 시간이 쌓이지 않아야 한다. anchor를 계속 현재로 밀어 경과분을 버린다.
                // 초과분(보상으로 받은 몫)이 있을 때 타이머가 돌지 않는 것도 이 한 줄이 담당한다.
                PlayerStats.StaminaAnchorUtc = now.Ticks;
                return;
            }

            DateTime anchor = new DateTime(anchorTicks, DateTimeKind.Utc);
            double elapsed = (now - anchor).TotalSeconds;
            if (elapsed < RefillSeconds)
                return;

            int gained = (int)(elapsed / RefillSeconds);
            int next = current + gained;
            if (next >= Max)
            {
                // 자연 회복은 상한을 넘지 않는다. 초과는 Grant로만 생긴다.
                PlayerStats.SetStamina(Max);
                PlayerStats.StaminaAnchorUtc = now.Ticks;
                Changed?.Invoke();
                return;
            }

            PlayerStats.SetStamina(next);
            // ★anchor를 now로 밀면 안 된다. 회복 직전의 자투리 시간이 매번 버려져서,
            //   앱을 자주 여닫는 유저만 회복이 느려진다. 소비한 만큼만 정확히 밀어야 한다.
            PlayerStats.StaminaAnchorUtc = anchor.AddSeconds((double)gained * RefillSeconds).Ticks;
            Changed?.Invoke();
        }

        /// <summary>날짜가 바뀌었으면 광고 시청 횟수를 0으로 되돌린다.</summary>
        private static void RollOverAdDay()
        {
            // 되감기 방어를 거친 시각의 "현지 날짜"를 하루의 기준으로 쓴다.
            int today = LocalDateKey(NowUtc());
            if (PlayerStats.StaminaAdDate == today)
                return;

            PlayerStats.StaminaAdDate = today;
            PlayerStats.StaminaAdCount = 0;
        }

        /// <summary>날짜를 yyyyMMdd 정수로. 비교만 하면 되므로 문자열보다 가볍다.</summary>
        private static int LocalDateKey(DateTime utc)
        {
            DateTime local = utc.ToLocalTime();
            return local.Year * 10000 + local.Month * 100 + local.Day;
        }

        /// <summary>
        /// 되감기를 차단한 현재 UTC 시각.
        /// </summary>
        /// <remarks>
        /// 스태미나를 기기 시계로 계산하면 시계를 앞으로 돌려 즉시 채울 수 있다. 서버 시각이 없으면
        /// 이걸 완전히 막을 수는 없지만, <b>관측한 가장 미래 시각을 기억해 되감기만 막으면</b>
        /// 이득이 일회성으로 끝난다. 시계를 앞으로 돌려 한 번 채우고 나면 그 시각이 기준으로 남아,
        /// 시계를 되돌려도 진짜 시간이 거기 도달할 때까지 회복이 멈추기 때문이다.
        /// 광고 일일 한도의 날짜 판정도 같은 시각을 쓰므로 하루를 여러 번 넘기는 것도 함께 막힌다.
        /// (UGS는 이 프로젝트에서 Authentication/Leaderboards만 쓰고 있어 서버 시각을 노출하지 않는다.
        ///  서버 시각을 쓰려면 별도 엔드포인트가 필요해 여기서는 로컬 방어만 둔다.)
        /// </remarks>
        private static DateTime NowUtc()
        {
            DateTime now = DateTime.UtcNow;
            long maxSeen = PlayerStats.StaminaMaxSeenUtc;

            if (maxSeen > 0 && now.Ticks < maxSeen)
                return new DateTime(maxSeen, DateTimeKind.Utc); // 시계가 과거로 갔다 → 시간이 멈춘 것으로 취급

            PlayerStats.StaminaMaxSeenUtc = now.Ticks;
            return now;
        }
        #endregion
    }
}
