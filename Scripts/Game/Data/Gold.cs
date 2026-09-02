using System;

namespace Boxal.Game
{
    /// <summary>
    /// 런을 넘겨 유지되는 상점 재화. 저장은 <see cref="PlayerStats"/>에 위임한다.
    /// </summary>
    /// <remarks>
    /// 판중에는 <see cref="GameManager.RunGold"/>에 쌓아 두고 게임오버에서 한 번만 <see cref="Add"/>로 넘긴다.
    /// 처치마다 여기에 쓰면 PlayerPrefs.Save()가 초당 여러 번 돌아 기기에서 끊긴다.
    /// </remarks>
    public static class Gold
    {
        /// <summary>잔액이 바뀌었을 때. 상점/홈 표시가 구독한다.</summary>
        public static event Action Changed;

        public static long Balance => PlayerStats.Gold;

        /// <summary>골드를 지급한다.</summary>
        public static void Add(long amount)
        {
            if (amount <= 0)
                return;
            PlayerStats.Gold = PlayerStats.Gold + amount;
            Changed?.Invoke();
        }

        /// <summary>충분하면 차감하고 true. 모자라면 아무것도 하지 않고 false.</summary>
        public static bool TrySpend(long amount)
        {
            if (amount <= 0)
                return true;
            long balance = PlayerStats.Gold;
            if (balance < amount)
                return false;

            PlayerStats.Gold = balance - amount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>디버그/테스트용.</summary>
        public static void SetBalance(long amount)
        {
            PlayerStats.Gold = amount < 0 ? 0 : amount;
            Changed?.Invoke();
        }
    }
}
