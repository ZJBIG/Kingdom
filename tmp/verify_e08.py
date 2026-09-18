import os, re, glob
root = r"D:/GitHub/Kingdom/Assets/Resources/Datas"
# guid -> resource id
guid2id = {}
for meta in glob.glob(os.path.join(root, "Resource", "**", "*.asset.meta"), recursive=True):
    g = re.search(r"^guid:/s*(/w+)", open(meta, encoding="utf-8").read(), re.M)
    if not g: continue
    asset = meta[:-5]
    if not os.path.exists(asset): continue
    m = re.search(r"^  id:/s*(/S+)", open(asset, encoding="utf-8").read(), re.M)
    if m: guid2id[g.group(1)] = m.group(1)

def show(name):
    p = os.path.join(root, "Building", "Industrial", name + ".asset")
    text = open(p, encoding="utf-8").read()
    print("="*10, name)
    for key in ("resourceGenerationRates", "resourceConsumptionRates", "resourceRequirements"):
        blk = re.search(r"^  %s:/s*$((?:/n  - .*|\n    .*)*)" % key, text, re.M)
        print("--", key)
        if not blk or not blk.group(1).strip():
            print("   (empty)"); continue
        for entry in re.findall(r"resource: \{fileID: \d+, guid: (\w+)", blk.group(1)):
            pass
        lines = blk.group(1).strip().split("\n")
        cur = None
        for ln in lines:
            g = re.search(r"guid: (\w+)", ln)
            if g: cur = guid2id.get(g.group(1), "UNKNOWN:" + g.group(1))
            r = re.search(r"^\s+(amount|rate):\s*\"?([\d.]+)\"?", ln)
            if r and cur: print("   %-24s %s=%s" % (cur, r.group(1), r.group(2)))
    print("-- upgradeTo:", re.search(r"^  upgradeTo:/s*(.*)$", text, re.M).group(1).strip())
    print("-- requiredResearch:", re.findall(r"^  requiredResearch:/s*/n((?:/s+-.*/n)*)", text, re.M))

for n in ("MachineFactory", "WireMill", "BuildingMaterialsComplex"):
    show(n)
