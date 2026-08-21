using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private const float ResearchQueueVerticalOffset = 38f;
    private RectTransform researchQueueViewport;
    private RectTransform researchQueueContent;
    private ScrollRect researchQueueScroll;
    private readonly List<GameObject> researchQueueVisuals = new();
    private sealed class ResearchQueueNodeVisualReferences
    {
        public Research Research;
        public Image Surface;
        public Image[] Outline;
    }
    private readonly Dictionary<Research, ResearchQueueNodeVisualReferences> researchQueueNodeVisualReferences = new();

    private void SetupResearchQueueGraphic(RectTransform toolbar)
    {
        if (toolbar == null)
            return;

        RectTransform queueHost = buildingQuantityControls == null
            ? toolbar.parent as RectTransform
            : buildingQuantityControls.parent as RectTransform;
        Transform oldViewport = queueHost == null ? null : queueHost.Find("ResearchQueueViewport");
        if (oldViewport == null)
        {
            Debug.LogError("[王国界面] Authored ResearchQueueViewport is missing under SafeAreaRoot/Content.");
            return;
        }
        toolbar.Find("Surface")?.gameObject.SetActive(false);
        researchQueueViewport = oldViewport as RectTransform;
        if (researchQueueViewport == null)
        {
            Debug.LogError("[王国界面] Authored ResearchQueueViewport has no RectTransform.");
            return;
        }
        Image viewportImage = researchQueueViewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            Debug.LogError("[王国界面] Authored ResearchQueueViewport has no Image.");
            return;
        }
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
        EnsureNestedCanvas(researchQueueViewport);
        researchQueueScroll = researchQueueViewport.GetComponent<ScrollRect>();
        if (researchQueueScroll == null)
        {
            Debug.LogError("[王国界面] Authored ResearchQueueViewport has no ScrollRect.");
            return;
        }
        researchQueueScroll.horizontal = true;
        researchQueueScroll.vertical = false;
        researchQueueScroll.inertia = false;
        researchQueueScroll.movementType = ScrollRect.MovementType.Clamped;
        researchQueueScroll.scrollSensitivity = 1f;

        researchQueueContent = researchQueueViewport.Find("ResearchQueueContent") as RectTransform;
        if (researchQueueContent == null)
        {
            Debug.LogError("[王国界面] Authored ResearchQueueContent is missing under ResearchQueueViewport.");
            return;
        }

        researchQueueScroll.viewport = researchQueueViewport;
        researchQueueScroll.content = researchQueueContent;
        RefreshResearchQueueGraphic();
        Debug.Log("[王国界面] Research queue graphic bound: horizontal drag, ResearchNode prefab, ResearchLine bus/end sprites");
    }

    private void RefreshResearchQueueGraphic()
    {
        if (researchQueueViewport == null || researchQueueContent == null || ResearchManager.Instance == null)
            return;

        for (int i = 0; i < researchQueueVisuals.Count; i++)
            if (researchQueueVisuals[i] != null)
                Destroy(researchQueueVisuals[i]);
        researchQueueVisuals.Clear();
        researchQueueNodeVisualReferences.Clear();

        List<Research> definitions = new();
        Research active = ResearchManager.Instance.ActiveResearch?.Definition;
        if (active != null)
            definitions.Add(active);
        IReadOnlyList<ResearchState> queued = ResearchManager.Instance.ResearchQueue;
        for (int i = 0; i < queued.Count; i++)
            if (queued[i]?.Definition != null)
                definitions.Add(queued[i].Definition);

        float queueHeight = Mathf.Max(ResearchNodeHeight + 4f, researchQueueViewport.rect.height);
        float chainWidth = ResearchGraphPaddingX * 2f + Mathf.Max(1, definitions.Count) * ResearchGridX;
        float width = Mathf.Max(researchQueueViewport.rect.width, chainWidth);
        researchQueueContent.sizeDelta = new Vector2(width, queueHeight);
        researchQueueContent.anchoredPosition = new Vector2(researchQueueContent.anchoredPosition.x, 0f);
        // Keep the sequence packed against the left edge. Only the vertical
        // axis is centered; horizontal overflow remains available for drag.
        float startX = definitions.Count == 0 ? 0f : ResearchGraphPaddingX;
        float nodeY = (queueHeight - ResearchNodeHeight) * .5f;
        for (int i = 0; i < definitions.Count; i++)
        {
            float x = startX + i * ResearchGridX;
            CreateResearchQueueNode(definitions[i], new Vector2(x, nodeY + ResearchQueueVerticalOffset));
            if (i > 0)
                CreateResearchQueueConnector(x - ResearchGridX + ResearchNodeWidth,
                    queueHeight * .5f + ResearchQueueVerticalOffset);
        }
        researchQueueScroll.viewport = researchQueueViewport;
        researchQueueScroll.content = researchQueueContent;
        researchQueueScroll.horizontal = true;
        researchQueueScroll.vertical = false;
        researchQueueScroll.movementType = ScrollRect.MovementType.Clamped;
        researchQueueScroll.StopMovement();
    }

    private void CreateResearchQueueNode(Research research, Vector2 position)
    {
        GameObject nodeObject = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.ResearchNode, researchQueueContent);
        if (nodeObject == null)
            return;
        researchQueueVisuals.Add(nodeObject);
        nodeObject.name = "ResearchQueueNode_" + research.Id;
        RectTransform node = nodeObject.GetComponent<RectTransform>();
        node.anchorMin = Vector2.zero;
        node.anchorMax = Vector2.zero;
        node.pivot = Vector2.zero;
        node.anchoredPosition = position;
        node.sizeDelta = new Vector2(ResearchNodeWidth, ResearchNodeHeight);

        ResearchManager.Instance.States.TryGetValue(research, out ResearchState state);
        ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
        Color accent = status == ResearchStatus.Completed ? Positive :
            status == ResearchStatus.Available ? Copper :
            status == ResearchStatus.Researching || status == ResearchStatus.Queued ?
            new Color(.38f, .68f, .86f, 1f) : TextSecondary;
        Image surface = node.GetComponent<Image>();
        surface.color = selectedResearchNode == research
            ? ResearchFocusSurface
            : status == ResearchStatus.Locked
            ? new Color(.10f, .12f, .12f, .94f)
            : new Color(.16f, .19f, .19f, .96f);
        Button button = node.GetComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectResearchQueueNode(research));
        if (node.GetComponent<UIPageScrollDragForwarder>() == null)
            node.gameObject.AddComponent<UIPageScrollDragForwarder>();
        Image[] outline = CreateResearchNodeBorder(node);
        ApplyResearchQueueSelectionVisual(research, surface, outline, status);
        researchQueueNodeVisualReferences[research] = new ResearchQueueNodeVisualReferences
        {
            Research = research,
            Surface = surface,
            Outline = outline
        };

        RectTransform frameRect = node.Find("EraFrame") as RectTransform;
        Image frame = frameRect == null ? null : frameRect.GetComponent<Image>();
        if (frame != null)
        {
            frame.sprite = LoadResearchTreeSprite(GetResearchEraTextureName(research.TechLevel));
            frame.color = Color.white;
            frame.raycastTarget = false;
        }
        RectTransform progressRect = frameRect == null ? null : frameRect.Find("ProgressFill") as RectTransform;
        Image progress = progressRect == null ? null : progressRect.GetComponent<Image>();
        if (progress != null)
        {
            progress.sprite = LoadResearchTreeSprite(GetResearchProgressTextureName(research.TechLevel));
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            progress.fillAmount = ResearchProgressFillAmount(state, status);
            progress.raycastTarget = false;
            progress.color = Color.white;
        }
        ResearchNodeLabel("Label", node, research.Label ?? research.Id, 24, TextPrimary);
        ResearchNodeLabel("Cost", node, state == null ? FormatResearchBaseCost(research) : state.BaseCost.ToGameString(), 20, TextSecondary);
        ResearchNodeLabel("Progress", node, ResearchProgressText(state, status), 20, TextSecondary);
        ResearchNodeLabel("State", node, ResearchStateLabel(research, status), 20,
            status == ResearchStatus.Completed ? Color.white : accent);
    }

    private void SelectResearchQueueNode(Research research)
    {
        ShowResearchDetails(research);
        RefreshResearchQueueGraphic();
    }

    private void RefreshResearchQueueSelectionVisuals()
    {
        if (ResearchManager.Instance == null)
            return;
        foreach (KeyValuePair<Research, ResearchQueueNodeVisualReferences> pair in researchQueueNodeVisualReferences)
        {
            Research research = pair.Key;
            ResearchQueueNodeVisualReferences visual = pair.Value;
            if (research == null || visual == null)
                continue;
            ResearchManager.Instance.States.TryGetValue(research, out ResearchState state);
            ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
            ApplyResearchQueueSelectionVisual(research, visual.Surface, visual.Outline, status);
        }
    }

    private void ApplyResearchQueueSelectionVisual(
        Research research,
        Image surface,
        Image[] outline,
        ResearchStatus status)
    {
        bool selected = research != null && research == selectedResearchNode;
        if (surface != null)
            surface.color = selected ? ResearchFocusSurface : status == ResearchStatus.Locked
                ? new Color(.10f, .12f, .12f, .94f)
                : new Color(.16f, .19f, .19f, .96f);
        if (outline == null)
            return;
        for (int i = 0; i < outline.Length; i++)
            if (outline[i] != null)
            {
                outline[i].color = selected ? ResearchFocusWhite : ResearchOutlineNormal;
                outline[i].enabled = true;
            }
    }

    private void CreateResearchQueueConnector(float x, float y)
    {
        GameObject lineObject = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.ResearchLine, researchQueueContent);
        GameObject arrowObject = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.ResearchLine, researchQueueContent);
        if (lineObject == null || arrowObject == null)
            return;
        researchQueueVisuals.Add(lineObject);
        researchQueueVisuals.Add(arrowObject);
        ConfigureQueueLine(lineObject.GetComponent<RectTransform>(), x, y, ResearchGridX - ResearchNodeWidth - 12f,
            LoadResearchTreeSprite("ResearchTree/ResearchLineHorizontal"));
        ConfigureQueueArrow(arrowObject.GetComponent<RectTransform>(), x + ResearchGridX - ResearchNodeWidth - 16f, y,
            LoadResearchTreeSprite("ResearchTree/ResearchLineEnd"));
    }

    private static void ConfigureQueueLine(RectTransform rect, float x, float y, float width, Sprite sprite)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 4f);
        Image image = rect.GetComponent<Image>();
        image.sprite = sprite;
        // Queue connectors are presentation-only and remain continuously
        // highlighted, matching the selected research-tree connector style.
        image.color = ResearchFocusWhite;
        image.raycastTarget = false;
        image.maskable = true;
    }

    private static void ConfigureQueueArrow(RectTransform rect, float x, float y, Sprite sprite)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(16f, 16f);
        Image image = rect.GetComponent<Image>();
        image.sprite = sprite;
        image.color = ResearchFocusWhite;
        image.raycastTarget = false;
        image.maskable = true;
    }
}
