import os
import re
import glob

ROOT = r"D:\GitHub\Kingdom\Assets\Resources\Datas"


def load_guid_map():
    mapping = {}
    pattern = os.path.join(ROOT, "Resource", "**", "*.asset.meta")
    for meta in glob.glob(pattern, recursive=True):
        text = open(meta, encoding="utf-8").read()
        g = re.search(r"^guid:\s*(\w+)", text, re.M)
        if not g:
            continue
        asset = meta[:-5]
        if not os.path.exists(asset):
            continue
        m = re.search(r"^\s+id:\s*(\S+)", open(asset, encoding="utf-8").read(), re.M)
        if m:
            mapping[g.group(1)] = m.group(1)
    return mapping


GUID2ID = load_guid_map()


def block(text, key):
    m = re.search(r"^  %s:[ \t]*\n((?:[ \t]+.*\n|\n)*)" % key, text, re.M)
    return m.group(1) if m else ""


def show(name):
    path = os.path.join(ROOT, "Building", "Industrial", name + ".asset")
    text = open(path, encoding="utf-8").read()
    print("=" * 12, name)
    for key in ("resourceGenerationRates", "resourceConsumptionRates", "resourceRequirements"):
        print("--", key)
        body = block(text, key)
        if not body.strip():
            print("   (empty)")
            continue
        current = None
        for line in body.split("\n"):
            g = re.search(r"guid: (\w+)", line)
            if g:
                current = GUID2ID.get(g.group(1), "UNKNOWN:" + g.group(1))
            r = re.search(r"(amount|rate):\s*\"?([0-9.eE+-]+)\"?", line)
            if r and current:
                print("   %-26s %s=%s" % (current, r.group(1), r.group(2)))
    m = re.search(r"^  upgradeTo:\s*(.*)$", text, re.M)
    print("-- upgradeTo:", m.group(1).strip() if m else "?")
    body = block(text, "requiredResearch")
    ids = re.findall(r"guid: (\w+)", body)
    print("-- requiredResearch count:", len(ids))


for n in ("MachineFactory", "WireMill", "BuildingMaterialsComplex"):
    show(n)
