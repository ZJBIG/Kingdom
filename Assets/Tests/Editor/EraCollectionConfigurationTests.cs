using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EraCollectionConfigurationTests
{
    [TestCase(KingdomUIPrefabLibrary.ResourceCard)]
    [TestCase(KingdomUIPrefabLibrary.BuildingCard)]
    public void EveryEraHasAnIndependentMaterialAndUsableAuthoredCard(string kind)
    {
        var prefabs = new HashSet<GameObject>();
        var artwork = new HashSet<Sprite>();
        foreach (TechLevel era in Enum.GetValues(typeof(TechLevel)))
        {
            string name = KingdomUIRoot.GetEraCollectionPrefabName(kind, era);
            GameObject prefab = KingdomUIPrefabLibrary.Load(name);
            Assert.That(prefabs.Add(prefab), Is.True, "Each era must resolve its own asset: " + era);
            Assert.That(prefab.GetComponent<Image>().sprite, Is.Not.Null, era.ToString());
            Sprite art = prefab.GetComponent<Image>().sprite;
            Assert.That(art, Is.SameAs(Resources.Load<Sprite>("Art/UI/EraCards/" + era)));
            Assert.That(artwork.Add(art), Is.True, "Each era uses independent generated artwork.");
            Assert.That(art.border.sqrMagnitude, Is.GreaterThan(0), "Sliced edges preserve the period-specific frame.");
            Assert.That(prefab.GetComponent<Image>().color.a, Is.GreaterThan(.85f));
            Assert.That(prefab.GetComponent<Button>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UIPageScrollDragForwarder>(), Is.Not.Null);
            foreach (string field in new[] { "Label", "Metadata", "Amount" })
                Assert.That(prefab.transform.Find(field).GetComponent<TMP_Text>(), Is.Not.Null, field);
            TMP_Text label = prefab.transform.Find("Label").GetComponent<TMP_Text>();
            Assert.That(label.enableWordWrapping, Is.True);
            Assert.That(label.enableAutoSizing, Is.True);
            Assert.That(prefab.transform.Find("Metadata").gameObject.activeSelf, Is.False);
            Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            if (kind == KingdomUIPrefabLibrary.ResourceCard)
            {
                Assert.That(prefab.transform.Find("Icon").GetComponent<Image>(), Is.Not.Null);
                Assert.That(prefab.transform.Find("ChangeRate").GetComponent<TMP_Text>(), Is.Not.Null);
            }
            else
            {
                foreach (string field in new[] { "TechLevel", "Effect" })
                    Assert.That(prefab.transform.Find(field).GetComponent<TMP_Text>(), Is.Not.Null, field);
                foreach (string action in new[] { "BuildButton", "DeconstructButton" })
                    Assert.That(prefab.transform.Find(action).GetComponent<Button>(), Is.Not.Null, action);
            }
            Assert.That(prefab.transform.Find("PinButton").GetComponent<Button>(), Is.Not.Null);
        }
    }

    [TestCase(KingdomUIPrefabLibrary.ResourceCard)]
    [TestCase(KingdomUIPrefabLibrary.BuildingCard)]
    public void BindingEraStylePreservesIndependentAuthoredSurfaceAndDecorations(string kind)
    {
        foreach (TechLevel era in Enum.GetValues(typeof(TechLevel)))
        {
            GameObject prefab = KingdomUIPrefabLibrary.Load(KingdomUIRoot.GetEraCollectionPrefabName(kind, era));
            GameObject row = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Image surface = row.GetComponent<Image>();
                Color original = surface.color;
                Sprite originalSprite = surface.sprite;
                Transform header = row.transform.Find("EraDesign/HeaderPlate");
                Color headerColor = header.GetComponent<Image>().color;
                KingdomUIRoot.ApplyEraRowStyle(row, era, 0);
                KingdomUIRoot.ApplyEraRowStyle(row, era, 1);
                Assert.That(Vector4.Distance(surface.color, original), Is.LessThan(.0001f),
                    "Binding must retain authored material values.");
                Assert.That(surface.sprite, Is.SameAs(originalSprite));
                Assert.That(Vector4.Distance(header.GetComponent<Image>().color, headerColor), Is.LessThan(.0001f));
                Assert.That(Vector4.Distance(row.GetComponent<Button>().colors.normalColor, Color.white), Is.LessThan(.0001f),
                    "Hover feedback must not multiply the normal material tint.");
                Assert.That(row.transform.Find("EraDesign").gameObject.activeSelf, Is.False,
                    "Generated frame artwork replaces the earlier procedural decoration.");
            }
            finally { UnityEngine.Object.DestroyImmediate(row); }
        }
    }
    [Test]
    public void BuildingCapacityIconsResolveThroughTheConfiguredSpriteResourcePath()
    {
        var canvas = new GameObject("CapacityIconTest", typeof(RectTransform), typeof(Canvas));
        try
        {
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1600f, 400f);
            GameObject row = UnityEngine.Object.Instantiate(
                KingdomUIPrefabLibrary.Load(KingdomUIPrefabLibrary.BuildingCard), canvas.transform);
            TMP_Text text = row.transform.Find("Effect").GetComponent<TMP_Text>();
            TMP_SpriteAsset icons = Resources.Load<TMP_SpriteAsset>("Sprite Assets/BuildingStatusIcons");
            Assert.That(icons.spriteCharacterTable.Count, Is.EqualTo(6), "The atlas defines exactly six status symbols.");
            Assert.That(text.font, Is.Not.Null);
            Assert.That(text.richText, Is.True);
            text.text = "<sprite=\"BuildingStatusIcons\" index=0> <sprite=\"BuildingStatusIcons\" index=1> <sprite=\"BuildingStatusIcons\" index=2> <sprite=\"BuildingStatusIcons\" index=3> <sprite=\"BuildingStatusIcons\" index=4> <sprite=\"BuildingStatusIcons\" index=5>";
            Canvas.ForceUpdateCanvases();
            TMP_TextInfo parsed = text.GetTextInfo(text.text);
            // TMP 3.0.9 overwrites spriteCount during mesh generation; inspect the parsed elements.
            Assert.That(parsed.characterCount, Is.EqualTo(11), "Six sprites separated by five spaces must resolve.");
            for (int i = 0; i < 6; i++)
            {
                TMP_CharacterInfo character = parsed.characterInfo[i * 2];
                Assert.That(character.elementType, Is.EqualTo(TMP_TextElementType.Sprite));
                Assert.That(character.spriteAsset, Is.SameAs(icons));
                Assert.That(character.spriteIndex, Is.EqualTo(i), "Each tag must resolve its exact glyph index.");
            }
            Assert.That(icons.material.mainTexture, Is.SameAs(icons.spriteSheet));
            for (int i = 0; i < 6; i++)
            {
                var rect = icons.spriteGlyphTable[i].glyphRect;
                Assert.That(rect.width, Is.GreaterThan(0));
                Assert.That(rect.x + rect.width, Is.LessThanOrEqualTo(icons.spriteSheet.width));
                Assert.That(rect.y + rect.height, Is.LessThanOrEqualTo(icons.spriteSheet.height));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }

}
