import glob
import os
import re

DATAS = r"D:\GitHub\Kingdom\Assets\Resources\Datas"

TARGETS = ("University", "DeepSpaceObservatory")

guids = {}
for asset in glob.glob(os.path.join(DATAS, "Building", "**", "*.asset"), recursive=True):
    text = open(asset, encoding="utf-8").read()
    ident = re.search(r"^\s+id:\s*(\S+)", text, re.M)
    if ident and ident.group(1) in TARGETS:
        meta = open(asset + ".meta", encoding="utf-8").read()
        guids[ident.group(1)] = re.search(r"^guid:\s*(\w+)", meta, re.M).group(1)

print("目标建筑 GUID:", guids)
print()

print("== 建筑本体关键字段 ==")
for asset in glob.glob(os.path.join(DATAS, "Building", "**", "*.asset"), recursive=True):
    text = open(asset, encoding="utf-8").read()
    ident = re.search(r"^\s+id:\s*(\S+)", text, re.M)
    if not ident or ident.group(1) not in TARGETS:
        continue
    print("--", ident.group(1))
    for key in ("TechLevel", "researchPowerGranted", "productivityConsumption", "spaceCost",
                "powerConsumptionRate", "logisticsConsumptionRate", "fleetPowerGranted",
                "attackPowerGranted", "defensePowerGranted"):
        m = re.search(r"^  %s:\s*\"?([^\s\"]+)\"?" % key, text, re.M)
        if m:
            print("   %-26s %s" % (key, m.group(1)))
    up = re.search(r"^  upgradeTo:\s*\{fileID: \d+, guid: (\w+)", text, re.M)
    print("   %-26s %s" % ("upgradeTo", "有" if up else "空"))

print()
print("== 指向它们的定向效果 ==")
for folder in ("Research", "Workshop"):
    for asset in sorted(glob.glob(os.path.join(DATAS, folder, "**", "*.asset"), recursive=True)):
        text = open(asset, encoding="utf-8").read()
        for name, guid in guids.items():
            if guid not in text:
                continue
            ident = re.search(r"^\s+id:\s*(\S+)", text, re.M)
            label = ident.group(1) if ident else os.path.basename(asset)
            body = text.split("effects:", 1)[-1] if "effects:" in text else ""
            for blk in re.findall(r"- Type:\s*(\d+)([\s\S]{0,240}?)value:\s*\"?([0-9.]+)\"?", body):
                if guid in blk[1]:
                    print("   %-9s %-30s -> %-22s Type=%s value=%s"
                          % (folder, label, name, blk[0], blk[2]))
            break
