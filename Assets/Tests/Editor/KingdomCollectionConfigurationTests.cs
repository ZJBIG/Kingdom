using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class KingdomCollectionConfigurationTests
{
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
        Assert.That(pin.targetGraphic.raycastTarget, Is.True, "Pin must receive its own pointer click.");
        TMP_Text title = prefab.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text metadata = prefab.transform.Find("Metadata")?.GetComponent<TMP_Text>();
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata.raycastTarget, Is.False);
        Assert.That(metadata.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(title.enableWordWrapping, Is.False);
        Assert.That(title.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Image accent = prefab.transform.Find("EraAccent")?.GetComponent<Image>();
        Assert.That(accent, Is.Not.Null);
        Assert.That(accent.raycastTarget, Is.False);
        Assert.That(prefab.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThan(90));
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
        Assert.That(root.transform.Find("SafeAreaRoot/DetailPanel/Surface/PinButton")
            ?.GetComponent<Button>(), Is.Not.Null);
    }
}
