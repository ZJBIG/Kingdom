using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private const float ResearchQueueVerticalOffset = 38f;
    private RectTransform researchQueueViewport;
    private RectTransform researchQueueContent;
    private ScrollRect researchQueueScroll;
    private readonly List<GameObject> researchQueueVisuals = new();
    // Queue mutations are common player actions. Keep the maximum number of
    // node/connector objects bounded instead of repeatedly Destroying and
    // Instantiating UI prefabs during a queue transition.
    private readonly List<GameObject> researchQueueNodePool = new();
    private readonly List<GameObject> researchQueueConnectorPool = new();
    private readonly List<Research> researchQueueDefinitionBuffer = new();
    private readonly List<Research> researchQueueDefinitionOrder = new();
    private int researchQueueGraphicRefreshCount;
    private int researchQueueGraphicRebuildCount;
    private sealed class ResearchQueueNodeVisualReferences
    {
        public Research Research;
        public Image Surface;
        public Image[] Outline;
        public Image ProgressImage;
        public TMP_Text ProgressText;
        public int StateVersion;
    }
    private readonly Dictionary<Research, ResearchQueueNodeVisualReferences> researchQueueNodeVisualReferences = new();

    private void SetupResearchQueueGraphic(RectTransform toolbar)
    {
        if (toolbar == null)
            return;

        RectTransform queueHost = buildingControls == null
            ? toolbar.parent as RectTransform
            : buildingControls.parent as RectTransform;
        Transform pageTool = queueHost == null ? null
            : queueHost.name == "PageTool" ? queueHost : queueHost.Find("PageTool");
        Transform oldViewport = pageTool == null ? null : pageTool.Find("ResearchQueueViewport");
        if (oldViewport == null)
        {
            Debug.LogError("[王国界面] Authored ResearchQueueViewport is missing under SafeAreaRoot/Content/PageTool.");
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
        if (researchQueueViewport.GetComponent<GraphicRaycaster>() == null)
            researchQueueViewport.gameObject.AddComponent<GraphicRaycaster>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;
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

        // The authored shell uses a small visual inset, but the queue is a
        // horizontal-only drag surface and must start at a neutral y position.
        researchQueueContent.anchoredPosition = new Vector2(
            researchQueueContent.anchoredPosition.x, 0f);
        researchQueueScroll.viewport = researchQueueViewport;
        researchQueueScroll.content = researchQueueContent;
        // The live-refresh pass can run before this authored overlay is bound
        // while the research page is being built. Keep one refresh pending so
        // that first binding cannot consume the queue dirty flag too early.
        researchQueueUiDirty = true;
        RefreshResearchQueueGraphic();
        Debug.Log("[王国界面] Research queue graphic bound: horizontal drag, ResearchNode prefab, ResearchLine bus/end sprites");
    }

    private void RefreshResearchQueueGraphic()
    {
        if (researchQueueViewport == null || researchQueueContent == null || ResearchManager.Instance == null)
            return;

        researchQueueGraphicRefreshCount++;
        float refreshStart = Time.realtimeSinceStartup;

        List<Research> definitions = researchQueueDefinitionBuffer;
        definitions.Clear();
        Research active = ResearchManager.Instance.ActiveResearch?.Definition;
        if (active != null)
            definitions.Add(active);
        IReadOnlyList<ResearchState> queued = ResearchManager.Instance.ResearchQueue;
        for (int i = 0; i < queued.Count; i++)
            if (queued[i]?.Definition != null)
                definitions.Add(queued[i].Definition);

        if (QueueDefinitionsMatch(definitions))
        {
            for (int i = 0; i < definitions.Count; i++)
                RefreshResearchQueueNodeVisual(definitions[i], researchQueueNodeVisualReferences[definitions[i]]);
#if UNITY_EDITOR
            float refreshMs = (Time.realtimeSinceStartup - refreshStart) * 1000f;
            if (refreshMs >= 5f)
                KingdomEditorPerfLog.Write(
                    $"[KingdomPerf] ResearchQueueGraphic refreshMs={refreshMs:0.0} " +
                    $"rebuild=False definitions={definitions.Count} visuals={researchQueueVisuals.Count} " +
                    $"contentChildren={researchQueueContent.childCount} refreshes={researchQueueGraphicRefreshCount} " +
                    $"rebuilds={researchQueueGraphicRebuildCount}");
#endif
            return;
        }

        int deactivatedVisuals = researchQueueVisuals.Count;
        for (int i = 0; i < researchQueueNodePool.Count; i++)
            if (researchQueueNodePool[i] != null)
                researchQueueNodePool[i].SetActive(false);
        for (int i = 0; i < researchQueueConnectorPool.Count; i++)
            if (researchQueueConnectorPool[i] != null)
                researchQueueConnectorPool[i].SetActive(false);
        researchQueueVisuals.Clear();
        researchQueueNodeVisualReferences.Clear();
        researchQueueGraphicRebuildCount++;

        // Vertical scrolling is disabled; keeping content taller than the
        // viewport makes ScrollRect clamp its y position even when we reset it.
        float queueHeight = Mathf.Max(1f, researchQueueViewport.rect.height);
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
        researchQueueDefinitionOrder.Clear();
        researchQueueDefinitionOrder.AddRange(definitions);
        researchQueueScroll.viewport = researchQueueViewport;
        researchQueueScroll.content = researchQueueContent;
        researchQueueScroll.horizontal = true;
        researchQueueScroll.vertical = false;
        researchQueueScroll.movementType = ScrollRect.MovementType.Clamped;
        researchQueueScroll.StopMovement();
#if UNITY_EDITOR
        float rebuildMs = (Time.realtimeSinceStartup - refreshStart) * 1000f;
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] ResearchQueueGraphic refreshMs={rebuildMs:0.0} rebuild=True " +
            $"definitions={definitions.Count} deactivated={deactivatedVisuals} " +
            $"active={researchQueueVisuals.Count} nodePool={researchQueueNodePool.Count} " +
            $"connectorPool={researchQueueConnectorPool.Count} contentChildren={researchQueueContent.childCount} " +
            $"refreshes={researchQueueGraphicRefreshCount} rebuilds={researchQueueGraphicRebuildCount}");
#endif
    }

    private bool QueueDefinitionsMatch(IReadOnlyList<Research> definitions)
    {
        if (definitions == null || definitions.Count != researchQueueNodeVisualReferences.Count)
            return false;
        for (int i = 0; i < definitions.Count; i++)
            if (!researchQueueNodeVisualReferences.ContainsKey(definitions[i]) ||
                i >= researchQueueDefinitionOrder.Count ||
                researchQueueDefinitionOrder[i] != definitions[i])
                return false;
        return true;
    }

    private void CreateResearchQueueNode(Research research, Vector2 position)
    {
        GameObject nodeObject = GetResearchQueueNodeObject();
        if (nodeObject == null)
            return;
        nodeObject.transform.SetParent(researchQueueContent, false);
        nodeObject.SetActive(true);
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
        RequireResearchNodeLabel("Label", node, research.Label ?? research.Id, TextPrimary);
        RequireResearchNodeLabel("Cost", node,
            state == null ? FormatResearchBaseCost(research) : state.BaseCost.ToGameString(),
            TextSecondary);
        TMP_Text progressText = RequireResearchNodeLabel(
            "Progress", node, ResearchProgressText(state, status), TextSecondary);
        RequireResearchNodeLabel("State", node, ResearchStateLabel(research, status),
            status == ResearchStatus.Completed ? Color.white : accent);
        researchQueueNodeVisualReferences[research] = new ResearchQueueNodeVisualReferences
        {
            Research = research,
            Surface = surface,
            Outline = outline,
            ProgressImage = progress,
            ProgressText = progressText,
            StateVersion = state == null ? -1 : state.Version
        };
    }

    private void RefreshActiveResearchQueueProgressVisual(ResearchManager manager)
    {
        ResearchState state = manager == null ? null : manager.ActiveResearch;
        Research research = state?.Definition;
        if (research == null ||
            !researchQueueNodeVisualReferences.TryGetValue(research, out ResearchQueueNodeVisualReferences visual) ||
            visual == null)
            return;

        ResearchStatus status = state.Status;
        if (visual.ProgressImage != null)
        {
            float fill = ResearchProgressFillAmount(state, status);
            if (Mathf.Abs(visual.ProgressImage.fillAmount - fill) > 0.0001f)
                visual.ProgressImage.fillAmount = fill;
        }
        if (visual.ProgressText != null)
            SetTextIfChanged(visual.ProgressText, ResearchProgressText(state, status));
    }

    private void RefreshResearchQueueNodeVisual(
        Research research,
        ResearchQueueNodeVisualReferences visual)
    {
        if (research == null || visual == null || visual.Surface == null ||
            ResearchManager.Instance == null)
            return;
        ResearchManager.Instance.States.TryGetValue(research, out ResearchState state);
        ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
        Color accent = status == ResearchStatus.Completed ? Positive :
            status == ResearchStatus.Available ? Copper :
            status == ResearchStatus.Researching || status == ResearchStatus.Queued ?
            new Color(.38f, .68f, .86f, 1f) : TextSecondary;
        ApplyResearchQueueSelectionVisual(research, visual.Surface, visual.Outline, status);

        int stateVersion = state == null ? -1 : state.Version;
        if (visual.StateVersion == stateVersion)
            return;
        visual.StateVersion = stateVersion;

        Transform node = visual.Surface.transform;
        if (visual.ProgressImage != null)
        {
            float fill = ResearchProgressFillAmount(state, status);
            if (Mathf.Abs(visual.ProgressImage.fillAmount - fill) > 0.0001f)
                visual.ProgressImage.fillAmount = fill;
        }
        RequireResearchNodeLabel("Cost", node,
            state == null ? FormatResearchBaseCost(research) : state.BaseCost.ToGameString(),
            TextSecondary);
        if (visual.ProgressText != null)
            SetTextIfChanged(visual.ProgressText, ResearchProgressText(state, status));
        RequireResearchNodeLabel("State", node, ResearchStateLabel(research, status),
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
                outline[i].color = GetResearchOutlineColor(research, selected);
                outline[i].enabled = true;
            }
    }

    private void CreateResearchQueueConnector(float x, float y)
    {
        GameObject lineObject = GetResearchQueueConnectorObject();
        if (lineObject != null)
            lineObject.SetActive(true);
        GameObject arrowObject = GetResearchQueueConnectorObject();
        if (lineObject == null || arrowObject == null)
        {
            if (lineObject != null)
                lineObject.SetActive(false);
            return;
        }
        lineObject.transform.SetParent(researchQueueContent, false);
        arrowObject.transform.SetParent(researchQueueContent, false);
        arrowObject.SetActive(true);
        researchQueueVisuals.Add(lineObject);
        researchQueueVisuals.Add(arrowObject);
        ConfigureQueueLine(lineObject.GetComponent<RectTransform>(), x, y, ResearchGridX - ResearchNodeWidth - 12f,
            LoadResearchTreeSprite("ResearchTree/ResearchLineHorizontal"));
        ConfigureQueueArrow(arrowObject.GetComponent<RectTransform>(), x + ResearchGridX - ResearchNodeWidth - 16f, y,
            LoadResearchTreeSprite("ResearchTree/ResearchLineEnd"));
    }

    private GameObject GetResearchQueueNodeObject()
    {
        for (int i = 0; i < researchQueueNodePool.Count; i++)
        {
            GameObject pooled = researchQueueNodePool[i];
            if (pooled != null && !pooled.activeSelf)
                return pooled;
        }

        GameObject created = KingdomUIPrefabLibrary.Instantiate(
            KingdomUIPrefabLibrary.ResearchNode,
            researchQueueContent);
        if (created != null)
            researchQueueNodePool.Add(created);
        return created;
    }

    private GameObject GetResearchQueueConnectorObject()
    {
        for (int i = 0; i < researchQueueConnectorPool.Count; i++)
        {
            GameObject pooled = researchQueueConnectorPool[i];
            if (pooled != null && !pooled.activeSelf)
                return pooled;
        }

        GameObject created = KingdomUIPrefabLibrary.Instantiate(
            KingdomUIPrefabLibrary.ResearchLine,
            researchQueueContent);
        if (created != null)
            researchQueueConnectorPool.Add(created);
        return created;
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
