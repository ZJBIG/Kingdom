using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EarlyGameUiPresentationTests
{
    [Test]
    public void HudUsesDistinctAuthoredIconsAndPreservesReadableValueSpace()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Transform grid = root.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid");
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedRowCount));
        Assert.That(layout.constraintCount, Is.EqualTo(2), "HUD is authored as two rows.");
        Assert.That(layout.startAxis, Is.EqualTo(GridLayoutGroup.Axis.Vertical));
        string[] order = { "Food", "Population", "ResearchPower", "Happiness", "Territory", "CurrentResearch", "Power", "Logistics" };
        for (int i = 0; i < order.Length; i++) Assert.That(grid.GetChild(i).name, Is.EqualTo(order[i]));
        var sprites = new System.Collections.Generic.HashSet<Sprite>();
        foreach (string name in new[] { "Food", "Happiness", "Population", "Territory", "ResearchPower", "Power", "Logistics", "CurrentResearch" })
        {
            Transform slot = grid.Find(name);
            Image frame = slot.GetComponent<Image>();
            Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(frame.sprite.name, Is.EqualTo("HudStatusFrame"));
            Image image = slot.Find("Icon").GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null, name);
            Assert.That(sprites.Add(image.sprite), Is.True, "Each metric must have a distinct symbol.");
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(slot.GetComponent<UITouchTooltip>(), Is.Not.Null, "Long press explains each symbol.");
            RectTransform value = slot.Find("Value") as RectTransform;
            Assert.That(value.offsetMin.x, Is.GreaterThan(image.rectTransform.anchoredPosition.x + image.rectTransform.sizeDelta.x));
        }
        Transform tooltip = root.transform.Find("SafeAreaRoot/Tooltip");
        Image tooltipFrame = tooltip.Find("Surface").GetComponent<Image>();
        Assert.That(tooltipFrame.sprite.name, Is.EqualTo("TooltipHelpFrame"));
        Assert.That(tooltipFrame.type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(tooltipFrame.raycastTarget, Is.False);
        RectTransform tooltipText = tooltip.Find("Text") as RectTransform;
        Assert.That(tooltipText.offsetMin.x, Is.GreaterThanOrEqualTo(24));
        Assert.That(tooltipText.offsetMin.y, Is.GreaterThanOrEqualTo(24));
    }

    [Test]
    public void ResourcePinsMatchBuildingGeometryAndEraCardsSelectWithoutDetailButton()
    {
        GameObject building = Resources.Load<GameObject>("UI/Kingdom/KingdomUIBuildingCard");
        RectTransform reference = building.transform.Find("PinButton") as RectTransform;
        foreach (string era in new[] { "", "Animal", "StoneAge", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech" })
        {
            string prefix = era.Length == 0 ? "" : "EraCards/" + era + "/";
            GameObject resource = Resources.Load<GameObject>("UI/Kingdom/" + prefix + "KingdomUIResourceCard");
            RectTransform pin = resource.transform.Find("PinButton") as RectTransform;
            Assert.That(Vector2.Distance(pin.sizeDelta, reference.sizeDelta), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(pin.anchoredPosition.x - reference.anchoredPosition.x), Is.LessThan(.01f));
            Assert.That(Vector2.Distance(((RectTransform)pin.Find("Icon")).sizeDelta, ((RectTransform)reference.Find("Icon")).sizeDelta), Is.LessThan(.01f));
        }
        GameObject card = Resources.Load<GameObject>("UI/Kingdom/KingdomUIEraCard");
        Assert.That(card.GetComponent<Button>(), Is.Not.Null);
        Assert.That(card.transform.Find("DetailAction"), Is.Null);
        Assert.That(card.transform.Find("QueueAction").GetComponent<Button>(), Is.Not.Null);
    }
}
