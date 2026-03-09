# Unity Runtime Avatar Generation Pipeline

This document describes the full runtime avatar generation flow using `OPENROUTER_API_KEY`, aligned with the Python pipeline goals and adapted to Unity build/runtime constraints.

## What Was Implemented

1. Full OpenRouter generation pipeline in Unity runtime.
2. Runtime character registration after generation.
3. Runtime sprite loading and sheet slicing.
4. Runtime custom character spawn path in `GameManager`.
5. Runtime sprite animation playback integrated into both player controllers.
6. Character select integration for latest generated character.

## End-to-End Flow

1. User opens `UploadAvatarScreen` from `CharacterSelectScreen` via `createDogButton`.
2. User selects a local image and enters dog name.
3. `UploadAvatarScreen` calls `OpenRouterAvatarService.GenerateProfileAvatar(...)`.
4. Service now runs full 7-step flow:
   1. Analyze features
   2. Generate profile
   3. Generate run sheet
   4. Generate eat sheet
   5. Generate walk sheet
   6. Generate boost sprite
   7. Register result payload
5. Files are saved to:
   - `<persistentDataPath>/custom_avatars/<character_id>/profile.png`
   - `<persistentDataPath>/custom_avatars/<character_id>/run.png`
   - `<persistentDataPath>/custom_avatars/<character_id>/eat.png`
   - `<persistentDataPath>/custom_avatars/<character_id>/walk.png`
   - `<persistentDataPath>/custom_avatars/<character_id>/boost.png`
6. `UploadAvatarScreen` builds runtime definition with `RuntimeSpriteSheetLoader.BuildDefinition(result)` and registers it via `RuntimeCharacterRegistry.Register(...)`.
7. Character select updates the last card slot to point to newest runtime character ID.
8. `GameManager` detects runtime character ID and spawns a template prefab, then attaches/initializes `RuntimeSpriteAnimationPlayer` and disables Animator for that spawned runtime character.
   - Spawn lookup uses `RuntimeCharacterRegistry.TryGetOrLoad(...)`, so custom avatar animation data is loaded from `Application.persistentDataPath/custom_avatars/<character_id>/` when needed.
9. `PlayerController` and `AIPlayerController` forward movement/eat/chili/boost/airborne state to runtime animation player.

## Key Files

- `Assets/Scripts/Services/OpenRouterAvatarService.cs`
- `Assets/Scripts/Services/AvatarGenerationResult.cs`
- `Assets/Scripts/Gameplay/RuntimeSpriteSheetLoader.cs`
- `Assets/Scripts/Gameplay/RuntimeSpriteAnimationPlayer.cs`
- `Assets/Scripts/Config/RuntimeCharacterDefinition.cs`
- `Assets/Scripts/Config/RuntimeCharacterRegistry.cs`
- `Assets/Scripts/UI/UploadAvatarScreen.cs`
- `Assets/Scripts/UI/CharacterSelectScreen.cs`
- `Assets/Scripts/Gameplay/GameManager.cs`
- `Assets/Scripts/Gameplay/PlayerController.cs`
- `Assets/Scripts/Gameplay/AIPlayerController.cs`

## Inspector Setup

1. `ConfigManager` must reference `OpenRouterConfigSO` (already auto-wired by existing config tooling).
2. In `GameManager`:
   - Optionally set `Runtime Character Template Prefab`.
   - If empty, runtime spawn falls back to first available mapped prefab.
3. `CharacterSelectScreen` should keep at least one card slot available (last slot is reused for latest custom character ID).

## Runtime Behavior Notes

1. This pipeline is build-compatible because it does not create Unity assets at runtime.
2. Generated sprites are loaded from disk and sliced in memory.
3. Runtime-generated characters are bootstrapped from disk (`custom_avatars`) into registry on app start paths (`CharacterSelect` and spawn flow).
4. Built-in characters continue using Animator flow unchanged.

## Known Limits

1. Character select currently maps newest runtime character to the last card slot rather than creating dynamic UI cards.
2. Aspect ratio hint is appended to prompt text; not all models guarantee strict output dimensions.

## Validation Performed

- C# error scan on changed files reported no errors.
- Python import warnings (`pygame` unresolved) remain from the separate Python workspace environment and are unrelated to Unity runtime implementation.

## Suggested Next Improvements

1. Add disk-to-registry bootstrap on app start to persist runtime character list across sessions.
2. Add explicit custom card UI (thumbnail + name) instead of reusing last built-in slot.
3. Add retry policy/backoff around OpenRouter requests and optional lightweight local cache metadata file.
