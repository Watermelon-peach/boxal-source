# Boxal

**▶ Play on itch.io: https://watermelonpeach.itch.io/boxal** (screenshots + free APK download)

Solo-developed mobile roguelite — gameplay, meta-progression, UI, and tooling all built by one person.
Unity 6000.4 (URP) · C# · Android.

## Highlights

- **`Tools/BalanceSim/`** — a standalone Python simulation of the round-by-round difficulty curve
  (enemy HP/DPS requirements, boss gates), used to tune numbers *before* touching Unity. Not
  something most solo/junior projects have.
- **`Docs/`** — design docs written before implementation (growth system, meta-progression, core
  loop, sound), not after-the-fact documentation.
- Full ownership of the stack: roguelite upgrade draws, persistent meta-progression (currency,
  shop, stamina/energy gating), a live global leaderboard (Unity Gaming Services), and all UI wiring.

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
