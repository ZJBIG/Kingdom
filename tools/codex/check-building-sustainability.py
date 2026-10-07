#!/usr/bin/env python
"""Static SUSTAINABILITY check: can each building actually be kept running when
it unlocks?

Companion to tools/codex/content-closure-check.ps1, which only answers "can this
research / workshop upgrade / building become REACHABLE". This answers the other
half of ToDoList D08:

    "闭包 PASS 不覆盖持续投入与升级后来源丧失。当前脚本按可建造条件把产出资源
     种类加入单调集合，没有同时证明维护、电力/物流/生产力可满足。
     做法：保留静态闭包作为必要条件，再补现实布局与升级后链路的 Unity 验收，
           不扩策略搜索。"

It checks four necessary conditions per building, all derived from definition
fields:

  1. every resource in resourceConsumptionRates (maintenance) has a producer;
  2. every resource in resourceRequirements (build cost) has a producer;
  3. the first producer of each such resource appears at or BEFORE the
     consumer's own TechLevel (a resource first produced in a later era cannot
     sustain an earlier building);
  4. a supplier of power / logistics / population capacity exists at or before
     the building's era, for buildings that consume those channels.

It is deliberately NOT a balance or layout check: "a supplier exists" is weaker
than "supply is sufficient". Per D08 the static side is only a NECESSARY
condition; the sufficiency side still needs Unity acceptance.

Read-only. Exit code 0 when every condition holds, 1 otherwise.
"""
import os
import re
import sys
from collections import defaultdict

ERA = {0: "Animal", 1: "StoneAge", 2: "Medieval", 3: "Industrial", 4: "Spacer", 5: "Ultra"}


def read(path):
    with open(path, encoding="utf-8", errors="replace") as handle:
        return handle.read()


def index_meta(directory, suffix):
    out = {}
    for dirpath, _dirnames, filenames in os.walk(directory):
        for name in filenames:
            if name.endswith(suffix):
                match = re.search(r"^guid:\s*([0-9a-f]+)",
                                  read(os.path.join(dirpath, name)), re.M)
                if match:
                    out[match.group(1)] = name[:-len(suffix)]
    return out


def field(text, key):
    match = re.search(r"^  %s:\s*(.*)$" % re.escape(key), text, re.M)
    return None if not match else match.group(1).strip().strip('"')


def num(text, key, default=0.0):
    raw = field(text, key)
    if raw is None:
        return default
    try:
        return float(raw)
    except ValueError:
        return default


def pairs(text, section, resource_index):
    match = re.search(r"^  %s:[ \t]*$" % re.escape(section), text, re.M)
    if not match:
        return []
    out, pending = [], None
    for line in text[match.end():].splitlines():
        if line.startswith("  - ") or line.startswith("    "):
            guid = re.search(r"guid:\s*([0-9a-f]+)", line)
            if guid:
                gid = guid.group(1)
                pending = resource_index.get(gid, "?" + gid[:8])
                continue
            amount = re.search(r"amount:\s*\"?([0-9.]+)\"?", line)
            if amount and pending:
                if float(amount.group(1)) > 0:
                    out.append((pending, float(amount.group(1))))
                pending = None
            continue
        if line.strip():
            break
    return out


def main():
    project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    if len(sys.argv) > 1:
        project_root = os.path.abspath(sys.argv[1])

    resource_dir = os.path.join(project_root, "Assets", "Resources", "Datas", "Resource")
    building_dir = os.path.join(project_root, "Assets", "Resources", "Datas", "Building")
    if not os.path.isdir(building_dir):
        print("Building definitions not found under %s" % building_dir)
        return 2

    resource_index = index_meta(resource_dir, ".asset.meta")

    buildings = {}
    for dirpath, _dirnames, filenames in os.walk(building_dir):
        for name in filenames:
            if not name.endswith(".asset"):
                continue
            text = read(os.path.join(dirpath, name))
            bid = field(text, "id") or name[:-6]
            buildings[bid] = {
                "era": int(num(text, "TechLevel", -1)),
                "generates": pairs(text, "resourceGenerationRates", resource_index),
                "consumes": pairs(text, "resourceConsumptionRates", resource_index),
                "requires": pairs(text, "resourceRequirements", resource_index),
                "power_c": num(text, "powerConsumptionRate"),
                "logistics_c": num(text, "logisticsConsumptionRate"),
                "productivity_c": num(text, "productivityConsumption"),
                "power_p": num(text, "powerProductionRate"),
                "logistics_p": num(text, "logisticsProductionRate"),
                "population": num(text, "populationCapacityGranted"),
            }

    first_era = {}
    first_producer = {}
    for bid, building in buildings.items():
        for resource, _amount in building["generates"]:
            if resource not in first_era or building["era"] < first_era[resource]:
                first_era[resource] = building["era"]
                first_producer[resource] = bid

    failures = []

    for bid, building in sorted(buildings.items(), key=lambda x: (x[1]["era"], x[0])):
        for kind, items in (("maintenance", building["consumes"]),
                            ("build requirement", building["requires"])):
            for resource, amount in items:
                if resource not in first_era:
                    failures.append("%-34s era=%-11s %-16s needs %-22s x%-6g -> NEVER PRODUCED"
                                    % (bid, ERA.get(building["era"], building["era"]),
                                       kind, resource, amount))
                elif first_era[resource] > building["era"]:
                    failures.append(
                        "%-34s era=%-11s %-16s needs %-22s x%-6g -> first produced in %s (%s)"
                        % (bid, ERA.get(building["era"], building["era"]), kind, resource,
                           amount, ERA.get(first_era[resource], first_era[resource]),
                           first_producer.get(resource, "?")))

    suppliers = defaultdict(list)
    housing = []
    for bid, building in buildings.items():
        if building["power_p"] > 0:
            suppliers["power"].append((building["era"], bid))
        if building["logistics_p"] > 0:
            suppliers["logistics"].append((building["era"], bid))
        if building["population"] > 0:
            housing.append((building["era"], bid))

    for bid, building in sorted(buildings.items(), key=lambda x: (x[1]["era"], x[0])):
        for channel, consumed in (("power", building["power_c"]),
                                  ("logistics", building["logistics_c"])):
            if consumed <= 0:
                continue
            if not any(era <= building["era"] for era, _ in suppliers[channel]):
                failures.append("%-34s era=%-11s needs %-16s -> no supplier at this era"
                                % (bid, ERA.get(building["era"], building["era"]), channel))
        if building["productivity_c"] > 0 and not any(
                era <= building["era"] for era, _ in housing):
            failures.append("%-34s era=%-11s needs %-16s -> no housing at this era"
                            % (bid, ERA.get(building["era"], building["era"]), "productivity"))

    print("Buildings: %d   resources with a producer: %d" % (len(buildings), len(first_era)))
    for channel in ("power", "logistics"):
        entries = sorted(suppliers[channel])
        print("  first %-10s supplier: %-24s era=%s" % (
            channel, entries[0][1] if entries else "(none)",
            ERA.get(entries[0][0], "?") if entries else "-"))
    entries = sorted(housing)
    print("  first housing supplier : %-24s era=%s" % (
        entries[0][1] if entries else "(none)", ERA.get(entries[0][0], "?") if entries else "-"))
    print()

    if failures:
        print("=== Static sustainability FAILURES (%d) ===" % len(failures))
        for line in failures:
            print("  " + line)
        print()
        print("These are NECESSARY-condition failures: a building cannot be kept")
        print("running when it unlocks. Fix the definition or the producer order.")
        return 1

    print("Static sustainability holds: every building's maintenance, build cost,")
    print("power, logistics and productivity channel has a supplier at or before")
    print("its own era.")
    print()
    print("This is a NECESSARY condition only. It does not prove supply is")
    print("sufficient, and it does not cover in-run layout or upgrade-chain source")
    print("loss -- those still need Unity acceptance (ToDoList D08).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
