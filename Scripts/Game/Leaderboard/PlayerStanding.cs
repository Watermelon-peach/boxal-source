namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 현재 플레이어의 순위 정보 + 전체 인원. UserInfo 백분위 슬라이더 계산에 쓴다.
    /// </summary>
    public readonly struct PlayerStanding
    {
        /// <summary>표시용 순위(1부터).</summary>
        public readonly int Rank;

        /// <summary>리더보드 전체 엔트리 수.</summary>
        public readonly int Total;

        /// <summary>내 점수.</summary>
        public readonly long Score;

        /// <summary>내 닉네임(태그 포함 가능).</summary>
        public readonly string PlayerName;

        public PlayerStanding(int rank, int total, long score, string playerName)
        {
            Rank = rank;
            Total = total;
            Score = score;
            PlayerName = playerName;
        }
    }
}
