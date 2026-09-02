# Boxal — Main Play 시스템 기획서

> 작성 기준: 2026-07-05 / Main Play 구조 1차 완성 시점
> 이 문서는 현재 구현된 인게임(Main Play) 전 시스템을 정리한 기획서다. 초기 성장 시스템 구상은
> [`GrowthSystem_Design.md`](GrowthSystem_Design.md) 참고.

---

## 1. 게임 개요

**장르:** 라운드 기반 생존 아레나 (모바일 캐주얼 / 엔드리스 하이스코어)

**코어 판타지:** 박스형 적(Boxmon)이 화면 위로 차곡차곡 쌓여 내려오고, 플레이어는 자동으로
회전하는 궤도 무기로 적을 타격한다. 점프·패링으로 적을 통제하고, 레벨업 성장과 궁극기·보스 보상으로
라운드가 오를수록 강해지는 적을 **따라잡고 앞지르는** 손맛을 준다.

**플랫폼:** 모바일 (터치 조작, 빅넘버 연출, 햅틱 라이브러리 도입 예정)

**승패:** 승리 조건 없음. 생명(=무기) 0 → 게임오버. 성과는 **점수(Points)** 와 도달 라운드로 남는다.

### 코어 루프

```
OnGameStart → 인트로 카메라 연출(3초) → StartRound
   ↓
라운드 진행: 30초 타이머 / 적 전멸 시 조기 클리어 / 5라운드마다 보스
   ↓
전투: 궤도 무기 자동 타격 + 차징점프/패링 + 궁극기
   ↓
처치 → 경험치·점수·궁극기 게이지 누적 → 레벨업 시 카드 택1 / 보스 처치 시 Legendary 보상
   ↓
피격 → 무기 1슬롯 비활성(딜 공백) → 생명 0이면 게임오버 → 결과 패널(점수/신기록)
```

---

## 2. 라운드 시스템 (`RoundManager`)

| 파라미터 | 값 | 설명 |
|---|---|---|
| `roundDuration` | 30초 | 라운드 제한시간. 초과 시 다음 라운드로 |
| `enemiesPerRound` | 5 | 일반 라운드 스폰 수 |
| 보스 주기 | 5라운드마다 | `roundCount % 5 == 0` |
| `bossHpMultiplier` | 7 | 보스 HP = 라운드 수 × 7 |
| `damageStepRounds` | 5 | 이 라운드 수마다 접촉 데미지 +1 |

- **일반 몹 HP** = 라운드 수 (선형). **보스 HP** = 라운드 수 × 7.
- **조기 클리어:** 타이머 종료 전 적 전멸 시 즉시 다음 라운드.
- **접촉 데미지(티어):** 박스몬이 플레이어에게 주는 데미지는 `EnemyContactDamage = (라운드-1)/5 + 1`.
  즉 1~5R=1, 6~10R=2, 11~15R=3 … 보스 티어가 오를수록 한 방이 아파진다.

---

## 3. 플레이어 & 전투

### 3.1 핵심 불변식 — "무기 = 생명 = DPS"

플레이어의 **생명 수 = 활성 궤도 무기 수**. 피격 시 생명이 줄면 무기도 함께 줄어 화력이 약해지고,
생명 0이면 게임오버. 회복하면 무기가 되살아난다. 생존과 공격이 하나로 묶인 구조.

### 3.2 궤도 무기 (`Orbit`) — 고정 슬롯 + 딜 공백

- 무기는 **고정 슬롯**으로 관리한다. 각도 간격은 활성 개수가 아니라 **최대 슬롯 수 기준**으로 고정.
- **피격 시** 무기를 파괴하지 않고 **비활성화** → 그 슬롯이 회전하는 "딜 공백"으로 남는다.
- **회복/무기 상한 증가**가 슬롯 채움·추가로 명확히 보인다.

| 파라미터 | 값 | 설명 |
|---|---|---|
| `radius` → `maxRadius` | 0.5 → 1.5 | 반경. 업그레이드는 상한을 향해 수렴(후반 효율 감소) |
| `rotationSpeed` | 120 deg/s | 기본 회전 속도 |
| 무기 크기 | 반경 비례 | 반경 커지면 무기도 커져 커버리지 유지 |

- 무기 콜라이더는 트리거. Boxmon과 접촉 시 `Player.EffectiveAtk`만큼 데미지.

### 3.3 조작

- **차징점프(`ChargeJump`):** 누르는 동안 충전(최대 2초), 떼면 점프. 낙하 시 `fallMultiplier`(2.5×)로
  빠르게 떨어져 타격감을 준다.
- **패링(`Parrying`):** 성공 시 적을 밀어내고 `Orbit.SpinBurst()`로 궤도를 360° 확 돌리는 "휘리릭" 연출.

### 3.4 공격력 레이어

- `Atk`(베이스, 업그레이드 소유) × `AtkMult`(궁극기 등 일시 배수) = `EffectiveAtk`(실제 타격값).
- 배수 레이어를 분리해, 궁극기·일시효과가 베이스 성장값을 오염시키지 않는다.

### 3.5 물리 (버그 수정 기록)

- 플레이어 Rigidbody는 **ContinuousDynamic** 충돌 판정. 궁극기 대시·강한 낙하로 빠르게 떨어질 때
  바닥(정적 MeshCollider)을 관통(터널링)하던 버그를 CCD로 해결.

---

## 4. 성장 시스템 — 레벨업 카드 (`LevelManager` + `UpgradeManager`)

### 4.1 흐름

박스몬 처치 → 고정 경험치 획득 → 임계치 도달 시 레벨업 → `timeScale=0` → **카드 3장 중 택1**.

| 파라미터 | 값 |
|---|---|
| `xpPerKill` | 1 |
| `baseXpToLevel` | 5 |
| `xpGrowth` | 1.15 (레벨당 필요 XP 배수) |
| `choiceCount` | 3 |

- 경험치는 float로 누적(경험치 배수 업그레이드의 소수 반영). HUD 표기: `998.0/999 xp`.
- **다중 레벨업:** 한 번에 여러 레벨이 오르면 카드가 큐에 쌓여 **선택할 때마다 다음 장이 이어진다**.

### 4.2 업그레이드 구조 (`UpgradeSO`, 데이터 주도)

`UpgradeSO : ScriptableObject` — id/이름/설명/아이콘/등급/스택여부/가중치 + `Apply()`/`CanOffer()`.
값 튜닝은 `.asset`에서, 새 효과 축만 서브클래스로 추가.

**일반 풀 (7종, `UpgradeManager.pool`):**

| 업그레이드 | 클래스 | 효과 축 |
|---|---|---|
| 공격력 증가 | `AtkUpgradeSO` | DPS (합/승) |
| 궤도 무기 +N | `WeaponCountUpgradeSO` | DPS + 생존 (상한↑ + 채움) |
| 회전 속도↑ | `OrbitSpeedUpgradeSO` | 타격 빈도 |
| 궤도 반경↑ | `OrbitRadiusUpgradeSO` | 사거리 (상한 수렴, 도달 시 미제시) |
| 즉시 회복 | `HealUpgradeSO` | 생존 |
| 궁극기 충전 효율↑ | `UltChargeUpgradeSO` | 궁극기 순환 |
| 궁극기 지속시간↑ | `UltDurationUpgradeSO` | 궁극기 강도 |

- 카드는 등급 배경 스프라이트 + 아이콘 + 이름/설명 + 등급 텍스트를 표시.
- 가중치 랜덤 추첨(한 제시 내 중복 없음), `CanOffer()`로 무의미한 후보(상한 도달 등) 제외.

### 4.3 등급(Grade) 체계

`UpgradeGrade` = **Common / Rare / Legendary**. 등급은 ① 추첨 가중치, ② 카드 배경 스프라이트,
③ 획득 경로를 구분한다.

- **Common / Rare** — 레벨업 카드 풀에서 등장. 가중치 랜덤 추첨.
- **Legendary** — 레벨업 풀에 없음. **보스 처치 시에만** 별도 풀에서 확정 지급 (7장 참고 → 6장 Legendary).

**레벨업 풀 가중치·확률 (현재값):**

| 등급 | 카드 | weight | 카드 1장당 확률 |
|---|---|---|---|
| Common | Attack Up / Faster Orbit / Wider Orbit / Repair / Ult Charge | 각 1.0 | 각 16.7% |
| Rare | Extra Weapon / Ult Duration | 각 0.5 | 각 8.3% |

- 총 가중치 6.0 기준 **등급 비율 = Common 83.3% : Rare 16.7%** (Rare는 개별 가중치가 Common의 절반).
- 단, 한 번에 **3장(중복 없이)** 제시하므로 "3장 중 Rare 최소 1장" 확률은 개별값보다 훨씬 높다(≈60%대).
- `CanOffer()`로 제외되는 카드가 생기면 그만큼 분모(총 가중치)가 줄어 나머지 확률이 올라간다.
- 가중치는 각 `.asset`의 `weight` 필드라 밸런싱 시 자유 조정.

---

## 5. 궁극기(광폭화) 시스템 (`UltimateManager`)

게이지를 채워 발동하는 일시 강화. 처치·시간으로 충전.

### 5.1 충전

| 파라미터 | 값 | 설명 |
|---|---|---|
| `chargePerKill` | 0.04 | 처치당 게이지(25킬 만충) |
| `chargePerSecond` | 0.01 | 시간 자동충전(100초 만충) — 처치 뜸한 보스전 대비 |

- 게이지 만충 시 UI로 "발동 가능" 강조. 버튼(UltGauge) 또는 에디터 아래 방향키로 발동.

### 5.2 2페이즈 발동

1. **대시 페이즈(1초):** 위로 대시(`ultDashSpeed=12`) + 궤도 절대 회전(초당 5바퀴=1800°/s) + SpinBurst.
2. **지속 페이즈(5초):** 궤도 회전속도 ×10 배수. 발동 내내 무적(`ForceInvulnerable`).

### 5.3 연출

- 발동 시 플레이어 몸체 색을 **#FF393B**(붉은색)로 + `VFX_Trail_Fire` 트레일 활성화. 종료 시 원복.
- 배수 레이어(`speedMult`/`AtkMult`)로 처리해 발동 중 레벨업이 베이스 스탯을 오염시키지 않음.

---

## 6. 보스 보상 — Legendary (`BossRewardManager`)

보스 처치 시 **매번**(5라운드마다) 선택지 없이 **Legendary 카드 1장 확정 지급**. 일반 3택1 풀과
완전히 분리된 전용 풀에서 가중치로 1개 추첨.

### 6.1 Legendary 풀 (6종)

| 이름 (id) | 효과 | 비고 |
|---|---|---|
| Power Surge (`atk_double`) | 공격력 ×2 | 보스마다 복리 누적 |
| Full Restore (`full_heal`) | 현재 상한까지 완전 회복 | |
| Weapon Cache (`weapon_count`) | 궤도 무기 +2 | |
| Guardian Shield (`shield`) | 다음 피격 1회 완전 무효 | 보유 중이면 미제시 |
| Overcharge Core (`ult_autocharge`) | 오토차지 효율 +20%p | 킬차지와 분리, 선형 가산(복리 아님) |
| Wisdom Rune (`xp_boost`) | 처치 경험치 +20% | |

- **보호막:** `Player.HasShield`가 씬의 Shield 오브젝트를 토글. 피격 1회를 무효화하고 소진.

### 6.2 보상 UI (`BossRewardUI`)

- 카드 1장(아이콘/이름/설명) + 타이머. **화면 아무 곳이나 터치** 또는 **5초 자동 닫힘**으로 확정.
- 뜨자마자 실수 스킵 방지를 위해 **최초 1초 입력 잠금**. `timeScale=0`이므로 unscaled 시간 사용.

### 6.3 레벨업과의 겹침 처리

보스 처치가 레벨업도 유발하면 **Legendary 보상 먼저 → 이후 레벨업 카드**. 한 번에 하나의 모달만
띄우고 그동안 `timeScale=0` 유지(모달 큐잉). 재시작 시 대기 구독 정리로 유령 트리거 방지.

---

## 7. 점수(Points) 시스템

- **획득:** 박스몬 처치 시 **현재 라운드 티어 데미지(`EnemyContactDamage`)만큼** 점수 누적.
  → 후반(높은 티어)일수록 처치당 점수가 크다.
- **인게임 HUD:** 999,999,999 이하 쉼표 표기(`13,289 Points`), 초과 시 축약(`2.0B Points`).
- **게임오버:** 항상 전체 쉼표 표기(`2,000,013,289 Points`).
- **신기록(New Record):** 라운드가 아닌 **점수 기준**(`PlayerStats.BestScore`).

---

## 8. UI / UX

| 화면 | 컴포넌트 | 내용 |
|---|---|---|
| HUD | `UiManager` | 라운드, 타이머(색·스케일 연출), 점수, 레벨/경험치, 궁극기 게이지 |
| 레벨업 카드 | `UpgradeCardUI` | 3택1 (등급배경/아이콘/이름/설명/등급) |
| Legendary | `BossRewardUI` | 카드 1장 + 타이머 |
| 퍼즈 | `PauseUI` | Resume/현재 Best·Time·Kill 표시, 인트로 중 진입 차단 |
| 게임오버 | `GameOverUI` | 도달 라운드/최고기록/생존시간/처치수/점수 + New Record 애니메이션 |

- **패턴:** 모든 오버레이 패널은 항상 활성인 `MainPlayCanvas`에 컴포넌트를 붙이고 `panel` 오브젝트만
  토글(비활성 GO에 붙이면 동작 안 하는 함정 회피).
- **스위치 토글(`SwitchToggle`, Util):** 재사용 UI 유틸. 손잡이 슬라이드 + 색/스프라이트 전환,
  `timeScale=0`에서도 동작(unscaled). (사운드/옵션 설정용, 로직 연결은 사운드 도입 시)

### New Record 애니메이션 (버그 수정 기록)

- 애니메이터 idle 상태에 클립이 없어, 블링크 중 패널이 꺼지면 시각 상태가 얼어붙어 다음 사이클에
  잔상이 남던 버그 → **신기록일 때만 NewRecord 오브젝트를 SetActive**로 직접 토글해 해결.

---

## 9. 데이터 영속 (`PlayerStats`)

PlayerPrefs를 감싸는 static 파사드(차후 리더보드/서버 확장 시 내부만 교체).

| 통계 | 키 | 설명 |
|---|---|---|
| `BestRound` | `boxal.stats.bestRound` | 최고 도달 라운드 |
| `TotalKills` | `boxal.stats.totalKills` | 누적 처치 수 |
| `BestScore` | `boxal.stats.bestScore` | 최고 점수(long, 문자열 저장) |

- 디버그 캔버스의 Reset 버튼 → `PlayerStats.ClearAll()`(래퍼 `DebugStatsReset` 경유).

---

## 10. 아키텍처 & 공통 유틸

- **싱글톤:** `GameManager`/`RoundManager`/`SpawnManager`/`LevelManager`/`UpgradeManager`/
  `UltimateManager`/`BossRewardManager`/`UiManager`/`Player` (제네릭 `Singleton<T>`).
- **오브젝트 풀:** `GameObjectPool` — Boxmon 재사용.
- **`NumberUtil`:** `FormatNumber`(축약 K/M/B), `FormatComma`(세자리 쉼표), `FormatMinSec`(mm:ss).
- **일시정지 규약:** 인트로/레벨업/보스보상/퍼즈 모두 `timeScale=0`으로 정지. 시간기반 연출은
  `unscaledDeltaTime` 사용.
- **파괴 연출:** Boxmon HP 0 → 파편화(MeshDemolisher) → Despawn 후 풀 반환.

---

## 11. 튜닝 노브 요약 (밸런싱 레버)

| 시스템 | 레버 | 현재값 |
|---|---|---|
| 라운드 | `bossHpMultiplier` / `damageStepRounds` | 7 / 5 |
| 성장 | `xpGrowth` | 1.15 |
| 궁극기 | `chargePerKill` / `chargePerSecond` / `speedMultiplier` | 0.04 / 0.01 / 10 |
| 플레이어 | `maxLife`(시작 무기) / `invulnerabilityPeriod` | 3 / 2초 |
| 궤도 | `rotationSpeed` / `maxRadius` | 120 / 1.5 |

---

## 12. 향후 확장 (Main Play 범위 밖)

- 메인 메뉴 / 홈 화면 (게임오버·퍼즈의 Home 버튼 연결 대기).
- 사운드 & 햅틱 (`SwitchToggle` 옵션 UI에 연결).
- 영구 메타 성장 (`PersistanceSingleton` + 재화 드랍).
- 보스 차별화 연출(크기/색/등장 알림), 패링 쿨다운 UI.
- Legendary 아이콘/등급 배경 등 아트 폴리시.
