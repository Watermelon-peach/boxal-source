namespace Boxal.Game.Audio
{
    /// <summary>
    /// 효과음(SFX) 식별자. 코드는 클립을 직접 참조하지 않고 이 ID로 재생을 요청하며,
    /// 실제 클립/볼륨/피치는 <see cref="SoundLibrary"/> 에서 인스펙터로 매핑한다.
    /// 번호 주석은 Docs/Sound_Design.md 의 목록 번호와 일치한다.
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // Tier 1
        BoxmonBreak = 1,   // 박스몬 파괴 (가장 자주 울림 → 피치 랜덤화 필수)
        Jump = 2,          // 점프
        Charge = 3,        // 차징 (루프)
        Parry = 4,         // 패링 성공
        PlayerHit = 5,     // 플레이어 피격
        LevelUp = 6,       // 레벨업
        CardSelect = 7,    // 카드 선택
        GameOver = 8,      // 게임오버

        // Tier 2
        BoxmonHit = 9,     // 박스몬 타격(안 죽음)
        UltReady = 10,     // 궁극기 충전 완료
        UltActivate = 11,  // 궁극기 발동
        UltEnd = 12,       // 궁극기 종료
        BossAlert = 13,    // 보스 등장 경고
        KingBossAlert = 14,// 왕보스 등장 경고
        RoundStart = 15,   // 라운드 시작

        // Tier 3
        BossRewardOffer = 16, // 보스 보상 등장
        RewardClaim = 17,     // 보상 획득
        LowHpAlert = 18,      // 한방컷 경고(LowHP)
        NewRecord = 19,       // 신기록
        UiClick = 20,         // UI 버튼 클릭
        Popup = 21,           // 팝업 열림/닫힘
    }

    /// <summary>배경음(BGM) 식별자.</summary>
    public enum BgmId
    {
        None = 0,
        Home = 1,
        Play = 2,
        KingBoss = 3, // 없으면 Play 유지
        Title = 4,    // 부팅(타이틀) 씬
    }
}
