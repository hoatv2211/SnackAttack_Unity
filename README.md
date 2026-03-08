# SnackAttack Unity

SnackAttack is a 2D arcade dog game implemented in Unity, with a Python prototype kept in the same repository for feature parity and reference.

This repository is focused on the Unity runtime at `Assets/`, with data-driven gameplay via JSON configs in `Assets/Resources/Config`.

## Highlights

- Multi-state game flow: Main Menu, Character Select, Settings, Storm Intro, Countdown, Playing, Game Over.
- Game modes: single player, 1P vs AI, and 2P local.
- Snack effects and score system with pooled snack spawning.
- Crowd Chaos event system with timed warning, voting, result effects, screen tint, and battlefield shake.
- Runtime config loading from JSON for controls, levels, snacks, AI difficulty, and visual effects.
- Audio manager with menu/gameplay music switching and user settings persistence.
- Editor tools for one-click scene setup, config import, and sprite animation controller generation.

## Tech Stack

| Area | Details |
| --- | --- |
| Engine | Unity `6000.3.10f1` |
| Language | C# |
| Render Pipeline | Universal Render Pipeline (`com.unity.render-pipelines.universal`) |
| Input | Unity Input System (`com.unity.inputsystem`) + runtime key mapping config |
| UI | UGUI + TextMeshPro |
| Data | JSON files in `Assets/Resources/Config` loaded at runtime |
| Optional prototype | Python module at `src/` |

## Project Structure

```text
SnackAttack_Unity/
	Assets/
		Scenes/                     # Main Unity scene(s)
		Scripts/
			Core/                     # Event and audio core systems
			Gameplay/                 # GameManager, players, AI, spawner, arena, snack
			UI/                       # MainMenu, CharacterSelect, Settings, HUD, GameOver
			Config/                   # Config SOs + runtime JSON mapping
		Resources/Config/           # Runtime JSON source of truth
		Editor/                     # Scene and animation/config editor tools
	Build_PC/                     # Built Windows player (if present)
	src/                          # Python prototype/reference implementation
	tools/                        # Utility scripts (for example animation config builder)
```

## Run In Unity Editor

1. Install Unity Hub and Unity Editor version `6000.3.10f1`.
2. Open this folder as a Unity project.
3. Open scene `Assets/Scenes/MainScene.unity`.
4. Press Play.

Optional first-time setup if scene/config assets are missing or out of date:

1. Run menu `SnackAttack/Import JSON Configs`.
2. Run menu `SnackAttack/Setup Main Scene`.
3. Save scene.

## Run Packaged Windows Build

If `Build_PC/` exists in your checkout, run:

```powershell
Build_PC\SnackAttack.exe
```

## Default Controls

From `Assets/Resources/Config/controls.json`:

| Player | Move Up | Move Down | Move Left | Move Right |
| --- | --- | --- | --- | --- |
| P1 | `W` | `S` | `A` | `D` |
| P2 | `Up` | `Down` | `Left` | `Right` |

Global actions:

| Action | Key |
| --- | --- |
| Pause | `Escape` |
| Confirm | `Return` |
| Back | `Backspace` |

Crowd Chaos debug voting keys (editor/runtime debug):

| Vote Slot | Key |
| --- | --- |
| Option 1 | `1` |
| Option 2 | `2` |
| Option 3 | `3` |
| Option 4 | `4` |

## Gameplay Flow

- Match enters `StormIntro` before countdown.
- Countdown plays `3 -> 2 -> 1 -> GO` and transitions into `Playing`.
- During `Playing`, snack spawning, effects, score updates, and AI behavior run continuously.
- At round end, winner is computed and the game either advances round or enters `GameOver`.

Reference logic document:

- `Assets/Scripts/GAME_LOGIC_REFERENCE.md`

## Crowd Chaos Voting System

Unity includes a full Crowd Chaos parity implementation:

- One trigger per round (default around 35 seconds remaining).
- Warning countdown phase (default 5 seconds) with danger UI/tint behavior.
- Live voting window (default 10 seconds).
- Result phase (default 3 seconds) plus optional action duration.
- Modes rotate by round: `Treat -> Action -> Trivia`.

Winner effects:

- Treat mode: burst spawns a specific snack via `SnackSpawner.SpawnSpecificSnack(...)`.
- Action mode: temporary battlefield bounds changes (`UNLEASHED` or `YANKED`).
- Trivia mode: score bonus/penalty based on selected answer correctness.

Public vote API used by integrations:

- `GameManager.SubmitCrowdChaosVote(optionId, voterId)`

## Runtime Config Files

All primary gameplay tuning lives in `Assets/Resources/Config`:

| File | Purpose |
| --- | --- |
| `game_settings.json` | Window/gameplay defaults and round baseline |
| `characters.json` | Character roster, speed, hitbox metadata |
| `snacks.json` | Snack score values, effects, spawn weights |
| `levels.json` | Level order, duration, spawn multiplier, snack pool |
| `ai_difficulty.json` | AI tuning presets (`easy`, `medium`, `hard`) |
| `controls.json` | Key mapping |
| `audio_settings.json` | Default audio flags/volumes |
| `powerup_visuals.json` | VFX tuning for aura, glow, streaks, indicators |
| `treat_attack_settings.json` | Treat Attack mode/voting values |
| `twitch_config.json` | Twitch channel and command settings |

## Editor Utilities

Menu commands in `Assets/Editor`:

- `SnackAttack/Import JSON Configs`
- `SnackAttack/Setup Main Scene`
- `SnackAttack/Animations/Build From Generated Config`

Animation pipeline files:

- Python config generator: `tools/build_unity_animator.py`
- Unity builder: `Assets/Editor/AutoSpriteAnimationBuilder.cs`
- Generated config: `Assets/Editor/Generated/auto_anim_config.json`
- Outputs: `Assets/Animations/Clips/` and `Assets/Animations/Controllers/`

Example animation build command (from repo root):

```powershell
.\.venv\Scripts\python tools\build_unity_animator.py
```

Batch build with Unity invocation (example):

```powershell
.\.venv\Scripts\python tools\build_unity_animator.py --run-unity --unity-exe "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe"
```

## Optional Python Prototype (`src/`)

The Python implementation is useful as a gameplay reference and for non-Unity experimentation.

### Requirements

- Python 3.10+ recommended.
- Packages typically needed by current source imports:
	- `pygame`
	- `requests`
	- `pillow`
	- `twitchio`

Install with:

```powershell
pip install pygame requests pillow twitchio
```

### Environment Variables

`src/game.py` validates required keys before startup:

- `OPENROUTER_API_KEY`
- `REMBG_API_KEY`

Optional Twitch integration keys used by chat/voting modules:

- `TWITCH_ACCESS_TOKEN`
- `TWITCH_CLIENT_ID`

Create a `.env` file at repository root with at least:

```dotenv
OPENROUTER_API_KEY=your_key
REMBG_API_KEY=your_key
TWITCH_ACCESS_TOKEN=optional_token
TWITCH_CLIENT_ID=optional_client_id
```

### Python Config Note

Python `ConfigManager` currently looks for JSON files under `<repo>/config`.

The authoritative configs in this Unity branch are under `Assets/Resources/Config`, so if you run Python prototype directly, mirror these files into a root `config/` directory first.

### Run Python Prototype

From repository root:

```powershell
python -m src.game
```

## Known Notes

- `Assets/Resources/Config/audio_settings.json` may have very low or disabled defaults; in-game Settings can override and persist values.
- Crowd Chaos HUD can auto-create fallback overlay elements if dedicated scene bindings are missing.
- Animation batch build should run when Unity project is not locked by another Unity instance.

## License

MIT. See `LICENSE`.