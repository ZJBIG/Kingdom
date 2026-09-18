import os
import re
import glob
from collections import defaultdict

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


RES = guid_map("Resource")


def section(text, key):
    """Return the raw block for a 2-space-indented key, stopping at the next 2-space key."""
    m = re.search(r"^  %s:[ \t]*\r?\n" % re.escape(key), text, re.M)
    if not m:
        return ""
    rest = text[m.end():]
    out = []
    for line in rest.split("\n"):
        if line.strip() == "":
            out.append(line)
            continue
        if re.match(r"^  \S", line):
            break
        out.append(line)
    return "\n".join(out)


def rates(text, key):
    result = {}
    body = section(text, key)
    current = None
    for line in body.split("\n"):
        g = re.search(r"guid: (\w+)", line)
        if g:
            current = RES.get(g.group(1), "UNKNOWN:" + g.group(1))
            continue
        r = re.search(r"amount:\s*\"?([0-9.eE+-]+)\"?", line)
        if r and current:
            result[current] = float(r.group(1))
    return result


buildings = {}
for asset in glob.glob(os.path.join(ROOT, "Building", "**", "*.asset"), recursive=True):
    text = open(asset, encoding="utf-8").read()
    m = re.search(r"^\s+id:\s*(\S+)", text, re.M)
    if not m:
        continue
    name = m.group(1)
    gen = rates(text, "resourceGenerationRates")
    con = rates(text, "resourceConsumptionRates")
    net = {}
    for r in set(gen) | set(con):
        net[r] = round(gen.get(r, 0.0) - con.get(r, 0.0), 6)
    buildings[name] = net

print("### 净产出 CopperWire 的建筑（老厂升级后是否还有来源）")
for name, net in sorted(buildings.items()):
    if net.get("CopperWire", 0) > 0:
        print("   %-32s net CopperWire = %s" % (name, net["CopperWire"]))
print()
print("### 净产出 Ceramic 的建筑")
for name, net in sorted(buildings.items()):
    if net.get("Ceramic", 0) > 0:
        print("   %-32s net Ceramic = %s" % (name, net["Ceramic"]))
print()
print("### 净产出 Electronics / Machinery / Engine / Glass / Concrete 的建筑")
for target in ("Electronics", "Machinery", "Engine", "Glass", "Concrete"):
    rows = [(n, net[target]) for n, net in buildings.items() if net.get(target, 0) > 0]
    print("--", target)
    for n, v in sorted(rows):
        print("   %-32s %s" % (n, v))

print()
print("### SiriusResourceBelt 关键字段")
path = os.path.join(ROOT, "Sector", "SiriusResourceBelt.asset")
text = open(path, encoding="utf-8").read()
for key in ("resourceConsumptionRates", "resourceGenerationRates", "costs", "requirements",
            "campaignDurationSeconds", "supplySatisfactionRequirement"):
    body = section(text, key)
    if body.strip():
        print("--", key)
        for line in body.split("\n"):
            if line.strip():
                print("   " + line.strip())
print("-- 全文关键行")
for line in text.split("\n"):
    if re.search(r"(?i)(campaignduration|seconds|amount|rate|reward|fleet|attack|defense)", line):
        print("   " + line.strip())
