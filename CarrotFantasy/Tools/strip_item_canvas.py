import glob
import os
import re

ITEM_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Game", "FightPart", "Item")


def parse_blocks(text):
    parts = re.split(r"(?=--- !u!)", text)
    blocks = {}
    order = []
    for part in parts:
        if not part.strip():
            continue
        match = re.match(r"--- !u!(\d+) &(\d+)\n", part)
        if not match:
            continue
        file_id = match.group(2)
        blocks[file_id] = {"type": match.group(1), "text": part, "file_id": file_id}
        order.append(file_id)
    return blocks, order


def get_field(block_text, field):
    match = re.search(rf"^\s*{re.escape(field)}: (.+)$", block_text, re.M)
    return match.group(1).strip() if match else None


def get_children(block_text):
    match = re.search(r"m_Children:\n((?:  - \{fileID: \d+\}\n?)*)", block_text)
    if not match:
        return []
    return re.findall(r"\{fileID: (\d+)\}", match.group(1))


def set_children(block_text, child_ids):
    if "m_Children:" not in block_text:
        return block_text
    lines = block_text.splitlines()
    out = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if line.strip() == "m_Children:":
            out.append(line)
            for child_id in child_ids:
                out.append(f"  - {{fileID: {child_id}}}")
            i += 1
            while i < len(lines) and lines[i].startswith("  - {fileID:"):
                i += 1
            continue
        out.append(line)
        i += 1
    suffix = "\n" if block_text.endswith("\n") else ""
    return "\n".join(out) + suffix


def parse_file_id(value):
    if not value:
        return None
    match = re.search(r"\{fileID: (\d+)\}", value)
    return match.group(1) if match else None


def strip_prefab(path):
    with open(path, "r", encoding="utf-8") as handle:
        text = handle.read()

    blocks, order = parse_blocks(text)
    go_to_rect = {}
    rect_to_go = {}

    item_canvas_go = None
    for file_id, block in blocks.items():
        block_text = block["text"]
        if block["type"] == "1":
            if get_field(block_text, "m_Name") == "ItemCanvas":
                item_canvas_go = file_id
        elif block["type"] in ("4", "224"):
            go_id = parse_file_id(get_field(block_text, "m_GameObject"))
            if go_id:
                rect_to_go[file_id] = go_id
                go_to_rect[go_id] = file_id

    if not item_canvas_go:
        return False

    item_canvas_rect = go_to_rect.get(item_canvas_go)
    if not item_canvas_rect:
        return False

    remove_ids = set()
    stack = [item_canvas_rect]
    while stack:
        rect_id = stack.pop()
        if rect_id in remove_ids:
            continue
        remove_ids.add(rect_id)
        go_id = rect_to_go.get(rect_id)
        if go_id:
            remove_ids.add(go_id)
            go_block = blocks.get(go_id)
            if go_block:
                for comp_id in re.findall(r"- component: \{fileID: (\d+)\}", go_block["text"]):
                    remove_ids.add(comp_id)
        rect_block = blocks.get(rect_id)
        if rect_block:
            for comp_id in re.findall(r"- component: \{fileID: (\d+)\}", rect_block["text"]):
                remove_ids.add(comp_id)
            stack.extend(get_children(rect_block["text"]))

    for file_id, block in blocks.items():
        if block["type"] in ("1", "4", "224"):
            continue
        go_id = parse_file_id(get_field(block["text"], "m_GameObject"))
        if go_id in remove_ids:
            remove_ids.add(file_id)

    parent_id = parse_file_id(get_field(blocks[item_canvas_rect]["text"], "m_Father"))
    if parent_id and parent_id in blocks:
        parent_block = blocks[parent_id]
        children = [child for child in get_children(parent_block["text"]) if child not in remove_ids]
        blocks[parent_id]["text"] = set_children(parent_block["text"], children)

    new_text = "".join(blocks[file_id]["text"] for file_id in order if file_id not in remove_ids)
    if new_text == text:
        return False

    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(new_text)
    return True


def main():
    changed = 0
    pattern = os.path.join(ITEM_DIR, "Item_*.prefab")
    for path in glob.glob(pattern):
        if strip_prefab(path):
            changed += 1
            print("stripped", os.path.basename(path))
    print("total", changed)


if __name__ == "__main__":
    main()
