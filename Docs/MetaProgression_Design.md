# Boxal — 메타 프로그레션(상점 · 업그레이드 해금) 기획서

> 작성 기준: 2026-08-24 / 코드 베이스 커밋 `6d50a52` + 이후 워킹트리 변경분
> 이 문서는 **런을 넘겨 유지되는** 두 시스템(골드/상점, 킬 누적 업그레이드 해금)의 로직을 정리한다.
> 런 한정 로그라이트 성장(카드 3택1)은 [`GrowthSystem_Design.md`](GrowthSystem_Design.md) 참고.
> UI(패널/배치)는 미구현 — 사용자가 씬에 배치하면 이쪽이 스크립트를 배선하는 방식으로 진행 중.

---

## 1. 배경 — 왜 필요했나

기존 성장(레벨업 카드)은 런 한정이라 게임을 끄면 전부 사라진다. 포트폴리오 프로젝트로서
"이것저것 기능을 갖춘 상태"를 보여주기 위해 **런을 넘겨 쌓이는 것**을 추가하기로 했다
(밸런스/리텐션 목적이 아니라 구현 범위를 넓히는 목적 — [`meta-layer-design`] 메모 참고).

두 축으로 나눴다:
1. **골드 → 상점**: 처치마다 버는 재화로 영구 레벨 업그레이드를 산다.
2. **킬 누적 → 해금**: 평생 누적 처치 수에 따라 런 중 카드 풀이 점점 넓어진다.

---

## 2. 골드 시스템

### 2.1 흐름

```
Boxmon 처치 (Boxmon.BreakBox)
    → GameManager.RegisterKill()
        RunKills++
        RunGold += ShopUpgrades.GoldPerKill   (판중엔 계정에 안 씀)
        UiManager.SetGold(RunGold)             (PlayScene HUD 실시간 표시)
    ↓ (라운드 반복, RunGold는 GameManager가 들고만 있음)
GameOverUI.Show()
    → Gold.Add(GameManager.Instance.RunGold)   (여기서 처음 계정에 반영)
    → PlayerStats.AddKills(runKills) 바로 옆
```

**판중에 즉시 반영하지 않는 이유:** 처치마다 `PlayerPrefs.Save()`가 돌면 후반 라운드에서
초당 여러 번 디스크 쓰기가 발생해 기기에서 끊긴다. `RunGold`는 메모리에만 있는 값이라 비용이 없고,
게임오버 시 한 번만 저장 계층에 합산한다.

**주의:** 앱을 강제 종료하면 그 판의 `RunGold`는 소실된다(점수와 동일한 특성). 정상 종료(게임오버,
퍼즈→포기)는 전부 `GameManager.GameOver()`를 거치므로 반영된다.

### 2.2 관련 스크립트

| 파일 | 역할 |
|---|---|
| `Scripts/Game/Data/Gold.cs` | 잔액 조회(`Balance`)·지급(`Add`)·차감(`TrySpend`)·`Changed` 이벤트 |
| `Scripts/Game/Data/PlayerStats.cs` | `Gold` 프로퍼티(PlayerPrefs 문자열 저장, long) |
| `Scripts/Game/Managers/GameManager.cs` | `RunGold` 프로퍼티, `RegisterKill()`에서 누적 |
| `Scripts/Game/Managers/GameOverUI.cs` | `Gold.Add(RunGold)` 커밋 |
| `Scripts/Game/Managers/UiManager.cs` | `goldUi`/`SetGold()` — PlayScene HUD 실시간 표시(RunGold 기준) |

### 2.3 UI 배선 현황

| 위치 | 씬 | 배선 상태 |
|---|---|---|
| 홈 `MainPanel/ResourceBars/ResourceBar_Golds` | Home | ✅ `GoldBarUI` 부착. `Text_Value`=계정 잔액(`Gold.Balance`, `Gold.Changed` 구독). `Button_Add`는 상점 페이지(`UiPager.GoToPage`)로 이동하게 만들어 뒀으나 **상점 패널이 아직 없어 `pager` 참조가 비어 있고 버튼은 비활성 상태.** 상점 패널 완성 시 `pager`만 연결하면 됨. |
| 플레이 `HUD/ResourceBar_Golds` | PlayScene | ✅ `UiManager.goldUi`에 `Text_Value` 연결. 판중 `RunGold`가 킬마다 실시간으로 오른다(계정 잔액과는 다른 값이니 혼동 주의). `Button_Add`는 판중에 갈 곳이 없어 **사용자가 직접 제거함.** |

**홈 바와 플레이 바는 같은 이름(`ResourceBar_Golds`)이지만 보여주는 값이 다르다.**
홈 = 계정 잔액(누적), 플레이 = 이번 판 임시 누적. 새로 손댈 때 헷갈리지 않도록 주의.

### 2.4 상점 판매 항목 — 수치

`Scripts/Game/Data/ShopUpgrades.cs`. 전부 `const`로 파일 상단에 모여 있어 인스펙터가 아니라
**코드에서 튜닝**한다(SO가 아닌 이유: 3종뿐이고 서로 독립적이라 SO 오버헤드가 불필요하다고 판단).

| # | 이름(기획 원안) | id (enum) | 레벨당 효과 | 비용 base | growth |
|---|---|---|---|---|---|
| 1 | 획득 골드 배수 | `GoldPerKill` | 처치당 골드 +1 | 100 | 1.18 (무한) |
| 2 | 최초 공격력 | `StartAttack` | 시작 Atk +1 | 150 | 1.80 (Lv10 상한) |
| 3 | 획득 포인트 배수 | `PointBonus` | 포인트 +5% | 120 | 1.18 (무한) |

- **레벨 상한은 `StartAttack`에만 있다(10). 골드·포인트는 무한이다**(2026-08-31 결정).
  상점을 "점수를 계속 불리는 엔드컨텐츠"로 두기로 했기 때문이다. 시작 공격력만 막는 이유는
  기본값이 1이라 레벨당 +1이 곧 배수이기 때문 — Lv50이면 ATK 51배라 전투가 무의미해진다.
  골드·포인트는 숫자만 커지는 축이라 밸런스를 안 건드린다.
  코드에서 상한을 볼 땐 `GetMaxLevel(id)` / `HasMaxLevel(id)`을 쓸 것(공통 상수 `MaxLevel`은 없앴다).
- 비용 = `baseCost × growth^(현재 레벨)`, **10골드 단위로 반올림**(표시가
  지저분해지지 않도록). 계산·조회는 `ShopUpgrades.GetNextCost(id)`.
  상한이 없어져서 레벨이 아주 높아지면 이 값이 long을 넘는다. 넘긴 채로 캐스팅하면 음수가 되고,
  음수는 "최대 레벨"을 뜻하는 -1과 구분이 안 돼 UI가 MAX로 오인하므로 `CostCeiling`에서 잘라낸다.
- **비용 성장률(growth)이 상한 유무로 갈린다**(2026-08-31 재조정).
  - **무한 축(골드·포인트) = 1.18.** 골드 수급은 레벨당 +1로 **선형**인데 가격은 **지수**라 둘은
    반드시 벌어지고, 그 지점부터 레벨이 멈춘다. 1.55/1.65였을 때는 그 천장이 **Lv15~20**이라
    (200판을 굴려도 3종 합쳐 39레벨) 상한을 없앤 의미가 없었다. 1.18은 천장을 **Lv55~60**으로 민 값이다.
  - **상한 축(공격력) = 1.80 유지.** Lv10에서 멈추니 천장 문제가 없고, "가장 비싼 전투력 강화 →
    골드 배수부터 올리는 게 정석"이라는 원래 의도를 지켜야 한다. 여기까지 1.18로 내리면
    10레벨 총액이 66,740 → 3,530골드가 되어 상한이 무의미해진다.
- 레벨별 비용(반올림 후):

  | 레벨 | GoldPerKill (1.18) | PointBonus (1.18) | StartAttack (1.80) |
  |---|---|---|---|
  | 1 | 100 | 120 | 150 |
  | 3 | 140 | 170 | 490 |
  | 5 | 190 | 230 | 1,570 |
  | 7 | 270 | 320 | 5,100 |
  | 10 | 440 | 530 | 29,750 |
  | **~10 누적** | **2,350** | **2,810** | **66,740 (MAX)** |
  | 20 | 2,320 | 2,790 | — |
  | **~20 누적** | **14,660** | **17,590** | — |
  | 30 | 12,150 | 14,580 | — |
  | **~30 누적** | **79,090** | **94,910** | — |

- 실제 진행 속도(한 판 150킬, 매 판 살 수 있는 것 중 제일 싼 것을 사는 플레이 가정):

  | 판수 | GoldPerKill | PointBonus | StartAttack | 처치당 골드 | 점수 배수 |
  |---|---|---|---|---|---|
  | 10 | 14 | 12 | 3 | 15 | 1.60배 |
  | 25 | 23 | 21 | 6 | 24 | 2.05배 |
  | 50 | 29 | 28 | 8 | 30 | 2.40배 |
  | 100 | 35 | 34 | 9 | 36 | 2.70배 |
  | 200 | 41 | 39 | 10 (MAX) | 42 | 2.95배 |

  공격력이 가장 비싸서 자연히 마지막에 채워진다(200판쯤 MAX) — 원래 의도한 구매 순서가 유지된다.

- 처치당 기본 골드는 1(`BaseGoldPerKill`). 참고로 초보 첫 판이 약 40~50킬, 숙련 후반 판이
  200~250킬 수준이라([`engagement-collapse-r15`] 시뮬 기준) 첫 업그레이드는 대략 2~3판 안에 산다.

### 2.5 효과 적용 지점

| 효과 | 적용 위치 | 비고 |
|---|---|---|
| 처치당 골드 | `GameManager.RegisterKill()` | `ShopUpgrades.GoldPerKill` 값을 그대로 `RunGold`에 가산 |
| 최초 공격력 | `Player`의 판 시작 초기화 (`Atk = 1.0 + ShopUpgrades.StartAttackBonus`) | 레벨업 카드의 Atk 증가와는 별개 레이어. 카드는 매 판 0부터 다시 쌓이고, 상점 보너스는 시작값에 얹힌다 |
| 획득 포인트 배수 | `GameManager.AddPoints()` | 모든 점수 획득 경로(일반 처치·보스·왕보스 10배)가 이 함수 하나를 지나므로 여기 한 곳에서만 곱하면 전체에 반영됨 |

### 2.6 상점 UI가 그려야 할 것 (미구현 — 참고용)

패널 하나당 항목 3개, 항목별로 필요한 표시:
- 이름 + 설명(`ShopUpgrades.GetEffectLabel(id)` — "Gold +1 per kill" 형식)
- 현재 값(`GetCurrentValueLabel(id)` — "3 / kill" 형식)
- 현재 레벨. `HasMaxLevel(id)`이 참일 때만 "Lv 3 / 10"처럼 분모를 붙이고,
  무한인 것(골드·포인트)은 "Lv 37"처럼 레벨만 쓴다
- 다음 레벨 비용(`GetNextCost(id)`, 최대 레벨이면 -1 반환 → "MAX" 표시로 분기.
  실질적으로 MAX가 뜨는 건 `StartAttack`뿐이다)
- 구매 버튼: `TryPurchase(id)` 호출, 반환값으로 성공/실패(골드 부족) 처리. `CanPurchase(id)`로 버튼
  활성/비활성 미리 판단 가능
- `ShopUpgrades.Changed`, `Gold.Changed` 둘 다 구독해서 갱신(레벨이 바뀌거나 골드가 바뀔 때 모두 필요)

---

## 3. 업그레이드 해금 시스템

### 3.1 설계 원칙 — 해금 상태는 저장하지 않는다

```csharp
public bool IsUnlocked => unlockKills <= 0 || PlayerStats.TotalKills >= unlockKills;
```

해금 여부를 별도 플래그로 저장하지 않고, **평생 누적 처치 수(`PlayerStats.TotalKills`)에서 매번
파생시킨다.** 저장 슬롯이 하나도 늘지 않고, 저장/동기화가 어긋나 "해금했는데 잠겨 보이는" 부류의
버그가 구조적으로 발생할 수 없다.

### 3.2 게이트 위치 — `CanOffer()`

```
UpgradeSO.CanOffer()  [non-virtual, 봉인됨]
    = IsUnlocked && CanOfferCore()
                        └─ 서브클래스가 재정의하는 종류별 조건 (상한 도달 등)
```

과거 `CanOffer()`가 `virtual`이라 `OrbitRadiusUpgradeSO`/`ShieldUpgradeSO`가 이를 재정의하면서
해금 검사를 우회할 수 있는 구멍이 있었다. 이번에 `CanOffer()`를 봉인하고 서브클래스는
`CanOfferCore()`만 재정의하도록 바꿨다. **새 업그레이드에 제시 조건을 넣을 땐 반드시
`CanOfferCore()`를 재정의할 것 — `CanOffer()`를 건드리면 컴파일 에러(non-virtual override 불가)로
바로 드러난다.**

`UpgradeManager`(레벨업 카드 추첨)와 `BossRewardManager`(보스 보상 추첨) 둘 다 후보 수집 시
`CanOffer()` 한 곳만 거치므로, 해금 로직을 한 번만 넣으면 두 시스템에 자동으로 반영된다.

### 3.3 해금 표 (누적 처치 수 기준)

**설계 이력:** 최초안은 최대 2200킬(라운드당 처치 밀도를 고려 안 한 값) → 사용자 피드백으로
1000킬로 압축 → "한 라운드에 5마리씩 나오는데도 빡세다"는 재피드백으로 **최종 500킬로 재압축**
(2026-08-24, 한 대화 안에서 두 차례 조정). **아래 표가 현재 유효한 값이다.**

| 누적 킬 | 등급 | id | 비고 |
|---|---|---|---|
| 0 | Common | `atk_common`, `orbit_radius_common`, `orbit_speed_common`, `ult_charge` | 처음부터 해금 |
| 0 | Legendary | `legendary_shield` | 처음부터 해금(사용자 지정) |
| 30 | Common | `heal_common` | 초보 첫 판(약 40~50킬) 직후 해금되도록 설계 |
| 70 | Rare | `ult_duration` | |
| 120 | Rare | `weapon_count_rare` | |
| 180 | Legendary | `legendary_full_heal` | 생존 축 먼저 |
| 260 | Legendary | `legendary_atk_double` | 화력 축 |
| 340 | Legendary | `legendary_weapon_count` | 화력 축 |
| 420 | Legendary | `legendary_xp_boost` | 성장 축 |
| 500 | Legendary | `legendary_ult_autocharge` | 편의 축, 마지막 해금 |

**설계 근거:**
- **Common은 Heal만 잠금, Rare는 전부 잠금, Legendary는 Shield만 해금**(사용자 지정 초기 상태).
- **30이라는 첫 해금값은 세 번의 조정 내내 고정이다.** "초보 첫 판 이후 Heal 해금"이라는 요구사항은
  전체 상한과 독립적인 조건이기 때문에, 상한을 줄일 때 앞쪽 값을 비례로 줄이면 안 된다
  (비례로 줄이면 15가 되어 첫 판 도중에 풀려버려 요구사항이 깨진다). **상한을 조정할 땐 뒤쪽
  간격만 압축할 것.**
- Legendary 내부 순서는 생존(Heal) → 화력(Atk, WeaponCount) → 성장(Xp) → 편의(UltChargeAuto).
  세부 순서는 지정 없이 이쪽 판단으로 정한 것(사용자: "그 안에서 세부 순서는 알아서").
- 값은 각 `.asset`의 `unlockKills` 필드에서 바로 조정 가능. **바꿀 때 `UpgradeCatalog.asset`도
  같이 재정렬해야 한다**(아래 3.5 참고 — 코드가 매번 정렬하는 게 아니라 애셋 저장 시점의
  배열 순서를 그대로 쓰는 곳이 있다).

### 3.4 관련 스크립트

| 파일 | 역할 |
|---|---|
| `Scripts/Game/Growth/UpgradeSO.cs` | `unlockKills` 필드, `IsUnlocked`, 봉인된 `CanOffer()` |
| `Scripts/Game/Growth/OrbitRadiusUpgradeSO.cs`, `ShieldUpgradeSO.cs` | `CanOfferCore()` 재정의(반경 상한/보호막 중복 방지) |
| `Scripts/Game/Growth/UpgradeCatalog.cs` (신규 SO) | 해금 패널 전용 표시 목록 (아래 3.5) |

### 3.5 `UpgradeCatalog` — 왜 별도 애셋이 필요했나

추첨 풀(`UpgradeManager.pool`)은 **PlayScene의 씬 오브젝트에 직렬화**돼 있어 홈 씬에서 참조할 수
없다. 해금 패널(홈, i1)이 "13개 중 5개 해금됨" 같은 목록을 그리려면 씬과 무관한 애셋이 필요해서
`Assets/Boxal/Data/Upgrades/UpgradeCatalog.asset`을 만들었다.

```csharp
catalog.Entries              // 전체 13개 (UpgradeSO 목록)
catalog.GetSortedByUnlock()  // unlockKills → 등급 → 이름 순 정렬
catalog.GetNextLocked()      // 아직 안 풀린 것 중 가장 가까운 하나 (다음 해금까지 N킬 표시용)
catalog.GetProgress(out unlocked, out total)  // "5 / 13" 진행도
```

**추첨 풀과 카탈로그는 별개의 목록이다.** 새 업그레이드(.asset)를 추가하면 **양쪽 다** 채워야
한다 — 풀에만 넣으면 카드로는 나오는데 해금 패널엔 안 보이고, 카탈로그에만 넣으면 그 반대가 된다.
둘 다 넣는 걸 깜빡여도 컴파일 에러가 안 나므로(둘 다 그냥 리스트) 새 업그레이드 추가 시 체크리스트로
기억해 둘 것.

### 3.6 해금 패널 UI가 그려야 할 것 (미구현 — 참고용)

- 목록: `catalog.GetSortedByUnlock()` 순서로 카드/행 나열
- 각 항목: 아이콘(잠기면 실루엣 처리 권장) + 이름 + `unlockKills` + 해금 여부(`upgrade.IsUnlocked`)
- 상단 진행도: `catalog.GetProgress()` → "5 / 13 Unlocked" 류
- (선택) "다음 해금까지 N킬" 배너: `catalog.GetNextLocked()`가 null이 아니면
  `nextLocked.unlockKills - PlayerStats.TotalKills`로 잔여 계산

---

## 4. 홈 페이저 인덱스 재배치 (UI 작업 시 필수)

상점(i3)·해금(i1) 패널이 추가되면 기존 페이지 인덱스가 통째로 밀린다.

| index | 이전 | 이후 |
|---|---|---|
| 0 | Settings | Settings (변화 없음) |
| 1 | Main | **Unlock (신규)** |
| 2 | Leaderboard | Main (이전 1번) |
| 3 | — | **Shop (신규)** |
| 4 | — | Leaderboard (이전 2번) |

**패널 앵커 값 (`PageContainer` 직속 자식, 전부 pivot 0.5/0.5, offset 0):**

| index | anchorMin | anchorMax |
|---|---|---|
| 0 | (0,0) | (1,1) |
| 1 | (1,0) | (2,1) |
| 2 | (2,0) | (3,1) |
| 3 | (3,0) | (4,1) |
| 4 | (4,0) | (5,1) |

앵커 프리셋 드롭다운으로는 1을 넘는 값을 못 넣으므로 **Anchors 폴드아웃에 Min/Max를 직접
입력**해야 한다. 바꾼 뒤 Left/Right/Top/Bottom을 0으로 다시 맞출 것.

패널이 다 만들어지면 스크립트에서 손볼 곳:
- `UiPager.pageCount` 3 → 5, `defaultPage` 1 → 2
- `HomeManager.leaderboardPageIndex` 2 → 4, `settingsPageIndex`는 0 유지
- `HomeManager.tabButtons` / `tabActivatedObjects` 배열 3 → 5개(순서 = 페이지 인덱스와 일치해야 함)
- `GoldBarUI.pager`(홈 바의 + 버튼) → 새 `UiPager` 참조, `shopPageIndex` = 3 확인

---

## 5. 아직 안 한 것

- 상점 패널 / 해금 패널 UI 배치(사용자 작업 예정) 및 그 이후 배선
- 홈 페이저 5페이지 재배치(위 4번)
- 업그레이드 선택 연출(카드 아이콘 → 퍼즈 버튼 이동·축소·소멸)
- 튜토리얼

관련 메모리: `meta-layer-design`, `shop-and-unlock`, `engagement-collapse-r15`, `feedback-dont-author-ui`
