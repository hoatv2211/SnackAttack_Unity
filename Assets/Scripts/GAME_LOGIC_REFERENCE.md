# Game Logic Reference (Python -> Unity)

Cap nhat theo code hien tai: 2026-03-08

Tai lieu nay tong hop logic game chinh da duoc doi chieu tu Python sang Unity, bao gom cac cap nhat moi nhat ve storm intro, audio, UI, mode, va reset round.

## 1. Scene vs In-Game

- Khi chinh scene (khong Play): chi bat man hinh dang lam (`MainMenu`/`SettingsScreen`/`CharacterSelectScreen`/`GameplayHUD`/`GameOverScreen`), tat cac man hinh con lai de tranh chong UI.
- Khi Play: `GameManager` set state som o `Awake/Start`, moi UI an/hien theo `GAME_STATE_CHANGED`.

## 2. Game States va Flow

| Python | Unity |
|--------|-------|
| MAIN_MENU | MainMenu |
| SETTINGS | Settings |
| CHARACTER_SELECT | CharacterSelect |
| INTRO (custom in Unity) | StormIntro |
| GAMEPLAY countdown | Countdown |
| GAMEPLAY playing | Playing |
| GAME_OVER | GameOver |

Flow tong quat:

- `MainMenu -> Settings -> MainMenu`
- `MainMenu -> CharacterSelect -> StormIntro -> Countdown -> Playing -> GameOver`
- Ket thuc round: het gio -> so diem P1/P2 -> cong round win -> du dieu kien thi `GameOver`, khong thi qua round tiep theo.

Trong `Playing`, he thong co them `Crowd Chaos` theo timeline vote + effect (chi kich hoat 1 lan moi round).

## 3. Start New Game va Storm Intro

`StartNewGame()` hien tai:

- Reset runtime state (round, score, round wins, level runtime).
- Spawn character theo chon lua + mode.
- Neu `useStormIntro = true`:
  - Goi `ClearSpawnedSnacksForNewRound()` ngay truoc luc show intro.
  - `ChangeState(StormIntro)`.
  - Bat `UI_storm_intro` (chi toggle GameObject/CanvasGroup, khong ep `Animator.Play(stateName)`).
  - Character movement intro:
    - P1 vao tu ben trai.
    - P2 vao tu ben phai.
    - Neu bat `stormIntroMoveToArenaCenter`, target la tam arena.
  - Facing intro:
    - P1 nhin sang phai.
    - P2 flip sang trai (`flipX = true`) khi bat dau intro.
  - Het `stormIntroDuration` + `stormIntroPostDelay` -> vao `StartCountdown()`.
- Neu `useStormIntro = false`: vao `StartCountdown()` truc tiep.

## 4. Countdown va Round Reset

- `StartCountdown()` hien tai van goi `ClearSpawnedSnacksForNewRound()` de dam bao san sach cho moi round.
- Countdown SFX parity:
  - So 3: `countdown_2_3`
  - So 2: `countdown_2_3`
  - So 1: `countdown_1`
- Het countdown -> `ChangeState(Playing)`.

## 4.1 Crowd Chaos + Voting

- Trigger sau `crowdChaosTriggerTimeSeconds` (mac dinh 35s elapsed trong round).
- Phase 1: countdown `CROWD CHAOS IN` (mac dinh 5s) + danger warning.
- Phase 2: voting `CROWD CHAOS LIVE` (mac dinh 10s).
- Phase 3: result message (mac dinh 3s) + apply winner effect.

Voting mode luan phien theo round:

- Round 1 -> `Treat`
- Round 2 -> `Action`
- Round 3 -> `Trivia`
- Round >3: lap lai chu ky tren.

Hieu ung winner:

- `Action`
  - `UNLEASHED`: mo rong movement bounds (co the cross territory trong split arena).
  - `YANKED`: thu hep movement bounds theo `crowdChaosYankWidthScale` trong thoi gian effect.
- `Treat`
  - Spawn burst snack theo winner option qua `SnackSpawner.SpawnSpecificSnack(...)`.
  - Co scale multiplier cho treat storm (`crowdChaosTreatScaleMultiplier`).
- `Trivia`
  - Neu dung dap an: cong diem cho ca P1/P2 (`crowdChaosTriviaCorrectBonusScore`).
  - Neu sai: tru diem cho ca P1/P2 (`crowdChaosTriviaWrongPenaltyScore`).

Visual parity:

- Battlefield shake qua root offset ngau nhien (`crowdChaosShakeIntensity`, `crowdChaosShakeDuration`).
- Red tint pulse tren HUD (`crowdChaosTintMaxAlpha`, `crowdChaosTintPulseSpeed`).
- Danger text: `DANGER: BATTLEFIELD INSTABILITY`.

Debug/local vote:

- Phim `1/2/3/4` map vao option `1..4` trong cua so voting.
- API runtime cho event ngoai: `GameManager.SubmitCrowdChaosVote(optionId, voterId)`.

## 5. Mode va Gameplay Roots

- Supported mode:
  - `single`
  - `vsai`
  - `2p`
- `GameManager` dung root theo mode:
  - `gameplayEntitiesSingleRoot`
  - `gameplayEntitiesVersusRoot`
  - fallback `gameplayEntitiesRoot`
- Spawn character theo slot `Player1`/`Player2`; `vsai` co the random P2 character (neu bat tuy chon).

## 6. Snack Spawn/Despawn

- `SnackSpawner` spawn interval:
  - `baseSpawnInterval / spawnRateMultiplier + random(-0.3, 0.3)`
- Gioi han so snack theo `maxSnacks`.
- Chon snack weighted random theo `spawnWeight` trong pool level.
- `ClearSpawnedSnacks()` despawn snack dang active va reset timer spawn.

## 7. Score va Effects

- Diem co ban tu `SnackData.points`.
- `Boost` (red_bull) nhan diem qua multiplier.
- Effects chinh:
  - `SpeedBoost` (1.5x)
  - `Slow` (0.5x)
  - `Chaos` (dao control)
  - `Invincibility` (bo qua slow/chaos)
  - `Boost` (score + movement multiplier)

Player va AI deu dung logic effect parity (activeEffects, multiplier, invincibility gate).

## 8. AI Logic

- Re-evaluate target theo `reactionDelay` (tu config difficulty).
- `EvaluateSnack` tinh diem dua tren:
  - point value
  - khoang cach
  - tranh penalty
  - uu tien powerup
- `decisionAccuracy`: chon best hoac random.
- `pathfindingEfficiency`: cang thap cang random drift.

## 9. Audio Parity

- `AudioManager` normalize key (khong phan biet space/underscore/case).
- Alias key quan trong:
  - `countdown_2_3` -> `2&3`
  - `countdown_1` -> `1`
- State-based music:
  - `MainMenu/Settings/CharacterSelect/GameOver` -> `background`
  - `StormIntro/Countdown/Playing` -> `gameplay`
- Audio settings (`music/sfx/master volume`, `music/sfx enabled`) duoc luu bang `PlayerPrefs` va apply lai luc game khoi dong.
- Snack collect SFX parity:
  - Luon play `dog_eat`
  - Sau do play snack-special (`broccoli`/`red_bull`/`chilli`) hoac fallback `point_earned`

## 10. UI Logic

- `CharacterSelectScreen`:
  - 2P theo 2 phase (P1 -> P2)
  - START -> `GameManager.StartNewGame()`
- `GameplayHUD`:
  - Hien trong `Countdown` va `Playing`
  - Co nut Back ve `MainMenu`
- `SettingsScreen`:
  - Hien khi state = `Settings`
  - Dieu khien live `Music/SFX` toggle + `Music/SFX/Master` volume slider
  - Back/Esc quay ve `MainMenu`
- `GameOverScreen`:
  - Hien winner theo round wins
  - Ho tro ten character + score/rich fallback

## 11. Config Mapping (can dong bo)

- `snacks.json`: snack id, points, effect type/magnitude/duration, spawn_weight.
- `levels.json`: round duration, spawn multiplier, snack_pool.
- `ai_difficulty.json`: reaction delay, decision accuracy, pathfinding efficiency, penalty/powerup behavior.

## 12. Script Chinh Lien Quan

- `Gameplay/GameManager.cs`
- `Gameplay/SnackSpawner.cs`
- `Gameplay/PlayerController.cs`
- `Gameplay/AIPlayerController.cs`
- `Core/AudioManager.cs`
- `UI/CharacterSelectScreen.cs`
- `UI/SettingsScreen.cs`
- `UI/GameplayHUD.cs`
- `UI/GameOverScreen.cs`
