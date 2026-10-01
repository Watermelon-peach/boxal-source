# Boxal

*[→ Read in English](README.md)*

**▶ itch.io에서 플레이: https://watermelonpeach.itch.io/boxal** (스크린샷 + 무료 APK 다운로드)

혼자 개발한 모바일 액션 캐주얼 로그라이트입니다. 게임플레이·메타 프로그레션·UI·툴까지 전부 1인 개발했습니다.
Unity 6000.4 (URP) · C# · Android.

## 코드 읽기 시작점

| 파일 | 볼 만한 부분 |
|---|---|
| [`Game/Player/Orbit.cs`](Scripts/Game/Player/Orbit.cs) | 궤도 무기를 고정 슬롯으로 관리(피격 시 파괴 대신 비활성화), 수렴형 반지름 성장(`GrowRadius`), 스핀 버스트 |
| [`Game/Managers/UpgradeManager.cs`](Scripts/Game/Managers/UpgradeManager.cs) | 레벨업 3택1 추첨 — 가중치 기반, 뽑힌 카드는 후보에서 빼고 다시 추첨 |
| [`Game/Growth/UpgradeSO.cs`](Scripts/Game/Growth/UpgradeSO.cs) | ScriptableObject 업그레이드 구조. `CanOffer()`는 재정의할 수 없게 두고 `CanOfferCore()`만 열어서, 서브클래스가 해금 조건을 건너뛸 수 없게 함 |
| [`Game/Leaderboard/LeaderboardManager.cs`](Scripts/Game/Leaderboard/LeaderboardManager.cs) | UGS(Unity Gaming Services) 초기화를 1프레임 미룬 이유(패키지 내부 리셋과의 순서 문제), 오프라인 기록 동기화 |
| [`Game/TransformSnapshot.cs`](Scripts/Game/TransformSnapshot.cs) | 오브젝트 풀 반환 시 부서진 박스 조각의 위치와 속도를 원래대로 복원 |
| [`Game/Growth/AutoSelectManager.cs`](Scripts/Game/Growth/AutoSelectManager.cs) + [`Tools/BalanceSim/`](Tools/BalanceSim/) | 밸런스 시뮬레이터 결과를 자동 선택 기능(`PickBest`)의 우선순위표로 옮김 |

게임 한 판의 흐름은 `GameManager.cs` → `RoundManager.cs` → `SpawnManager.cs` 순서로 따라가면 됩니다.

## 특징

- **`Tools/BalanceSim/`** — 라운드별 난이도 곡선(몬스터 HP/DPS 요구치, 보스 게이트)을
  Unity에 손대기 *전에* 검증한 독립 파이썬 시뮬레이터입니다.
- **`Docs/`** — 구현 전에 먼저 쓴 설계 문서(성장 시스템, 메타 프로그레션, 코어 루프, 사운드)입니다.
  사후 문서화가 아니라 설계 단계의 산출물입니다.
- 스택 전체를 혼자 담당했습니다: 로그라이트 업그레이드 뽑기, 영구 저장되는 메타 프로그레션(재화·상점·
  스태미나), 글로벌 리더보드(Unity Gaming Services), UI 배선까지.

## 이 리포에 대해

Boxal 전체 유니티 프로젝트에서 **코드만 발췌**해 포트폴리오 열람용으로 공개한 리포입니다.

전체 프로젝트(유료 Unity Asset Store 패키지 ~950MB 포함)는 private 리포에 있습니다. 라이선스가
빌드에 포함하는 것은 허용하지만, 원본 에셋 파일을 공개 재배포하는 것은 금지하기 때문입니다. 이 리포에는
제가 직접 작성한 코드·설계 문서·툴만 들어 있습니다.

- `Scripts/` — 전체 C# 소스(게임플레이, 메타 프로그레션, UI 배선)
- `Docs/` — 설계 문서(성장 시스템, 메타 프로그레션, 메인 플레이 루프, 사운드)
- `Tools/BalanceSim/` — 라운드별 난이도 밸런싱용 파이썬 시뮬레이션
- `CREDITS.md` — 서드파티 오디오 라이선스 표기(CC BY / MIT)

이 리포는 그대로는 Unity에서 열리거나 빌드되지 않습니다(씬 파일과 서드파티 에셋이 빠져 있음).
실제로 플레이 가능한 빌드는 위 itch.io 링크에서 받을 수 있습니다.
