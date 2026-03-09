# Unity Avatar Runtime Pipeline

This document reflects the current Unity runtime avatar pipeline after recent refactors.

## Goals

1. Support full AI avatar generation at runtime.
2. Support fast simulation/testing without waiting for AI.
3. Persist and reuse generated avatars across sessions.
4. Keep runtime safe for player builds (no editor-only asset authoring at runtime).

## Current Data Sources

Runtime avatar data can come from three sources:

1. AI-generated output in `Application.persistentDataPath/custom_avatars/<id>/`.
2. Default fallback assets in `StreamingAssets/custom_avatars/<id>/`.
3. Test template assets in `StreamingAssets/template/<id>/` when `UploadAvatarScreen.isTest = true`.

## Canonical Avatar File Format

Each avatar folder uses the same file contract:

- `profile.png`
- `run.png`
- `eat.png`
- `walk.png`
- `boost.png`

## End-to-End Flow

1. User opens `UploadAvatarScreen` from `CharacterSelectScreen`.
2. User enters dog name and clicks generate.
3. Branch by mode:
   - `isTest = false`: call `OpenRouterAvatarService.GenerateProfileAvatar(...)` (full 7-step AI generation).
   - `isTest = true`: skip AI and copy files from `StreamingAssets/template/<testTemplateId>/` into persistent path.
4. On success, `UploadAvatarScreen`:
   - builds runtime definition via `RuntimeSpriteSheetLoader.BuildDefinition(result)`
   - registers it via `RuntimeCharacterRegistry.Register(...)`
   - updates preview and success state.
5. In Unity Editor only, AI-generated output is mirrored to:
   - `Assets/StreamingAssets/custom_avatars/<characterId>/`
   so generated samples become reusable defaults.
6. `RuntimeCharacterRegistry` ensures defaults are available:
   - sync missing files from `StreamingAssets/custom_avatars` to persistent path
   - load definitions from persistent path into memory registry.
7. `CharacterSelectScreen` rebuilds character cards dynamically from prefab:
   - includes base characters + all runtime custom avatars
   - applies auto-grid layout.
8. `GameManager` spawn path uses `RuntimeCharacterRegistry.TryGetOrLoad(...)`:
   - if runtime avatar exists, load custom definition and spawn template prefab
   - attach/init `RuntimeSpriteAnimationPlayer` and disable animator on that runtime instance.

## Character Select UI (Refactored)

`CharacterSelectScreen` now uses prefab-driven dynamic card generation.

### Card Generation

- Source fields:
  - `characterCardPrefab`
  - `characterCardsParent`
  - fallback via `autoUseFirstSceneCardAsPrefab`
- Build model list:
  - configured base IDs (`characterIdsByCard`)
  - runtime IDs from `RuntimeCharacterRegistry.GetAll()`
- Instantiate one card per model and bind click by generated index.

### Auto Grid Rules

- If card count `<= 6`: fixed `3 x 2` layout.
- If card count `> 6`: fixed `4 x n` layout with row wrap.

## Upload Screen Modes

`UploadAvatarScreen` supports two generation modes.

### AI Mode

- Uses OpenRouter full pipeline.
- Produces profile/run/eat/walk/boost into persistent path.
- In editor, mirrors result to `Assets/StreamingAssets/custom_avatars/<id>/`.

### Test Mode

- Controlled by `isTest`.
- Uses `testTemplateId` and `testStepDelay`.
- Copies from `StreamingAssets/template/<id>/` into persistent path.
- Emits same status-step flow as AI mode for UI consistency.

## Runtime Registry Fallback Strategy

`RuntimeCharacterRegistry` now includes fallback sync from `StreamingAssets`.

1. On `EnsureLoadedFromDisk()`:
   - sync missing avatar files from `StreamingAssets/custom_avatars` to persistent path
   - scan persistent folders and load definitions.
2. On `TryGetOrLoad(id)`:
   - try memory registry first
   - ensure missing files for that specific id are copied from `StreamingAssets/custom_avatars/<id>`
   - load definition from persistent path.

This guarantees that if files are missing on device, defaults from `StreamingAssets` are used automatically.

## Runtime Animation Behavior

`RuntimeSpriteAnimationPlayer` behavior is currently:

1. Eat/chili has priority when active.
2. Fly state (`isBoost` or `isAirborne`) uses `boostFrames`.
3. Idle state shows static first frame from `run.png` (`runFrames[0]`).
4. Moving state uses `runFrames` (fallback to walk when run unavailable).

## Key Scripts

- `Assets/Scripts/UI/UploadAvatarScreen.cs`
- `Assets/Scripts/UI/CharacterSelectScreen.cs`
- `Assets/Scripts/Services/OpenRouterAvatarService.cs`
- `Assets/Scripts/Services/AvatarGenerationResult.cs`
- `Assets/Scripts/Config/RuntimeCharacterDefinition.cs`
- `Assets/Scripts/Config/RuntimeCharacterRegistry.cs`
- `Assets/Scripts/Gameplay/RuntimeSpriteSheetLoader.cs`
- `Assets/Scripts/Gameplay/RuntimeSpriteAnimationPlayer.cs`
- `Assets/Scripts/Gameplay/GameManager.cs`
- `Assets/Scripts/Gameplay/PlayerController.cs`
- `Assets/Scripts/Gameplay/AIPlayerController.cs`

## Inspector Checklist

1. `UploadAvatarScreen`
   - Optional: `isTest`, `testTemplateId`, `testStepDelay`.
2. `CharacterSelectScreen`
   - Assign `characterCardPrefab` and `characterCardsParent`.
   - Optional grid tuning: `gridCellSize`, `gridSpacing`, `gridPadding`.
3. `GameManager`
   - Optional: assign `runtimeCharacterTemplatePrefab`.

## Known Constraints

1. Dynamic card visuals depend on prefab/card hierarchy (image and label components must be present).
2. Runtime file operations assume file-based access to StreamingAssets path (works on desktop/editor; platform-specific handling may be needed for packaged mobile/web).
3. AI output quality and exact sprite dimensions still depend on model behavior.

## Validation Summary

1. C# compile checks for modified files pass (`No errors found`).
2. Local fallback tests confirmed missing persistent files can be restored from `StreamingAssets/custom_avatars`.
3. Test-mode generation path works without API dependency.
