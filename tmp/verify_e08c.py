import os
import re
import glob

ROOT = r"D:\GitHub\Kingdom\Assets\Resources\Datas"


def guid_map(folder):
    mapping = {}
    for meta in glob.glob(os.path.join(ROOT, folder, "**", "*.asset.meta"), recursive=True):
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


RES = guid_map("Research")
BLD = guid_map("Building")
SECTOR = guid_map("Sector")

print("### 三工厂 requiredResearch 实际解锁研究")
for name in ("MachineFactory", "WireMill", "BuildingMaterialsComplex"):
    path = os.path.join(ROOT, "Building", "Industrial", name + ".asset")
    text = open(path, encoding="utf-8").read()
    m = re.search(r"^  requiredResearch:\n((?:\s+-.*\n)*)", text, re.M)
    ids = [RES.get(g, "UNKNOWN:" + g) for g in re.findall(r"guid: (\w+)", m.group(1))] if m else []
    print("  %-26s -> %s" % (name, ids))

print()
print("### 章程提议的解锁研究是否存在")
for rid in ("DeepSpaceIndustrialIntegration", "OrbitalPowerTransmission", "OrbitalEngineering"):
    hits = glob.glob(os.path.join(ROOT, "Research", "**", rid + ".asset"), recursive=True)
    print("  %-32s %s" % (rid, "存在 " + hits[0] if hits else "缺失"))

print()
print("### 章程提议的新建筑是否已存在")
for bid in ("OrbitalMachiningComplex", "OrbitalWireWorks", "OrbitalBuildingMaterialsWorks"):
    hits = glob.glob(os.path.join(ROOT, "Building", "**", bid + ".asset"), recursive=True)
    print("  %-34s %s" % (bid, "已存在" if hits else "不存在"))

print()
print("### 星区/战役对 Electronics/Engine/Machinery 的需求抽样")
targets = {"Electronics", "Engine", "Machinery", "Concrete", "Glass", "CopperWire", "Ceramic"}
for asset in glob.glob(os.path.join(ROOT, "Sector", "**", "*.asset"), recursive=True):
    text = open(asset, encoding="utf-8").read()
    name = re.search(r"^\s+id:\s*(\S+)", text, re.M)
    name = name.group(1) if name else os.path.basename(asset)
    rows = []
    for key in ("resourceConsumptionRates", "resourceRequirements", "costs"):
        m = re.search(r"^  %s:[ \t]*\n((?:[ \t]+.*\n|\n)*)" % key, text, re.M)
        if not m:
            continue
        cur = None
        for line in m.group(1).split("\n"):
            g = re.search(r"guid: (\w+)", line)
            if g:
                cur = g.group(1)
            r = re.search(r"(amount|rate):\s*\"?([0-9.eE+-]+)\"?", line)
            if r and cur:
                rows.append((key, cur, r.group(1), r.group(2)))
    if rows:
        print("--", name)
        for key, g, field, value in rows:
            rid = None
            print("   %-24s %-14s %s=%s" % (key, g[:8], field, value))
