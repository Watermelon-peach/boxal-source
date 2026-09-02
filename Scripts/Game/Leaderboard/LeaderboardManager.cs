using System.Collections.Generic;
using System.Threading.Tasks;
using Boxal.Util;
using UnityEngine;

namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// 홈/게임오버 UI가 사용하는 리더보드 파사드(씬 전환에도 살아남는 싱글톤).
    /// 실제 백엔드 호출은 <see cref="ILeaderboardService"/>에 위임하고, 여기서는
    /// 초기화 트리거·닉네임(로컬 PlayerStats 연동)·마지막 조회 결과 캐시를 담당한다.
    /// UI는 이 클래스만 알면 되고 UGS를 직접 참조하지 않는다.
    /// </summary>
    public class LeaderboardManager : PersistanceSingleton<LeaderboardManager>
    {
        private readonly ILeaderboardService service = new UgsLeaderboardService();

        /// <summary>마지막으로 성공한 상위 점수 조회 결과(오프라인 재진입 시 즉시 표시용).</summary>
        private IReadOnlyList<LeaderboardRow> cachedTop = System.Array.Empty<LeaderboardRow>();
        public IReadOnlyList<LeaderboardRow> CachedTop => cachedTop;

        public bool IsReady => service.IsReady;

        protected override void Awake()
        {
            base.Awake();
            // 중복 인스턴스는 base.Awake()가 파괴 예약했으므로 초기화를 시작하지 않는다.
            if (Instance != this)
                return;

            // 앱 시작과 동시에 초기화를 시작해 둔다(실패해도 조용히 넘어감). 결과는 await하지 않는다.
            StartCoroutine(InitializeAfterSceneLoad());
        }

        /// <summary>
        /// 한 프레임 지연 후 초기화한다.
        /// UGS Leaderboards 패키지는 에디터에서 <c>[RuntimeInitializeOnLoadMethod]</c>(=AfterSceneLoad)로
        /// <c>LeaderboardsService.s_Instance</c>를 null로 리셋한다. 씬 로드 중(Awake)에 초기화하면
        /// 그 리셋이 방금 세팅된 인스턴스를 지워버려, UnityServices는 Initialized인데
        /// <c>LeaderboardsService.Instance</c>만 null인 상태가 된다(모든 조회가 "not been initialized"로 실패).
        /// 한 프레임 뒤면 AfterSceneLoad 리셋이 이미 끝난 뒤라 안전하다.
        /// </summary>
        private System.Collections.IEnumerator InitializeAfterSceneLoad()
        {
            yield return null;
            _ = InitializeAndSyncAsync();
        }

        /// <summary>초기화한 뒤, 오프라인에서 세운 미전송 기록이 있으면 이어서 올린다.</summary>
        private async Task InitializeAndSyncAsync()
        {
            await service.InitializeAsync();
            await SyncPendingBestAsync();
        }

        /// <summary>
        /// 서버에 아직 반영되지 않은 최고 기록을 올린다(오프라인에서 세운 기록의 복구 경로).
        /// 리더보드가 Keep Best 설정이라 서버는 최고값만 유지하므로, 판마다 큐에 쌓아 둘 필요 없이
        /// <b>로컬 최고 기록 하나만</b> 올리면 오프라인에서 몇 판을 했든 결과가 같다.
        /// 실패하면 SyncedScore를 갱신하지 않으므로 다음 실행에서 자동으로 다시 시도한다.
        /// </summary>
        public async Task SyncPendingBestAsync()
        {
            if (!PlayerStats.HasUnsyncedScore)
                return;
            await SubmitScoreAsync(PlayerStats.BestScore);
        }

        /// <summary>
        /// 닉네임을 설정한다. <b>서버가 승인한 경우에만</b> 로컬에 저장한다.
        /// 실패해도 로컬에 저장하면 HasPlayerName이 true가 되어 입력 팝업이 다시 뜨지 않는데,
        /// 정작 서버엔 이름이 없어 리더보드에는 "Player"로 표시되는 불일치가 영구히 굳는다.
        /// 저장 기준을 "서버 승인"으로 두면 로컬 이름과 리더보드 표시가 항상 일치한다.
        /// </summary>
        public async Task<NameSubmitResult> SetPlayerNameAsync(string name)
        {
            NameSubmitResult result = await service.SetPlayerNameAsync(name);
            if (result.Success)
                PlayerStats.PlayerName = result.AppliedName;
            return result;
        }

        /// <summary>
        /// 점수를 리더보드에 제출한다(신기록 여부와 무관하게 서버가 최고값만 유지).
        /// 성공하면 반영된 지점을 기록해, 오프라인에서 쌓인 기록과 이미 올라간 기록을 구분한다.
        /// </summary>
        public async Task<bool> SubmitScoreAsync(long score)
        {
            bool submitted = await service.SubmitScoreAsync(score);
            if (submitted && score > PlayerStats.SyncedScore)
                PlayerStats.SyncedScore = score;
            return submitted;
        }

        /// <summary>상위 count명을 조회하고, 성공 시 캐시를 갱신한다.</summary>
        public async Task<IReadOnlyList<LeaderboardRow>> GetTopScoresAsync(int count)
        {
            var rows = await service.GetTopScoresAsync(count);
            if (rows.Count > 0)
                cachedTop = rows;
            return rows;
        }

        /// <summary>현재 플레이어 본인의 순위(기록 없으면 null).</summary>
        public Task<LeaderboardRow?> GetMyRankAsync() => service.GetMyRankAsync();

        /// <summary>현재 플레이어의 순위+전체 인원(UserInfo 슬라이더용).</summary>
        public Task<PlayerStanding?> GetMyStandingAsync() => service.GetMyStandingAsync();
    }
}
