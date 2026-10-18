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
        Assert.That(rect.rect.width, Is.GreaterThan(rect.rect.height * 2f));
        TMP_Text title = node.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text relationship = node.transform.Find("Status").GetComponent<TMP_Text>();
        Assert.That(title.fontSize, Is.GreaterThan(relationship.fontSize));
        Assert.That(title.rectTransform.rect.height, Is.GreaterThan(relationship.rectTransform.rect.height));
        Assert.That((title.fontStyle & FontStyles.Underline) == 0, Is.True);
    }

    [TestCase("ResourceCard")]
    [TestCase("BuildingCard")]
    [TestCase("WorkshopCard")]
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
        Image accent = prefab.transform.Find("EraAccent")?.GetComponent<Image>();
        Assert.That(accent.raycastTarget, Is.False);
    }
}
