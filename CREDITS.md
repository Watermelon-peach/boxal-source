# Boxal 오디오 라이선스 / 크레딧

이 폴더의 모든 오디오 파일의 출처와 라이선스를 기록한다.
**오디오 파일을 추가할 때마다 이 표에 한 줄을 같이 추가할 것.** 나중에는 출처를 절대 복구할 수 없다.

원본 사운드 보관처
- `D:\Projects\SoundLab\Assets\` — 팩별 폴더 + `_UsedInProjects/SoundsSourcesForBoxal/` 스테이징
- `E:\Soniss.com-GDC2026-GameAudioBundle\` — Sonniss GDC 2026 번들(zip 5개)

라이선스 원문 사본
- `Karugamo_LICENSE.txt` (이 폴더) — Karugamo BGM의 MIT 전문. 원본은 Unity 에셋 패키지에 동봉된
  `D:\Projects\SoundLab\Assets\Karugamo\LICENSE.txt`. **설정 패널 Credits에서 이 파일을 표시한다**
  (`SettingsPanelUI.licenseFullText`) — MIT가 요구하는 "사본에 허가 문구 포함"은 이렇게 충족한다.
- `Sonniss_GDC2026_Bundle_LICENSE.txt` (이 폴더) — 번들 원본 readme. 표기 의무는 없지만,
  파일명에 출처가 남지 않는 `SFX/BoxmonHit/*`의 royalty-free 근거라서 보관한다.
  위 `E:\` 드라이브가 없어도 근거가 남도록 리포 안에 사본을 둔 것이다.

전 항목 출처 확인 완료: 2026-07-30

---

## 요약

**상업적 이용에 걸림돌 없음.** NonCommercial 라이선스는 하나도 없다.
출시 전 처리할 것은 아래 두 가지뿐이다.

1. **CC BY / MIT 항목의 크레딧 표기** — 아래 "게임 내 표기 문구"를 설정 패널 Credits에 넣는다. (의무)
2. **공개 리포 재배포 주의** — `SFX/LevelUp`, `SFX/CardSelect`, `SFX/GameOver`의 3개 파일은
   Unity Asset Store 표준 EULA라 **원본 파일을 공개 리포에 두면 안 된다.** 게임 빌드에 포함하는 건 문제없다.
   (리포를 비공개로 두면 해결됨 — 애초에 `Assets/Feel` 등 유료 에셋 때문에 비공개가 필수다)

---

## 전체 목록

| 프로젝트 경로 | 원본 / 제작자 | 출처 | 라이선스 | 크레딧 | 공개 재배포 |
|---|---|---|---|---|---|
| `BGM/Home/KBF_3m_Dungeon_Natural_07_A.ogg` | Karugamo BGM | https://karugamobgm.com | **MIT** | 저작권 고지 필요 | 가능 |
| `BGM/Play/KBF_Battle_Nomal_05.ogg` | Karugamo BGM | https://karugamobgm.com | **MIT** | 저작권 고지 필요 | 가능 |
| `BGM/KingBoss/KBF_3m_Battle_Boss_02.ogg` | Karugamo BGM | https://karugamobgm.com | **MIT** | 저작권 고지 필요 | 가능 |
| `BGM/Title/KBF_3m_Field_Land_05.ogg` | Karugamo BGM | https://karugamobgm.com | **MIT** | 저작권 고지 필요 | 가능 |
| `SFX/BoxmonBreak/750822__artninja__custom_short_explosion_impact_sound.wav` | custom_short_explosion_impact_sound / Artninja | https://freesound.org/s/750822/ | **CC BY 4.0** | **필요** | 가능 |
| `SFX/BoxmonHit/BoxmonHit00~03.wav` | Sonniss #GameAudioGDC 2026 Bundle (편집·트림함) | https://sonniss.com | **Royalty-free** | 불필요 | 주의(아래 ★) |
| `SFX/Charge/239503__luckylittleraven__going-up-and-down-chirp.wav` | going-up-and-down-chirp / luckylittleraven | https://freesound.org/s/239503/ | **CC BY 3.0** | **필요** | 가능 |
| `SFX/Parry/118510__soneproject__cartbox-kick-drum.wav` | cartbox kick drum / soneproject | https://freesound.org/s/118510/ | **CC0** | 불필요 | 가능 |
| `SFX/Jump/177848+crossbow.wav` ① | Modulated Ruler FX (Spring Jump Cartoon Noise) / Motion_S | https://freesound.org/s/177848/ | **CC BY 4.0** | **필요** | 가능 |
| `SFX/Jump/177848+crossbow.wav` ② | crossbow.wav / 4crain (Shooting Sound 팩) | Unity Asset Store — Shooting Sound | 무료·사용 자유, **재판매 금지** | 불필요 | 믹스본이라 무방 |
| `SFX/PlayerHit/593909__newlocknew__crushing-kick_1-x317lrs.wav` | Crushing kick_1 x3(17lrs) / newlocknew | https://freesound.org/s/593909/ | **CC BY 4.0** | **필요** | 가능 |
| `SFX/LevelUp/Magic Score 5.wav` | Cyberwave Orchestra | [Asset Store 295538](https://assetstore.unity.com/packages/audio/sound-fx/hints-stars-points-rewards-sound-effects-lite-pack-295538) | **Unity AS 표준 EULA (무료)** | 불필요 | ⚠️**불가** |
| `SFX/CardSelect/Magic Score 9.wav` | Cyberwave Orchestra | 위와 동일 | **Unity AS 표준 EULA (무료)** | 불필요 | ⚠️**불가** |
| `SFX/GameOver/Mysterious Chapter.wav` | Cyberwave Orchestra | 위와 동일 | **Unity AS 표준 EULA (무료)** | 불필요 | ⚠️**불가** |

**`SFX/Jump/177848+crossbow.wav`는 두 소스를 합친 파생 저작물이다.** freesound 177848(CC BY 4.0)과
Shooting Sound 팩의 `crossbow.wav`를 믹스했다. 더 엄격한 조건인 CC BY가 결합물 전체에 적용되므로 크레딧이 필요하다.

★**Sonniss 번들 재배포**: EULA는 "사운드를 **있는 그대로 판매**하는 것"을 금지한다(무상 배포를 명시적으로
금지하지는 않는다). Boxal의 파일은 트림·편집되어 프로젝트에 통합된 상태이므로 EULA가 명시적으로 허용하는
"licensee project에 포함된 형태"에 해당한다. 다만 원본에 가까운 형태로 공개하는 것은 피하는 편이 안전하다.

---

## 게임 내 표기 문구

설정 패널의 Credits 항목에 넣을 텍스트. CC BY와 MIT는 표기가 의무이고, 나머지는 관례상 함께 적는다.

```
Sound Credits

— Music —
Karugamo BGM (karugamobgm.com)
  Copyright 2020 Karugamo BGM — MIT License

— Sound Effects —
"custom_short_explosion_impact_sound" by Artninja
  freesound.org/s/750822/ — CC BY 4.0
"going-up-and-down-chirp" by luckylittleraven
  freesound.org/s/239503/ — CC BY 3.0
"Modulated Ruler FX" by Motion_S
  freesound.org/s/177848/ — CC BY 4.0
"Crushing kick_1 x3(17lrs)" by newlocknew
  freesound.org/s/593909/ — CC BY 4.0
"cartbox kick drum" by soneproject
  freesound.org/s/118510/ — CC0

Sonniss #GameAudioGDC Bundle (sonniss.com)
Shooting Sound pack by 4crain
Hints, Stars, Points & Rewards SFX Lite Pack
  by Cyberwave Orchestra
```

★MIT는 "저작권 고지와 허가 문구를 사본에 포함"할 것을 요구한다. 위 Copyright 줄만으로 부족하다고 보는
견해도 있어, **`Karugamo_LICENSE.txt` 전문을 Credits 화면에서 그대로 보여주는 것으로 해결했다**
(2026-07-30, `SettingsPanelUI`). 이 파일이 빌드에 들어가려면 인스펙터의 `licenseFullText`에
연결돼 있어야 한다 — `Assets/` 안에 있기만 해서는 빌드에 포함되지 않는다.

★**표기 문구는 일부러 ASCII만 쓴다.** 프로젝트의 LilitaOne SDF 폰트는 ASCII 96자만 구운 정적 아틀라스라
em dash(—)와 ©가 렌더되지 않는다(TMP 폴백도 0개). 위 블록에서 `-`를 쓴 것은 오타가 아니다.
본문 폰트로 `LiberationSans SDF`(250자, — © 포함)를 쓰면 제약이 없어진다.

★**Karugamo 공식 사이트 약관과 MIT는 서로 다르다.** 사이트(karugamobgm.com)의 이용약관은
"양도·재배포 금지"라고 적혀 있지만, **우리가 받은 사본은 Unity 에셋 패키지판이고 거기에는 MIT 전문이
동봉돼 있다.** 우리 사본에 적용되는 것은 동봉된 MIT다(더 넓은 허락). 나중에 이 표를 의심하게 되면
사이트 약관이 아니라 `Karugamo_LICENSE.txt`를 근거로 볼 것.

---

## 라이선스 종류 참고

| 라이선스 | 상업적 이용 | 크레딧 | 공개 리포 재배포 |
|---|---|---|---|
| **CC0** / Public Domain | 가능 | 불필요 | 가능 |
| **CC BY** (Attribution) | 가능 | **필수** | 가능(고지 유지) |
| **MIT** | 가능 | 저작권 고지 필요 | 가능(고지 유지) |
| **Sonniss GDC 번들** | 가능 | 불필요 | 원본 그대로 판매 금지 |
| **Unity Asset Store 표준 EULA** | 게임에 사용 가능 | 불필요 | ⚠️ **불가** — 빌드 포함만 허용 |
| **CC BY-NC** | **불가** | - | - |

## 새 오디오를 추가할 때

1. **다운로드한 즉시** 위 표에 한 줄 추가 (나중에 하면 반드시 잊는다)
2. freesound 파일은 **파일명 앞의 ID 숫자를 지우지 말 것** — 유일한 출처 추적 수단이다
   (개명이 필요하면 개명 전 원본명을 이 문서에 기록)
3. 여러 소스를 합치면 **양쪽 라이선스가 모두 따라오고, 더 엄격한 쪽이 결합물 전체에 적용**된다
4. 라이선스가 **NC(NonCommercial)이면 받지 말 것**
5. Sonniss 번들은 **AI 학습에 사용하는 것이 EULA로 금지**되어 있다 (AI 오디오 툴에 입력 금지)
