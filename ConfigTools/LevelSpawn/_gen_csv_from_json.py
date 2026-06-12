# -*- coding: utf-8 -*-
"""一次性脚本：从关卡 JSON 生成 Chapter*_spawn.csv"""
import json
import os
import re
import glob

BASE = os.path.join(os.path.dirname(__file__), "..", "..", "CarrotFantasy", "Assets", "Game", "Json", "Level")
OUT_DIR = os.path.dirname(__file__)


def main():
    files = sorted(glob.glob(os.path.join(BASE, "Level*.json")))
    chapters = {1: [], 2: [], 3: []}

    for fp in files:
        name = os.path.basename(fp)
        m = re.match(r"Level(\d+)_(\d+)\.json", name)
        if not m:
            continue
        big, level = int(m.group(1)), int(m.group(2))
        with open(fp, "r", encoding="utf-8-sig") as f:
            data = json.load(f)
        rounds = data.get("roundInfo") or []
        for ri, rnd in enumerate(rounds, start=1):
            round_index = rnd.get("roundIndex", ri)
            phases = rnd.get("phases") or []
            for pe in phases:
                chapters[big].append({
                    "big": big,
                    "level": level,
                    "round": round_index,
                    "waveGap": rnd.get("waveGap", 0),
                    "phaseId": pe.get("phaseId", 1),
                    "phaseGap": pe.get("phaseGap", 0),
                    "spawnMode": pe.get("spawnMode", "sequential"),
                    "monsterIds": pe.get("monsterIds", ""),
                    "delay": pe.get("delay", 0.8),
                    "comment": "大关{0} 小关{1} 第{2}波".format(big, level, round_index),
                })

    header = "bigLevelId,levelId,roundIndex,waveGap,phaseId,phaseGap,spawnMode,monsterIds,delay,comment"
    for ch in sorted(chapters.keys()):
        rows = chapters[ch]
        rows.sort(key=lambda r: (r["level"], r["round"], r["phaseId"]))
        lines = [header]
        for r in rows:
            mids = r["monsterIds"]
            if "," in mids:
                mids = '"' + mids + '"'
            delay = r["delay"]
            delay_s = str(int(delay)) if delay == int(delay) else str(delay)

            def fmt_num(n):
                return str(int(n)) if n == int(n) else str(n)

            lines.append(",".join([
                str(r["big"]),
                str(r["level"]),
                str(r["round"]),
                fmt_num(r["waveGap"]),
                str(r["phaseId"]),
                fmt_num(r["phaseGap"]),
                r["spawnMode"],
                mids,
                delay_s,
                r["comment"],
            ]))
        path = os.path.join(OUT_DIR, "Chapter{0}_spawn.csv".format(ch))
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        print("Chapter{0}: {1} rows".format(ch, len(rows)))


if __name__ == "__main__":
    main()
