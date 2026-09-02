# -*- coding: utf-8 -*-
"""
Boxal 밸런스 시뮬레이터 (2026-07-07)
====================================
실제 코드/씬 직렬화값 기준 (PlayScene.unity + Data/Upgrades/*.asset, 씬값 우선 규칙).

핵심 모델
- 전방 DPS: 무기가 궤도를 돌며 머리 위 스택의 최하단 박스에 바퀴당 1회 타격.
  hits/sec = 활성무기수 × (deg/s ÷ 360),  hit당 Atk.
- 궤도 반경: 커버리지 전용으로 취급(DPS 영향 없음 → 픽하면 사실상 손해).
- AFK 접촉 데미지: 적이 살아있는 동안 무적 3초 주기로 티어 데미지 피격.
  (박스는 플레이어 +20f 위에서 낙하 → 첫 접촉까지 ~2초)
- 라운드 타이머 30초. 만료 시 일반 라운드는 다음 웨이브가 "위에 추가 스폰"(잔존 유지),
  왕보스 라운드는 게임오버(하드 게이트).
- ★디스폰 지연 2초: currentEnemies는 처치 2초 후 감소 → 왕보스 실효 데드라인 = 28초.
- 궁극기: 게이지 만충 시 즉시 발동 가정. 1초 대시(1800°/s) + (5+보너스)초(회전×10), 내내 무적.

검증된 상수 출처
- PlayScene.unity: rotationSpeed=120, radius 0.5→max1.5, maxLife=3,
  xpGrowth=1.15, baseXpToLevel=5, bossHpMultiplier=7, kingBossInterval=20,
  kingBossHpMultiplier=30, damageStepRounds=5, roundDuration=30, enemiesPerRound=5,
  chargePerKill=0.04, chargePerSecond=0.01, dashDuration=1, durationSeconds=5.
- Upgrades: Atk+1 / Speed+10%p / Radius grow0.4 / Weapon+1(w0.5) / Heal+2 /
  UltCharge×1.2 / UltDur+1s(w0.5). Legendary: Atk×1.5, FullHeal, Weapon+2,
  Shield(1회), AutoCharge+0.2, Xp×1.2.
"""
import math
import random
import statistics as stats
from collections import Counter

# ---------- 상수 (씬/asset 실측) ----------
ROT_BASE = 120.0
MAX_LIFE0 = 3
ATK0 = 1
ROUND_DUR = 30.0
ENEMIES_PER_ROUND = 5
BOSS_MUL = 7.0
KING_INTERVAL = 20
KING_MUL = 25.0
DMG_STEP = 5
XP_BASE = 5
XP_GROWTH = 1.15
INVULN = 3.0
FALL_DELAY = 2.0        # 첫 낙하 접촉까지
SETTLE = 0.25           # 처치 후 다음 박스가 궤도에 내려앉는 시간
DESPAWN = 2.0           # 처치 → currentEnemies 감소 지연 (라운드 클리어 판정 지연)
CHARGE_KILL = 0.04
CHARGE_SEC = 0.01
ULT_DASH = 1.0
ULT_DASH_DEG = 1800.0   # 5 rev/s
ULT_DUR = 5.0
ULT_SPEED_MULT = 10.0
DT = 0.1
MAX_ROUND = 60

# 레벨업 풀: (이름, weight)
LEVEL_POOL = [
    ("atk", 1.0), ("speed", 1.0), ("radius", 1.0), ("heal", 1.0),
    ("ultcharge", 1.0), ("weapon", 0.5), ("ultdur", 0.5),
]
LEGENDARY_POOL = ["l_atk", "l_fullheal", "l_weapon", "l_shield", "l_auto", "l_xp"]


class Run:
    def __init__(self, policy="random", profile="afk", seed=0,
                 ult_hold=False, king_mul=KING_MUL, afk_from=None,
                 xp_mode="flat", xp_growth=XP_GROWTH, mob_scale=False):
        # --- 성장 곡선 레버 (R15 이후 카드가 안 뜨는 문제 대응) ---
        # xp_mode : "flat"=처치당 XP 1 (현재) / "tier"=처치당 XP를 라운드 티어에 비례
        # xp_growth: 레벨당 필요 XP 증가율 (현재 1.15)
        # mob_scale: True면 일반 라운드 몹 수를 5 + (r-1)//5 로 증가
        self.xp_mode = xp_mode
        self.xp_growth = xp_growth
        self.mob_scale = mob_scale
        self.rng = random.Random(seed)
        self.policy = policy          # random | greedy
        self.profile = profile        # afk | dodge (접촉 완전 회피)
        self.ult_hold = ult_hold      # True면 궁극기를 보스/왕보스 라운드에만 사용
        self.king_mul = king_mul
        self.afk_from = afk_from      # 이 라운드부터 회피 포기(접촉뎀 노출). None=profile 그대로
        # 성장 상태
        self.atk = ATK0
        self.rot = ROT_BASE
        self.max_life = MAX_LIFE0
        self.lives = MAX_LIFE0
        self.radius = 0.5
        self.kill_eff = 1.0
        self.auto_eff = 1.0
        self.ult_bonus = 0.0
        self.xp_mult = 1.0
        self.shield = False
        # 레벨
        self.level = 1
        self.xp = 0.0
        self.xp_next = XP_BASE
        # 궁극기
        self.charge = 0.0
        self.ult_t = -1.0   # 남은 궁극기 시간(음수=비활성)
        # 전투 상태
        self.round = 0
        self.queue = []          # (hp, is_boss, is_king)
        self.round_timer = 0.0
        self.king_round = False
        self.contact_t = FALL_DELAY
        self.hit_acc = 0.0
        self.settle_t = 0.0
        self.clear_t = -1.0      # 마지막 처치 후 디스폰 대기(>=0이면 카운트다운)
        self.dead = False
        self.death_cause = None
        self.picks = Counter()
        self.clear_times = {}    # round -> 클리어까지 걸린 시간(초)
        self.contact_hits = 0    # 받은 접촉 피격 횟수(AFK)
        self.round_time = {}     # round -> 그 라운드에 흐른 총 시간(초)
        self.round_ult_time = {}  # round -> 그 라운드 중 궁극기(=무적) 시간(초)
        # --- "할 게 있나" 계측: 라운드별 플레이어 의사결정/조작 횟수 ---
        self.round_levelups = {}   # round -> 레벨업(카드 택1) 횟수
        self.round_ult_casts = {}  # round -> 궁극기 발동 횟수
        self.round_jumps = {}      # round -> 피격 회피에 필요한 점프 횟수(=접촉 창)
        self.round_timeouts = {}   # round -> 타이머 만료로 넘어갔는지(1/0)
        self.round_dps = {}        # round -> 그 라운드 진입 시점의 DPS 스냅샷

    # ---------- 성장 ----------
    def apply(self, up):
        self.picks[up] += 1
        if up == "atk":
            self.atk = (self.atk + 1) * 1.0   # 실수 유지(게임 Player.Atk=double, 데미지는 캐리 누적)
        elif up == "speed":
            self.rot += ROT_BASE * 0.1
        elif up == "radius":
            self.radius += (1.5 - self.radius) * 0.4
        elif up == "heal":
            self.lives = min(self.lives + 2, self.max_life)
        elif up == "ultcharge":
            self.kill_eff *= 1.2
        elif up == "weapon":
            self.max_life += 1
            self.lives = min(self.lives + 1, self.max_life)
        elif up == "ultdur":
            self.ult_bonus += 1.0
        elif up == "l_atk":
            self.atk = self.atk * 1.5   # 실수 유지(버림 없이 ×1.5 정확 반영)
        elif up == "l_fullheal":
            self.lives = self.max_life
        elif up == "l_weapon":
            self.max_life += 2
            self.lives = min(self.lives + 2, self.max_life)
        elif up == "l_shield":
            self.shield = True
        elif up == "l_auto":
            self.auto_eff += 0.2
        elif up == "l_xp":
            self.xp_mult *= 1.2

    def offer_level_cards(self):
        pool = [(n, w) for n, w in LEVEL_POOL
                if not (n == "radius" and self.radius >= 1.49)]
        cards = []
        cand = pool[:]
        for _ in range(min(3, len(cand))):
            total = sum(w for _, w in cand)
            r = self.rng.random() * total
            for i, (n, w) in enumerate(cand):
                r -= w
                if r <= 0:
                    cards.append(n)
                    cand.pop(i)
                    break
        return cards

    def pick_card(self, cards):
        if self.policy == "greedy":
            prio = ["atk", "weapon", "speed", "ultcharge", "heal", "ultdur", "radius"]
            # 목숨이 위험하면 힐 우선
            if self.lives <= max(1, self.max_life // 3) and "heal" in cards:
                return "heal"
            for p in prio:
                if p in cards:
                    return p
            return cards[0]
        return self.rng.choice(cards)

    def legendary_reward(self, is_king):
        pool = [u for u in LEGENDARY_POOL
                if not (u == "l_fullheal" and is_king)
                and not (u == "l_shield" and self.shield)]
        self.apply(self.rng.choice(pool))

    # ---------- 전투 ----------
    def gain_xp(self):
        base = self.tier_dmg() if self.xp_mode == "tier" else 1.0
        self.xp += base * self.xp_mult
        while self.xp >= self.xp_next:
            self.xp -= self.xp_next
            self.level += 1
            self.round_levelups[self.round] = self.round_levelups.get(self.round, 0) + 1
            self.xp_next = math.ceil(XP_BASE * (self.xp_growth ** (self.level - 1)))
            cards = self.offer_level_cards()
            if cards:
                self.apply(self.pick_card(cards))

    def on_kill(self, is_boss, is_king):
        self.charge = min(1.0, self.charge + CHARGE_KILL * self.kill_eff)
        if is_king:
            self.lives = self.max_life          # 왕보스 풀힐
        if is_boss:
            self.legendary_reward(is_king)      # Legendary 확정 1장
        self.gain_xp()
        self.settle_t = SETTLE

    def tier_dmg(self):
        return max(1, (self.round - 1) // DMG_STEP + 1)

    def start_round(self):
        self.round += 1
        r = self.round
        self.king_round = (KING_INTERVAL > 0 and r % KING_INTERVAL == 0)
        if self.king_round:
            self.queue.append([int(r * self.king_mul), True, True])
        elif r % 5 == 0:
            self.queue.append([int(r * BOSS_MUL), True, False])
        else:
            count = ENEMIES_PER_ROUND + (r - 1) // 5 if self.mob_scale else ENEMIES_PER_ROUND
            for _ in range(count):
                self.queue.append([r, False, False])
        # 라운드 진입 시점 DPS 스냅샷(최종 스탯을 쓰면 초반이 과대평가된다)
        self.round_dps[r] = self.lives * (self.rot / 360.0) * self.atk
        self.round_timer = ROUND_DUR
        self.settle_t = FALL_DELAY   # 박스가 궤도까지 낙하하는 시간(타격 불가)
        if self.contact_t <= 0:
            self.contact_t = FALL_DELAY

    def deg_per_sec(self):
        if self.ult_t >= 0:
            total = ULT_DASH + ULT_DUR + self.ult_bonus
            elapsed = total - self.ult_t
            return ULT_DASH_DEG if elapsed < ULT_DASH else self.rot * ULT_SPEED_MULT
        return self.rot

    def step(self):
        dt = DT
        self.round_time[self.round] = self.round_time.get(self.round, 0.0) + dt
        if self.ult_t >= 0:
            self.round_ult_time[self.round] = self.round_ult_time.get(self.round, 0.0) + dt
        self.round_timer -= dt
        # 오토차지
        if self.ult_t < 0 and self.charge < 1.0:
            self.charge = min(1.0, self.charge + CHARGE_SEC * self.auto_eff * dt)
        # 궁극기 발동/진행 (hold 정책이면 보스/왕보스 상대일 때만 사용)
        if self.ult_t < 0 and self.charge >= 1.0 and self.queue:
            if not self.ult_hold or self.queue[0][1]:
                self.ult_t = ULT_DASH + ULT_DUR + self.ult_bonus
                self.charge = 0.0
                self.round_ult_casts[self.round] = self.round_ult_casts.get(self.round, 0) + 1
        if self.ult_t >= 0:
            self.ult_t -= dt
            if self.ult_t < 0:
                self.ult_t = -1.0
        # 디스폰 대기(라운드 클리어 판정)
        # (2026-07-07 수정 반영: 왕보스를 28~30초에 잡아도 aliveBoxmons 기준으로
        #  생존 판정하므로 디스폰 대기 중 타이머 만료는 더 이상 게임오버가 아님)
        if self.clear_t >= 0:
            self.clear_t -= dt
            if self.clear_t < 0:
                self.start_round()
            return
        # 타격
        if self.settle_t > 0:
            self.settle_t -= dt
        elif self.queue:
            self.hit_acc += self.lives * (self.deg_per_sec() / 360.0) * dt
            hits = int(self.hit_acc)
            if hits > 0:
                self.hit_acc -= hits
                dmg = hits * self.atk
                while dmg > 0 and self.queue:
                    e = self.queue[0]
                    if e[0] <= dmg:
                        dmg -= e[0]
                        self.queue.pop(0)
                        self.on_kill(e[1], e[2])
                        if self.settle_t > 0:
                            break
                    else:
                        e[0] -= dmg
                        dmg = 0
        # 접촉 창(=박스가 머리 위에 닿는 순간). 궁극기 중엔 무적이라 창이 열리지 않는다.
        # 창이 열릴 때마다 플레이어는 점프로 피해야 한다 → 열린 횟수 = 요구 조작량.
        # 실제 피해는 회피를 포기한 경우(afk profile 또는 afk_from 이후)에만 적용한다.
        exposed = self.profile == "afk" or (self.afk_from is not None and self.round >= self.afk_from)
        if self.queue and self.ult_t < 0:
            self.contact_t -= dt
            if self.contact_t <= 0:
                self.round_jumps[self.round] = self.round_jumps.get(self.round, 0) + 1
                self.contact_t = INVULN
                if exposed:
                    d = self.tier_dmg()
                    self.contact_hits += 1
                    if self.shield:
                        self.shield = False
                    else:
                        self.lives -= d
                        if self.lives <= 0:
                            self.dead = True
                            self.death_cause = "contact"
                            return
        # 라운드 종료 판정
        if not self.queue and self.clear_t < 0:
            self.clear_times[self.round] = ROUND_DUR - self.round_timer
            self.clear_t = DESPAWN     # 디스폰 2초 후 다음 라운드
        elif self.round_timer <= 0:
            if self.king_round:
                # ★디스폰 지연 포함: 여기 도달 = 30초 내 클리어 판정 실패
                self.dead = True
                self.death_cause = "kinggate"
            else:
                self.round_timeouts[self.round] = 1
                self.start_round()     # 잔존 위에 다음 웨이브 추가

    def simulate(self):
        self.start_round()
        guard = int(3600 / DT)  # 런당 최대 1시간
        while not self.dead and self.round <= MAX_ROUND and guard > 0:
            self.step()
            guard -= 1
        return self


def run_batch(policy, profile, n=300, ult_hold=False, king_mul=KING_MUL, tag=""):
    runs = [Run(policy, profile, seed=i, ult_hold=ult_hold,
                king_mul=king_mul).simulate() for i in range(n)]
    deaths = [r.round for r in runs if r.dead]
    causes = Counter(r.death_cause for r in runs if r.dead)
    survived = sum(1 for r in runs if not r.dead)
    king_reached = {k: 0 for k in (20, 40, 60)}
    king_passed = {k: 0 for k in (20, 40, 60)}
    for r in runs:
        for k in (20, 40, 60):
            if r.round > k or (r.round == k and not r.dead):
                king_reached[k] += 1
                king_passed[k] += 1
            elif r.round == k and r.dead:
                king_reached[k] += 1
    print(f"\n=== policy={policy}, profile={profile}"
          f"{', ult=보스전용' if ult_hold else ''}"
          f"{f', kingMul={king_mul:g}' if king_mul != KING_MUL else ''}"
          f"{' ' + tag if tag else ''}  (n={n}) ===")
    if deaths:
        print(f"사망 라운드: 중앙값 R{int(stats.median(deaths))}, "
              f"p10 R{int(sorted(deaths)[len(deaths)//10])}, "
              f"p90 R{int(sorted(deaths)[len(deaths)*9//10])}  | 원인: {dict(causes)}")
    print(f"R{MAX_ROUND} 생존(끝까지): {survived}/{n} ({100*survived/n:.0f}%)")
    for k in (20, 40, 60):
        if king_reached[k]:
            print(f"왕보스 R{k}: 도달 {king_reached[k]}/{n}, "
                  f"도달자 중 통과 {100*king_passed[k]/king_reached[k]:.0f}%")
        else:
            print(f"왕보스 R{k}: 도달자 없음")
    # 라운드별 클리어 시간 중앙값 (30초 대비 여유 확인)
    marks = [1, 3, 5, 10, 15, 19, 20, 25, 30, 40, 50, 60]
    parts = []
    for m in marks:
        ts = [r.clear_times[m] for r in runs if m in r.clear_times]
        if ts:
            parts.append(f"R{m}:{stats.median(ts):.1f}s")
    print("클리어시간(중앙값): " + "  ".join(parts))
    # 대표 런의 성장 곡선 (seed 0)
    r0 = runs[0]
    print(f"[seed0] 도달 R{r0.round}, Atk={r0.atk}, rot={r0.rot:.0f}, "
          f"무기 {r0.lives}/{r0.max_life}, Lv{r0.level}, 접촉피격 {r0.contact_hits}회, "
          f"픽: {dict(r0.picks)}")
    return runs


def dps_table():
    """업그레이드 없는 '이론 벽' — 필요 DPS vs 라운드 (참고용)."""
    print("\n=== 라운드별 총 HP / 30초 클리어 필요 DPS ===")
    print(f"{'R':>3} {'유형':>6} {'총HP':>7} {'필요DPS':>8}")
    for r in [1, 5, 10, 15, 19, 20, 25, 30, 39, 40, 50, 59, 60]:
        if KING_INTERVAL and r % KING_INTERVAL == 0:
            hp, kind, limit = int(r * KING_MUL), "왕보스", ROUND_DUR - DESPAWN
        elif r % 5 == 0:
            hp, kind, limit = int(r * BOSS_MUL), "보스", ROUND_DUR
        else:
            hp, kind, limit = r * ENEMIES_PER_ROUND, "일반", ROUND_DUR
        print(f"{r:>3} {kind:>6} {hp:>7} {hp/limit:>8.1f}")


def afk_margin_sweep(policy="greedy", ult_hold=True, n=300):
    """몇 라운드부터 액티브 회피를 포기해도(접촉뎀 노출) 버틸 수 있는지 탐색.
    R1~afk_from-1은 정상적으로 회피(dodge)하다가, afk_from 라운드부터는 회피를
    그만둔다고 가정 — 실제 플레이어가 "이 시점부터는 방치해도 되네" 하고 느끼는
    지점을 찾기 위함. 기준(끝까지 회피)의 R60 생존율 대비 급락하지 않는 최소
    afk_from을 찾으면 그게 체감 방치 임계 라운드."""
    print(f"\n=== 방치(회피 포기) 임계 라운드 탐색: policy={policy}, ult_hold={ult_hold} (n={n}) ===")
    baseline = [Run(policy, "dodge", seed=i, ult_hold=ult_hold).simulate() for i in range(n)]
    base_survive = sum(1 for r in baseline if not r.dead) / n
    print(f"[기준: 끝까지 회피] R60 생존 {base_survive*100:.0f}%")
    for afk_from in (5, 10, 15, 20, 25, 30, 35, 40, 45, 50):
        runs = [Run(policy, "dodge", seed=i, ult_hold=ult_hold, afk_from=afk_from).simulate()
                for i in range(n)]
        survive = sum(1 for r in runs if not r.dead) / n
        causes = Counter(r.death_cause for r in runs if r.dead)
        deaths = [r.round for r in runs if r.dead]
        med = f"R{int(stats.median(deaths))}" if deaths else "-"
        contact_deaths = sum(1 for r in runs if r.dead and r.death_cause == "contact"
                              and r.round >= afk_from)
        print(f"afk_from=R{afk_from:>2}: R60 생존 {survive*100:5.1f}% "
              f"(기준의 {survive/base_survive*100:5.1f}%)  "
              f"사망중앙값={med}  원인={dict(causes)}  "
              f"접촉사 {contact_deaths}")


def ult_uptime_curve(policy="greedy", ult_hold=False, n=300):
    """라운드별 궁극기 가동률(=완전 무적 시간 비율).
    궁극기는 지속 내내 ForceInvulnerable이라, 가동률이 100%에 붙는 순간부터는
    접촉 데미지가 원천 차단된다 = 점프(유일한 조작)를 할 이유가 사라진다.
    '방치형이 되는 시점'의 가장 유력한 후보라 라운드별로 추적한다."""
    label = "궁 아낌(보스전용)" if ult_hold else "궁 즉시사용"
    print(f"\n=== 궁극기 가동률(무적 비율) 곡선: policy={policy}, {label} (n={n}) ===")
    runs = [Run(policy, "dodge", seed=i, ult_hold=ult_hold).simulate() for i in range(n)]
    print(f"{'R':>3} {'가동률':>7} {'표본':>5}")
    for r in range(1, MAX_ROUND + 1):
        vals = [runs_r.round_ult_time.get(r, 0.0) / runs_r.round_time[r]
                for runs_r in runs if runs_r.round_time.get(r, 0) > 0]
        if len(vals) < 20:      # 표본 적으면 노이즈라 생략
            continue
        if r <= 25 or r % 5 == 0:
            print(f"{r:>3} {stats.median(vals)*100:6.1f}% {len(vals):>5}")


def engagement_curve(policy="greedy", n=300):
    """'할 게 없어지는 시점' 계측.
    이 게임에서 플레이어가 하는 일은 셋뿐이다 — 점프(유일한 실시간 조작),
    레벨업 카드 택1, 궁극기 발동. 라운드별로 이 셋의 발생 빈도를 세면
    조작/판단이 언제 희박해지는지 보인다.
      - 점프: 접촉 창이 열린 횟수(궁극기 무적 중엔 안 열림)
      - 카드: 레벨업 횟수 (성장 결정)
      - 궁: 발동 횟수
      - 클리어율: 제한시간 내 전멸시킨 비율 (낮으면 몹이 쌓여 방치 진행)"""
    print(f"\n=== 라운드별 '할 일' 밀도: policy={policy} (n={n}) ===")
    runs = [Run(policy, "dodge", seed=i).simulate() for i in range(n)]
    print(f"{'R':>3} {'점프':>6} {'카드':>6} {'궁':>6} {'클리어율':>8} {'라운드길이':>9}")
    for r in range(1, MAX_ROUND + 1):
        alive = [x for x in runs if x.round_time.get(r, 0) > 0]
        if len(alive) < 20:
            continue
        if not (r <= 12 or r % 5 == 0):
            continue
        jumps = stats.mean(x.round_jumps.get(r, 0) for x in alive)
        cards = stats.mean(x.round_levelups.get(r, 0) for x in alive)
        ults = stats.mean(x.round_ult_casts.get(r, 0) for x in alive)
        cleared = 1 - stats.mean(x.round_timeouts.get(r, 0) for x in alive)
        dur = stats.mean(x.round_time[r] for x in alive)
        print(f"{r:>3} {jumps:>6.1f} {cards:>6.2f} {ults:>6.2f} "
              f"{cleared*100:>7.0f}% {dur:>8.1f}s")


def lever_compare(n=300, policy="greedy"):
    """성장 곡선 레버 3종 비교.
    문제: 처치 수(XP 공급)는 라운드당 ~4.2로 상수인데 필요 XP는 1.15^L로 지수 증가
    → R15 이후 레벨업(카드 택1)이 사실상 멈춘다.
    레버 A: 처치당 XP를 라운드 티어에 비례 (공급을 수요와 같은 형태로)
    레버 B: xpGrowth 인하 (수요 기울기를 낮춤)
    레버 C: 라운드가 오를수록 몹 수 증가 (공급 자체를 늘림)
    카드 빈도가 펴지는지와 함께 밸런스 부작용(클리어 시간·왕보스 통과율)도 본다."""
    configs = [
        ("현재",            dict()),
        ("A: XP=티어비례",   dict(xp_mode="tier")),
        ("B: xpGrowth 1.10", dict(xp_growth=1.10)),
        ("B: xpGrowth 1.08", dict(xp_growth=1.08)),
        ("C: 몹수 증가",     dict(mob_scale=True)),
    ]
    # 레벨업은 라운드당 0/1로 뚝뚝 끊겨 단일 라운드 값은 노이즈가 크다.
    # 구간(밴드)으로 묶어 "카드 1장당 몇 라운드"로 본다 — 낮을수록 결정이 자주 생긴다.
    bands = [(1, 10), (11, 20), (21, 30), (31, 40), (41, 60)]
    print(f"\n=== 성장 레버 비교: 카드 1장당 몇 라운드 (낮을수록 좋음) — policy={policy}, n={n} ===")
    print(f"{'설정':>16} " + " ".join(f"{f'R{a}-{b}':>9}" for a, b in bands))
    results = {}
    for label, kw in configs:
        runs = [Run(policy, "dodge", seed=i, **kw).simulate() for i in range(n)]
        results[label] = runs
        cells = []
        for a, b in bands:
            # 그 구간을 실제로 플레이한 라운드 수와 그 동안 뜬 카드 수를 전부 합산
            rounds = sum(1 for x in runs for r in range(a, b + 1) if x.round_time.get(r, 0) > 0)
            cards = sum(x.round_levelups.get(r, 0) for x in runs for r in range(a, b + 1))
            cells.append(f"{rounds/cards:>9.1f}" if cards else f"{'없음':>9}")
        print(f"{label:>16} " + " ".join(cells))

    print(f"\n--- 부작용 점검 ---")
    print(f"{'설정':>16} {'R20통과':>8} {'R60생존':>8} {'총카드':>7} "
          f"{'R19길이':>8} {'R59길이':>8}")
    for label, _ in configs:
        runs = results[label]
        reach20 = [x for x in runs if x.round > 20 or (x.round == 20 and not x.dead)]
        survive = sum(1 for x in runs if not x.dead)
        # R60까지 완주한 런 기준 총 획득 카드 수(성장 총량) — 인플레 확인용
        full = [x for x in runs if not x.dead]
        cards = f"{stats.median(sum(x.round_levelups.values()) for x in full):.0f}" \
            if len(full) >= 20 else "-"
        d19 = [x.round_time[19] for x in runs if x.round_time.get(19, 0) > 0]
        d59 = [x.round_time[59] for x in runs if x.round_time.get(59, 0) > 0]
        s19 = f"{stats.median(d19):.1f}s" if len(d19) >= 20 else "-"
        s59 = f"{stats.median(d59):.1f}s" if len(d59) >= 20 else "-"
        print(f"{label:>16} {100*len(reach20)/n:>7.0f}% {100*survive/n:>7.0f}% "
              f"{cards:>7} {s19:>8} {s59:>8}")


def power_margin_curve(policy="greedy", n=300):
    """'방치 지점은 필연인가' 검증.
    플레이어 DPS = 무기수 × (회전/360) × Atk — 업그레이드가 세 항을 각각 올려 곱해진다.
    적 위협 = 라운드에 선형(HP=r, 접촉뎀=(r-1)/5+1).
    여유배수 = 플레이어 DPS / 그 라운드 클리어에 필요한 DPS.
    이 값이 단조 증가하면 위협이 0으로 수렴 = 방치 지점은 구조적 필연.
    또 박스 1마리당 TTK도 같이 본다 — 실제 접촉뎀은 박스가 머리 위 1.1유닛에
    들어오기 전에 죽으면 0이므로, TTK가 낙하시간보다 짧아지는 순간이 진짜 방치 임계."""
    print(f"\n=== 파워 여유배수 곡선 (방치 필연성 검증) — policy={policy}, n={n} ===")
    runs = [Run(policy, "dodge", seed=i).simulate() for i in range(n)]
    print(f"{'R':>3} {'플레이어DPS':>11} {'필요DPS':>8} {'여유배수':>8} {'박스1마리TTK':>12}")
    for r in [1, 3, 5, 10, 15, 19, 25, 29, 35, 39, 45, 49, 55, 59]:
        alive = [x for x in runs if x.round_time.get(r, 0) > 0]
        if len(alive) < 20:
            continue
        dps = stats.median(x.round_dps[r] for x in alive)
        if KING_INTERVAL and r % KING_INTERVAL == 0:
            hp, limit = r * KING_MUL, ROUND_DUR - DESPAWN
        elif r % 5 == 0:
            hp, limit = r * BOSS_MUL, ROUND_DUR
        else:
            hp, limit = r * ENEMIES_PER_ROUND, ROUND_DUR
        need = hp / limit
        ttk = r / dps if dps > 0 else float("inf")   # 일반 몹 1마리(HP=r) 처치 시간
        print(f"{r:>3} {dps:>11.1f} {need:>8.1f} {dps/need:>7.1f}x {ttk:>11.2f}s")


if __name__ == "__main__":
    dps_table()
    for prof in ("afk", "dodge"):
        for pol in ("random", "greedy"):
            run_batch(pol, prof, n=300)
    # 궁극기를 보스전에 아껴 쓰는 숙련 플레이
    run_batch("greedy", "dodge", n=300, ult_hold=True)
    run_batch("random", "dodge", n=300, ult_hold=True)
    # 왕보스 HP 배율 스윕 (적정값 탐색)
    for km in (25, 20, 15):
        run_batch("greedy", "dodge", n=300, ult_hold=True, king_mul=km)
    # 궁극기 가동률(무적 비율) 곡선 — 방치형 전환 시점 후보 (결론: 10~20%로 평평, 무관)
    ult_uptime_curve(policy="greedy", ult_hold=False, n=300)
    # '할 게 없어지는 시점' — 라운드별 조작/판단 밀도 (결론: R15부터 카드 0)
    engagement_curve(policy="greedy", n=300)
    # 성장 곡선 레버 3종 비교
    lever_compare(n=300, policy="greedy")
    # 방치 지점의 구조적 필연성 — 여유배수가 단조 증가하는지
    power_margin_curve(policy="greedy", n=300)
    power_margin_curve(policy="random", n=300)
    # 아래는 결론이 난 탐색이라 기본 실행에서 제외(필요하면 주석 해제).
    # 회피 포기 시점 스윕 → 어느 라운드든 100% 접촉사(모델 한계, 실제와 불일치)
    # afk_margin_sweep(policy="greedy", ult_hold=True, n=300)
    # afk_margin_sweep(policy="random", ult_hold=True, n=300)
