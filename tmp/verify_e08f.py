import os
import re
import glob

ROOT = r"D:\GitHub\Kingdom\Assets\Resources\Datas"

TARGETS = {
    "d7108dfb178fc7742b642aff880bcb8c": "MachineFactory",
    "116d35601a1dec544838b03dbdf70894": "WireMill",
    "eceb037f89465d9419e3a36f8a208e95": "BuildingMaterialsComplex",
}


def meta_guid(path):
    text = open(path + ".meta", encoding="utf-8").read()
    m = re.search(r"^guid:\s*(\w+)", text, re.M)
    return m.group(1) if m else None


def ident(path):
    text = open(path, encoding="utf-8").read()
    m = re.search(r"^\s+id:\s*(\S+)", text, re.M)
    return m.group(1) if m else os.path.basename(path)


def effect_blocks(text):
    """Yield (Type, BuildingGuid, ResourceGuid, value) tuples from the effects list."""
    m = re.search(r"^  effects:[ \t]*\r?\n", text, re.M)
    if not m:
        return
    out = []
    current = {}
    for line in text[m.end():].split("\n"):
        if re.match(r"^  [A-Za-z_]", line):
            break
        t = re.search(r"^\s+- Type:\s*(\d+)", line)
        if t:
            if current:
                out.append(current)
            current = {"type": t.group(1), "building": None, "resource": None, "value": None}
            continue
        b = re.search(r"Building:\s*\{fileID: \d+, guid: (\w+)\}", line)
        if b and current:
            current["building"] = b.group(1)
            continue
        r = re.search(r"Resource:\s*\{fileID: \d+, guid: (\w+)\}", line)
        if r and current:
            current["resource"] = r.group(1)
            continue
        v = re.search(r"^\s+value:\s*\"?([0-9.eE+-]+)\"?", line)
        if v and current:
            current["value"] = v.group(1)
    if current:
        out.append(current)
    for item in out:
        yield item


def scan(folder, label):
    print("### %s 中指向三工厂的效果" % label)
    hits = 0
    for asset in sorted(glob.glob(os.path.join(ROOT, folder, "**", "*.asset"), recursive=True)):
        text = open(asset, encoding="utf-8").read()
        for eff in effect_blocks(text):
            if eff["building"] in TARGETS:
                hits += 1
                print("   %-32s Type=%-3s -> %-26s value=%s" % (
                    ident(asset), eff["type"], TARGETS[eff["building"]], eff["value"]))
    if hits == 0:
        print("   (无)")
    print()


scan("Workshop", "Workshop 升级")
scan("Research", "Research")
