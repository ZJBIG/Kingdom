import os
import re
import glob

ROOT = r"D:\GitHub\Kingdom\Assets\Resources\Datas"

TARGETS = {
    "d7108dfb178fc7742b642aff880bcb8c": "MachineFactory",
    "116d35601a1dec544838b03dbdf70894": "WireMill",
    "eceb037f89465d9419e3a36f8a208e95": "BuildingMaterialsComplex",
}


def ident(path):
    text = open(path, encoding="utf-8").read()
    m = re.search(r"^\s+id:\s*(\S+)", text, re.M)
    return m.group(1) if m else os.path.basename(path)


def effects(text):
    m = re.search(r"^  effects:[ \t]*\r?\n", text, re.M)
    if not m:
        return []
    out = []
    current = None
    for line in text[m.end():].split("\n"):
        if re.match(r"^  [A-Za-z_]", line):
            break
        t = re.search(r"^\s+- Type:\s*(\d+)", line)
        if t:
            if current:
                out.append(current)
            current = {"type": t.group(1), "building": None, "resource": None, "value": None}
            continue
        if current is None:
            continue
        if "Building:" in line:
            g = re.search(r"guid: (\w+)", line)
            current["building"] = g.group(1) if g else None
        elif "Resource:" in line:
            g = re.search(r"guid: (\w+)", line)
            current["resource"] = g.group(1) if g else None
        elif re.match(r"^\s+value:", line):
            v = re.search(r"value:\s*\"?([0-9.eE+-]+)\"?", line)
            current["value"] = v.group(1) if v else None
    if current:
        out.append(current)
    return out


for folder, label in (("Workshop", "Workshop 升级"), ("Research", "Research")):
    print("### %s 中指向三工厂的效果" % label)
    hits = 0
    for asset in sorted(glob.glob(os.path.join(ROOT, folder, "**", "*.asset"), recursive=True)):
        text = open(asset, encoding="utf-8").read()
        for eff in effects(text):
            if eff["building"] in TARGETS:
                hits += 1
                print("   %-34s Type=%-3s -> %-26s value=%s" % (
                    ident(asset), eff["type"], TARGETS[eff["building"]], eff["value"]))
    if hits == 0:
        print("   (无)")
    print()
