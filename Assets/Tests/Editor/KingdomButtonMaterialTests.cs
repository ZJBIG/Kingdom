using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class KingdomButtonMaterialTests
{
    [Test]
    public void EveryAuthoredButtonHasARealSlicedMaterialAndPreservesItsLabel()
    {
        var materials = new HashSet<Sprite>();
        foreach (string name in new[] { "NavigationStone", "ActionWood", "ControlMetal" })
        {
            Sprite sprite = Resources.Load<Sprite>("Art/UI/Buttons/" + name);
            Assert.That(sprite.border.sqrMagnitude, Is.GreaterThan(0), name + " needs authored nine-slice borders.");
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
            Assert.That(importer.mipmapEnabled, Is.False);
            materials.Add(sprite);
        }
        materials.Add(Resources.Load<Sprite>("Art/UI/Story/StoryButtonNormal"));
        int checkedButtons = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/UI/Kingdom" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Button button in prefab.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "Backdrop") continue;
                if (button.name == "PinButton" && button.targetGraphic is Image pin)
                {
                    Assert.That(pin.transform.parent, Is.SameAs(button.transform));
                    Assert.That(pin.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Pins/PinOutline")));
                    Assert.That(pin.raycastTarget, Is.False);
                    Assert.That(pin.preserveAspect, Is.True);
                    Assert.That(((RectTransform)button.transform).rect.width, Is.GreaterThan(pin.rectTransform.rect.width));
                    Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                    Assert.That(button.GetComponent<Image>().sprite, Is.Null);
                    Assert.That(button.GetComponent<Image>().color.a, Is.LessThan(.01f));
                    Assert.That(button.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
                    continue;
                }
                if (button.targetGraphic is TMP_Text link)
                {
                    string linkContext = path + "/" + button.name;
                    Assert.That(linkContext == "Assets/Resources/UI/Kingdom/KingdomUIResourceTreeRow.prefab/Label" ||
                        linkContext == "Assets/Resources/UI/Kingdom/KingdomUISectorBuildingCard.prefab/Name" ||
                        linkContext == "Assets/Resources/UI/Kingdom/KingdomUISectorBuildingCard.prefab/Stage", Is.True,
                        "Only the authored production/sector navigation links use text targets.");
                    Assert.That(link.gameObject, Is.SameAs(button.gameObject), linkContext);
                    Assert.That(link.font, Is.Not.Null, linkContext);
                    Assert.That(link.raycastTarget, Is.True, linkContext);
                    Assert.That((link.fontStyle & FontStyles.Underline) != 0,
                        Is.EqualTo(path == "Assets/Resources/UI/Kingdom/KingdomUISectorBuildingCard.prefab"), linkContext);
                    continue;
                }
                Image image = button.targetGraphic as Image;
                if (image != null && image.color.a < .01f && image.sprite == null) continue;
                string context = path + "/" + button.name;
                Assert.That(image.gameObject, Is.SameAs(button.gameObject), context + " skin must remain the button target.");
                bool eraSurface = button.gameObject == prefab && prefab.transform.Find("EraDesign") != null &&
                    path.StartsWith("Assets/Resources/UI/Kingdom/EraCards/");
                if (eraSurface)
                {
                    string eraName = path.Split('/')[5];
                    Assert.That(image.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/EraCards/" + eraName)), context);
                }
                else Assert.That(materials.Contains(image.sprite), Is.True, context + " has no approved material.");
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), context);
                Assert.That(image.sprite.border.sqrMagnitude, Is.GreaterThan(0), context);
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    Assert.That(label.font, Is.Not.Null, context);
                    Assert.That(label.raycastTarget, Is.False, context + " label must not intercept input.");
                }
                checkedButtons++;
            }
        }
        Assert.That(checkedButtons, Is.GreaterThan(50), "The inventory must cover the root, controls, and repeated row prefabs.");
        Assert.That(Resources.Load<GameObject>("UI/Kingdom/KingdomUIButton"), Is.Not.Null,
            "Dynamically bound actions need the same authored button prefab.");
    }

    [Test]
    public void ResearchLabelsKeepHierarchyAndMusicSeparatesSkinFromIcon()
    {
        GameObject node = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResearchNode");
        TMP_Text title = node.transform.Find("Label").GetComponent<TMP_Text>();
        foreach (string name in new[] { "Cost", "Progress", "State" })
        {
            TMP_Text metadata = node.transform.Find(name).GetComponent<TMP_Text>();
            Assert.That(metadata.fontSize, Is.LessThan(title.fontSize));
        }
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Transform music = root.transform.Find("SafeAreaRoot/Content/PageHost/Music");
        int icons = 0;
        foreach (Button button in music.GetComponentsInChildren<Button>(true))
        {
            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null) continue;
            Assert.That(button.targetGraphic, Is.SameAs(button.GetComponent<Image>()));
            Assert.That(icon.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.sprite, Is.Not.Null);
            icons++;
        }
        Assert.That(icons, Is.GreaterThan(2), "Fixed music transport controls must retain their authored icons.");
        GameObject row = Resources.Load<GameObject>("UI/Kingdom/KingdomUIMusicTrack");
        Assert.That(row.transform.Find("PlayPause/Icon").GetComponent<Image>(), Is.Not.Null);
    }
}
