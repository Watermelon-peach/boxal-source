namespace Boxal.Game.Leaderboard
{
    /// <summary>닉네임 설정 실패 사유. UI가 사용자에게 다른 안내를 보여주기 위해 구분한다.</summary>
    public enum NameSubmitError
    {
        /// <summary>실패 없음(성공).</summary>
        None,
        /// <summary>네트워크/서비스 미준비. 사용자 잘못이 아니므로 재시도를 유도한다.</summary>
        Offline,
        /// <summary>이름이 규칙에 맞지 않아 거절됨. 사용자에게 규칙을 다시 보여준다.</summary>
        InvalidName,
        /// <summary>그 밖의 실패.</summary>
        Unknown
    }

    /// <summary>
    /// 닉네임 설정 결과. 기존처럼 실패를 전부 null로 뭉개면 UI가 사유를 알 수 없어
    /// "오프라인이라 다시 시도해야 함"과 "이름이 잘못돼 고쳐야 함"을 구분하지 못한다.
    /// </summary>
    public readonly struct NameSubmitResult
    {
        /// <summary>성공 여부.</summary>
        public readonly bool Success;
        /// <summary>서버가 실제로 반영한 이름(태그 포함, 예: "Boxal#1234"). 실패 시 null.</summary>
        public readonly string AppliedName;
        /// <summary>실패 사유. 성공이면 None.</summary>
        public readonly NameSubmitError Error;

        private NameSubmitResult(bool success, string appliedName, NameSubmitError error)
        {
            Success = success;
            AppliedName = appliedName;
            Error = error;
        }

        public static NameSubmitResult Ok(string appliedName)
        {
            return new NameSubmitResult(true, appliedName, NameSubmitError.None);
        }

        public static NameSubmitResult Fail(NameSubmitError error)
        {
            return new NameSubmitResult(false, null, error);
        }
    }
}
