using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class KingdomCollectionConfigurationTests
{
    [Test]
    public void ProductionNodesUseTexturedSurfacesAndCompactCaptionSlots()
    {
        GameObject node = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeRow");
        Image surface = node.GetComponent<Image>();
        Assert.That(surface.sprite, Is.Not.Null, "Production nodes must use an authored texture.");
        Assert.That(surface.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(surface.color.a, Is.GreaterThan(.9f));
        RectTransform rect = node.GetComponent<RectTransform>();
        Assert.That(rect.rect.height, Is.GreaterThan(rect.rect.width),
            "Production nodes use the portrait illustration card layout.");
        RectTransform artwork = node.transform.Find("Icon").GetComponent<RectTransform>();
        Assert.That(artwork.rect.width, Is.GreaterThan(0f));
        Assert.That(artwork.rect.height, Is.EqualTo(artwork.rect.width).Within(.01f));
        TMP_Text title = node.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text relationship = node.transform.Find("Status").GetComponent<TMP_Text>();
        Assert.That(title.fontSize, Is.GreaterThan(relationship.fontSize));
        Assert.That(title.rectTransform.rect.height, Is.GreaterThan(relationship.rectTransform.rect.height));
        Assert.That((title.fontStyle & FontStyles.Underline) == 0, Is.True);
    }

    [Test]
    public void SectorExpansionUsesCornerControlWithoutVerticalDivider()
    {
        Transform row = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorRow").transform;
        RectTransform button = row.Find("Buildings").GetComponent<RectTransform>();
        Assert.That(button.anchorMin, Is.EqualTo(Vector2.one));
        Assert.That(button.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(button.sizeDelta.x, Is.GreaterThan(button.sizeDelta.y));
        Assert.That(button.sizeDelta.x, Is.LessThan(120f));
        TMP_Text label = button.Find("Label").GetComponent<TMP_Text>();
        Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        Assert.That(label.text.Length, Is.EqualTo(1), "Only the expand glyph belongs in the corner control.");
        Assert.That(label.font.HasCharacter('▲', true, true), Is.True);
        Assert.That(label.font.HasCharacter('▼', true, true), Is.True);
        Transform menu = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorBuildingMenu").transform;
        Assert.That(menu.Find("Heading"), Is.Null);
        Assert.That(menu.Find("Divider"), Is.Null);
    }

    [Test]
    public void SectorFacilitiesMatchOrdinaryBuildingSizeAndOmitPin()
    {
        GameObject sector = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorBuildingCard");
        GameObject ordinary = Resources.Load<GameObject>("UI/Kingdom/KingdomUIBuildingCard");
        Assert.That(sector.transform.Find("PinButton"), Is.Null);
        Assert.That(sector.GetComponent<LayoutElement>().preferredHeight,
            Is.EqualTo(ordinary.GetComponent<LayoutElement>().preferredHeight).Within(.01f));
        RectTransform sectorIcon = sector.transform.Find("BuildingIcon").GetComponent<RectTransform>();
        RectTransform ordinaryIcon = ordinary.transform.Find("BuildingIcon").GetComponent<RectTransform>();
        Assert.That(Vector2.Distance(sectorIcon.sizeDelta, ordinaryIcon.sizeDelta), Is.LessThan(.01f));
        Assert.That(sector.transform.Find("Build").GetComponent<Button>(), Is.Not.Null);
        Assert.That(sector.transform.Find("ToggleEnabled").GetComponent<Button>(), Is.Not.Null);
        Assert.That(sector.transform.Find("Stage"), Is.Null);
        Assert.That(sector.transform.Find("Effect"), Is.Null);
        Assert.That(sector.transform.Find("Cost"), Is.Null);
        Assert.That(sector.transform.Find("Build/MegastructureIcon").GetComponent<Image>().sprite,
            Is.SameAs(Resources.Load<Sprite>("Art/UI/Buttons/MegastructureBuild")));
    }

    [TestCase("HudFood")]
    [TestCase("HudFoodStorage")]
    [TestCase("HudPopulation")]
    [TestCase("HudResearchPower")]
    [TestCase("HudProductivity")]
    [TestCase("HudPower")]
    [TestCase("HudLogistics")]
    public void BuildingDetailIconsReuseHudTextureAndHaveValidGlyph(string name)
    {
        TMP_SpriteAsset asset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/" + name);
        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.spriteSheet, Is.SameAs(Resources.Load<Texture2D>("Art/UI/Status/" + name)));
        Assert.That(asset.material.mainTexture, Is.SameAs(asset.spriteSheet));
        Assert.That(asset.spriteCharacterTable.Count, Is.EqualTo(1));
        Assert.That(asset.spriteGlyphTable.Count, Is.EqualTo(1));
        Assert.That(asset.spriteGlyphTable[0].glyphRect.width, Is.EqualTo(asset.spriteSheet.width));
        Assert.That(asset.spriteGlyphTable[0].glyphRect.height, Is.EqualTo(asset.spriteSheet.height));
    }

    [Test]
    public void SectorFramesUseDistinctSlicedNebulaSurfaces()
    {
        GameObject row = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorRow");
        GameObject card = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorBuildingCard");
        Image parent = row.transform.Find("SectorDesign/Surface").GetComponent<Image>();
        Image child = card.transform.Find("SectorDesign/Surface").GetComponent<Image>();
        Assert.That(parent.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Sectors/SectorPanel")));
        Assert.That(child.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Sectors/FacilityPanel")));
        Assert.That(child.sprite, Is.Not.SameAs(parent.sprite));
        Assert.That(parent.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(child.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(child.pixelsPerUnitMultiplier, Is.GreaterThan(parent.pixelsPerUnitMultiplier));
        Assert.That(card.transform.Find("Metadata"), Is.Null, "Facility cards omit redundant construction status.");
        GameObject menu = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorBuildingMenu");
        Assert.That(menu.transform.Find("Divider"), Is.Null);
        foreach (GameObject prefab in new[] { row, card })
        {
            Assert.That(prefab.GetComponent<Image>().color.a, Is.LessThan(.01f));
            Assert.That(prefab.transform.Find("SectorDesign/Starfield"), Is.Null);
            Assert.That(prefab.transform.Find("SectorDesign/UpperGlow"), Is.Null);
            Assert.That(prefab.transform.Find("SectorDesign/LowerGlow"), Is.Null);
        }
    }

    [Test]
    public void DetailResourceRowsUseAnAuthoredSlicedShellAndSingleLineAmounts()
    {
        GameObject row = KingdomUIPrefabLibrary.Load(KingdomUIPrefabLibrary.DetailResourceRow);
        Image shell = row.GetComponent<Image>();
        Assert.That(shell.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Sectors/DetailResourcePanel")));
        Assert.That(shell.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(shell.raycastTarget, Is.False);
        foreach (string name in new[] { "Label", "Amount" })
        {
            TMP_Text text = row.transform.Find(name).GetComponent<TMP_Text>();
            Assert.That(text.enableWordWrapping, Is.False);
            Assert.That(text.enableAutoSizing, Is.True);
            Assert.That(text.raycastTarget, Is.False);
        }
    }

    [TestCase("ResourceCard")]
    [TestCase("BuildingCard")]
    [TestCase("SectorRow")]
    public void AuthoredCollectionRowsProvidePinMetadataAndClippedTitles(string suffix)
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUI" + suffix);
        Assert.That(prefab.transform.Find("PinButton")?.GetComponent<Button>(), Is.Not.Null);
        TMP_Text title = prefab.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text metadata = prefab.transform.Find("Metadata")?.GetComponent<TMP_Text>();
        Assert.That(metadata.raycastTarget, Is.False);
        Assert.That(metadata.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(title.enableWordWrapping, Is.EqualTo(suffix != "SectorRow"));
        Assert.That(metadata.gameObject.activeSelf, Is.EqualTo(suffix == "SectorRow"));
        Assert.That(title.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Image surface = suffix == "SectorRow"
            ? prefab.transform.Find("SectorDesign/Surface").GetComponent<Image>()
            : prefab.GetComponent<Image>();
        Assert.That(surface.sprite, Is.Not.Null, "Rows retain their authored card surface.");
        foreach (string decoration in new[] { "EraAccent", "CardFrame", "SectorDesign" })
        {
            Transform root = prefab.transform.Find(decoration);
            if (root == null) continue;
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                Assert.That(image.raycastTarget, Is.False, image.name + " decoration must not block row input.");
        }
    }

    [Test]
    public void WorkshopCardUsesBuildingGeometryWithEraAndPurchaseButNoPin()
    {
        GameObject workshop = Resources.Load<GameObject>("UI/Kingdom/KingdomUIWorkshopCard");
        GameObject building = Resources.Load<GameObject>("UI/Kingdom/KingdomUIBuildingCard");
        GameObject timber = Resources.Load<GameObject>("UI/Kingdom/EraCards/Animal/KingdomUIBuildingCard");
        Assert.That(workshop.GetComponent<Image>().sprite, Is.SameAs(timber.GetComponent<Image>().sprite));
        Assert.That(workshop.GetComponent<Image>().color.a, Is.GreaterThan(.99f));
        Assert.That(workshop.transform.Find("EraDesign/TimberUpper"), Is.Not.Null,
            "Workshop cards share the ordinary clay-pit timber frame rather than a tinted flat panel.");
        Assert.That(workshop.transform.Find("PinButton"), Is.Null);
        Assert.That(workshop.transform.Find("DeconstructButton"), Is.Null);
        Assert.That(workshop.GetComponent<LayoutElement>().preferredHeight,
            Is.EqualTo(building.GetComponent<LayoutElement>().preferredHeight).Within(.01f));
        RectTransform icon = workshop.transform.Find("WorkshopIcon").GetComponent<RectTransform>();
        RectTransform buildingIcon = building.transform.Find("BuildingIcon").GetComponent<RectTransform>();
        Assert.That(Vector2.Distance(icon.sizeDelta, buildingIcon.sizeDelta), Is.LessThan(.01f));
        Assert.That(Vector2.Distance(icon.anchoredPosition, buildingIcon.anchoredPosition), Is.LessThan(.01f));
        foreach (string name in new[] { "Label", "TechLevel" })
        {
            TMP_Text text = workshop.transform.Find(name).GetComponent<TMP_Text>();
            TMP_Text expected = building.transform.Find(name).GetComponent<TMP_Text>();
            Assert.That(text.fontSize, Is.EqualTo(expected.fontSize).Within(.01f));
            Assert.That(Vector2.Distance(text.rectTransform.anchorMin, expected.rectTransform.anchorMin), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(text.rectTransform.anchorMax, expected.rectTransform.anchorMax), Is.LessThan(.001f));
        }
        Assert.That(workshop.transform.Find("Effect").GetComponent<TMP_Text>(), Is.Not.Null);
        Button purchase = workshop.transform.Find("PurchaseButton").GetComponent<Button>();
        Assert.That(purchase, Is.Not.Null);
        Assert.That(Vector2.Distance(((RectTransform)purchase.transform).sizeDelta,
            building.transform.Find("BuildButton").GetComponent<RectTransform>().sizeDelta), Is.LessThan(.01f));
    }
}
