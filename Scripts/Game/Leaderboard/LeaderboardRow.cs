namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 리더보드 한 줄을 표현하는 백엔드 무관 DTO. UGS/서버/로컬 어느 소스에서 오든
    /// UI는 이 타입만 다루면 되도록 추상화한다(UGS의 Models.LeaderboardEntry와 이름 충돌 회피).
    /// </summary>
    public readonly struct LeaderboardRow
    {
        /// <summary>표시용 순위(1부터 시작). UGS는 0-based라 매핑 시 +1 한다.</summary>
        public readonly int Rank;

        /// <summary>표시용 닉네임. UGS는 "Name#1234" 형태로 태그가 붙어 올 수 있다.</summary>
        public readonly string PlayerName;

        /// <summary>점수.</summary>
        public readonly long Score;

        /// <summary>현재 플레이어 본인 행이면 true(리스트에서 하이라이트용).</summary>
        public readonly bool IsCurrentPlayer;

        /// <summary>UI 표시용 이름. UGS가 자동으로 붙이는 "#1234" 태그를 떼어낸다.</summary>
        public string DisplayName => StripTag(PlayerName);

        public LeaderboardRow(int rank, string playerName, long score, bool isCurrentPlayer)
        {
            Rank = rank;
            PlayerName = playerName;
            Score = score;
            IsCurrentPlayer = isCurrentPlayer;
        }

        /// <summary>
        /// "Name#1234" → "Name". UGS Player Names는 이름 중복을 허용하는 대신 서버가 임의의 숫자 태그를
        /// 자동으로 붙이며, 이를 끄는 옵션은 없다. 태그는 데이터에 그대로 두고 표시할 때만 떼어낸다
        /// (데이터에서 지우면 되돌릴 수 없고, 동명이인 구분이 필요해질 때 쓸 수 없다).
        /// 본인 행 식별은 이름이 아니라 PlayerId 비교(IsCurrentPlayer)로 하므로 태그를 숨겨도 정확하다.
        /// </summary>
        public static string StripTag(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            int tagIndex = name.LastIndexOf('#');
            // tagIndex > 0 : 이름이 '#'로 시작하는 경우 빈 문자열이 되는 것을 막는다.
            return tagIndex > 0 ? name.Substring(0, tagIndex) : name;
        }
    }
}
