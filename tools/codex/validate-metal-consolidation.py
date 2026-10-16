"""Read-only validation of the consolidated metal definitions and Sprite metadata."""
import json
from decimal import Decimal
from pathlib import Path
import re
import struct
import yaml

METAL_IDS = ("BasicMetalOre", "AdvancedMetalOre", "SpecialMetalOre", "BasicMetalIngot",
             "AdvancedMetalIngot", "Steel", "TitaniumAlloy")


def document(text):
    plain = "\n".join("---" if line.startswith("--- !u!") else line
                      for line in text.splitlines() if not line.startswith("%"))
    return yaml.safe_load(plain)["MonoBehaviour"]


def guid(path):
    return re.search(r"^guid: ([0-9a-f]{32})", path.read_text(encoding="utf-8-sig"), re.M)[1]


def validate(root):
    definitions = {}
    resources = {}
    for path in (root / "Assets/Resources/Datas").rglob("*.asset"):
        data = document(path.read_text(encoding="utf-8-sig"))
        definitions[path] = data
        if "Resource" in path.relative_to(root / "Assets/Resources/Datas").parts:
            resources[data["id"]] = (path, data, guid(path.with_suffix(".asset.meta")))
    for identity in METAL_IDS:
        path, data, _ = resources[identity]
        assert path.stem == data["m_Name"] == identity, identity
    for identity, (path, data, _) in resources.items():
        assert "inventoryResource" not in data, identity
        assert path.stem == identity, identity
    pair_groups = 0
    sources, sinks = set(), set()

    def check(value, context):
        nonlocal pair_groups
        if isinstance(value, dict):
            for key, item in value.items():
                check(item, context + "/" + key)
        elif isinstance(value, list):
            pairs = [item for item in value if isinstance(item, dict) and "resource" in item and "amount" in item]
            if pairs:
                ids = [item["resource"]["guid"] for item in pairs]
                assert len(ids) == len(set(ids)), "Duplicate pair: " + context
                pair_groups += 1
                positive = {item["resource"]["guid"] for item in pairs if Decimal(str(item["amount"])) > 0}
                if context.rsplit("/", 1)[-1] in ("resourceGenerationRates", "occupiedResourceRatesPerSecond", "resourceRewards"):
                    sources.update(positive)
                else:
                    sinks.update(positive)
            for item in value:
                check(item, context)

    for path, data in definitions.items():
        check(data, str(path.relative_to(root)))
        produced = {item["resource"]["guid"] for item in data.get("resourceGenerationRates", [])}
        consumed = {item["resource"]["guid"] for item in data.get("resourceConsumptionRates", [])}
        assert not produced & consumed, "Self-consuming flow: " + str(path)
        effect_targets = set()
        for effect in data.get("effects", []):
            resource = effect.get("Resource", {}).get("guid")
            if resource:
                key = (effect["Type"], resource, effect.get("Building", {}).get("guid"))
                assert key not in effect_targets, "Duplicated effect: " + str(path)
                effect_targets.add(key)
    for identity, (_, data, resource_guid) in resources.items():
        assert resource_guid in sources, "Resource without source: " + identity
        assert resource_guid in sinks, "Resource without consumer: " + identity
    art = json.loads((root / "data/metal-consolidation-20261014/art-source.json").read_text(encoding="utf-8-sig"))
    atlas = root / art["atlas"]
    metadata = yaml.safe_load(atlas.with_suffix(".png.meta").read_text(encoding="utf-8-sig"))
    assert metadata["guid"] == art["atlas_guid"]
    width, height = struct.unpack(">II", atlas.read_bytes()[16:24])
    sprites = metadata["TextureImporter"]["spriteSheet"]["sprites"]
    assert {sprite["name"]: sprite["internalID"] for sprite in sprites} == art["sprite_ids"]
    for sprite in sprites:
        rect = sprite["rect"]
        assert rect["width"] > 0 and rect["height"] > 0
        assert 0 <= rect["x"] <= width - rect["width"]
        assert 0 <= rect["y"] <= height - rect["height"]
    for identity, sprite_id in zip(METAL_IDS, art["sprite_ids"].values()):
        reference = resources[identity][1]["Sprite"]
        assert reference == {"fileID": sprite_id, "guid": art["atlas_guid"], "type": 3}, identity
    return {"definitions": len(definitions), "resource_identities": len(resources),
            "inventories": len(resources), "pair_groups": pair_groups,
            "sprite_rects": len(sprites), "atlas_size": [width, height]}


if __name__ == "__main__":
    print(json.dumps(validate(Path(__file__).resolve().parents[2]), indent=2))
