#!/usr/bin/env python3
"""将 Assets/Game/Json/Level 下 roundInfo 数量同步到 MapConfig.mTotalRound。"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "CarrotFantasy"
LEVEL_DIR = ROOT / "Assets" / "Game" / "Json" / "Level"
MAP_PATHS = [
    ROOT / "Assets" / "StreamingAssets" / "Json" / "MapConfig.json",
    ROOT / "Assets" / "Game" / "Json" / "MapConfig.json",
]


def collect_wave_counts():
    counts = {}
    for path in sorted(LEVEL_DIR.glob("Level*.json")):
        m = re.match(r"Level(\d+)_(\d+)\.json", path.name, re.I)
        if not m:
            continue
        big, lvl = int(m.group(1)), int(m.group(2))
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        counts[(big, lvl)] = len(data.get("roundInfo") or [])
    return counts


def sync_mapconfig(map_path: Path, wave_counts: dict) -> int:
    if not map_path.exists():
        print(f"skip missing: {map_path}")
        return 0

    data = json.loads(map_path.read_text(encoding="utf-8-sig"))
    stages = data["unLockedNormalModelLevelList"]
    changed = 0
    for (big, lvl), count in wave_counts.items():
        idx = (big - 1) * 5 + (lvl - 1)
        if idx < 0 or idx >= len(stages):
            continue
        old = stages[idx]["mTotalRound"]
        if old != count:
            print(f"{map_path.name}: {big}-{lvl} {old} -> {count}")
            stages[idx]["mTotalRound"] = count
            changed += 1

    if changed:
        map_path.write_text(
            json.dumps(data, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
        )
    print(f"{map_path}: updated {changed}")
    return changed


def main():
    wave_counts = collect_wave_counts()
    total = 0
    for mp in MAP_PATHS:
        total += sync_mapconfig(mp, wave_counts)
    print(f"done, total changes: {total}")


if __name__ == "__main__":
    main()
