# Boxal

*[한국어로 보기 →](README.ko.md)*

**▶ Play on itch.io: https://watermelonpeach.itch.io/boxal** (screenshots + free APK download)

Solo-developed mobile action-casual roguelite — gameplay, meta-progression, UI, and tooling all built by one person.
Unity 6000.4 (URP) · C# · Android.

## Where to start reading

| File | What to look at |
|---|---|
| [`Game/Player/Orbit.cs`](Scripts/Game/Player/Orbit.cs) | Orbit weapons as fixed slots (deactivated on hit, never destroyed), converging radius growth (`GrowRadius`), spin burst |
| [`Game/Managers/UpgradeManager.cs`](Scripts/Game/Managers/UpgradeManager.cs) | Level-up 3-card draw — weighted, each picked card is removed before the next draw |
| [`Game/Growth/UpgradeSO.cs`](Scripts/Game/Growth/UpgradeSO.cs) | ScriptableObject upgrades. `CanOffer()` is non-virtual and only `CanOfferCore()` is overridable, so no subclass can skip the unlock check |
| [`Game/Leaderboard/LeaderboardManager.cs`](Scripts/Game/Leaderboard/LeaderboardManager.cs) | Why UGS (Unity Gaming Services) init is delayed one frame (ordering issue with the package's own reset), offline score sync |
| [`Game/TransformSnapshot.cs`](Scripts/Game/TransformSnapshot.cs) | Restores the broken box fragments' transforms and velocities when returned to the object pool |
| [`Game/Growth/AutoSelectManager.cs`](Scripts/Game/Growth/AutoSelectManager.cs) + [`Tools/BalanceSim/`](Tools/BalanceSim/) | Balance-simulator results carried over into the auto-select feature's priority table (`PickBest`) |

To follow one run end to end: `GameManager.cs` → `RoundManager.cs` → `SpawnManager.cs`.

## Highlights

- **`Tools/BalanceSim/`** — a standalone Python simulation of the round-by-round difficulty curve
  (enemy HP/DPS requirements, boss gates), used to tune numbers *before* touching Unity.
- **`Docs/`** — design docs written before implementation (growth system, meta-progression, core
  loop, sound), not after-the-fact documentation.
- Full ownership of the stack: roguelite upgrade draws, persistent meta-progression (currency,
  shop, stamina), a global leaderboard (Unity Gaming Services), and all UI wiring.

## About this repository

This is a **code-only extract** from Boxal's full Unity project, published for portfolio review.

The complete project (including ~950MB of paid Unity Asset Store packages) lives in a private
repository — their license permits use in a shipped build, but not public redistribution of the
raw asset files. This repo contains only the code, design docs, and tools I personally wrote:

- `Scripts/` — full C# source (gameplay, meta-progression, UI wiring)
- `Docs/` — design documents (growth system, meta progression, main play loop, sound)
- `Tools/BalanceSim/` — Python simulation used to balance round-by-round difficulty
- `CREDITS.md` — third-party audio licensing (CC BY / MIT attribution)

This repo won't open/build in Unity as-is (scene files and third-party assets are excluded).
For a working build, see the itch.io link above.
