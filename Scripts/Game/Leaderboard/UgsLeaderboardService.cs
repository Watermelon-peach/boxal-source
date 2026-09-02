using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;
using UgsModels = Unity.Services.Leaderboards.Models;

namespace Boxal.Game.Leaderboard
{
    /// <summary>
    /// Unity Gaming Services 기반 리더보드 구현.
    /// 실제 동작하려면 Editor에서 프로젝트가 Unity Cloud에 링크되고(Project Settings → Services)
    /// 대시보드에 <see cref="LeaderboardId"/> 리더보드가 생성돼 있어야 한다(Phase 0).
    /// 링크/네트워크가 없으면 InitializeAsync가 조용히 실패하고 IsReady=false로 남아,
    /// 호출부는 예외 없이 빈 결과를 받는다.
    /// </summary>
    public class UgsLeaderboardService : ILeaderboardService
    {
        /// <summary>대시보드에서 만들 리더보드 ID(내림차순 정렬). Phase 0에서 동일한 ID로 생성할 것.</summary>
        public const string LeaderboardId = "top_score";

        private bool isReady;
        private Task initTask;      // 진행 중인 초기화를 공유해 중복 초기화를 막는다

        public bool IsReady => isReady;

        public Task InitializeAsync()
        {
            // 이미 초기화됐거나 진행 중이면 그 Task를 그대로 반환(멱등).
            if (initTask != null)
                return initTask;
            initTask = InitializeInternalAsync();
            return initTask;
        }

        private async Task InitializeInternalAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                isReady = true;
            }
            catch (Exception e)
            {
                // 클라우드 미링크/오프라인/서비스 예외 → 리더보드 비활성 상태로 계속 진행.
                Debug.LogWarning($"[Leaderboard] 초기화 실패(오프라인/미링크 가능): {e.Message}");
                isReady = false;
                initTask = null; // 다음 시도에서 재초기화할 수 있게 초기화 Task를 비운다
            }
        }

        public async Task<NameSubmitResult> SetPlayerNameAsync(string name)
        {
            // 초기화조차 안 됐으면 네트워크/링크 문제로 간주한다(사용자 잘못 아님).
            if (!await EnsureReadyAsync())
                return NameSubmitResult.Fail(NameSubmitError.Offline);
            try
            {
                // UGS는 이름 뒤에 "#1234" 태그를 붙여 반영된 최종 이름을 돌려준다.
                string applied = await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
                return string.IsNullOrEmpty(applied)
                    ? NameSubmitResult.Fail(NameSubmitError.Unknown)
                    : NameSubmitResult.Ok(applied);
            }
            catch (RequestFailedException e)
            {
                Debug.LogWarning($"[Leaderboard] 닉네임 설정 실패({e.ErrorCode}): {e.Message}");
                return NameSubmitResult.Fail(ClassifyNameError(e.ErrorCode));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] 닉네임 설정 실패: {e.Message}");
                return NameSubmitResult.Fail(NameSubmitError.Unknown);
            }
        }

        /// <summary>UGS 에러 코드를 UI가 이해하는 실패 사유로 변환한다.</summary>
        private static NameSubmitError ClassifyNameError(int errorCode)
        {
            if (errorCode == CommonErrorCodes.TransportError
                || errorCode == CommonErrorCodes.Timeout
                || errorCode == CommonErrorCodes.ServiceUnavailable)
                return NameSubmitError.Offline;
            if (errorCode == AuthenticationErrorCodes.InvalidParameters)
                return NameSubmitError.InvalidName;
            return NameSubmitError.Unknown;
        }

        public async Task<bool> SubmitScoreAsync(long score)
        {
            if (!await EnsureReadyAsync())
                return false;
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] 점수 제출 실패: {e.Message}");
                return false;
            }
        }

        public async Task<IReadOnlyList<LeaderboardRow>> GetTopScoresAsync(int count)
        {
            if (!await EnsureReadyAsync())
                return Array.Empty<LeaderboardRow>();
            try
            {
                string myId = AuthenticationService.Instance.PlayerId;
                var page = await LeaderboardsService.Instance.GetScoresAsync(
                    LeaderboardId, new GetScoresOptions { Offset = 0, Limit = count });

                var rows = new List<LeaderboardRow>(page.Results.Count);
                foreach (var e in page.Results)
                    rows.Add(ToRow(e, myId));
                return rows;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] 상위 점수 조회 실패: {e.Message}");
                return Array.Empty<LeaderboardRow>();
            }
        }

        public async Task<LeaderboardRow?> GetMyRankAsync()
        {
            if (!await EnsureReadyAsync())
                return null;
            try
            {
                string myId = AuthenticationService.Instance.PlayerId;
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId);
                return ToRow(entry, myId);
            }
            catch (Exception e)
            {
                // 아직 제출 기록이 없으면 서버가 404를 던진다 → 기록 없음으로 간주.
                Debug.LogWarning($"[Leaderboard] 내 순위 조회 실패(기록 없음 가능): {e.Message}");
                return null;
            }
        }

        /// <summary>현재 플레이어의 순위+전체 인원. 기록 없거나 실패 시 null.</summary>
        public async Task<PlayerStanding?> GetMyStandingAsync()
        {
            if (!await EnsureReadyAsync())
                return null;
            try
            {
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId);
                // 전체 엔트리 수는 아무 페이지나 조회했을 때의 Total에서 얻는다(Limit 1이면 충분).
                var page = await LeaderboardsService.Instance.GetScoresAsync(
                    LeaderboardId, new GetScoresOptions { Offset = 0, Limit = 1 });
                string name = string.IsNullOrEmpty(entry.PlayerName) ? "Player" : entry.PlayerName;
                return new PlayerStanding(entry.Rank + 1, page.Total, (long)entry.Score, name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] 내 순위+전체 조회 실패(기록 없음 가능): {e.Message}");
                return null;
            }
        }

        /// <summary>초기화가 안 됐으면 시도하고, 준비됐는지 여부를 반환한다.</summary>
        private async Task<bool> EnsureReadyAsync()
        {
            if (!isReady)
                await InitializeAsync();
            return isReady;
        }

        /// <summary>UGS 엔트리(0-based rank, double score)를 표시용 LeaderboardRow로 변환.</summary>
        private static LeaderboardRow ToRow(UgsModels.LeaderboardEntry e, string myPlayerId)
        {
            return new LeaderboardRow(
                rank: e.Rank + 1,
                playerName: string.IsNullOrEmpty(e.PlayerName) ? "Player" : e.PlayerName,
                score: (long)e.Score,
                isCurrentPlayer: e.PlayerId == myPlayerId);
        }
    }
}
