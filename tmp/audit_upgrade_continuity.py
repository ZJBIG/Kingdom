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
        v = round(gen.get(r, 0.0) - con.get(r, 0.0), 6)
        if v > 0:
            net[r] = v
    up = re.search(r"^  upgradeTo:\s*\{fileID: \d+, guid: (\w+)", text, re.M)
    buildings[name] = {"net": net, "upgrade": up.group(1) if up else None}

BLD = guid_map("Building")
for name, data in buildings.items():
    if data["upgrade"]:
        data["upgradeId"] = BLD.get(data["upgrade"], "UNKNOWN:" + data["upgrade"])
    else:
        data["upgradeId"] = None

# producers per resource (net)
producers = {}
for name, data in buildings.items():
    for r in data["net"]:
        producers.setdefault(r, []).append(name)

edges = [(n, d) for n, d in sorted(buildings.items()) if d["upgradeId"]]
print("### 既有升级边总数: %d" % len(edges))
print()

print("### 升级后净产出集合减少的边（潜在供应链缺口）")
problems = 0
for old, data in edges:
    new = data["upgradeId"]
    newnet = buildings.get(new, {}).get("net", {})
    if new not in buildings:
        print("   [未知后继] %-30s -> %s" % (old, new))
        continue
    lost = {r: v for r, v in data["net"].items() if r not in newnet}
    if not lost:
        continue
    problems += 1
    print("   %-30s -> %-30s 丢失: %s" % (old, new,
          ", ".join("%s(%s/s)" % (r, v) for r, v in sorted(lost.items()))))
    for r in sorted(lost):
        others = [p for p in producers.get(r, []) if p != old]
        print("        该资源其他净产出建筑: %s" % (", ".join(others) if others else "无（断链）"))
if problems == 0:
    print("   (无)")
print()

print("### 全库单源资源（净产出建筑数 == 1）")
for r in sorted(producers):
    if len(producers[r]) == 1:
        p = producers[r][0]
        succ = buildings[p]["upgradeId"]
        tail = (" -> 后继 %s%s" % (succ, "" if (succ in buildings and r in buildings[succ]["net"]) else "（后继不产此资源）")) if succ else "（无后继，可继续补建）"
        print("   %-22s 仅 %-30s%s" % (r, p, tail))
