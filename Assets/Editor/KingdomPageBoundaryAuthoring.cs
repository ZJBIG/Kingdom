using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Clipping belongs to viewports, never decorative panels or page content.</summary>
public static class KingdomPageBoundaryAuthoring
{
    private const string RootPath = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";

    [MenuItem("Tools/Kingdom/UI/Author Page Clip Boundaries")]
    public static void Author()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RootPath);
        try
        {
            int removed = Apply(root);
            PrefabUtility.SaveAsPrefabAsset(root, RootPath);
            AuthorScrollFrameTemplate();
            AuthorResearchNode();
            string reviewPath = "Assets/Resources/UI/Kingdom/KingdomUIReviewControls.prefab";
            GameObject review = PrefabUtility.LoadPrefabContents(reviewPath);
            try
            {
                foreach (ScrollRect scroll in review.GetComponentsInChildren<ScrollRect>(true))
                    if (scroll.viewport != null) AuthorFrame(scroll.viewport);
                review.transform.Find("DetailTools/Locate").gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(review, reviewPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(review); }
            AssetDatabase.SaveAssets();
            Debug.Log("[PageBoundaries] Removed shell clipping components=" + removed);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static int Apply(GameObject root)
    {
        HashSet<Transform> viewports = new();
        foreach (ScrollRect scroll in root.GetComponentsInChildren<ScrollRect>(true))
            if (scroll.viewport != null) viewports.Add(scroll.viewport);
        // These are independent preview/queue boundaries, including authored
        // templates whose ScrollRect references are bound only at runtime.
        foreach (string path in new[] {
            "SafeAreaRoot/Content/PageHost",
            "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport",
            "SafeAreaRoot/Content/PageHost/Production/GraphViewport",
            "SafeAreaRoot/Content/PageHost/Music/DataRows/MusicSurface/TrackListViewport",
            "SafeAreaRoot/Content/PageTool/ResearchQueueViewport",
            "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport",
            "SafeAreaRoot/StoryIllustrationPreview/Viewport",
            "UnsafeAreaTicker/FeedViewport" })
        {
            Transform viewport = root.transform.Find(path);
            if (viewport != null) viewports.Add(viewport);
        }
        int removed = 0;
        foreach (string path in new[] { "SafeAreaRoot/Content", "SafeAreaRoot/DetailPanel" })
        {
            Transform panel = root.transform.Find(path);
            if (panel == null) continue;
            foreach (Mask mask in panel.GetComponentsInChildren<Mask>(true))
                if (!viewports.Contains(mask.transform)) { Object.DestroyImmediate(mask); removed++; }
            foreach (RectMask2D mask in panel.GetComponentsInChildren<RectMask2D>(true))
                if (!viewports.Contains(mask.transform)) { Object.DestroyImmediate(mask); removed++; }
        }
        AuthorPageScrollbar(root);
        foreach (Transform viewport in viewports) AuthorFrame(viewport);
        return removed;
    }

    private static void AuthorPageScrollbar(GameObject root)
    {
        Transform panel = root.transform.Find("SafeAreaRoot/Content");
        RectTransform viewport = panel.Find("PageHost") as RectTransform;
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.content = viewport.Find("Overview") as RectTransform;
        // Clip at the inner edge of the inset, two-pixel frame.
        viewport.GetComponent<RectMask2D>().padding = new Vector4(3, 3, 3, 3);
        RectTransform track = panel.Find("PageVerticalScrollbar") as RectTransform;
        if (track == null) track = viewport.Find("PageVerticalScrollbar") as RectTransform;
        if (track == null) track = Rect("PageVerticalScrollbar", panel);
        track.SetParent(panel, false);
        track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1, .5f);
        // Align with the viewport vertically, outside its right clipping edge.
        track.offsetMin = new Vector2(-20f, viewport.offsetMin.y + 6f);
        track.offsetMax = new Vector2(-6f, viewport.offsetMax.y - 6f);
        Image surface = track.GetComponent<Image>() ?? track.gameObject.AddComponent<Image>();
        surface.color = new Color(.07f, .09f, .08f, .6f);
        surface.raycastTarget = true;
        RectTransform sliding = Rect("SlidingArea", track);
        sliding.anchorMin = Vector2.zero; sliding.anchorMax = Vector2.one;
        sliding.offsetMin = new Vector2(2, 2); sliding.offsetMax = new Vector2(-2, -2);
        RectTransform handleRect = Rect("Handle", sliding);
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
        Image handle = handleRect.GetComponent<Image>() ?? handleRect.gameObject.AddComponent<Image>();
        handle.color = new Color(.66f, .61f, .43f, .9f);
        handle.raycastTarget = true;
        Scrollbar bar = track.GetComponent<Scrollbar>() ?? track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handleRect;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = 0;
    }

    private static void AuthorFrame(Transform viewport)
    {
        RectTransform frame = Rect("ScrollFrame", viewport);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(1, 1); frame.offsetMax = new Vector2(-1, -1);
        (frame.GetComponent<LayoutElement>() ?? frame.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
        foreach (string side in new[] { "Top", "Bottom", "Left", "Right" })
        {
            bool horizontal = side == "Top" || side == "Bottom";
            bool far = side == "Top" || side == "Right";
            RectTransform rect = Rect(side, frame);
            rect.anchorMin = horizontal ? new Vector2(0, far ? 1 : 0) : new Vector2(far ? 1 : 0, 0);
            rect.anchorMax = horizontal ? new Vector2(1, far ? 1 : 0) : new Vector2(far ? 1 : 0, 1);
            rect.pivot = horizontal ? new Vector2(.5f, far ? 1 : 0) : new Vector2(far ? 1 : 0, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = new Color(.65f, .55f, .35f, .85f);
            image.raycastTarget = false;
            // The page frame surrounds the clipping rectangle rather than being clipped by it.
            image.maskable = viewport.name != "PageHost";
        }
        frame.SetAsLastSibling();
    }

    private static void AuthorScrollFrameTemplate()
    {
        GameObject host = new("ScrollFrameTemplate", typeof(RectTransform));
        try
        {
            AuthorFrame(host.transform);
            PrefabUtility.SaveAsPrefabAsset(host.transform.Find("ScrollFrame").gameObject,
                "Assets/Resources/UI/Kingdom/KingdomUIScrollFrame.prefab");
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static void AuthorResearchNode()
    {
        const string path = "Assets/Resources/UI/Kingdom/KingdomUIResearchNode.prefab";
        GameObject node = PrefabUtility.LoadPrefabContents(path);
        try
        {
            KingdomButtonAuthoring.Apply(node.GetComponent<Button>());
            PrefabUtility.SaveAsPrefabAsset(node, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(node); }
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        if (parent.Find(name) is RectTransform existing) return existing;
        GameObject child = new(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }
}
