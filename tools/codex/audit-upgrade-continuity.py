#!/usr/bin/env python3
"""Kingdom 建筑升级边「净产出连续性」只读审计。

用途
----
回答 D05/D08 提出的验收问题：某条升级链解锁后，旧层级停止接收新建
（BuildingManager.CanConstructNew -> IsHighestUnlockedChainTier），
此时每条仍被消费的资源是否还有可再扩产的合法来源。

口径
----
- 净产出 = resourceGenerationRates - resourceConsumptionRates（同值出现在两表
  的是加工吞吐，相减后为 0，不计入净产出）。
- 只读：仅解析 Assets/Resources/Datas/**/*.asset 与其 .meta，不写任何文件、
  不访问 Unity、不修改资产。
- 输出是**定义级静态审计**，不是运行时净流，也不替代 Unity 编译、EditMode、
  PlayMode 或真机验收。数值断言需在 Unity 中按真实布局复核。

用法
----
    python tools/codex/audit-upgrade-continuity.py [项目根目录]

默认项目根为当前工作目录。
"""

import glob
import os
import re
import sys


def load_guid_map(datas_root, folder):
    mapping = {}
    pattern = os.path.join(datas_root, folder, "**", "*.asset.meta")
    for meta in glob.glob(pattern, recursive=True):
        text = open(meta, encoding="utf-8").read()
        match = re.search(r"^guid:\s*(\w+)", text, re.M)
        if not match:
            continue
        asset = meta[: -len(".meta")]
        if not os.path.exists(asset):
            continue
        ident = re.search(r"^\s+id:\s*(\S+)", open(asset, encoding="utf-8").read(), re.M)
        if ident:
            mapping[match.group(1)] = ident.group(1)
    return mapping


def section(text, key):
    """Return a 2-space-indented YAML key's block, stopping at the next 2-space key."""
    match = re.search(r"^  %s:[ \t]*\r?\n" % re.escape(key), text, re.M)
    if not match:
        return ""
    lines = []
    for line in text[match.end():].split("\n"):
        if line.strip() == "":
            lines.append(line)
            continue
        if re.match(r"^  [A-Za-z_]", line):
            break
        lines.append(line)
    return "\n".join(lines)


def rates(text, key, guid_map):
    result = {}
    current = None
    for line in section(text, key).split("\n"):
        guid = re.search(r"guid: (\w+)", line)
        if guid:
            current = guid_map.get(guid.group(1), "UNKNOWN:" + guid.group(1))
            continue
        amount = re.search(r"amount:\s*\"?([0-9.eE+-]+)\"?", line)
        if amount and current:
            result[current] = float(amount.group(1))
    return result


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
    datas = os.path.join(root, "Assets", "Resources", "Datas")
    if not os.path.isdir(datas):
        print("找不到内容定义目录：%s" % datas)
        return 2

    resources = load_guid_map(datas, "Resource")
    buildings = load_guid_map(datas, "Building")

    data = {}
    for asset in glob.glob(os.path.join(datas, "Building", "**", "*.asset"), recursive=True):
        text = open(asset, encoding="utf-8").read()
        ident = re.search(r"^\s+id:\s*(\S+)", text, re.M)
        if not ident:
            continue
        name = ident.group(1)
        gen = rates(text, "resourceGenerationRates", resources)
        con = rates(text, "resourceConsumptionRates", resources)
        net = {}
        for resource in set(gen) | set(con):
            value = round(gen.get(resource, 0.0) - con.get(resource, 0.0), 6)
            if value > 0:
                net[resource] = value
        upgrade = re.search(r"^  upgradeTo:\s*\{fileID: \d+, guid: (\w+)", text, re.M)
        data[name] = {
            "net": net,
            "upgrade": buildings.get(upgrade.group(1)) if upgrade else None,
        }

    producers = {}
    for name, info in data.items():
        for resource in info["net"]:
            producers.setdefault(resource, []).append(name)

    edges = [(n, i) for n, i in sorted(data.items()) if i["upgrade"]]
    print("升级边总数：%d" % len(edges))
    print()
    print("== 升级后净产出集合减少的边 ==")
    found = 0
    for old, info in edges:
        new = info["upgrade"]
        if new not in data:
            print("  [后继缺失] %s -> %s" % (old, new))
            found += 1
            continue
        lost = {r: v for r, v in info["net"].items() if r not in data[new]["net"]}
        if not lost:
            continue
        found += 1
        print("  %s -> %s" % (old, new))
        for resource, value in sorted(lost.items()):
            others = [p for p in producers.get(resource, []) if p != old]
            print("      丢失 %s(%s/s)；其他净产出：%s"
                  % (resource, value, ", ".join(others) if others else "无（断链）"))
    if not found:
        print("  （无）")
    print()
    print("== 单源资源（净产出建筑数为 1）==")
    for resource in sorted(producers):
        if len(producers[resource]) != 1:
            continue
        producer = producers[resource][0]
        successor = data[producer]["upgrade"]
        if successor is None:
            verdict = "无后继，可继续补建"
        elif resource in data.get(successor, {}).get("net", {}):
            verdict = "后继 %s 已覆盖" % successor
        else:
            verdict = "后继 %s 不产此资源（断链风险）" % successor
        print("  %-22s 仅 %-32s %s" % (resource, producer, verdict))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
