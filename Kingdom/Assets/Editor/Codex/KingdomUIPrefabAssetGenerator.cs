using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the reusable UI templates used by the runtime Kingdom UI. Data
/// objects are still instantiated from definitions; visual GameObjects are
/// never authored one-off for an individual research item.
/// </summary>
internal static class KingdomUIPrefabAssetGenerator
{
    private const string Folder = "Assets/Resources/UI/Kingdom";

    [InitializeOnLoadMethod]
    private static void ScheduleEnsure()
    {
        EditorApplication.delayCall += EnsureTemplatesExist;
    }

    [MenuItem("Tools/Kingdom/UI/Generate Reusable Prefabs")]
    private static void GenerateFromMenu() => GenerateMissingTemplates();

    public static void Generate() => GenerateMissingTemplates();

    private static void EnsureTemplatesExist()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            return;
        GenerateMissingTemplates();
    }

    private static void GenerateMissingTemplates()
    {
        bool changed = false;
        // Existing ResearchNode assets are intentionally not re-saved during
        // editor startup. Unity can retain a stale in-memory prefab object
        // with a missing component and write it back during an automatic
        // migration. The checked-in asset is already authored and validated;
        // only create it when the asset is genuinely absent.
        changed |= SaveIfMissing(KingdomUIPrefabLibrary.ResearchNode, CreateResearchNodeTemplate);
        changed |= SaveIfMissing(KingdomUIPrefabLibrary.ResearchLine, CreateResearchLineTemplate);
        changed |= SaveIfMissing(KingdomUIPrefabLibrary.ResearchGraph, CreateResearchGraphTemplate);
        // The row/card generator owns the data-driven list templates. Run it
        // before validating the shared prefab registry so startup validation
        // cannot report a transient missing TextRow/FlowRow asset.
        KingdomUIRepeatedPrefabGenerator.GenerateAllForBatch();
        if (changed)
            AssetDatabase.SaveAssets();
        ValidateReusablePrefabAssets();
    }

    private static bool EnsureResearchNodePrefab()
    {
        string path = Folder + "/" + KingdomUIPrefabLibrary.ResearchNode + ".prefab";
        if (!File.Exists(path))
        {
            GameObject template = CreateResearchNodeTemplate();
            PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);
            Debug.Log("[KingdomUI] Reusable UI prefab generated: " + path);
            return true;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        bool changed = false;
        try
        {
            int removedMissingScripts = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            if (removedMissingScripts > 0)
            {
                changed = true;
                Debug.Log("[KingdomUI] Removed missing scripts from ResearchNode prefab: " + removedMissingScripts);
            }
            changed |= EnsureComponent<Image>(root);
            Button button = EnsureComponent<Button>(root, ref changed);
            changed |= EnsureComponent<Outline>(root);
            changed |= EnsureComponent<UIResearchGraphDragForwarder>(root);
            changed |= EnsureComponent<UITouchTooltip>(root);

            Image surface = root.GetComponent<Image>();
            if (button != null && button.targetGraphic == null)
            {
                button.targetGraphic = surface;
                changed = true;
            }

            RectTransform rootRect = root.transform as RectTransform;
            if (rootRect != null && rootRect.sizeDelta == Vector2.zero)
            {
                rootRect.sizeDelta = new Vector2(205f, 50f);
                changed = true;
            }

            RectTransform frame = EnsureChild(rootRect, "EraFrame", ref changed);
            if (frame != null)
            {
                changed |= EnsureComponent<Image>(frame.gameObject);
                Image frameImage = frame.GetComponent<Image>();
                if (frameImage != null && frameImage.raycastTarget)
                {
                    frameImage.raycastTarget = false;
                    changed = true;
                }
                RectTransform progress = EnsureChild(frame, "ProgressFill", ref changed);
                if (progress != null)
                {
                    changed |= EnsureComponent<Image>(progress.gameObject);
                    Image progressImage = progress.GetComponent<Image>();
                    if (progressImage != null && (progressImage.type != Image.Type.Filled || progressImage.raycastTarget))
                    {
                        progressImage.type = Image.Type.Filled;
                        progressImage.fillMethod = Image.FillMethod.Horizontal;
                        progressImage.fillOrigin = 0;
                        progressImage.fillAmount = 0f;
                        progressImage.raycastTarget = false;
                        changed = true;
                    }
                }
            }

            int remainingMissingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            if (remainingMissingScripts > 0)
            {
                Debug.LogError("[KingdomUI] Refusing to save ResearchNode prefab with missing scripts: " + remainingMissingScripts);
                return false;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[KingdomUI] Reusable ResearchNode prefab migrated: " + path);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return changed;
    }

    private static bool EnsureComponent<T>(GameObject target) where T : Component
    {
        bool changed = false;
        EnsureComponent<T>(target, ref changed);
        return changed;
    }

    private static T EnsureComponent<T>(GameObject target, ref bool changed) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;
        changed = true;
        return target.AddComponent<T>();
    }

    private static RectTransform EnsureChild(RectTransform parent, string name, ref bool changed)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find(name);
        if (existing is RectTransform existingRect)
            return existingRect;

        RectTransform child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        child.SetParent(parent, false);
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.one;
        child.offsetMin = Vector2.zero;
        child.offsetMax = Vector2.zero;
        changed = true;
        return child;
    }

    private static void ValidateReusablePrefabAssets()
    {
        for (int i = 0; i < KingdomUIPrefabLibrary.AllReusablePrefabs.Length; i++)
        {
            string prefabName = KingdomUIPrefabLibrary.AllReusablePrefabs[i];
            string path = Folder + "/" + prefabName + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                Debug.LogError("[KingdomUI] Reusable UI prefab failed to import: " + path);
        }
    }

    private static bool SaveIfMissing(string prefabName, System.Func<GameObject> factory)
    {
        string path = Folder + "/" + prefabName + ".prefab";
        if (File.Exists(path))
            return false;

        GameObject template = factory();
        PrefabUtility.SaveAsPrefabAsset(template, path);
        Object.DestroyImmediate(template);
        Debug.Log("[KingdomUI] Reusable UI prefab generated: " + path);
        return true;
    }

    private static GameObject CreateResearchNodeTemplate()
    {
        GameObject root = new GameObject("KingdomUIResearchNode", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(Outline), typeof(UIResearchGraphDragForwarder), typeof(UITouchTooltip));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(205f, 50f);
        Image surface = root.GetComponent<Image>();
        surface.color = new Color(.16f, .19f, .19f, .96f);
        surface.raycastTarget = true;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        Outline outline = root.GetComponent<Outline>();
        outline.effectColor = Color.clear;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform frame = Child(rootRect, "EraFrame", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.raycastTarget = false;

        RectTransform progress = Child(frame, "ProgressFill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image progressImage = progress.gameObject.AddComponent<Image>();
        progressImage.type = Image.Type.Filled;
        progressImage.fillMethod = Image.FillMethod.Horizontal;
        progressImage.fillOrigin = 0;
        progressImage.fillAmount = 0f;
        progressImage.raycastTarget = false;

        CreateLabel(rootRect, "Label", new Vector2(.27f, .26f), new Vector2(1f, .74f),
            new Vector2(8f, 0f), new Vector2(-8f, 0f));
        CreateLabel(rootRect, "Cost", new Vector2(.27f, .06f), new Vector2(.62f, .34f),
            new Vector2(8f, 0f), new Vector2(-2f, 0f));
        CreateLabel(rootRect, "Progress", new Vector2(.62f, .06f), new Vector2(1f, .34f),
            new Vector2(2f, 0f), new Vector2(-8f, 0f));
        CreateLabel(rootRect, "State", new Vector2(.27f, .74f), new Vector2(1f, .98f),
            new Vector2(8f, 0f), new Vector2(-8f, -1f));
        return root;
    }

    private static GameObject CreateResearchLineTemplate()
    {
        GameObject root = new GameObject("KingdomUIResearchLine", typeof(RectTransform), typeof(Image));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.pivot = Vector2.zero;
        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        return root;
    }

    private static GameObject CreateResearchGraphTemplate()
    {
        GameObject root = new GameObject("KingdomUIResearchGraph", typeof(RectTransform), typeof(Image),
            typeof(RectMask2D), typeof(ScrollRect));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image viewportImage = root.GetComponent<Image>();
        viewportImage.color = new Color(.045f, .05f, .055f, 1f);
        viewportImage.raycastTarget = true;

        RectTransform content = Child(rootRect, "ResearchGraphContent", Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.zero);
        content.pivot = Vector2.zero;
        content.sizeDelta = new Vector2(1f, 1f);
        RectTransform dragSurface = Child(content, "ResearchGraphDragSurface", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        Image dragImage = dragSurface.gameObject.AddComponent<Image>();
        dragImage.color = Color.clear;
        dragImage.raycastTarget = true;
        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = rootRect;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return root;
    }

    private static RectTransform Child(RectTransform parent, string name, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private static void CreateLabel(RectTransform parent, string name, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = Child(parent, name, min, max, offsetMin, offsetMax);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = name == "Label" ? 24f : 12f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
    }
}
