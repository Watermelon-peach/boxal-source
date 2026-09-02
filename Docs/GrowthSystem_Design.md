# Boxal — 게임 개요 & 성장 시스템 기획서

> 작성 기준: 2026-06-30 / 코드 베이스 커밋 `f4285ee`

---

## 1. 게임 개요 (1페이지)

**장르:** 라운드 기반 생존 아레나 (모바일 캐주얼)

**코어 판타지:** 박스 적(Boxmon)이 위로 차곡차곡 쌓이고, 플레이어는 궤도 무기로 자동 타격하며
점프·패링으로 적을 통제한다. 라운드가 오를수록 강해지는 적을, **성장으로 따라잡고 앞지르는** 손맛.

**플랫폼:** 모바일 (NiceVibrations 햅틱, 빅넘버 연출, 단순 터치 조작)

**코어 루프:**
1. `GameManager.OnGameStart` → `Player.PlayerInitSettings` → `RoundManager.StartRound`
2. 라운드: 30초 타이머, 적 전멸 시 조기 클리어, 5라운드마다 보스
3. `SpawnManager`: Boxmon 풀에서 Y축 위로 스폰 (HP = 라운드 수)
4. 전투: 궤도 무기(`Orbit`) 접촉 시 `Player.Atk` 대미지, 차징점프/패링으로 통제
5. 피격: 무기 = 생명 = DPS. 맞으면 무기 1개 소실 + 무적시간. 생명 0 → 게임오버

**승패:** 패배 = 생명 0. 승리 조건 없음(엔드리스 하이스코어 = 도달 라운드).

---

## 2. 핵심 문제 정의 (성장이 필요한 이유)

현재 밸런스 곡선이 **플레이어에게 일방적으로 불리**하다:

| 항목 | 현재 상태 | 문제 |
|---|---|---|
| 적 HP | 라운드마다 선형 증가 (`Spawn(roundCount)`) | 계속 강해짐 |
| 플레이어 공격력 | `Atk = 1` 고정 | 성장 없음 |
| 궤도 무기 수 | 생명에 종속, 최대 6 | 상한 고정 |
| 회복 | 라운드 보상 외 없음 | 손실 누적 |

→ **성장 시스템의 1순위 목표:** 플레이어 파워가 라운드별 적 HP 증가를 따라잡고 앞지르게 만들어
"한 판 더" 루프를 성립시킨다.

---

## 3. 성장 시스템 설계 (확정 방향)

**결정 사항:**
- 지속 범위: **런 한정 로그라이트** (게임 한 판 안에서만 누적, 사망 시 초기화 — 세이브 불필요)
- 획득·선택: **라운드 클리어 시 3장 중 택1**
- 플랫폼: **모바일**

### 3.1 흐름

```
RoundTimer 종료 / 적 전멸
        ↓
Time.timeScale = 0  (CameraWork의 timeScale 제어 방식 재사용)
        ↓
UpgradeManager.OfferChoices()  → 카드 3장 추첨 + 선택 UI 표시
        ↓
플레이어 선택 → 효과 적용 (Player/Orbit/Parrying/ChargeJump 프로퍼티 가산·승산)
        ↓
Time.timeScale = 1 → RoundManager.StartRound()
```

> 구현 포인트: 지금 `RoundTimer`/`StartRound`가 직결돼 있으므로, 그 사이에 "선택 대기" 단계를 삽입한다.

### 3.2 업그레이드 풀 (전부 기존 코드에 매핑)

| 업그레이드 | 연결 지점 | 효과 축 | 비고 |
|---|---|---|---|
| 공격력 증가 | `Player.Atk` | DPS (핵심) | 곱/합 — 밸런스 시 결정 |
| 궤도 무기 +1 | `Orbit` 무기 수, `Player.maxLife` | DPS + 생존 | 현재 상한 6 → 성장으로 확장 |
| 회전 속도↑ | `Orbit.rotationSpeed` | 타격 빈도/커버리지 | |
| 궤도 반경 변경 | `Orbit.radius` | 사거리 | |
| 패링 쿨다운↓ | `Parrying.parryCoolDown` | 군중제어 | |
| 패링 파워↑ | `Parrying.power` | 군중제어 | |
| 차징점프 강화 | `ChargeJump.maxForce` 등 | 기동/도달 | |
| 즉시 회복 / 무적시간↑ | `Player.AddLife`, `invulnerabilityPeriod` | 생존 | 손실 누적 완화 |

> 등급(일반/희귀/전설) + 추첨 가중치로 변주 가능. 스택 가능 여부는 업그레이드별 지정.

### 3.3 아키텍처 (기존 패턴 유지)

- **`UpgradeSO : ScriptableObject`** — id, 이름, 설명, 아이콘, 등급, 스택 가능 여부, 적용 로직.
  데이터 주도라 밸런싱/추가가 쉬움.
- **`UpgradeManager : Singleton<UpgradeManager>`** — 풀 보관, 3장 추첨(중복/조건 필터), 선택분 적용.
- **`RoundManager` 훅** — 라운드 종료 시 `UpgradeManager.OfferChoices()` 호출, 선택 콜백에서 `StartRound()`.
- **업그레이드 선택 UI** — `UiManager`에 카드 3장 패널 추가 (모바일 터치 대응).

---

## 4. 향후 확장 (이번 범위 밖)

- **영구 메타 성장**: 미사용 중인 `PersistanceSingleton` + Boxmon 격파 시 재화 드랍을 추가하면
  런 한정 위에 영구 강화 레이어를 얹을 수 있음 (모바일 리텐션에 유리).
- 보스 차별화 연출 (크기/색/등장 알림 — `RoundManager.cs:67` TODO).
- 패링 쿨다운 UI 연결 (`Parrying.cs:42` TODO).

---

## 5. 구현 순서 제안

1. `UpgradeSO` 스키마 + 샘플 업그레이드 3~5종 작성
2. `UpgradeManager` (추첨 + 적용) 구현
3. `RoundManager` ↔ `UpgradeManager` 흐름 연결 (timeScale 일시정지)
4. 카드 선택 UI (`UiManager` 확장)
5. 밸런스 1차 패스 (적 HP 곡선 vs 성장 곡선 맞추기)
