using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KingdomPlannerAuthoring
{
    [MenuItem("Tools/Kingdom/UI/Author Planner Pages")]
    public static void Author()
    {
        const string path = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            KingdomPageBoundaryAuthoring.Apply(root);
            ApplyOverview(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EraCollectionAuthoring.Author();
        EraCollectionArtAuthoring.Author();
        AssetDatabase.SaveAssets();
        Debug.Log("[PlannerPages] Contextual Overview, focused Era and independent era card assets authored.");
    }

    public static void ApplyOverview(GameObject root)
    {
        Transform overview = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview");
        foreach (string name in new[] { "PrimaryCard", "SecondaryCard" })
        {
            GameObject card = overview.Find(name).gameObject;
            KingdomCardPolishAuthoring.ApplyCard(card);
            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(28, 28, 24, 24);
            LayoutElement cardSize = card.GetComponent<LayoutElement>();
            if (cardSize != null) { cardSize.minHeight = -1; cardSize.preferredHeight = -1; cardSize.flexibleHeight = 0; }
            TMP_Text text = card.transform.Find("Text").GetComponent<TMP_Text>();
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontSize = 28;
            LayoutElement textSize = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            textSize.flexibleWidth = 1; textSize.preferredHeight = -1; textSize.minHeight = -1;
            textSize.flexibleHeight = 0;
            if (name == "PrimaryCard")
            {
                text.text = "<b>当前目标</b>\n正在读取当前行动。";
                Button action = card.transform.Find("NavigationButton").GetComponent<Button>();
                LayoutElement actionSize = action.GetComponent<LayoutElement>() ?? action.gameObject.AddComponent<LayoutElement>();
                actionSize.preferredWidth = 300; actionSize.preferredHeight = 64;
                actionSize.flexibleWidth = 0; actionSize.flexibleHeight = 0;
            }
            else
            {
                text.text = "<b>最近进展</b>";
                card.SetActive(false);
            }
        }
        root.transform.Find("SafeAreaRoot/Content/PageTool/OverviewNavigationToolbar").gameObject.SetActive(false);
    }
}
