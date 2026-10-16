using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates independent era cards; existing authored variants remain untouched.</summary>
public static class EraCollectionAuthoring
{
    private const string Folder = "Assets/Resources/UI/Kingdom/";

    [MenuItem("Tools/Kingdom/UI/Author Era Collection Cards")]
    public static void Author()
    {
        EnsureFolder(Folder.TrimEnd('/') + "/EraCards");
        foreach (TechLevel era in Enum.GetValues(typeof(TechLevel)))
        {
            string directory = Folder + "EraCards/" + era;
            EnsureFolder(directory);
            foreach (string name in new[] { KingdomUIPrefabLibrary.ResourceCard, KingdomUIPrefabLibrary.BuildingCard })
            {
                string path = directory + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                GameObject card = PrefabUtility.LoadPrefabContents(Folder + name + ".prefab");
                try
                {
                    Design(card, era);
                    PrefabUtility.SaveAsPrefabAsset(card, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(card); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[EraCollection] Seven independent resource/building card designs authored; existing variants preserved.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    private static void Design(GameObject card, TechLevel era)
    {
        // Reserve two title lines while retaining inherited state/action column anchors and pin hit areas.
        LayoutElement layout = card.GetComponent<LayoutElement>();
        layout.preferredHeight = Mathf.Max(144f, layout.preferredHeight);
        ((RectTransform)card.transform).sizeDelta = new Vector2(
            ((RectTransform)card.transform).sizeDelta.x, layout.preferredHeight);
        TMP_Text label = card.transform.Find("Label").GetComponent<TMP_Text>();
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        label.enableAutoSizing = true;
        label.fontSize = label.fontSizeMax = 42;
        label.fontSizeMin = 30;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.rectTransform.anchorMin = new Vector2(label.rectTransform.anchorMin.x, 0);
        label.rectTransform.anchorMax = new Vector2(label.rectTransform.anchorMax.x, 1);
        TMP_Text metadata = card.transform.Find("Metadata").GetComponent<TMP_Text>();
        metadata.gameObject.SetActive(false);
        metadata.enableWordWrapping = true;
        metadata.enableAutoSizing = true;
        metadata.fontSize = metadata.fontSizeMax = 20;
        metadata.fontSizeMin = 16;
        metadata.overflowMode = TextOverflowModes.Ellipsis;
        string texture = era == TechLevel.Animal || era == TechLevel.Medieval
            ? "Backgrounds/NavigationTimber" : era == TechLevel.StoneAge
            ? "Backgrounds/ContentSlate" : "Buttons/ControlMetal";
        Image surface = card.GetComponent<Image>();
        surface.sprite = Resources.Load<Sprite>("Art/UI/" + texture);
        surface.type = Image.Type.Sliced;
        surface.color = era switch
        {
            TechLevel.Animal => new Color(.44f, .40f, .27f, .96f),
            TechLevel.StoneAge => new Color(.68f, .65f, .54f, .96f),
            TechLevel.Medieval => new Color(.57f, .39f, .27f, .96f),
            TechLevel.Industrial => new Color(.46f, .55f, .58f, .96f),
            TechLevel.Spacer => new Color(.25f, .42f, .47f, .96f),
            TechLevel.Ultra => new Color(.39f, .32f, .52f, .96f),
            _ => new Color(.50f, .46f, .31f, .96f)
        };
        Transform oldFrame = card.transform.Find("CardFrame");
        if (oldFrame != null) oldFrame.gameObject.SetActive(false);
        card.transform.Find("EraAccent").gameObject.SetActive(false);
        GameObject design = new GameObject("EraDesign", typeof(RectTransform), typeof(LayoutElement));
        design.transform.SetParent(card.transform, false);
        design.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform bounds = (RectTransform)design.transform;
        bounds.anchorMin = Vector2.zero; bounds.anchorMax = Vector2.one;
        bounds.offsetMin = bounds.offsetMax = Vector2.zero;
        design.transform.SetAsFirstSibling();
        Color accent = KingdomUIRoot.GetEraAccent(era);
        float titleEnd = ((RectTransform)card.transform.Find("Label")).anchorMax.x;
        Image header = Piece(bounds, "HeaderPlate", new Vector2(0, 0), new Vector2(titleEnd, 1),
            new Vector2(68, 2), new Vector2(-4, -8), new Color(.03f, .045f, .035f, .58f));
        // Resource art retains its inherited column, separate from the title plate.
        if (card.transform.Find("Icon") != null) header.rectTransform.offsetMin = new Vector2(148, 2);
        switch (era)
        {
            case TechLevel.Animal:
                Rail(bounds, "TimberUpper", .94f, .04f, .97f, 6, accent);
                Rail(bounds, "TimberLower", .06f, .02f, .94f, 5, accent);
                for (int i = 0; i < 4; i++)
                {
                    Knot(bounds, "Lashing" + i, .025f + i * .315f, .94f, 14, accent, 32);
                    Knot(bounds, "CrossLashing" + i, .025f + i * .315f, .94f, 14, accent, -32);
                }
                break;
            case TechLevel.StoneAge:
                for (int i = 0; i < 9; i++)
                {
                    Piece(bounds, "StoneBlock" + i, new Vector2(i / 9f, .91f),
                        new Vector2((i + 1) / 9f, 1), new Vector2(3, 2), new Vector2(-3, -2), accent);
                    Piece(bounds, "FoundationBlock" + i, new Vector2(i / 9f, 0),
                        new Vector2((i + 1) / 9f, .055f), new Vector2(4, 1), new Vector2(-4, -1), accent * .65f);
                }
                break;
            case TechLevel.Medieval:
                Rail(bounds, "OakBeam", .04f, .015f, .985f, 8, accent);
                for (int i = 0; i < 3; i++)
                {
                    float x = .025f + .475f * i;
                    Knot(bounds, "ArchLeft" + i, x, .93f, 22, accent, 40);
                    Knot(bounds, "ArchRight" + i, x + .018f, .93f, 22, accent, -40);
                    Knot(bounds, "BronzeBoss" + i, x + .009f, .06f, 8, accent, 45);
                }
                break;
            case TechLevel.Industrial:
                Rail(bounds, "SteelUpperFlange", .96f, 0, 1, 8, accent * .72f);
                Rail(bounds, "SteelLowerFlange", .035f, 0, 1, 8, accent * .72f);
                for (int i = 0; i < 8; i++)
                {
                    Knot(bounds, "UpperRivet" + i, .02f + i * .137f, .96f, 5, accent, 0);
                    Knot(bounds, "LowerRivet" + i, .02f + i * .137f, .035f, 5, accent, 0);
                }
                break;
            case TechLevel.Spacer:
                for (int i = 0; i < 4; i++)
                {
                    Rail(bounds, "HUDTopSegment" + i, .97f, i * .25f + .02f, i * .25f + .20f, 3, accent);
                    Rail(bounds, "HUDBottomSegment" + i, .03f, i * .25f + .06f, i * .25f + .24f, 2, accent);
                }
                Piece(bounds, "HUDLeftBracket", Vector2.zero, new Vector2(0, 1), new Vector2(2, 12), new Vector2(5, -12), accent);
                Piece(bounds, "HUDRightBracket", new Vector2(1, 0), Vector2.one, new Vector2(-5, 12), new Vector2(-2, -12), accent);
                break;
            case TechLevel.Ultra:
                Rail(bounds, "PhaseOuterRail", .97f, .025f, .77f, 2, accent);
                Rail(bounds, "PhaseInnerRail", .91f, .10f, .88f, 1, accent);
                Rail(bounds, "PhaseReturn", .04f, .18f, .98f, 2, accent);
                for (int i = 0; i < 3; i++) Knot(bounds, "PhaseCrystal" + i, .81f + i * .065f, .96f, 8, accent, 45);
                break;
            case TechLevel.Archotech:
                for (int i = 0; i < 5; i++)
                {
                    Rail(bounds, "AscendingFragment" + i, .82f + i * .035f, .02f + i * .075f, .075f + i * .075f, 3, accent);
                    Rail(bounds, "DescendingFragment" + i, .16f - i * .028f, .62f + i * .065f, .66f + i * .065f, 2, accent);
                }
                Knot(bounds, "FloatingCore", .975f, .86f, 12, accent, 45);
                Knot(bounds, "OrbitShard", .94f, .94f, 6, accent, 20);
                break;
        }
    }

    private static Image Piece(Transform parent, string name, Vector2 min, Vector2 max,
        Vector2 insetMin, Vector2 insetMax, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color; image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = insetMin; rect.offsetMax = insetMax;
        return image;
    }

    private static void Rail(Transform parent, string name, float y, float left, float right, float thickness, Color color) =>
        Piece(parent, name, new Vector2(left, y), new Vector2(right, y),
            new Vector2(0, -thickness / 2), new Vector2(0, thickness / 2), color);

    private static void Knot(Transform parent, string name, float x, float y, float size, Color color, float angle)
    {
        Image image = Piece(parent, name, new Vector2(x, y), new Vector2(x, y),
            new Vector2(-size / 2, -2), new Vector2(size / 2, 2), color);
        image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
    }
}
