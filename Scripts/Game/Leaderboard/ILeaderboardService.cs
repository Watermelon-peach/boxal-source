using System.Collections.Generic;
using System.Threading.Tasks;

namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 리더보드 백엔드 추상화. 현재 구현은 UGS(UgsLeaderboardService)지만,
    /// 나중에 서버/다른 BaaS로 갈아끼울 때 이 인터페이스만 구현하면 되도록 캡슐화한다.
    /// 모든 메서드는 네트워크 실패 시 예외를 던지지 않고 안전한 기본값(빈 리스트/null/false)을 반환한다 —
    /// 오프라인이어도 홈 화면이 죽지 않게 하는 게 계약이다.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>초기화 및 로그인이 끝나 제출/조회가 가능한 상태인지.</summary>
        bool IsReady { get; }

        /// <summary>서비스 초기화 + 익명 로그인. 여러 번 호출해도 실제 초기화는 1회만 수행한다.</summary>
        Task InitializeAsync();

        /// <summary>
        /// 닉네임을 설정한다. 성공 시 실제 반영된 이름(태그 포함)을, 실패 시 사유를 담아 반환한다.
        /// 이 메서드만 예외적으로 실패 사유를 구분해 돌려주는 이유는, 강제 입력 팝업이
        /// "재시도하면 되는 실패(오프라인)"와 "고쳐야 하는 실패(이름 규칙 위반)"를 다르게 안내해야 하기 때문이다.
        /// </summary>
        Task<NameSubmitResult> SetPlayerNameAsync(string name);

        /// <summary>점수를 제출한다. 성공하면 true. 서버가 최고 점수만 유지하므로 낮은 점수 제출도 안전하다.</summary>
        Task<bool> SubmitScoreAsync(long score);

        /// <summary>상위 count명을 순위대로 반환. 실패 시 빈 리스트.</summary>
        Task<IReadOnlyList<LeaderboardRow>> GetTopScoresAsync(int count);

        /// <summary>현재 플레이어 본인의 순위 행. 기록이 없거나 실패하면 null.</summary>
        Task<LeaderboardRow?> GetMyRankAsync();

        /// <summary>현재 플레이어의 순위+전체 인원(백분위 슬라이더용). 기록 없거나 실패 시 null.</summary>
        Task<PlayerStanding?> GetMyStandingAsync();
    }
}
