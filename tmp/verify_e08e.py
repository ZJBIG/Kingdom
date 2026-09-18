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


RES = guid_map("Resource")


def section(text, key):
    m = re.search(r"^  %s:[ \t]*\r?\n" % re.escape(key), text, re.M)
    if not m:
        return ""
    out = []
    for line in text[m.end():].split("\n"):
        if line.strip() == "":
            out.append(line)
            continue
        if re.match(r"^  [A-Za-z_]", line):
            break
        out.append(line)
    return "\n".join(out)


def rates(text, key):
    result = {}
    current = None
    for line in section(text, key).split("\n"):
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

for target in ("CopperWire", "Ceramic", "Electronics", "Machinery", "Engine", "Glass", "Concrete"):
    rows = [(n, net[target]) for n, net in buildings.items() if net.get(target, 0) > 0]
    print("### 净产出 %s 的建筑 (%d)" % (target, len(rows)))
    for n, v in sorted(rows):
        print("   %-34s %s" % (n, v))
    print()

print("### SiriusResourceBelt campaignResourceRatesPerSecond 映射")
path = os.path.join(ROOT, "Sector", "SiriusResourceBelt.asset")
text = open(path, encoding="utf-8").read()
for key in ("campaignResourceRatesPerSecond", "occupiedResourceRatesPerSecond",
            "resourceRewards", "campaignCostResourceRatesPerSecond"):
    body = section(text, key)
    if not body.strip():
        continue
    print("--", key)
    current = None
    for line in body.split("\n"):
        g = re.search(r"guid: (\w+)", line)
        if g:
            current = RES.get(g.group(1), "UNKNOWN:" + g.group(1))
            continue
        r = re.search(r"amount:\s*\"?([0-9.eE+-]+)\"?", line)
        if r and current:
            print("   %-24s %s" % (current, r.group(1)))

print()
print("### 星区前置/解锁")
for key in ("requiredResearch", "prerequisites", "requiredBuildings"):
    body = section(text, key)
    if body.strip():
        print("--", key)
        print(body.strip()[:400])
