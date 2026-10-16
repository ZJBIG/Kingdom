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
        Assert.That(prefab, Is.Not.Null);
        Button pin = prefab.transform.Find("PinButton")?.GetComponent<Button>();
        Assert.That(pin, Is.Not.Null, suffix);
        AssertPinIcon(pin);
        TMP_Text title = prefab.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text metadata = prefab.transform.Find("Metadata")?.GetComponent<TMP_Text>();
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata.raycastTarget, Is.False);
        Assert.That(metadata.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(title.enableWordWrapping, Is.EqualTo(suffix != "SectorRow"));
        Assert.That(metadata.gameObject.activeSelf, Is.EqualTo(suffix == "SectorRow"));
        Assert.That(title.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Image accent = prefab.transform.Find("EraAccent")?.GetComponent<Image>();
        Assert.That(accent, Is.Not.Null);
        Assert.That(accent.raycastTarget, Is.False);
        Assert.That(prefab.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThan(90));
    }

    private static void AssertPinIcon(Button pin)
    {
        Assert.That(pin.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(pin.GetComponent<Image>().sprite, Is.Null);
        Assert.That(pin.GetComponent<Image>().color.a, Is.LessThan(.01f));
        Assert.That(pin.GetComponentsInChildren<TMP_Text>(true), Is.Empty, "Pin has no text label.");
        Image icon = pin.transform.Find("Icon")?.GetComponent<Image>();
        Assert.That(icon, Is.Not.Null);
        Assert.That(icon.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Pins/PinOutline")));
        Assert.That(icon.preserveAspect, Is.True);
        Assert.That(pin.targetGraphic, Is.SameAs(icon));
        Assert.That(icon.raycastTarget, Is.False, "Invisible larger button receives clicks.");
        Assert.That(icon.rectTransform.sizeDelta.x, Is.InRange(35, 37));
        Assert.That(icon.rectTransform.sizeDelta.y, Is.InRange(35, 37));
        Assert.That(((RectTransform)pin.transform).sizeDelta.x, Is.GreaterThan(icon.rectTransform.sizeDelta.x));
    }

    [Test]
    public void EraAccentsDistinguishEveryPlayableEraAndDetailPinIsAuthored()
    {
        TechLevel[] eras = { TechLevel.Animal, TechLevel.StoneAge, TechLevel.Medieval,
            TechLevel.Industrial, TechLevel.Spacer, TechLevel.Ultra, TechLevel.Archotech };
        for (int i = 0; i < eras.Length; i++)
            for (int j = i + 1; j < eras.Length; j++)
                Assert.That(Vector4.Distance(KingdomUIRoot.GetEraAccent(eras[i]),
                    KingdomUIRoot.GetEraAccent(eras[j])), Is.GreaterThan(.05f));
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        AssertPinIcon(root.transform.Find("SafeAreaRoot/DetailPanel/Surface/PinButton").GetComponent<Button>());
    }
}
