#!/usr/bin/env python3
"""
Build Unity AnimationClips and AnimatorControllers from existing sprite sheets.

How it works:
1) Scan PNG + .meta files under a sprite folder.
2) Group files by character and animation state from filename patterns.
3) Write a generated JSON config for Unity Editor.
4) Optionally invoke Unity in batchmode to build assets automatically.

This script expects the Unity-side builder script:
Assets/Editor/AutoSpriteAnimationBuilder.cs
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


STATE_PATTERNS: List[Tuple[str, Sequence[str]]] = [
    ("Idle", (r"\bidle\b",)),
    ("Walk", (r"\bwalk(?:ing)?\b",)),
    ("Run", (r"\brun(?:ning)?\b",)),
    ("Eat", (r"eat[_\- ]?attack", r"\beat\b", r"\battack\b")),
    ("Fly", (r"face[_\- ]?camera[_\- ]?flight", r"\bflight\b", r"\bfly(?:ing)?\b")),
    ("Boost", (r"\bboost(?:ed|ing)?\b", r"\bwinged\b")),
    ("Chili", (r"chili", r"steam[_\- ]?ears", r"\bsteam\b")),
]

# Match Python controller update flow: Idle <-> Run, Eat one-shot, Chili one-shot.
STATE_PRIORITY = ["Idle", "Run", "Eat", "Chili", "Fly", "Boost", "Walk"]
NON_LOOP_STATES = {"Eat", "Chili"}

# State-specific FPS mirrored from Python source defaults.
STATE_FPS_OVERRIDES: Dict[str, float] = {
    "Run": 10.0,            # RUN_FRAME_DURATION = 0.1
    "Walk": 6.6666667,      # walk_in_frame_duration = 0.15
    "Eat": 8.3333333,       # EAT_FRAME_DURATION = 0.12
    "Chili": 8.3333333,     # close to chili sequence pacing in gameplay
    "Boost": 10.0,          # single-sheet boost sprite fallback
}

DEFAULT_EXECUTE_METHOD = (
    "SnackAttack.EditorScripts.AutoSpriteAnimationBuilder.BuildFromGeneratedConfigBatchMode"
)
DEFAULT_BUILDER_SCRIPT = "Assets/Editor/AutoSpriteAnimationBuilder.cs"
DEFAULT_UNITY_LOG_FILE = "Logs/anim_builder_batch.log"


@dataclass
class ClipCandidate:
    state: str
    texture_path: str
    frames: List[str]
    fps: float
    loop: bool


def parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate Unity animation build config from sprite sheet meta files.",
    )
    parser.add_argument(
        "--project-root",
        default=".",
        help="Path to Unity project root (default: current directory).",
    )
    parser.add_argument(
        "--sprites-dir",
        default="Assets/Sprite sheets",
        help="Folder containing sprite PNG files (default: Assets/Sprite sheets).",
    )
    parser.add_argument(
        "--clips-dir",
        default="Assets/Animations/Clips",
        help="Output folder for generated .anim files.",
    )
    parser.add_argument(
        "--controllers-dir",
        default="Assets/Animations/Controllers",
        help="Output folder for generated .controller files.",
    )
    parser.add_argument(
        "--config-path",
        default="Assets/Editor/Generated/auto_anim_config.json",
        help="Output config path for Unity builder script.",
    )
    parser.add_argument(
        "--fps",
        type=float,
        default=10.0,
        help="Default frame rate for generated clips.",
    )
    parser.add_argument(
        "--run-unity",
        action="store_true",
        help="Run Unity batchmode after generating config.",
    )
    parser.add_argument(
        "--unity-exe",
        default="",
        help="Path to Unity executable (Unity.exe).",
    )
    parser.add_argument(
        "--execute-method",
        default=DEFAULT_EXECUTE_METHOD,
        help="Unity static execute method used in batchmode.",
    )
    parser.add_argument(
        "--unity-log-file",
        default=DEFAULT_UNITY_LOG_FILE,
        help=(
            "Unity -logFile destination (relative to project root or absolute path) "
            f"(default: {DEFAULT_UNITY_LOG_FILE})."
        ),
    )
    parser.add_argument(
        "--log-tail-lines",
        type=int,
        default=80,
        help="How many lines from Unity log to print when batchmode fails.",
    )
    parser.add_argument(
        "--allow-locked-project",
        action="store_true",
        help="Allow running batchmode even if Temp/UnityLockfile exists.",
    )
    parser.add_argument(
        "--builder-script",
        default=DEFAULT_BUILDER_SCRIPT,
        help="Path to Unity builder C# script used for existence checks.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print summary only, do not write config or invoke Unity.",
    )
    return parser.parse_args(argv)


def detect_state(stem: str) -> Optional[str]:
    lower_name = stem.lower()
    for state, patterns in STATE_PATTERNS:
        for pattern in patterns:
            if re.search(pattern, lower_name):
                return state
    return None


def detect_character(stem: str) -> Optional[str]:
    # Character is assumed to be the leading alphabetic token.
    match = re.match(r"^([A-Za-z]+)", stem)
    if not match:
        return None
    raw = match.group(1)
    return raw[:1].upper() + raw[1:].lower()


def frame_sort_key(name: str) -> Tuple[str, int, str]:
    match = re.search(r"_(\d+)$", name)
    if not match:
        return (name.lower(), 10**9, name.lower())
    base = name[: match.start()]
    return (base.lower(), int(match.group(1)), name.lower())


def parse_meta(meta_path: Path) -> Tuple[Optional[str], List[str]]:
    text = meta_path.read_text(encoding="utf-8")

    guid_match = re.search(r"(?m)^guid:\s*([0-9a-fA-F]+)\s*$", text)
    guid = guid_match.group(1) if guid_match else None

    frames: List[str] = []
    table_match = re.search(r"nameFileIdTable:\r?\n((?:\s{6}.+\r?\n)+)", text)
    if table_match:
        for line in table_match.group(1).splitlines():
            entry_match = re.match(r"\s{6}(.+?):\s*(-?\d+)\s*$", line)
            if entry_match:
                frames.append(entry_match.group(1).strip())

    if not frames:
        # Fallback for uncommon meta layouts.
        fallback = re.findall(r"(?m)^\s*second:\s*(.+?)\s*$", text)
        frames = [name.strip() for name in fallback if name.strip()]

    unique_frames = sorted(dict.fromkeys(frames), key=frame_sort_key)
    return guid, unique_frames


def as_asset_path(path: Path, project_root: Path) -> str:
    return path.resolve().relative_to(project_root).as_posix()


def should_loop(state: str) -> bool:
    return state not in NON_LOOP_STATES


def pick_default_state(states: Iterable[str]) -> str:
    state_set = set(states)
    for preferred in STATE_PRIORITY:
        if preferred in state_set:
            return preferred
    return sorted(state_set)[0]


def collect_candidates(
    project_root: Path,
    sprites_dir: Path,
    fps: float,
) -> Tuple[Dict[str, Dict[str, ClipCandidate]], List[str]]:
    grouped: Dict[str, Dict[str, ClipCandidate]] = {}
    skipped: List[str] = []

    png_files = sorted(sprites_dir.rglob("*.png"), key=lambda p: p.as_posix().lower())

    for png_path in png_files:
        texture_asset_path = as_asset_path(png_path, project_root)
        texture_asset_path_lower = texture_asset_path.lower()

        # wing_up/wing_down are global VFX references, not per-character animation clips.
        if "/wings/" in texture_asset_path_lower:
            skipped.append(f"Wing helper overlay ignored: {texture_asset_path}")
            continue

        meta_path = Path(str(png_path) + ".meta")
        if not meta_path.exists():
            skipped.append(f"Missing meta: {texture_asset_path}")
            continue

        state = detect_state(png_path.stem)
        if state is None and "/boost_wings/" in texture_asset_path_lower and "boost" in png_path.stem.lower():
            state = "Boost"

        character = detect_character(png_path.stem)
        if state is None or character is None:
            skipped.append(f"Unknown naming, skipped: {texture_asset_path}")
            continue

        _guid, frames = parse_meta(meta_path)
        if not frames:
            skipped.append(f"No frame names in meta, skipped: {as_asset_path(meta_path, project_root)}")
            continue

        state_fps = STATE_FPS_OVERRIDES.get(state, fps)
        candidate = ClipCandidate(
            state=state,
            texture_path=texture_asset_path,
            frames=frames,
            fps=state_fps,
            loop=should_loop(state),
        )

        by_state = grouped.setdefault(character, {})
        existing = by_state.get(state)
        if existing is None or len(candidate.frames) > len(existing.frames):
            if existing is not None:
                skipped.append(
                    "Replaced duplicate "
                    f"{character}/{state} with richer frame set: {texture_asset_path}"
                )
            by_state[state] = candidate
        else:
            skipped.append(f"Duplicate state ignored: {texture_asset_path}")

    return grouped, skipped


def build_config_payload(
    grouped: Dict[str, Dict[str, ClipCandidate]],
    clips_dir: str,
    controllers_dir: str,
    default_fps: float,
) -> dict:
    characters_payload = []

    for character in sorted(grouped.keys()):
        states = dict(grouped[character])

        # Python runtime uses Idle as first frame of Run if no dedicated idle sheet exists.
        if "Idle" not in states and "Run" in states:
            run_clip = states["Run"]
            if run_clip.frames:
                states["Idle"] = ClipCandidate(
                    state="Idle",
                    texture_path=run_clip.texture_path,
                    frames=[run_clip.frames[0]],
                    fps=run_clip.fps,
                    loop=True,
                )

        # Requested behavior: Boost animation should use the same sprite sheet as
        # face_camera_flight (Fly) instead of boost_wings fallback art.
        if "Fly" in states and states["Fly"].frames:
            fly_clip = states["Fly"]
            states["Boost"] = ClipCandidate(
                state="Boost",
                texture_path=fly_clip.texture_path,
                frames=list(fly_clip.frames),
                fps=STATE_FPS_OVERRIDES.get("Boost", fly_clip.fps),
                loop=True,
            )

        ordered_state_names = sorted(
            states.keys(), key=lambda s: (STATE_PRIORITY.index(s) if s in STATE_PRIORITY else 999, s)
        )

        clips_payload = []
        for state_name in ordered_state_names:
            clip = states[state_name]
            clips_payload.append(
                {
                    "state": clip.state,
                    "clipPath": f"{clips_dir}/{character}/{character}_{clip.state}.anim",
                    "texturePath": clip.texture_path,
                    "fps": clip.fps,
                    "loop": clip.loop,
                    "frames": clip.frames,
                }
            )

        characters_payload.append(
            {
                "name": character,
                "controllerPath": f"{controllers_dir}/{character}.controller",
                "defaultState": pick_default_state(states.keys()),
                "clips": clips_payload,
            }
        )

    return {
        "version": 1,
        "defaultFps": default_fps,
        "characters": characters_payload,
    }


def find_unity_executable(explicit_path: str) -> Optional[Path]:
    if explicit_path:
        candidate = Path(explicit_path).expanduser().resolve()
        if candidate.exists():
            return candidate
        return None

    env_candidate = os.environ.get("UNITY_EXE", "").strip()
    if env_candidate:
        candidate = Path(env_candidate).expanduser().resolve()
        if candidate.exists():
            return candidate

    windows_hub_roots = [
        Path(r"C:\Program Files\Unity\Hub\Editor"),
        Path(r"C:\Program Files (x86)\Unity\Hub\Editor"),
    ]

    discovered: List[Path] = []
    for root in windows_hub_roots:
        if not root.exists():
            continue
        discovered.extend(root.glob("*/Editor/Unity.exe"))

    if discovered:
        # Pick newest by lexical version folder name.
        return sorted(discovered, key=lambda p: p.parts[-3], reverse=True)[0]

    fallback = Path(r"C:\Program Files\Unity\Editor\Unity.exe")
    if fallback.exists():
        return fallback

    return None


def run_unity_batch(
    unity_exe: Path,
    project_root: Path,
    execute_method: str,
    config_asset_path: str,
    unity_log_file: str,
) -> Tuple[int, Path]:
    log_file_path = Path(unity_log_file).expanduser()
    if not log_file_path.is_absolute():
        log_file_path = (project_root / log_file_path).resolve()
    log_file_path.parent.mkdir(parents=True, exist_ok=True)

    command = [
        str(unity_exe),
        "-batchmode",
        "-quit",
        "-projectPath",
        str(project_root),
        "-executeMethod",
        execute_method,
        "-animConfig",
        config_asset_path,
        "-logFile",
        str(log_file_path),
    ]

    print("[INFO] Running Unity batchmode:")
    print("       " + " ".join(f'"{token}"' if " " in token else token for token in command))

    result = subprocess.run(command, check=False)
    return result.returncode, log_file_path


def is_unity_project_locked(project_root: Path) -> bool:
    lock_file = project_root / "Temp" / "UnityLockfile"
    return lock_file.exists()


def read_log_tail(log_file: Path, max_lines: int) -> List[str]:
    if max_lines <= 0 or not log_file.exists():
        return []

    text = log_file.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    return lines[-max_lines:]


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = parse_args(argv)

    project_root = Path(args.project_root).expanduser().resolve()
    sprites_dir = (project_root / args.sprites_dir).resolve()

    if not project_root.exists():
        print(f"[ERROR] Project root does not exist: {project_root}")
        return 1
    if not sprites_dir.exists():
        print(f"[ERROR] Sprite directory does not exist: {sprites_dir}")
        return 1

    grouped, skipped = collect_candidates(project_root=project_root, sprites_dir=sprites_dir, fps=args.fps)

    if not grouped:
        print("[ERROR] No valid character/state clips were detected.")
        if skipped:
            print("[INFO] First skip reasons:")
            for item in skipped[:10]:
                print(f"  - {item}")
        return 2

    payload = build_config_payload(
        grouped=grouped,
        clips_dir=args.clips_dir.replace("\\", "/").rstrip("/"),
        controllers_dir=args.controllers_dir.replace("\\", "/").rstrip("/"),
        default_fps=args.fps,
    )

    config_asset_path = args.config_path.replace("\\", "/")
    config_file_path = (project_root / config_asset_path).resolve()

    total_clips = sum(len(character["clips"]) for character in payload["characters"])
    print(f"[INFO] Characters detected: {len(payload['characters'])}")
    print(f"[INFO] Clips to build: {total_clips}")
    print(f"[INFO] Output config: {config_asset_path}")

    if skipped:
        print(f"[INFO] Skipped items: {len(skipped)}")
        for item in skipped[:20]:
            print(f"  - {item}")

    if args.dry_run:
        print("[INFO] Dry run enabled. No files were written.")
        return 0

    config_file_path.parent.mkdir(parents=True, exist_ok=True)
    config_file_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print("[OK] Config file generated.")

    if not args.run_unity:
        print(
            "[NEXT] Open Unity and run menu: "
            "SnackAttack/Animations/Build From Generated Config"
        )
        return 0

    builder_script = (project_root / args.builder_script).resolve()
    if not builder_script.exists():
        print(f"[ERROR] Missing Unity builder script: {builder_script}")
        return 3

    if is_unity_project_locked(project_root) and not args.allow_locked_project:
        print("[ERROR] Project appears to be open in Unity (Temp/UnityLockfile exists).")
        print("        Close Unity and rerun, or pass --allow-locked-project.")
        return 5

    unity_exe = find_unity_executable(args.unity_exe)
    if unity_exe is None:
        print("[ERROR] Unity executable not found.")
        print("        Pass --unity-exe \"C:/.../Unity.exe\" or set UNITY_EXE.")
        return 4

    exit_code, log_file_path = run_unity_batch(
        unity_exe=unity_exe,
        project_root=project_root,
        execute_method=args.execute_method,
        config_asset_path=config_asset_path,
        unity_log_file=args.unity_log_file,
    )
    if exit_code != 0:
        print(f"[ERROR] Unity batchmode failed with exit code {exit_code}")
        print(f"[INFO] Unity log file: {log_file_path}")
        tail_lines = read_log_tail(log_file_path, args.log_tail_lines)
        if tail_lines:
            print(f"[INFO] Last {len(tail_lines)} lines from Unity log:")
            for line in tail_lines:
                print(line)
        return exit_code

    print(f"[INFO] Unity log file: {log_file_path}")
    print("[OK] Unity batchmode completed successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
