"""修复 "Main Object Name 'X' does not match filename 'Y'" 警告。

批量把 .anim / .controller 资源主对象的 m_Name 改成与文件名一致（等价于在
Unity Inspector 里重命名主对象），GUID、引用和文件路径都不变。

用法（在工程根目录执行）：
    python Tools/fix_main_object_names.py            # 只预览
    python Tools/fix_main_object_names.py --apply    # 实际修改

修改后回到 Unity，等待自动 Refresh（或按 Ctrl+R）；重新导入后不会再产生该警告，
Console 里已有的旧记录清空即可。
"""

import argparse
import os
import sys

ASSETS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets")
TYPES = ("AnimationClip:", "AnimatorController:")


def decode(data):
    if data.startswith(b"\xef\xbb\xbf"):
        return data[3:].decode("utf-8"), True
    return data.decode("utf-8"), False


def encode(text, bom):
    return (b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8")


def find_main_name(lines):
    """返回主对象 m_Name 所在行号与当前值；只在该资源自己的 YAML 文档内查找。"""
    type_index = None
    for i, line in enumerate(lines):
        if line.rstrip("\r\n") in TYPES:
            type_index = i
            break
    if type_index is None:
        return None, None

    # 主对象 m_Name 就在文档头几行，不能扫进后面的 AnimatorState / AnimationClip 文档。
    for i in range(type_index + 1, min(type_index + 20, len(lines))):
        stripped = lines[i].rstrip("\r\n")
        if stripped.startswith("--- "):
            break
        if stripped.startswith("  m_Name:"):
            return i, stripped.split(":", 1)[1].strip()
    return None, None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="实际写入，缺省只预览")
    args = parser.parse_args()

    mismatches = []
    for root, _, files in os.walk(ASSETS):
        for name in files:
            ext = os.path.splitext(name)[1]
            if ext not in (".anim", ".controller"):
                continue

            path = os.path.join(root, name)
            expected = os.path.splitext(name)[0]
            with open(path, "rb") as f:
                text, bom = decode(f.read())
            lines = text.splitlines(keepends=True)
            index, actual = find_main_name(lines)
            if index is None:
                print(f"[skip] 未找到主对象 m_Name: {path}")
                continue
            if actual == expected:
                continue

            mismatches.append((path, actual, expected))
            if args.apply:
                ending = "\r\n" if lines[index].endswith("\r\n") else "\n"
                lines[index] = "  m_Name: " + expected + ending
                with open(path, "wb") as f:
                    f.write(encode("".join(lines), bom))

    print(f"mismatches={len(mismatches)} apply={args.apply}")
    for path, actual, expected in mismatches:
        print(f"{os.path.relpath(path, ASSETS)}: {actual!r} -> {expected!r}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
