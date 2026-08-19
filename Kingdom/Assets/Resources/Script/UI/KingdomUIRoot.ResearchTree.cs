using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    // Exact values from ResearchTreeSK/Defs/ModOptions/ModOptions.xml.
    // The research assets use this same integer-cell coordinate system.
    private const float ResearchNodeWidth = 205f;
    private const float ResearchNodeHeight = 50f;
    private const float ResearchGridX = 255f;
    private const float ResearchGridY = 60f;
    private const float ResearchGraphPaddingX = 25f;
    private const float ResearchGraphPaddingY = 5f;
    private const int ResearchTopPaddingRows = 2;
    private const float ResearchTopPadding = ResearchTopPaddingRows * ResearchGridY;
    private const float ResearchCurveRadius = 10f;
    private const float ResearchLineThickness = 4f;
    private const float ResearchConnectorOverlap = 1f;
    private const float ResearchArrowThickness = 16f;
    private const float ResearchNodeMarginHalf = 25f;
    private enum ResearchCurveType { LeftTop, LeftBottom, RightTop, RightBottom }
    private readonly struct ResearchArrowCacheKey : IEquatable<ResearchArrowCacheKey>
    {
        public readonly Research From;
        public readonly Research To;
        public readonly Vector2Int TargetGrid;

        public ResearchArrowCacheKey(
            Research from,
            Research to,
            Vector2Int targetGrid)
        {
            From = from;
            To = to;
            TargetGrid = targetGrid;
        }

        public bool Equals(ResearchArrowCacheKey other) =>
            object.ReferenceEquals(From, other.From) &&
            object.ReferenceEquals(To, other.To) &&
            TargetGrid == other.TargetGrid;

        public override bool Equals(object obj) =>
            obj is ResearchArrowCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = From == null ? 0 :
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(From);
                hash = hash * 397 + (To == null ? 0 :
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(To));
                hash = hash * 397 + TargetGrid.GetHashCode();
                return hash;
            }
        }
    }
    private static readonly Color ResearchFocusWhite = new Color(1f, 1f, 1f, 1f);
    // Keep the idle graph subdued so the selected closure reads immediately.
    private static readonly Color ResearchOutlineNormal = new Color(.28f, .30f, .30f, 1f);
    private static readonly Color ResearchArrowColor = new Color(.22f, .24f, .24f, 1f);
    private static readonly Color ResearchFocusSurface = new Color(.30f, .36f, .36f, 1f);
    private static readonly Dictionary<string, Sprite> researchTreeSprites = new();
    private readonly Dictionary<Pair<Research, Research>, List<Image>> researchTreeLinkVisuals = new();
    private readonly Dictionary<string, Image> researchSharedLineVisuals = new();
    private readonly Dictionary<ResearchArrowCacheKey, Image> researchArrowVisuals = new();
    private readonly Dictionary<Research, Image[]> researchTreeOutlines = new();
    private RectTransform researchGraphLineLayer;
    private int researchTreeMaximumRow;
    private int researchTopologyDuplicateCount;
    private int researchTopologyBackwardsEdgeCount;
    private int researchTopologyInversionCount;
    private Research selectedResearchNode;
    private string lastLoggedResearchClosureTarget;
    private TMP_Text researchTreeQueueLabel;
    private UIResearchGraphGesture researchGraphGesture;
    private bool researchProgressVisualLogged;
    private bool researchTreeBuildInProgress;
    private string lastResearchNodeStateSignature;
    private string lastResearchDetailSignature;
    private string lastResearchQueueSignature;
    private string lastResearchBusSignature;
    private string lastSelectedResearchId;
    private string lastActiveResearchId;

    private void RefreshResearchDynamicUI()
    {
        if (!researchTreePageBuilt || ResearchManager.Instance == null)
            return;

        RestoreResearchTreeSelection();
        ResearchManager manager = ResearchManager.Instance;
        RefreshActiveResearchProgressVisual(manager);
        // Progress fill is intentionally refreshed every UI cycle. Structural
        // signatures are only needed after a queue/state or selection event;
        // rebuilding 79-node signatures on an unchanged page creates
        // avoidable strings and hash sets indefinitely.
        if (!researchDynamicUiDirty || researchDynamicSignatureRefreshTimer < 0.5f)
            return;
        researchDynamicSignatureRefreshTimer = 0f;
        string nodeSignature = BuildResearchNodeStateSignature();
        string queueSignature = BuildResearchQueueSignature();
        string detailSignature = BuildResearchDetailSignature();
        bool nodesChanged = !string.Equals(lastResearchNodeStateSignature, nodeSignature, StringComparison.Ordinal);
        bool queueChanged = !string.Equals(lastResearchQueueSignature, queueSignature, StringComparison.Ordinal);
        bool detailsChanged = !string.Equals(lastResearchDetailSignature, detailSignature, StringComparison.Ordinal);
        bool selectionChanged = !string.Equals(lastSelectedResearchId, selectedResearchNode?.Id ?? string.Empty, StringComparison.Ordinal);
        bool activeChanged = !string.Equals(lastActiveResearchId, manager.ActiveResearch?.Definition?.Id ?? string.Empty, StringComparison.Ordinal);
        // Bus topology/focus can only change when a node state, selection, or
        // active research changes. Rebuilding the transitive closure on every
        // 100 ms progress refresh was unnecessary allocation and graph work.
        string busSignature = nodesChanged || selectionChanged || activeChanged
            ? BuildResearchBusSignature()
            : lastResearchBusSignature;
        bool busChanged = !string.Equals(lastResearchBusSignature, busSignature, StringComparison.Ordinal);

        if (nodesChanged || busChanged)
            RefreshResearchTreeVisuals(busChanged);
        if (queueChanged)
            RefreshResearchQueueToolbar();

        if (detailsChanged && selectedResearchNode != null)
            RefreshSelectedResearchDetails(selectedResearchNode);

        CaptureResearchRefreshSignatures(
            nodeSignature,
            queueSignature,
            busSignature,
            detailSignature);
        researchDynamicUiDirty = false;
    }

    private void CaptureResearchRefreshSignatures(
        string nodeSignature,
        string queueSignature,
        string busSignature,
        string detailSignature)
    {
        if (ResearchManager.Instance == null)
            return;
        lastResearchNodeStateSignature = nodeSignature;
        lastResearchQueueSignature = queueSignature;
        lastResearchBusSignature = busSignature;
        lastResearchDetailSignature = detailSignature;
        lastSelectedResearchId = selectedResearchNode?.Id ?? string.Empty;
        lastActiveResearchId = ResearchManager.Instance.ActiveResearch?.Definition?.Id ?? string.Empty;
    }

    // Detail selection is an infrequent structural action; keep this
    // compatibility path separate from the 100 ms live-refresh path.
    private void CaptureResearchRefreshSignatures()
    {
        researchDynamicUiDirty = false;
        CaptureResearchRefreshSignatures(
            BuildResearchNodeStateSignature(),
            BuildResearchQueueSignature(),
            BuildResearchBusSignature(),
            BuildResearchDetailSignature());
    }

    private string BuildResearchNodeStateSignature()
    {
        ResearchManager manager = ResearchManager.Instance;
        var signature = new StringBuilder(researchTreeNodes.Count * 48);
        foreach (KeyValuePair<Research, Button> pair in researchTreeNodes)
        {
            Research research = pair.Key;
            if (research == null)
                continue;
            manager.States.TryGetValue(research, out ResearchState state);
            ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
            signature.Append(research.Id).Append(':').Append((int)status).Append(':')
                .Append(manager.IsQueued(research) ? '1' : '0').Append(':')
                .Append(manager.ActiveResearch?.Definition == research ? '1' : '0').Append(':')
                .Append(state != null && state.CostPaid ? '1' : '0').Append(';');
        }
        return signature.ToString();
    }

    private string BuildResearchQueueSignature()
    {
        ResearchManager manager = ResearchManager.Instance;
        var signature = new StringBuilder();
        ResearchState active = manager.ActiveResearch;
        signature.Append(active?.Definition?.Id ?? string.Empty).Append(':')
            .Append(active == null ? -1 : QuantizedResearchProgress(active)).Append('|');
        IReadOnlyList<ResearchState> queue = manager.ResearchQueue;
        for (int i = 0; i < queue.Count; i++)
        {
            ResearchState state = queue[i];
            signature.Append(i).Append(':').Append(state?.Definition?.Id ?? string.Empty).Append(':')
                .Append(state == null ? -1 : (int)state.Status).Append(':')
                .Append(state != null && state.CostPaid ? '1' : '0').Append('|');
        }
        return signature.ToString();
    }

    private string BuildResearchBusSignature()
    {
        HashSet<string> focusedIds = CollectSelectedResearchPrerequisiteIds();
        var ids = new List<string>(focusedIds);
        ids.Sort(StringComparer.Ordinal);
        var signature = new StringBuilder(selectedResearchNode?.Id ?? string.Empty);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            signature.Append('|').Append(id);
            if (DataBase<Research>.TryFind(id, out Research research) &&
                ResearchManager.Instance.States.TryGetValue(research, out ResearchState state))
                signature.Append(':').Append((int)state.Status);
        }
        return signature.ToString();
    }

    private string BuildResearchDetailSignature()
    {
        Research research = selectedResearchNode;
        if (research == null || ResearchManager.Instance == null)
            return string.Empty;
        ResearchManager manager = ResearchManager.Instance;
        manager.States.TryGetValue(research, out ResearchState state);
        var signature = new StringBuilder(research.Id).Append(':')
            .Append(state == null ? -1 : (int)state.Status).Append(':')
            .Append(QuantizedResearchProgress(state)).Append(':')
            .Append(state != null && state.CostPaid ? '1' : '0');
        IReadOnlyList<ResearchState> queue = manager.ResearchQueue;
        for (int i = 0; i < queue.Count; i++)
            if (queue[i]?.Definition == research)
                signature.Append(":queue=").Append(i);
        if (manager.ActiveResearch?.Definition == research)
            signature.Append(":active");
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = research.ResourceRequirements;
        if (requirements != null && ResourceManager.Instance != null)
            for (int i = 0; i < requirements.Count; i++)
            {
                Resource resource = requirements[i].First;
                if (resource == null)
                    continue;
                signature.Append('|').Append(resource.Id).Append('=');
                signature.Append(ResourceManager.Instance.GetAmount(resource).ToGameString());
                if (state != null)
                    signature.Append('/').Append(state.GetPaidResourceCost(resource).ToGameString());
            }
        return signature.ToString();
    }

    private static int QuantizedResearchProgress(ResearchState state)
    {
        if (state == null)
            return 0;
        return Mathf.RoundToInt(Mathf.Clamp01((float)state.ProgressRatio.ToDouble()) * 1000f);
    }

    private static readonly string[] ResearchTreeWarmupSprites =
    {
        "ResearchTree/ResearchTreeBackground",
        "ResearchTree/ResearchLineCircle",
        "ResearchTree/ResearchLineHorizontal",
        "ResearchTree/ResearchLineVertical",
        "ResearchTree/ResearchLineEnd",
        "ResearchTree/ResearchEraAnimal",
        "ResearchTree/ResearchEraNeolithic",
        "ResearchTree/ResearchEraMedieval",
        "ResearchTree/ResearchEraIndustrial",
        "ResearchTree/ResearchEraSpacer",
        "ResearchTree/ResearchEraUltra",
        "ResearchTree/ResearchEraArchotech",
        "ResearchTree/ProgressAnimal",
        "ResearchTree/ProgressNeolithic",
        "ResearchTree/ProgressMedieval",
        "ResearchTree/ProgressIndustrial",
        "ResearchTree/ProgressSpacer",
        "ResearchTree/ProgressUltra",
        "ResearchTree/ProgressArchotech"
    };

    private static void PreloadResearchTreeAssets()
    {
        int prefabCount = KingdomUIPrefabLibrary.PreloadAll();
        int loaded = 0;
        for (int i = 0; i < ResearchTreeWarmupSprites.Length; i++)
        {
            if (LoadResearchTreeSprite(ResearchTreeWarmupSprites[i]) != null)
                loaded++;
        }
        Debug.Log($"[王国界面] Research tree cache warmed: prefabs={prefabCount}/{KingdomUIPrefabLibrary.AllReusablePrefabs.Length}, sprites={loaded}/{ResearchTreeWarmupSprites.Length}");
    }

    private void BuildResearchTreePage(RectTransform parent)
    {
        if (researchTreeBuildInProgress)
            return;
        researchTreeBuildInProgress = true;
        StartCoroutine(BuildResearchTreePageCoroutine(parent));
    }

    private IEnumerator BuildResearchTreePageCoroutine(RectTransform parent)
    {
        float buildStartTime = Time.realtimeSinceStartup;
        Debug.Log($"[王国界面] BuildResearchTreePage implementation=ResearchTreeSK-IntegerGrid-v8 parent={parent} parentRect={parent.rect.size}");
        researchTreeNodes.Clear();
        researchTreeLinkVisuals.Clear();
        researchSharedLineVisuals.Clear();
        researchArrowVisuals.Clear();
        researchTreeOutlines.Clear();
        nextRowTop = 980f;
        // DataRows is only the page slot. The graph viewport and its drag
        // surface are authored in the scene shell. Only repeated graph
        // content is created below.
        Transform authoredGraph = parent.Find("ResearchGraphViewport");
        if (authoredGraph == null)
        {
            Debug.LogError("[王国界面] Scene is missing ResearchGraphViewport; fixed research UI will not be generated at runtime.");
            yield break;
        }
        GameObject graphObject = authoredGraph.gameObject;
        graphObject.name = "ResearchGraphViewport";
        researchGraphViewport = graphObject.GetComponent<RectTransform>();
        researchGraphViewport.anchorMin = Vector2.zero;
        researchGraphViewport.anchorMax = Vector2.one;
        researchGraphViewport.offsetMin = Vector2.zero;
        researchGraphViewport.offsetMax = Vector2.zero;
        EnsureNestedCanvas(researchGraphViewport);
        // Keep the viewport clip present even if a scene author removes it.
        if (researchGraphViewport.GetComponent<RectMask2D>() == null)
            researchGraphViewport.gameObject.AddComponent<RectMask2D>();

        Image viewportImage = researchGraphViewport.GetComponent<Image>();
        Sprite background = LoadResearchTreeSprite("ResearchTree/ResearchTreeBackground");
        if (background != null)
        {
            // The authored texture already contains the graph background tone
            // and grid. Do not multiply it by the old near-black fallback tint.
            viewportImage.color = Color.white;
            viewportImage.sprite = background;
            viewportImage.type = Image.Type.Tiled;
        }
        else
        {
            viewportImage.color = new Color(.045f, .05f, .055f, 1f);
        }

        ScrollRect graphScroll = researchGraphViewport.GetComponent<ScrollRect>();
        graphScroll.viewport = researchGraphViewport;
        graphScroll.horizontal = true;
        graphScroll.vertical = true;
        graphScroll.inertia = false;
        graphScroll.movementType = ScrollRect.MovementType.Clamped;
        graphScroll.scrollSensitivity = 1f;
        researchGraphContent = researchGraphViewport.Find("ResearchGraphContent") as RectTransform;
        if (researchGraphContent == null)
        {
            Debug.LogError("[王国界面] Scene is missing ResearchGraphContent; research graph will not be generated.");
            yield break;
        }
        // The graph contains thousands of line Graphics. Keep that subtree
        // in its own nested Canvas so panning does not rebuild the page's
        // other UI batches on every touch sample.
        EnsureNestedCanvas(researchGraphContent);
        researchGraphContent.pivot = Vector2.zero;
        researchGraphContent.anchoredPosition = Vector2.zero;
        graphScroll.content = researchGraphContent;
        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        float positionsStartTime = Time.realtimeSinceStartup;
        Dictionary<Research, Vector2> positions = CreateResearchTreePositions(definitions);
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchBuildPhase phase=positions elapsedMs={(Time.realtimeSinceStartup - positionsStartTime) * 1000f:0.0} definitions={definitions.Count}");
#endif
        // Topology/layout analysis is synchronous. Return one frame before
        // creating thousands of connector Graphics so its cost cannot merge
        // with the first render/layout frame of the research page.
        yield return null;
        if (researchGraphContent == null)
            yield break;
        float contentWidth = ResearchGraphPaddingX * 2f + ResearchNodeWidth;
        float contentHeight = ResearchTopPadding + ResearchGraphPaddingY * 2f + ResearchNodeHeight;
        foreach (Vector2 position in positions.Values)
        {
            contentWidth = Mathf.Max(contentWidth, position.x + ResearchNodeWidth + ResearchGraphPaddingX);
            contentHeight = Mathf.Max(contentHeight, position.y + ResearchNodeHeight + ResearchGraphPaddingY);
        }
        LogResearchGridLayout(definitions, positions);
        // Keep the reference grid spacing exact. The ScrollRect must only
        // scroll when this measured graph actually exceeds its viewport.
        researchGraphContent.sizeDelta = new Vector2(contentWidth, contentHeight);
        Transform authoredLineLayer = researchGraphContent.Find("ResearchGraphLineLayer");
        if (authoredLineLayer == null)
        {
            Debug.LogError("[王国界面] Scene is missing ResearchGraphLineLayer; code will not create a static graph UI layer at runtime.");
            yield break;
        }
        researchGraphLineLayer = authoredLineLayer as RectTransform;
        if (researchGraphLineLayer != null)
        {
            Canvas lineCanvas = researchGraphLineLayer.GetComponent<Canvas>();
            bool addedLineCanvas = lineCanvas == null;
            if (lineCanvas == null)
                lineCanvas = researchGraphLineLayer.gameObject.AddComponent<Canvas>();
            lineCanvas.overrideSorting = false;
            lineCanvas.pixelPerfect = false;
#if UNITY_EDITOR
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] CanvasIsolation owner=ResearchGraphLineLayer " +
                $"canvas=True addedCanvas={addedLineCanvas} raycaster=False");
#endif
        }
        researchGraphLineLayer.anchorMin = Vector2.zero;
        researchGraphLineLayer.anchorMax = Vector2.zero;
        researchGraphLineLayer.pivot = Vector2.zero;
        researchGraphLineLayer.anchoredPosition = Vector2.zero;
        researchGraphLineLayer.sizeDelta = researchGraphContent.sizeDelta;
        researchGraphLineLayer.SetAsLastSibling();
        Debug.Log("[王国界面] Research static shell source=scene: viewport, content, lineLayer, dragSurface, toolbar; runtime creates only data-driven repeated nodes, links and era bands.");
        // RectTransform.rect is used below to convert the ResearchTreeSK
        // top-left coordinates into Unity's bottom-left coordinates. Force
        // the layout now, before any connector is created; otherwise the
        // first connector batch can use the old content height.
        Canvas.ForceUpdateCanvases();
        // ResearchTreeSK has an invisible draggable layer over the graph.
        // Keep this layer behind links and nodes so empty graph space is a
        // valid drag target without stealing node clicks.
        RectTransform dragSurface = researchGraphContent.Find("ResearchGraphDragSurface") as RectTransform;
        if (dragSurface == null)
        {
            Debug.LogError("[王国界面] Scene is missing ResearchGraphDragSurface; graph panning is disabled.");
            yield break;
        }
        Image dragSurfaceImage = dragSurface.GetComponent<Image>();
        dragSurfaceImage.color = Color.clear;
        dragSurfaceImage.raycastTarget = true;
        dragSurface.SetAsFirstSibling();
        // ResearchTree_SK keeps the background in the viewport and uses the
        // era textures on each node. It does not stretch an era texture into
        // a full-height column behind the graph.
        float linksStartTime = Time.realtimeSinceStartup;
        yield return CreateResearchTreeLinks(researchGraphContent, definitions, positions);
#if UNITY_EDITOR
        float linksDurationMs = (Time.realtimeSinceStartup - linksStartTime) * 1000f;
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchBuildPhase phase=links elapsedMs={linksDurationMs:0.0} durationMs={linksDurationMs:0.0} links={researchTreeLinkVisuals.Count} shared={researchSharedLineVisuals.Count}");
#endif
        Debug.Log($"[王国界面] Research graph routes: links={researchTreeLinkVisuals.Count}, sharedBusSegments={researchSharedLineVisuals.Count}");
        float nodesStartTime = Time.realtimeSinceStartup;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research != null && positions.TryGetValue(research, out Vector2 position))
                CreateResearchTreeNode(researchGraphContent, research, position);
            if ((i + 1) % 4 == 0)
                yield return null;
        }
        Canvas.ForceUpdateCanvases();
#if UNITY_EDITOR
        float nodesDurationMs = (Time.realtimeSinceStartup - nodesStartTime) * 1000f;
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchBuildPhase phase=nodes elapsedMs={nodesDurationMs:0.0} durationMs={nodesDurationMs:0.0} nodes={researchTreeNodes.Count}");
#endif
        RestoreResearchTreeSelection();
        BindAuthoredResearchTreeOverlay(researchGraphViewport);
        // The gesture is authored on the fixed graph shell. Runtime only binds
        // data-dependent bounds; it never creates fixed UI components.
        researchGraphGesture = researchGraphViewport.GetComponent<UIResearchGraphGesture>();
        if (researchGraphGesture == null)
        {
            Debug.LogError("[王国界面] Scene is missing authored UIResearchGraphGesture; graph panning will not be generated at runtime.");
            yield break;
        }
        Canvas.ForceUpdateCanvases();
        researchGraphGesture.Initialize(researchGraphViewport, researchGraphContent);
        researchTreePageBuilt = true;
        // Defer read-only validation and TMP visual inspection by one frame;
        // they can otherwise compete with the first research-page gesture.
        StartCoroutine(CompleteResearchTreeDiagnostics(definitions, buildStartTime));
        researchTreeBuildInProgress = false;
        // Let the newly created graph present one frame before the first
        // full visual refresh walks every node and connector visual.
        yield return null;
        if (!researchTreePageBuilt || researchGraphContent == null)
            yield break;
        RefreshResearchTreeVisuals();
        RefreshResearchQueueToolbar();
        if (selectedResearchNode != null)
            ShowResearchDetails(selectedResearchNode);
        Debug.Log($"[王国界面] Research page build complete: nodes={researchTreeNodes.Count}, elapsedMs={(Time.realtimeSinceStartup - buildStartTime) * 1000f:0.0}");
    }

    private System.Collections.IEnumerator CompleteResearchTreeDiagnostics(
        IReadOnlyList<Research> definitions,
        float buildStartTime)
    {
        yield return null;
        if (!researchTreePageBuilt || researchGraphContent == null)
            yield break;
        float layoutStartTime = Time.realtimeSinceStartup;
        LogRuntimeResearchNodeLayout();
        float layoutDurationMs = (Time.realtimeSinceStartup - layoutStartTime) * 1000f;
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchBuildPhase phase=layoutDiagnostics elapsedMs={layoutDurationMs:0.0} durationMs={layoutDurationMs:0.0}");
#endif

        // Validation is read-only but still walks every node and prerequisite.
        // Give the first interactive frame back to the graph before doing it.
        yield return null;
        if (!researchTreePageBuilt || researchGraphContent == null)
            yield break;
        float validationStartTime = Time.realtimeSinceStartup;
        ValidateRuntimeResearchGraph(definitions);
        float validationDurationMs = (Time.realtimeSinceStartup - validationStartTime) * 1000f;
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchBuildPhase phase=graphValidation elapsedMs={validationDurationMs:0.0} durationMs={validationDurationMs:0.0}");
#endif
    }

    private void LogResearchGridLayout(IReadOnlyList<Research> definitions,
        IReadOnlyDictionary<Research, Vector2> positions)
    {
        var occupied = new HashSet<Vector2Int>();
        int duplicateCount = 0;
        int backwardsEdgeCount = 0;
        int intermediateNodeCrossingCount = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research == null || !positions.TryGetValue(research, out Vector2 position))
                continue;
            Vector2Int grid = GetResearchGridPosition(position);
            if (!occupied.Add(grid))
                duplicateCount++;
            if (research.Prerequisites == null)
                continue;
            for (int p = 0; p < research.Prerequisites.Count; p++)
            {
                Research prerequisite = research.Prerequisites[p];
                if (prerequisite == null || !positions.TryGetValue(prerequisite, out Vector2 prerequisitePosition))
                    continue;
                if (GetResearchGridPosition(prerequisitePosition).x >= grid.x)
                {
                    backwardsEdgeCount++;
                    if (backwardsEdgeCount <= 40)
                        Debug.LogError($"[王国界面] Backward research edge: prerequisite={prerequisite.Id}@{GetResearchGridPosition(prerequisitePosition)} target={research.Id}@{grid}");
                }
                else
                {
                    Vector2Int prerequisiteGrid = GetResearchGridPosition(prerequisitePosition);
                    for (int column = prerequisiteGrid.x + 1; column < grid.x; column++)
                        if (occupied.Contains(new Vector2Int(column, grid.y)))
                            intermediateNodeCrossingCount++;
                }
            }
        }
        int maxColumn = 0;
        int maxRow = 0;
        foreach (Vector2Int cell in occupied)
        {
            maxColumn = Mathf.Max(maxColumn, cell.x);
            maxRow = Mathf.Max(maxRow, cell.y);
        }
        Debug.Log($"[王国界面] Research grid layout: nodes={occupied.Count}, duplicates={duplicateCount}, backwardsEdges={backwardsEdgeCount}, intermediateNodeCrossings={intermediateNodeCrossingCount}, maxGrid=({maxColumn},{maxRow}), grid={ResearchGridX}x{ResearchGridY}, layoutSource=topology-integer-grid");
    }

    private void LogRuntimeResearchNodeLayout()
    {
        var actualCells = new HashSet<string>();
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        foreach (KeyValuePair<Research, Button> pair in researchTreeNodes)
        {
            RectTransform rect = pair.Value == null ? null : pair.Value.transform as RectTransform;
            if (rect == null)
                continue;
            Vector2 position = GetNodeTopLeftPosition(rect);
            actualCells.Add(Mathf.RoundToInt(position.x) + "," + Mathf.RoundToInt(position.y));
            minX = Mathf.Min(minX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxX = Mathf.Max(maxX, position.x);
            maxY = Mathf.Max(maxY, position.y);
        }
        Debug.Log($"[王国界面] Research runtime node rects: nodes={researchTreeNodes.Count}, uniquePositions={actualCells.Count}, range=({minX},{minY})-({maxX},{maxY}), content={researchGraphContent.rect.size}");
    }

    private void ValidateRuntimeResearchGraph(IReadOnlyList<Research> definitions)
    {
        var occupied = new HashSet<Vector2Int>();
        int duplicateCount = 0;
        int backwardsEdgeCount = 0;
        int missingNodeCount = 0;
        int missingDragForwarderCount = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research == null)
                continue;
            if (!researchTreeNodes.TryGetValue(research, out Button button) || button == null)
            {
                missingNodeCount++;
                continue;
            }
            if (button.GetComponent<UIResearchGraphDragForwarder>() == null)
                missingDragForwarderCount++;
            RectTransform targetRect = button.transform as RectTransform;
            Vector2Int targetGrid = GetResearchGridPosition(targetRect == null
                ? Vector2.zero
                : GetNodeTopLeftPosition(targetRect));
            if (!occupied.Add(targetGrid))
                duplicateCount++;
            if (research.Prerequisites == null)
                continue;
            for (int p = 0; p < research.Prerequisites.Count; p++)
            {
                Research prerequisite = research.Prerequisites[p];
                if (prerequisite == null || !researchTreeNodes.TryGetValue(prerequisite, out Button prerequisiteButton) ||
                    prerequisiteButton == null)
                    continue;
                RectTransform prerequisiteRect = prerequisiteButton.transform as RectTransform;
                Vector2Int prerequisiteGrid = GetResearchGridPosition(prerequisiteRect == null
                    ? Vector2.zero
                    : GetNodeTopLeftPosition(prerequisiteRect));
                if (prerequisiteGrid.x >= targetGrid.x)
                    backwardsEdgeCount++;
            }
        }

        bool positiveBounds = researchGraphViewport != null && researchGraphContent != null &&
            researchGraphViewport.rect.width > 1f && researchGraphViewport.rect.height > 1f &&
            researchGraphContent.rect.width > 1f && researchGraphContent.rect.height > 1f;
        bool dragSurfacePresent = researchGraphContent != null &&
            researchGraphContent.Find("ResearchGraphDragSurface") != null;
        bool valid = duplicateCount == 0 && backwardsEdgeCount == 0 && missingNodeCount == 0 &&
            missingDragForwarderCount == 0 &&
            positiveBounds && dragSurfacePresent;
        Debug.Log($"[王国界面] Research graph runtime validation: valid={valid}, duplicates={duplicateCount}, backwardsEdges={backwardsEdgeCount}, missingNodes={missingNodeCount}, missingDragForwarders={missingDragForwarderCount}, positiveBounds={positiveBounds}, dragSurface={dragSurfacePresent}");
        if (!valid)
            Debug.LogError("[王国界面] Research graph runtime validation failed; see counts above.");
    }

    private void LogResearchVisualDiagnostics()
    {
        int loggedLabels = 0;
        foreach (KeyValuePair<Research, Button> pair in researchTreeNodes)
        {
            if (pair.Value == null)
                continue;
            Transform labelTransform = pair.Value.transform.Find("Label");
            TMP_Text label = labelTransform == null ? null : labelTransform.GetComponent<TMP_Text>();
            Text legacyLabel = labelTransform == null ? null : labelTransform.GetComponent<Text>();
            if (label == null && legacyLabel == null)
            {
                Debug.LogError($"[王国界面] Research visual: missing label node={pair.Key?.Id}");
                continue;
            }
            int visibleCharacters = 0;
            string labelText;
            string fontName;
            Vector2 labelSize;
            bool labelActive;
            Color labelColor;
            int vertexCount;
            bool canvasCulled;
            if (label != null)
            {
                // The diagnostic only prints the first three labels. Forcing
                // a mesh rebuild for all 79 nodes during page entry creates a
                // multi-second hitch and is unrelated to graph correctness.
                if (loggedLabels < 3)
                {
                    label.ForceMeshUpdate();
                    for (int characterIndex = 0; characterIndex < label.textInfo.characterCount; characterIndex++)
                        if (label.textInfo.characterInfo[characterIndex].isVisible)
                            visibleCharacters++;
                }
                labelText = label.text;
                fontName = label.font == null ? "null" : label.font.name;
                labelSize = label.rectTransform.rect.size;
                labelActive = label.gameObject.activeInHierarchy;
                labelColor = label.color;
                vertexCount = label.canvasRenderer == null ? 0 : 1;
                canvasCulled = label.canvasRenderer != null && label.canvasRenderer.cull;
            }
            else
            {
                labelText = legacyLabel.text;
                visibleCharacters = string.IsNullOrEmpty(labelText) ? 0 : labelText.Length;
                fontName = legacyLabel.font == null ? "null" : legacyLabel.font.name;
                labelSize = legacyLabel.rectTransform.rect.size;
                labelActive = legacyLabel.gameObject.activeInHierarchy;
                labelColor = legacyLabel.color;
                vertexCount = legacyLabel.cachedTextGenerator == null ? 0 : legacyLabel.cachedTextGenerator.vertexCount;
                canvasCulled = legacyLabel.canvasRenderer != null && legacyLabel.canvasRenderer.cull;
            }
            if (loggedLabels++ < 3)
                Debug.Log($"[王国界面] Research visual label: id={pair.Key?.Id}, text='{labelText}', active={labelActive}, font={fontName}, rect={labelSize}, visible={visibleCharacters}, color={labelColor}, vertices={vertexCount}, culled={canvasCulled}");
        }

        // Index node rectangles once. The previous diagnostic searched all
        // research nodes for every arrow, creating a seconds-long page-entry
        // hitch in the current 2735-arrow tree.
        var nodeRectByGrid = new Dictionary<Vector2Int, RectTransform>();
        foreach (KeyValuePair<Research, Button> nodePair in researchTreeNodes)
        {
            RectTransform nodeRect = nodePair.Value == null
                ? null
                : nodePair.Value.transform as RectTransform;
            if (nodeRect != null)
                nodeRectByGrid[GetResearchGridPosition(GetNodeTopLeftPosition(nodeRect))] = nodeRect;
        }

        int loggedArrows = 0;
        int misalignedArrows = 0;
        foreach (KeyValuePair<ResearchArrowCacheKey, Image> pair in researchArrowVisuals)
        {
            if (pair.Value == null)
                continue;
            RectTransform rect = pair.Value.rectTransform;
            if (nodeRectByGrid.TryGetValue(pair.Key.TargetGrid, out RectTransform nodeRect))
            {
                Vector2 nodeTopLeft = GetNodeTopLeftPosition(nodeRect);
                float arrowTop = researchGraphContent.rect.height - rect.anchoredPosition.y - rect.rect.height;
                float arrowCenter = arrowTop + rect.rect.height * .5f;
                float nodeCenter = nodeTopLeft.y + nodeRect.rect.height * .5f;
                float arrowRight = rect.anchoredPosition.x + rect.rect.width;
                if (Mathf.Abs(arrowCenter - nodeCenter) > .5f ||
                    Mathf.Abs(arrowRight - nodeTopLeft.x) > .5f)
                    misalignedArrows++;
                if (loggedArrows < 3)
                    Debug.Log($"[王国界面] Research visual end arrow alignment: from={pair.Key.From?.Id}, to={pair.Key.To?.Id}, arrowRight={arrowRight:0.##}, nodeLeft={nodeTopLeft.x:0.##}");
            }
            if (loggedArrows++ < 3)
                Debug.Log($"[王国界面] Research visual end arrow: from={pair.Key.From?.Id}, to={pair.Key.To?.Id}, local={rect.anchoredPosition}, size={rect.rect.size}");
        }
    }

    private Vector2 GetNodeTopLeftPosition(RectTransform node)
    {
        if (node == null || researchGraphContent == null)
            return Vector2.zero;
        return new Vector2(node.anchoredPosition.x,
            researchGraphContent.rect.height - node.anchoredPosition.y - node.rect.height);
    }

    private void RestoreResearchTreeSelection()
    {
        if (ResearchManager.Instance == null)
            return;
        string selectedId = ResearchManager.Instance.SelectedResearchId;
        if (string.IsNullOrWhiteSpace(selectedId))
            return;
        if (DataBase<Research>.TryFind(selectedId, out Research selected))
        {
            if (selectedResearchNode == null ||
                !string.Equals(selectedResearchNode.Id, selected.Id, StringComparison.Ordinal))
                selectedResearchNode = selected;
        }
        else
        {
            Debug.LogWarning("[王国界面] 研究选择已失效，忽略不存在的研究资产: " + selectedId);
            selectedResearchNode = null;
        }
    }

    private void BindAuthoredResearchTreeOverlay(RectTransform viewport)
    {
        Transform authoredToolbar = viewport.Find("ResearchTreeToolbar");
        if (authoredToolbar == null)
        {
            Debug.LogError("[王国界面] Scene is missing ResearchTreeToolbar; fixed research UI will not be generated at runtime.");
            return;
        }
        RectTransform toolbar = authoredToolbar as RectTransform;
        if (toolbar == null)
        {
            Debug.LogError("[王国界面] Authored ResearchTreeToolbar has no RectTransform.");
            return;
        }

        // Keep the authored search slot as an explicit inactive placeholder
        // for scene/test compatibility. The queue owns this toolbar region;
        // the placeholder never renders or receives input.
        Transform search = toolbar.Find("Search");
        if (search == null)
        {
            GameObject searchPlaceholder = new GameObject("Search", typeof(RectTransform));
            searchPlaceholder.transform.SetParent(toolbar, false);
            search = searchPlaceholder.transform;
        }
        search.gameObject.SetActive(false);

        researchTreeQueueLabel = toolbar.Find("Queue")?.GetComponent<TMP_Text>();
        if (researchTreeQueueLabel == null)
        {
            Debug.LogError("[王国界面] Authored ResearchTreeToolbar is missing Queue.");
            return;
        }
        // Queue entries are deliberately separated by real line breaks. A
        // delimiter without whitespace is not a reliable TMP wrap boundary,
        // so long queues used to render as one overflowing line.
        researchTreeQueueLabel.enableWordWrapping = true;
        researchTreeQueueLabel.overflowMode = TextOverflowModes.Overflow;
        researchTreeQueueLabel.alignment = TextAlignmentOptions.TopLeft;
        RectTransform queueRect = researchTreeQueueLabel.rectTransform;
        Canvas.ForceUpdateCanvases();
        Vector2 queueOffsetMin = queueRect.offsetMin;
        queueOffsetMin.x = 168f;
        queueRect.offsetMin = queueOffsetMin;
        toolbar.SetAsLastSibling();
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchQueueLayout wrap={researchTreeQueueLabel.enableWordWrapping} width={queueRect.rect.width:0.0} height={queueRect.rect.height:0.0}");
#endif
    }

    private void RefreshResearchQueueToolbar()
    {
        if (researchTreeQueueLabel == null)
            return;
        ResearchManager manager = ResearchManager.Instance;
        RectTransform queueRect = researchTreeQueueLabel.rectTransform;

        List<string> entries = new();
        List<string> layoutEntries = new();
        if (manager.ActiveResearch?.Definition != null)
        {
            var def = manager.ActiveResearch.Definition;
            var state = manager.GetState(def);
            entries.Add(def.Label + $"[{ResearchProgressText(state,state.Status)}]");
            layoutEntries.Add(def.Label);
        }

        IReadOnlyList<ResearchState> queue = manager.ResearchQueue;
        for (int i = 0; i < queue.Count; i++)
        {
            ResearchState state = queue[i];
            if (state?.Definition == null)
                continue;
            string stateLabel = state.Status == ResearchStatus.WaitingResources || !state.CostPaid
                ? "等待资源"
                : "正在排队";
            entries.Add((i + 1) + "." + state.Definition.Label + $"[{stateLabel}]");
            layoutEntries.Add((i + 1) + "." + state.Definition.Label + $"[{stateLabel}]");
        }

        // Use explicit Unicode escapes here because this source file contains
        // older mojibake literals. The queue header must contain a real line
        // break before entries so TMP can wrap and measure the queue.
        string text = entries.Count == 0
            ? "\u7814\u7a76\u961f\u5217\uFF1A\u7A7A"
            : "\u7814\u7a76\u961f\u5217\uFF1A\n" + string.Join("\n", entries);
        string layoutText = layoutEntries.Count == 0
            ? "\u7814\u7a76\u961f\u5217\uFF1A\u7A7A"
            : "\u7814\u7a76\u961f\u5217\uFF1A\n" + string.Join("\n", layoutEntries);
        float width = Mathf.Max(1f, queueRect.rect.width);
        bool layoutChanged = !string.Equals(lastResearchQueueLayoutText, layoutText, StringComparison.Ordinal) ||
            Mathf.Abs(lastResearchQueueLayoutWidth - width) > 0.5f;
        float preferredHeight = queueRect.rect.height;
        if (layoutChanged)
        {
            preferredHeight = researchTreeQueueLabel.GetPreferredValues(text, width, 4096f).y;
            float targetHeight = Mathf.Max(70f, preferredHeight + 8f);
            if (Mathf.Abs(queueRect.sizeDelta.y - targetHeight) > 0.5f)
            {
                Vector2 sizeDelta = queueRect.sizeDelta;
                sizeDelta.y = targetHeight;
                queueRect.sizeDelta = sizeDelta;
            }
            lastResearchQueueLayoutText = layoutText;
            lastResearchQueueLayoutWidth = width;
        }

        bool textChanged = researchTreeQueueLabel.text != text;
        if (textChanged)
        {
            researchTreeQueueLabel.text = text;
        }
#if UNITY_EDITOR
        if (layoutChanged || !researchQueueVisualDiagnosticLogged)
        {
            researchTreeQueueLabel.ForceMeshUpdate(false, false);
            int lineCount = researchTreeQueueLabel.textInfo == null
                ? 0
                : researchTreeQueueLabel.textInfo.lineCount;
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] ResearchQueueVisual entries={entries.Count} lines={lineCount} " +
                $"preferredHeight={preferredHeight:0.0} rectHeight={researchTreeQueueLabel.rectTransform.rect.height:0.0} " +
                $"wrap={researchTreeQueueLabel.enableWordWrapping} width={researchTreeQueueLabel.rectTransform.rect.width:0.0}");
            researchQueueVisualDiagnosticLogged = true;
        }
#endif
    }

    private Dictionary<Research, Vector2> CreateResearchTreePositions(IReadOnlyList<Research> definitions)
    {
        Dictionary<Research, Vector2> topologyPositions = CreateTopologyResearchTreePositions(definitions);
        bool topologyUsable = IsTopologyResearchLayoutUsable(definitions, topologyPositions);
        // 布局只由研究前置关系生成，拓扑诊断不再依赖资产坐标。
        Debug.Log($"[王国界面] Research topology decision: accepted={topologyUsable}, duplicates={researchTopologyDuplicateCount}, backwardsEdges={researchTopologyBackwardsEdgeCount}, inversions={researchTopologyInversionCount}, layoutSource=topology-integer-grid");
        return topologyPositions;
    }

    private bool IsTopologyResearchLayoutUsable(IReadOnlyList<Research> definitions,IReadOnlyDictionary<Research, Vector2> positions)
    {
        var occupied = new HashSet<Vector2Int>();
        var edges = new List<Vector4>();
        int maximumRow = 0;
        researchTopologyDuplicateCount = 0;
        researchTopologyBackwardsEdgeCount = 0;
        researchTopologyInversionCount = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research == null || !positions.TryGetValue(research, out Vector2 position))
                continue;
            int column = Mathf.RoundToInt((position.x - ResearchGraphPaddingX) / ResearchGridX);
            int row = Mathf.RoundToInt((position.y - ResearchGraphPaddingY - ResearchTopPadding) / ResearchGridY);
            if (!occupied.Add(new Vector2Int(column, row)))
                researchTopologyDuplicateCount++;
            maximumRow = Mathf.Max(maximumRow, row);
            if (research.Prerequisites == null)
                continue;
            for (int p = 0; p < research.Prerequisites.Count; p++)
            {
                Research prerequisite = research.Prerequisites[p];
                if (prerequisite == null || !positions.TryGetValue(prerequisite, out Vector2 prerequisitePosition))
                    continue;
                int prerequisiteColumn = Mathf.RoundToInt((prerequisitePosition.x - ResearchGraphPaddingX) / ResearchGridX);
                int prerequisiteRow = Mathf.RoundToInt((prerequisitePosition.y - ResearchGraphPaddingY - ResearchTopPadding) / ResearchGridY);
                if (prerequisiteColumn >= column)
                    researchTopologyBackwardsEdgeCount++;
                edges.Add(new Vector4(prerequisiteColumn, prerequisiteRow, column, row));
            }
        }

        // A topological placement is accepted only when its straight grid
        // corridors do not invert each other. Otherwise the reference asset
        // grid is the deterministic, already-authored ResearchTreeSK layout.
        for (int i = 0; i < edges.Count; i++)
        {
            Vector4 first = edges[i];
            for (int j = i + 1; j < edges.Count; j++)
            {
                Vector4 second = edges[j];
                if (first.x == second.x || first.z == second.z)
                    continue;
                bool horizontalOverlap = first.x < second.z && second.x < first.z;
                bool inverted = (first.y - second.y) * (first.w - second.w) < 0f;
                if (horizontalOverlap && inverted)
                    researchTopologyInversionCount++;
            }
        }

        // Keep a genuinely compact tree compact; there is no reason to make
        // a short DAG scroll merely to manufacture an empty vertical range.
        researchTreeMaximumRow = maximumRow;
        return researchTopologyDuplicateCount == 0 &&
            researchTopologyBackwardsEdgeCount == 0 &&
            researchTopologyInversionCount == 0;
    }

    private Dictionary<Research, Vector2> CreateTopologyResearchTreePositions(IReadOnlyList<Research> definitions)
    {
        // The graph is laid out on integer cells only.  A plain depth layout
        // is not sufficient for this project: nodes from different eras can
        // otherwise occupy the same visual region.  Era blocks are therefore
        // allocated from left to right, while prerequisite depth still wins
        // whenever a cross-era edge needs more columns.
        var ordered = new List<Research>();
        var known = new HashSet<Research>();
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research != null && known.Add(research))
                ordered.Add(research);
        }
        ordered.Sort(CompareResearchStable);

        var depth = new Dictionary<Research, int>();
        var visiting = new HashSet<Research>();
        var cycleNodes = new HashSet<Research>();
        for (int i = 0; i < ordered.Count; i++)
            ComputeResearchDepth(ordered[i], known, depth, visiting, cycleNodes);

        var row = new Dictionary<Research, int>();
        var column = new Dictionary<Research, int>();
        var occupied = new HashSet<Vector2Int>();
        int eraCount = Enum.GetValues(typeof(TechLevel)).Length;
        int previousEraEnd = -1;
        for (int era = 0; era < eraCount; era++)
        {
            var eraNodes = new List<Research>();
            for (int i = 0; i < ordered.Count; i++)
                if ((int)ordered[i].TechLevel == era)
                    eraNodes.Add(ordered[i]);
            if (eraNodes.Count == 0)
                continue;

            int eraStart = previousEraEnd + 2;
            // Assign columns in topological order within the era.  Stable ID
            // is only the tie-breaker; assigning all columns before sorting
            // allowed a same-era prerequisite to land to the right of its
            // dependent node.
            eraNodes.Sort((left, right) =>
            {
                int leftDepth = depth.TryGetValue(left, out int ld) ? ld : 0;
                int rightDepth = depth.TryGetValue(right, out int rd) ? rd : 0;
                int result = leftDepth.CompareTo(rightDepth);
                return result != 0 ? result : CompareResearchStable(left, right);
            });
            for (int i = 0; i < eraNodes.Count; i++)
            {
                Research research = eraNodes[i];
                int deepestPrerequisiteColumn = -1;
                if (research.Prerequisites != null)
                    for (int p = 0; p < research.Prerequisites.Count; p++)
                        if (research.Prerequisites[p] != null &&
                            column.TryGetValue(research.Prerequisites[p], out int prerequisiteColumn))
                            deepestPrerequisiteColumn = Mathf.Max(deepestPrerequisiteColumn, prerequisiteColumn);
                int depthColumn = depth.TryGetValue(research, out int researchDepth) ?
                    Mathf.Max(0, researchDepth - 1) : 0;
                column[research] = Mathf.Max(eraStart, depthColumn, deepestPrerequisiteColumn + 1);
            }

            // With columns fixed, put nodes on the nearest prerequisite lane.
            eraNodes.Sort((left, right) =>
            {
                int leftAnchor = GetMinimumPlacedPrerequisiteRow(left, row, known);
                int rightAnchor = GetMinimumPlacedPrerequisiteRow(right, row, known);
                int result = leftAnchor.CompareTo(rightAnchor);
                return result != 0 ? result : CompareResearchStable(left, right);
            });

            int eraEnd = eraStart;
            for (int i = 0; i < eraNodes.Count; i++)
            {
                Research research = eraNodes[i];
                int rowIndex = GetMinimumPlacedPrerequisiteRow(research, row, known);
                int researchColumn = column[research];
                while (!occupied.Add(new Vector2Int(researchColumn, rowIndex)))
                    rowIndex++;
                row[research] = rowIndex;
                eraEnd = Mathf.Max(eraEnd, researchColumn);
            }
            previousEraEnd = eraEnd;
            Debug.Log($"[王国界面] Research era block: era={(TechLevel)era}, nodes={eraNodes.Count}, columns={eraStart}-{eraEnd}");
        }

        RepairResearchRowsToAvoidNodeCrossing(ordered, known, column, row, occupied);

        var gridPositions = new Dictionary<Research, Vector2Int>();
        int maximumRow = 0;
        int maximumColumn = 0;
        foreach (KeyValuePair<Research, int> entry in row)
        {
            int researchColumn = column.TryGetValue(entry.Key, out int c) ? c : 0;
            Vector2Int grid = new(researchColumn, entry.Value);
            gridPositions[entry.Key] = grid;
            maximumRow = Mathf.Max(maximumRow, grid.y);
            maximumColumn = Mathf.Max(maximumColumn, grid.x);
        }
        researchTreeMaximumRow = maximumRow;
        Debug.Log($"[王国界面] Research graph topology candidate: nodes={gridPositions.Count}, layers={maximumColumn + 1}, rows={maximumRow + 1}, cycles={cycleNodes.Count}");

        var positions = new Dictionary<Research, Vector2>();
        foreach (KeyValuePair<Research, Vector2Int> entry in gridPositions)
            positions[entry.Key] = new Vector2(
                ResearchGraphPaddingX + entry.Value.x * ResearchGridX,
                ResearchGraphPaddingY + ResearchTopPadding + entry.Value.y * ResearchGridY);
        return positions;
    }

    private static void RepairResearchRowsToAvoidNodeCrossing(IReadOnlyList<Research> ordered,
        HashSet<Research> known, IReadOnlyDictionary<Research, int> columns,
        Dictionary<Research, int> rows, HashSet<Vector2Int> occupied)
    {
        // A long horizontal bus must never pass through a third-party node.
        // Move only the target on the first available integer row; this keeps
        // the relationship layout deterministic and leaves the connector free
        // to use the shared grid bus without visually entering another card.
        var byColumn = new List<Research>(ordered);
        byColumn.Sort((left, right) =>
        {
            int result = columns[left].CompareTo(columns[right]);
            return result != 0 ? result : CompareResearchStable(left, right);
        });
        for (int i = 0; i < byColumn.Count; i++)
        {
            Research target = byColumn[i];
            if (target == null || target.Prerequisites == null || !rows.TryGetValue(target, out int targetRow))
                continue;
            occupied.Remove(new Vector2Int(columns[target], targetRow));
            int candidateRow = targetRow;
            while (HasIntermediateNodeOnResearchRow(target, candidateRow, columns, rows, known) ||
                   occupied.Contains(new Vector2Int(columns[target], candidateRow)))
                candidateRow++;
            rows[target] = candidateRow;
            occupied.Add(new Vector2Int(columns[target], candidateRow));
        }
    }

    private static bool HasIntermediateNodeOnResearchRow(Research target, int targetRow,
        IReadOnlyDictionary<Research, int> columns, IReadOnlyDictionary<Research, int> rows,
        HashSet<Research> known)
    {
        if (target == null || target.Prerequisites == null || !columns.TryGetValue(target, out int targetColumn))
            return false;
        for (int i = 0; i < target.Prerequisites.Count; i++)
        {
            Research prerequisite = target.Prerequisites[i];
            if (prerequisite == null || !known.Contains(prerequisite) ||
                !columns.TryGetValue(prerequisite, out int prerequisiteColumn) ||
                prerequisiteColumn >= targetColumn)
                continue;
            for (int column = prerequisiteColumn + 1; column < targetColumn; column++)
                foreach (KeyValuePair<Research, int> entry in columns)
                    if (entry.Value == column && rows.TryGetValue(entry.Key, out int row) && row == targetRow)
                        return true;
        }
        return false;
    }

    private static int GetMinimumPlacedPrerequisiteRow(Research research,
        IReadOnlyDictionary<Research, int> placedRows, HashSet<Research> known)
    {
        int row = 0;
        bool hasPrerequisite = false;
        if (research != null && research.Prerequisites != null)
        {
            for (int i = 0; i < research.Prerequisites.Count; i++)
            {
                Research prerequisite = research.Prerequisites[i];
                if (prerequisite == null || !known.Contains(prerequisite) ||
                    !placedRows.TryGetValue(prerequisite, out int prerequisiteRow))
                    continue;
                row = hasPrerequisite ? Mathf.Min(row, prerequisiteRow) : prerequisiteRow;
                hasPrerequisite = true;
            }
        }
        return hasPrerequisite ? row : 0;
    }

    private static void ComputeResearchDepth(Research research, HashSet<Research> known,
        Dictionary<Research, int> depth, HashSet<Research> visiting, HashSet<Research> cycleNodes)
    {
        if (research == null || depth.ContainsKey(research))
            return;
        if (!visiting.Add(research))
        {
            cycleNodes.Add(research);
            return;
        }

        int maximumPrerequisiteDepth = 0;
        if (research.Prerequisites != null)
        {
            for (int i = 0; i < research.Prerequisites.Count; i++)
            {
                Research prerequisite = research.Prerequisites[i];
                if (prerequisite == null || !known.Contains(prerequisite))
                    continue;
                ComputeResearchDepth(prerequisite, known, depth, visiting, cycleNodes);
                if (depth.TryGetValue(prerequisite, out int prerequisiteDepth))
                    maximumPrerequisiteDepth = Mathf.Max(maximumPrerequisiteDepth, prerequisiteDepth);
            }
        }
        visiting.Remove(research);
        depth[research] = maximumPrerequisiteDepth + 1;
    }

    private static void OrderResearchLayer(Dictionary<int, List<Research>> layers, int layer,
        Dictionary<Research, int> row, Dictionary<Research, List<Research>> successors, bool forward)
    {
        if (!layers.TryGetValue(layer, out List<Research> nodes) || nodes.Count < 2)
            return;
        var scores = new Dictionary<Research, float>();
        for (int i = 0; i < nodes.Count; i++)
        {
            Research node = nodes[i];
            float total = 0f;
            int count = 0;
            if (forward && node.Prerequisites != null)
            {
                for (int p = 0; p < node.Prerequisites.Count; p++)
                {
                    Research prerequisite = node.Prerequisites[p];
                    if (prerequisite != null && row.TryGetValue(prerequisite, out int prerequisiteRow))
                    {
                        total += prerequisiteRow;
                        count++;
                    }
                }
            }
            else if (!forward && successors.TryGetValue(node, out List<Research> targets))
            {
                for (int t = 0; t < targets.Count; t++)
                    if (row.TryGetValue(targets[t], out int targetRow))
                    {
                        total += targetRow;
                        count++;
                    }
            }
            scores[node] = count == 0 ? row[node] : total / count;
        }
        nodes.Sort((left, right) =>
        {
            int result = scores[left].CompareTo(scores[right]);
            if (result != 0)
                return result;
            result = row[left].CompareTo(row[right]);
            return result != 0 ? result : CompareResearchStable(left, right);
        });
        // Keep the barycentre's absolute row instead of compacting every
        // layer to 0..N. FluffyResearchTree does the same: this preserves
        // vertical separation between independent branches and creates a
        // real scroll surface for a large research tree.
        int nextRow = 0;
        int groupOffset = Mathf.FloorToInt((nodes.Count - 1) * .5f);
        for (int i = 0; i < nodes.Count; i++)
        {
            int desiredRow = Mathf.RoundToInt(scores[nodes[i]]) - groupOffset;
            int assignedRow = Mathf.Max(nextRow, desiredRow);
            row[nodes[i]] = assignedRow;
            nextRow = assignedRow + 1;
        }
    }

    private static int CompareResearchStable(Research left, Research right)
    {
        int result = string.Compare(left == null ? string.Empty : left.Id,
            right == null ? string.Empty : right.Id, StringComparison.Ordinal);
        if (result != 0)
            return result;
        return string.Compare(left == null ? string.Empty : left.Label,
            right == null ? string.Empty : right.Label, StringComparison.Ordinal);
    }

    private static void ImproveResearchLayerCrossings(int layer, IReadOnlyList<Research> allResearch,
        Dictionary<Research, int> depth, Dictionary<Research, int> row,
        Dictionary<int, List<Research>> layers)
    {
        if (!layers.TryGetValue(layer, out List<Research> nodes) || nodes.Count < 2)
            return;
        int current = CountResearchCrossingsAtBoundary(layer - 1, allResearch, depth, row) +
            CountResearchCrossingsAtBoundary(layer, allResearch, depth, row);
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Research first = nodes[i];
            Research second = nodes[i + 1];
            int firstRow = row[first];
            row[first] = row[second];
            row[second] = firstRow;
            int candidate = CountResearchCrossingsAtBoundary(layer - 1, allResearch, depth, row) +
                CountResearchCrossingsAtBoundary(layer, allResearch, depth, row);
            if (candidate <= current)
                current = candidate;
            else
            {
                row[second] = row[first];
                row[first] = firstRow;
            }
        }
    }

    private static int CountResearchCrossingsAtBoundary(int boundary,
        IReadOnlyList<Research> allResearch, Dictionary<Research, int> depth,
        Dictionary<Research, int> row)
    {
        if (boundary < 0)
            return 0;
        var segments = new List<Vector2Int>();
        for (int i = 0; i < allResearch.Count; i++)
        {
            Research target = allResearch[i];
            if (target == null || target.Prerequisites == null ||
                !depth.TryGetValue(target, out int targetDepth) || targetDepth - 1 != boundary + 1)
                continue;
            for (int p = 0; p < target.Prerequisites.Count; p++)
            {
                Research prerequisite = target.Prerequisites[p];
                if (prerequisite == null || !depth.TryGetValue(prerequisite, out int prerequisiteDepth) ||
                    prerequisiteDepth - 1 != boundary || !row.ContainsKey(prerequisite) || !row.ContainsKey(target))
                    continue;
                segments.Add(new Vector2Int(row[prerequisite], row[target]));
            }
        }
        segments.Sort((left, right) =>
        {
            int result = left.x.CompareTo(right.x);
            return result != 0 ? result : left.y.CompareTo(right.y);
        });
        int crossings = 0;
        for (int i = 0; i < segments.Count; i++)
            for (int j = i + 1; j < segments.Count; j++)
                if (segments[i].x != segments[j].x && segments[i].y != segments[j].y &&
                    ((segments[i].x < segments[j].x && segments[i].y > segments[j].y) ||
                     (segments[i].x > segments[j].x && segments[i].y < segments[j].y)))
                    crossings++;
        return crossings;
    }

    private void CreateResearchEraBands(RectTransform content, IReadOnlyList<Research> definitions,
        IReadOnlyDictionary<Research, Vector2> positions)
    {
        string[] names = { "原始时代", "新石器时代", "中世纪", "工业时代", "太空时代", "极致时代", "远古科技时代" };
        string[] textures = { "ResearchEraAnimal", "ResearchEraNeolithic", "ResearchEraMedieval", "ResearchEraIndustrial", "ResearchEraSpacer", "ResearchEraUltra", "ResearchEraArchotech" };
        float[] minX = new float[names.Length];
        float[] maxX = new float[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            minX[i] = float.PositiveInfinity;
            maxX[i] = float.NegativeInfinity;
        }
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research == null || !positions.TryGetValue(research, out Vector2 position))
                continue;
            int era = Mathf.Clamp((int)research.TechLevel, 0, names.Length - 1);
            minX[era] = Mathf.Min(minX[era], position.x);
            maxX[era] = Mathf.Max(maxX[era], position.x + ResearchNodeWidth);
        }
        float runningX = 24f;
        for (int i = 0; i < names.Length; i++)
        {
            if (float.IsInfinity(minX[i]))
            {
                minX[i] = runningX;
                maxX[i] = runningX + 440f;
            }
            minX[i] = Mathf.Max(0f, Mathf.Min(minX[i] - 70f, runningX));
            maxX[i] = Mathf.Max(maxX[i], minX[i] + 440f);
            GameObject bandObject = KingdomUIPrefabLibrary.Instantiate(
                KingdomUIPrefabLibrary.ResearchEraBand, content);
            if (bandObject == null)
            {
                Debug.LogError("[王国界面] Missing reusable ResearchEraBand prefab");
                continue;
            }
            RectTransform band = bandObject.GetComponent<RectTransform>();
            band.name = "ResearchEraBand_" + i;
            band.anchorMin = Vector2.zero;
            band.anchorMax = Vector2.zero;
            band.pivot = Vector2.zero;
            band.anchoredPosition = new Vector2(minX[i], 0);
            band.sizeDelta = new Vector2(maxX[i] - minX[i], content.sizeDelta.y);
            Image image = band.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, .12f);
            image.sprite = LoadResearchTreeSprite("ResearchTree/" + textures[i]);
            image.preserveAspect = false;
            image.raycastTarget = false;
            band.SetAsFirstSibling();
            TMP_Text title = band.Find("Title").GetComponent<TMP_Text>();
            title.text = names[i];
            title.fontSize = 28;
            title.color = Copper;
            title.font = sharedFontAsset != null ? sharedFontAsset : TMP_Settings.defaultFontAsset;
            RectTransform titleRect = title.transform as RectTransform;
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(24, -62);
            titleRect.offsetMax = new Vector2(-24, -18);
            runningX = maxX[i] + 16f;
        }
    }

    private System.Collections.IEnumerator CreateResearchTreeLinks(RectTransform content, IReadOnlyList<Research> definitions,
        IReadOnlyDictionary<Research, Vector2> positions)
    {
        // Direct geometry equivalent of ResearchTreeSK.Edge.Add(from,to).
        // Every part is positioned by an integer (X,Y) grid key and reused
        // for all edges that traverse that same key. Curves are quarter
        // sprites from Lines/circle.png, exactly like the reference mod.
        int edgeCount = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research target = definitions[i];
            if (target == null || target.Prerequisites == null)
                continue;
            for (int p = 0; p < target.Prerequisites.Count; p++)
            {
                Research prerequisite = target.Prerequisites[p];
                if (prerequisite == null || !positions.TryGetValue(prerequisite, out Vector2 fromPosition) ||
                    !positions.TryGetValue(target, out Vector2 toPosition))
                    continue;
                Vector2Int from = GetResearchGridPosition(fromPosition);
                Vector2Int to = GetResearchGridPosition(toPosition);
                CreateReferenceResearchEdge(content, from, to, prerequisite, target);
                edgeCount++;
                if (edgeCount % 2 == 0)
                    yield return null;
            }
        }
    }

    private Vector2Int GetResearchGridPosition(Vector2 position)
    {
        int displayRow = Mathf.RoundToInt((position.y - ResearchGraphPaddingY - ResearchTopPadding) / ResearchGridY);
        return new Vector2Int(
            Mathf.RoundToInt((position.x - ResearchGraphPaddingX) / ResearchGridX),
            displayRow);
    }

    private void CreateReferenceResearchEdge(RectTransform content, Vector2Int from, Vector2Int to,
        Research prerequisite, Research target)
    {
        Pair<Research, Research> linkKey = new(prerequisite, target);
        Color color = GetResearchLinkColor(prerequisite, target);
        int dy = to.y - from.y;
        int dx = to.x - from.x;
        // This is a direct transcription of ResearchTreeSK.Edge.Add. The
        // dictionaries behind CreateResearchLinePart are the Unity equivalent
        // of the mod's VerticalLines/HorizontalLines/Curves caches, so a grid
        // segment can carry several prerequisite relationships.
        if (dy > 0)
        {
            CreateResearchCurve(content, from.x + 1, from.y + .5f,
                ResearchCurveType.RightTop, linkKey, color);
            for (int i = 0; i < dy; i++)
            {
                CreateResearchVerticalLine(content, from.x + 1, from.y + i + .5f,
                    linkKey, color, false);
                if (i > 0)
                    CreateResearchVerticalLine(content, from.x + 1, from.y + i + .5f,
                        linkKey, color, true);
            }
        }
        else if (dy < 0)
        {
            CreateResearchCurve(content, from.x + 1, from.y + .5f,
                ResearchCurveType.RightBottom, linkKey, color);
            for (int i = 0; i > dy; i--)
            {
                CreateResearchVerticalLine(content, from.x + 1, from.y + i - .5f,
                    linkKey, color, false);
                if (i < 0)
                    CreateResearchVerticalLine(content, from.x + 1, from.y + 1 + i - .5f,
                        linkKey, color, true);
            }
        }
        else
        {
            CreateResearchHorizontalLine(content, from.x + 1, to.y + .5f,
                linkKey, color, true);
        }

        if (dx > 0)
        {
            if (dy > 0)
                CreateResearchCurve(content, from.x + 1, to.y + .5f,
                    ResearchCurveType.LeftBottom, linkKey, color);
            else if (dy < 0)
                CreateResearchCurve(content, from.x + 1, to.y + .5f,
                    ResearchCurveType.LeftTop, linkKey, color);
            else
                CreateResearchHorizontalLine(content, to.x, to.y + .5f,
                    linkKey, color, true);

            for (int i = 0; i < dx - 1; i++)
            {
                CreateResearchHorizontalLine(content, from.x + 1 + i, to.y + .5f,
                    linkKey, color, false);
                CreateResearchHorizontalLine(content, from.x + 2 + i, to.y + .5f,
                    linkKey, color, true);
            }
        }

        CreateResearchHorizontalLine(content, from.x + 1, from.y + .5f,
            linkKey, color, true, true);
        CreateResearchEndArrow(content, to.x, to.y + .5f, linkKey, color);
    }

    private void CreateResearchVerticalLine(RectTransform content, float x, float y,
        Pair<Research, Research> linkKey, Color color, bool overCurve)
    {
        // ResearchTreeSK's X/Y are full-cell coordinates. The node itself is
        // placed at X*255+25, Y*60+5; the connector formulas intentionally
        // remain in the unpadded full-cell space.
        float pixelX = x * ResearchGridX - ResearchLineThickness * .5f;
        // ResearchTreeSK has two different classes here. The over-curve
        // piece is a short 4x20 overlay, not a shortened full-cell line.
        float pixelY = ResearchTopPadding + (overCurve
            ? y * ResearchGridY - ResearchCurveRadius
            : y * ResearchGridY + ResearchCurveRadius - ResearchConnectorOverlap);
        float pixelHeight = overCurve
            ? ResearchCurveRadius * 2f
            : ResearchGridY - ResearchCurveRadius * 2f + ResearchConnectorOverlap * 2f;
        CreateResearchLinePart(content, (overCurve ? "VO:" : "V:") + x + ":" + y,
            new Rect(pixelX, pixelY, ResearchLineThickness, pixelHeight),
            LoadResearchTreeSprite("ResearchTree/ResearchLineVertical"), linkKey, color);
    }

    private void CreateResearchHorizontalLine(RectTransform content, float x, float y,
        Pair<Research, Research> linkKey, Color color, bool overCurve, bool startLine = false)
    {
        float pixelX = startLine
            ? x * ResearchGridX - ResearchNodeMarginHalf
            : x * ResearchGridX + (overCurve ? -ResearchCurveRadius : ResearchCurveRadius - ResearchConnectorOverlap);
        float pixelY = ResearchTopPadding + y * ResearchGridY - ResearchLineThickness * .5f;
        float pixelWidth;
        if (startLine)
        {
            // Exact ResearchTreeSK.StartLine width. LineThickness is the
            // rectangle height, not part of its horizontal extent.
            pixelWidth = ResearchNodeMarginHalf - ResearchCurveRadius + ResearchConnectorOverlap;
        }
        else if (overCurve)
        {
            // Exact ResearchTreeSK.HorizontalLineOverCurve size: 20x4.
            pixelWidth = ResearchCurveRadius * 2f;
        }
        else
        {
            pixelWidth = ResearchGridX - ResearchCurveRadius * 2f + ResearchConnectorOverlap * 2f;
        }
        CreateResearchLinePart(content, (startLine ? "S:" : overCurve ? "HO:" : "H:") + x + ":" + y,
            new Rect(pixelX, pixelY, pixelWidth, ResearchLineThickness),
            LoadResearchTreeSprite("ResearchTree/ResearchLineHorizontal"), linkKey, color);
    }

    private void CreateResearchEndArrow(RectTransform content, float x, float y,
        Pair<Research, Research> linkKey, Color color)
    {
        // Exact ResearchTreeSK.EndArrow rectangle. With NodeMargins.x=50,
        // the 16px arrow ends at the target card's 25px left inset.
        float pixelWidth = ResearchNodeMarginHalf - ResearchCurveRadius + ResearchConnectorOverlap;
        float pixelX = x * ResearchGridX + ResearchCurveRadius - ResearchConnectorOverlap;
        float pixelY = ResearchTopPadding + y * ResearchGridY - ResearchArrowThickness * .5f;
        Image arrow = CreateResearchLinePart(content, "E:" + x + ":" + y,
            new Rect(pixelX, pixelY, pixelWidth, ResearchArrowThickness),
            LoadResearchTreeSprite("ResearchTree/ResearchLineEnd"), linkKey, color);
        if (arrow != null)
        {
            ResearchArrowCacheKey cacheKey = new(
                linkKey.First,
                linkKey.Second,
                new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y - .5f)));
            researchArrowVisuals[cacheKey] = arrow;
        }
    }

    private void CreateResearchCurve(RectTransform content, float x, float y,
        ResearchCurveType type, Pair<Research, Research> linkKey, Color color)
    {
        string key = "C:" + x + ":" + y + ":" + (int)type;
        float pixelX = x * ResearchGridX - ResearchCurveRadius;
        float pixelY = ResearchTopPadding + y * ResearchGridY - ResearchCurveRadius;
        Sprite sprite = LoadResearchCurveSprite(type);
        CreateResearchLinePart(content, key,
            new Rect(pixelX, pixelY, ResearchCurveRadius * 2f, ResearchCurveRadius * 2f),
            sprite, linkKey, color);
    }

    private Image CreateResearchLinePart(RectTransform content, string key, Rect rect,
        Sprite sprite, Pair<Research, Research> linkKey, Color color)
    {
        if (!researchSharedLineVisuals.TryGetValue(key, out Image image))
        {
            RectTransform lineParent = researchGraphLineLayer != null ? researchGraphLineLayer : content;
            GameObject lineObject = KingdomUIPrefabLibrary.Instantiate(
                KingdomUIPrefabLibrary.ResearchLine, lineParent);
            if (lineObject == null)
            {
                Debug.LogError("[王国界面] Missing reusable ResearchLine prefab; connector was skipped");
                return null;
            }
            lineObject.name = "ResearchLinePart_" + key;
            RectTransform part = lineObject.GetComponent<RectTransform>();
            // rect is expressed in the graph's top-left integer-grid space;
            // convert it against the actual content height, including both
            // graph paddings and the enlarged touch node.
            float graphHeight = content.rect.height;
            part.anchorMin = Vector2.zero;
            part.anchorMax = Vector2.zero;
            part.pivot = Vector2.zero;
            part.anchoredPosition = new Vector2(rect.x, graphHeight - rect.y - rect.height);
            part.sizeDelta = rect.size;
            image = part.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = false;
            image.raycastTarget = false;
            // Connectors must participate in the viewport RectMask2D; setting
            // this false lets lines escape into the navigation/detail panels.
            image.maskable = true;
            researchSharedLineVisuals[key] = image;
        }
        AddResearchTreeLinkVisual(linkKey, image);
        return image;
    }

    private void AddResearchTreeLinkVisual(Pair<Research, Research> linkKey, Image visual)
    {
        if (!researchTreeLinkVisuals.TryGetValue(linkKey, out List<Image> visuals))
        {
            visuals = new List<Image>();
            researchTreeLinkVisuals[linkKey] = visuals;
        }
        if (visual != null && !visuals.Contains(visual))
            visuals.Add(visual);
    }

    private void CreateResearchTreeNode(RectTransform content, Research research, Vector2 position)
    {
        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);
        ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
        Color accent = status == ResearchStatus.Completed ? Positive :
            status == ResearchStatus.Available ? Copper :
            status == ResearchStatus.Researching || status == ResearchStatus.Queued ?
                new Color(.38f, .68f, .86f, 1f) : TextSecondary;

        GameObject nodeObject = KingdomUIPrefabLibrary.Instantiate(
            KingdomUIPrefabLibrary.ResearchNode, content);
        if (nodeObject == null)
        {
            Debug.LogError("[王国界面] Missing reusable ResearchNode prefab; node was skipped: " + research.Id);
            return;
        }
        nodeObject.name = "ResearchNode_" + research.Id;
        RectTransform node = nodeObject.GetComponent<RectTransform>();
        if (node == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab has no RectTransform: " + research.Id);
            Destroy(nodeObject);
            return;
        }
        // ResearchTreeSK stores node positions as pixel rectangles. The
        // prefab supplies the complete visual and interaction component tree;
        // only data-driven geometry and presentation values are changed here.
        node.anchorMin = Vector2.zero;
        node.anchorMax = Vector2.zero;
        node.pivot = Vector2.zero;
        // `position` is expressed in the same top-left integer-grid space
        // used by ResearchTreeSK. Unity UI children use a bottom-left local
        // origin here, so convert the top edge once before placing the node.
        // Lines perform the identical conversion in CreateResearchLinePart;
        // assigning the raw y value was the reason nodes and glass-tube
        // connectors appeared disconnected and heavily overlapped.
        float nodeY = content.rect.height - position.y - ResearchNodeHeight;
        node.anchoredPosition = new Vector2(position.x, nodeY);
        node.sizeDelta = new Vector2(ResearchNodeWidth, ResearchNodeHeight);
        Image surface = node.GetComponent<Image>();
        if (surface == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing its authored Image: " + research.Id);
            Destroy(nodeObject);
            return;
        }
        surface.color = status == ResearchStatus.Locked ?
            new Color(.10f, .12f, .12f, .94f) : new Color(.16f, .19f, .19f, .96f);
        Button button = node.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing its authored Button: " + research.Id);
            Destroy(nodeObject);
            return;
        }
        button.targetGraphic = surface;
        // Research selection is rendered by RefreshResearchTreeVisuals;
        // ColorTint would overwrite the focused surface immediately after
        // the click and erase the intended P2 contrast.
        button.transition = Selectable.Transition.None;
        ApplyButtonColors(button, surface.color);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ShowResearchDetails(research));
        if (node.GetComponent<UIResearchGraphDragForwarder>() == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing its authored drag forwarder: " + research.Id);
            return;
        }
        researchTreeNodes[research] = button;
        Image[] outline = CreateResearchNodeBorder(node);
        if (outline == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing its authored SelectionBorder: " + research.Id);
            Destroy(nodeObject);
            return;
        }
        researchTreeOutlines[research] = outline;

        RectTransform frameRect = node.Find("EraFrame") as RectTransform;
        if (frameRect == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing EraFrame: " + research.Id);
            return;
        }
        Image frame = frameRect.GetComponent<Image>();
        if (frame == null)
        {
            Debug.LogError("[王国界面] ResearchNode EraFrame is missing Image: " + research.Id);
            return;
        }
        frame.sprite = LoadResearchTreeSprite(GetResearchEraTextureName(research.TechLevel));
        frame.color = Color.white;
        frame.preserveAspect = false;
        frame.raycastTarget = false;

        // P2 is the complete era background. P3 is the only clipped overlay;
        // a full-size progress track would cover P2 even at 0%.
        RectTransform progressRect = frameRect.Find("ProgressFill") as RectTransform;
        if (progressRect == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing ProgressFill: " + research.Id);
            return;
        }
        Image progress = progressRect.GetComponent<Image>();
        if (progress == null)
        {
            Debug.LogError("[王国界面] ResearchNode ProgressFill is missing Image: " + research.Id);
            return;
        }
        progress.sprite = LoadResearchTreeSprite(GetResearchProgressTextureName(research.TechLevel));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;
        progress.fillAmount = ResearchProgressFillAmount(state, status);
        // Preserve the authored P3 texture; fillAmount controls its visible area.
        progress.color = Color.white;
        progress.canvasRenderer.SetAlpha(1f);
        progress.enabled = true;
        progress.gameObject.SetActive(true);
        progressRect.SetAsLastSibling();
        progress.raycastTarget = false;

        // Use the same shared-font Label factory as every other Kingdom page.
        // This keeps Chinese glyph resolution and material setup identical to
        // the working resource/building/research-detail labels.
        string nodeTitle = string.IsNullOrEmpty(research.Label) ? research.Id : research.Label;
        TMP_Text titleLabel = ResearchNodeLabel("Label", node, nodeTitle, 24, Color.white);
        if (titleLabel == null)
            return;
        titleLabel.color = Color.white;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.enableWordWrapping = false;
        titleLabel.overflowMode = TextOverflowModes.Overflow;
        titleLabel.raycastTarget = false;
        titleLabel.enabled = true;
        TMP_Text costLabel = ResearchNodeLabel("Cost", node, state == null ? FormatResearchBaseCost(research) : state.BaseCost.ToGameString(), 20, TextSecondary);
        costLabel.alignment = TextAlignmentOptions.Center;
        TMP_Text progressLabel = ResearchNodeLabel("Progress", node, ResearchProgressText(state, status), 20, TextSecondary);
        progressLabel.alignment = TextAlignmentOptions.Center;
        TMP_Text stateLabel = ResearchNodeLabel("State", node, ResearchStateLabel(status), 20, accent);
        stateLabel.alignment = TextAlignmentOptions.Center;
        // The title is intentionally the last visual child. Era/progress
        // sprites must never cover the research name.
        titleLabel.transform.SetAsLastSibling();
        UITouchTooltip tooltip = node.GetComponent<UITouchTooltip>();
        if (tooltip == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing its authored tooltip: " + research.Id);
            return;
        }
        tooltip.SetTooltip(research.Description);
    }

    private static Image[] CreateResearchNodeBorder(RectTransform node)
    {
        if (node == null)
            return null;
        Transform border = node.Find("SelectionBorder");
        if (border == null)
            return null;
        border.SetSiblingIndex(Mathf.Min(1, node.childCount - 1));
        Image[] bars =
        {
            border.Find("Top")?.GetComponent<Image>(),
            border.Find("Bottom")?.GetComponent<Image>(),
            border.Find("Left")?.GetComponent<Image>(),
            border.Find("Right")?.GetComponent<Image>()
        };
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null)
                return null;
            bars[i].color = ResearchOutlineNormal;
            bars[i].raycastTarget = false;
        }
        return bars;
    }

    private static TMP_Text ResearchNodeLabel(string name, Transform parent, string text, int size, Color color)
    {
        RectTransform rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab is missing label: " + name);
            return null;
        }
        TextMeshProUGUI label = rect.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            Debug.LogError("[王国界面] ResearchNode prefab label has no TMP component: " + name);
            return null;
        }
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.font = sharedFontAsset != null ? sharedFontAsset : TMP_Settings.defaultFontAsset;
        if (label.font != null && label.font.material != null)
            label.fontSharedMaterial = label.font.material;
        label.raycastTarget = false;
        ConfigureResearchNodeLabelRect(rect, name);
        return label;
    }

    private static void ConfigureResearchNodeLabelRect(RectTransform rect, string name)
    {
        bool title = name == "Label";
        float left = title ? 0f : name == "Cost" ? 0f : name == "Progress" ? .27f : .67f;
        float right = title ? 1f : name == "Cost" ? .26f : name == "Progress" ? .66f : 1f;
        rect.anchorMin = new Vector2(left, title ? .34f : 0f);
        rect.anchorMax = new Vector2(right, title ? 1f : .34f);
        rect.offsetMin = new Vector2(title ? 8f : 2f, title ? 0f : 1f);
        rect.offsetMax = new Vector2(title ? -8f : -2f, title ? 0f : -1f);
        rect.pivot = new Vector2(.5f, .5f);
    }

    private static bool IsResearchCompleted(Research research)
    {
        return ResearchManager.Instance != null && research != null &&
            ResearchManager.Instance.IsResearchCompleted(research.Id);
    }

    private static ResearchStatus GetResearchStatus(Research research)
    {
        if (research != null && ResearchManager.Instance != null &&
            ResearchManager.Instance.States.TryGetValue(research, out ResearchState state))
            return state.Status;
        return ResearchStatus.Locked;
    }

    private Color GetResearchLinkColor(Research prerequisite, Research target)
    {
        if (IsResearchInSelectedPrerequisitePath(prerequisite, target))
            return ResearchFocusWhite;
        return ResearchArrowColor;
    }

    private bool IsResearchInSelectedPrerequisitePath(Research prerequisite, Research target)
    {
        if (selectedResearchNode == null || prerequisite == null || target == null)
            return false;
        // A shared bus segment may be registered by several edges.  Endpoint
        // membership alone is too permissive: it can light a segment between
        // two focused nodes even when that exact edge is not a prerequisite.
        // Validate the actual directed edge and use stable IDs for the
        // closure, so visual selection cannot depend on object identity.
        bool directPrerequisite = false;
        if (target.Prerequisites != null)
            for (int i = 0; i < target.Prerequisites.Count; i++)
                if (target.Prerequisites[i] != null &&
                    string.Equals(target.Prerequisites[i].Id, prerequisite.Id, StringComparison.Ordinal))
                {
                    directPrerequisite = true;
                    break;
                }
        if (!directPrerequisite)
            return false;
        HashSet<string> focused = CollectSelectedResearchPrerequisiteIds();
        return focused.Contains(prerequisite.Id) && focused.Contains(target.Id);
    }

    private HashSet<Research> CollectSelectedResearchPrerequisites()
    {
        var focused = new HashSet<Research>();
        if (selectedResearchNode != null)
            CollectPrerequisiteClosure(selectedResearchNode, focused);
        return focused;
    }

    private HashSet<string> CollectSelectedResearchPrerequisiteIds()
    {
        var focused = new HashSet<string>(StringComparer.Ordinal);
        CollectPrerequisiteIdClosure(selectedResearchNode, focused);
        return focused;
    }

    private static void CollectPrerequisiteIdClosure(Research research, HashSet<string> focused)
    {
        if (research == null || string.IsNullOrEmpty(research.Id) || !focused.Add(research.Id) ||
            research.Prerequisites == null)
            return;
        for (int i = 0; i < research.Prerequisites.Count; i++)
            CollectPrerequisiteIdClosure(research.Prerequisites[i], focused);
    }

    private static void CollectPrerequisiteClosure(Research research, HashSet<Research> focused)
    {
        if (research == null || !focused.Add(research) || research.Prerequisites == null)
            return;
        for (int i = 0; i < research.Prerequisites.Count; i++)
            CollectPrerequisiteClosure(research.Prerequisites[i], focused);
    }

    private void RefreshResearchTreeVisuals(bool refreshBus = true)
    {
        HashSet<string> focusedIds = CollectSelectedResearchPrerequisiteIds();
        if (selectedResearchNode != null &&
            !string.Equals(lastLoggedResearchClosureTarget, selectedResearchNode.Id, StringComparison.Ordinal))
        {
            var focusedLabels = new List<string>();
            foreach (KeyValuePair<Research, Button> node in researchTreeNodes)
                if (node.Key != null && focusedIds.Contains(node.Key.Id))
                    focusedLabels.Add(node.Key.Id);
            focusedLabels.Sort(StringComparer.Ordinal);
            lastLoggedResearchClosureTarget = selectedResearchNode.Id;
            Debug.Log($"[王国界面] 研究选择闭包目标={selectedResearchNode.Id}，节点={string.Join(",", focusedLabels)}");
            if (string.Equals(selectedResearchNode.Id, "ModernUniversity", StringComparison.Ordinal))
                Debug.Log($"[王国界面] 现代大学闭包检查：是否包含星际导航={focusedIds.Contains("InterstellarNavigation")}");
        }
        foreach (KeyValuePair<Research, Button> pair in researchTreeNodes)
        {
            Research research = pair.Key;
            Button button = pair.Value;
            if (research == null || button == null)
                continue;

            ResearchState state = null;
            if (ResearchManager.Instance != null)
                ResearchManager.Instance.States.TryGetValue(research, out state);
            ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
            Color accent = status == ResearchStatus.Completed ? Positive :
                status == ResearchStatus.Available ? Copper :
                status == ResearchStatus.Researching || status == ResearchStatus.Queued ?
                new Color(.38f, .68f, .86f, 1f) : TextSecondary;
            bool focused = focusedIds.Contains(research.Id);

            Image surface = button.targetGraphic as Image;
            if (surface != null)
                SetColorIfChanged(surface, focused ? ResearchFocusSurface : status == ResearchStatus.Locked ?
                    new Color(.10f, .12f, .12f, .94f) : new Color(.16f, .19f, .19f, .96f));
            if (researchTreeOutlines.TryGetValue(research, out Image[] outline) && outline != null)
            {
                for (int i = 0; i < outline.Length; i++)
                {
                    if (outline[i] == null)
                        continue;
                    outline[i].color = focused ? ResearchFocusWhite : ResearchOutlineNormal;
                    outline[i].enabled = true;
                }
            }
            Transform progress = button.transform.Find("EraFrame/ProgressFill");
            if (progress != null && progress.TryGetComponent(out Image progressImage))
            {
                float fill = ResearchProgressFillAmount(state, status);
                if (Mathf.Abs(progressImage.fillAmount - fill) > 0.0001f)
                    progressImage.fillAmount = fill;
                SetColorIfChanged(progressImage, Color.white);
                progressImage.canvasRenderer.SetAlpha(1f);
                progressImage.enabled = true;
                progress.gameObject.SetActive(true);
                // Keep the progress fill on top of its era frame, but only
                // reorder when it is not already last. SetAsLastSibling on
                // every node in every visual refresh dirties the graph
                // content and forces a Canvas layout pass, which is a
                // sustained cost while the research page is open.
                if (progress.GetSiblingIndex() != progress.parent.childCount - 1)
                    progress.SetAsLastSibling();
                if (!researchProgressVisualLogged)
                {
                    researchProgressVisualLogged = true;
                    Debug.Log($"[王国界面] Research progress visual: id={research.Id}, fill={progressImage.fillAmount:0.000}, color={progressImage.color}, alpha={progressImage.color.a:0.000}, active={progress.gameObject.activeSelf}, sibling={progress.GetSiblingIndex()}");
                }
            }
            Transform stateLabel = button.transform.Find("State");
            if (stateLabel != null && stateLabel.TryGetComponent(out TMP_Text stateText))
            {
                SetTextIfChanged(stateText, ResearchStateLabel(status));
                SetColorIfChanged(stateText, accent);
            }
            Transform costLabel = button.transform.Find("Cost");
            if (costLabel != null && costLabel.TryGetComponent(out TMP_Text costText))
                SetTextIfChanged(costText, state == null ? FormatResearchBaseCost(research) : state.BaseCost.ToGameString());
            Transform progressLabel = button.transform.Find("Progress");
            if (progressLabel != null && progressLabel.TryGetComponent(out TMP_Text progressText))
                SetTextIfChanged(progressText, ResearchProgressText(state, status));
            Transform label = button.transform.Find("Label");
            if (label != null && label.TryGetComponent(out TMP_Text researchLabel))
                SetColorIfChanged(researchLabel, status == ResearchStatus.Completed ? Color.white : TextPrimary);
            if (label != null && label.TryGetComponent(out Text legacyResearchLabel))
                SetColorIfChanged(legacyResearchLabel, status == ResearchStatus.Completed ? Color.white : TextPrimary);
            if (stateLabel != null && stateLabel.TryGetComponent(out TMP_Text refreshedStateText))
                SetColorIfChanged(refreshedStateText, status == ResearchStatus.Completed ? Color.white : accent);
        }

        if (refreshBus)
        {
            var focusedLineVisuals = new HashSet<Image>();
            foreach (KeyValuePair<Pair<Research, Research>, List<Image>> pair in researchTreeLinkVisuals)
            {
                bool focused = IsResearchInSelectedPrerequisitePath(pair.Key.First, pair.Key.Second);
                if (!focused)
                    continue;
                for (int i = 0; i < pair.Value.Count; i++)
                    if (pair.Value[i] != null)
                        focusedLineVisuals.Add(pair.Value[i]);
            }

            foreach (KeyValuePair<string, Image> pair in researchSharedLineVisuals)
            {
                Image visual = pair.Value;
                if (visual == null)
                    continue;
                bool focused = focusedLineVisuals.Contains(visual);
                visual.color = focused ? ResearchFocusWhite : ResearchArrowColor;
                visual.canvasRenderer.SetAlpha(1f);
            }

            // Ordinary buses go first; highlighted buses go last so their
            // pixels remain visible at crossings.
            if (researchGraphLineLayer != null)
            {
                int siblingIndex = 0;
                foreach (KeyValuePair<string, Image> pair in researchSharedLineVisuals)
                    if (pair.Value != null && !focusedLineVisuals.Contains(pair.Value))
                        pair.Value.transform.SetSiblingIndex(siblingIndex++);
                foreach (KeyValuePair<string, Image> pair in researchSharedLineVisuals)
                    if (pair.Value != null && focusedLineVisuals.Contains(pair.Value))
                        pair.Value.transform.SetSiblingIndex(siblingIndex++);
            }
        }
    }

    public bool SetResearchGraphLinesVisibleForPerfTest(bool visible)
    {
        if (researchGraphLineLayer == null)
            return false;
        researchGraphLineLayer.gameObject.SetActive(visible);
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] ResearchGraphLines visible={visible} " +
            $"parts={researchSharedLineVisuals.Count} links={researchTreeLinkVisuals.Count}");
#endif
        return true;
    }

    private void RefreshActiveResearchProgressVisual(ResearchManager manager)
    {
        ResearchState state = manager == null ? null : manager.ActiveResearch;
        Research research = state?.Definition;
        if (research == null || !researchTreeNodes.TryGetValue(research, out Button button) || button == null)
            return;

        ResearchStatus status = state.Status;
        Transform progress = button.transform.Find("EraFrame/ProgressFill");
        if (progress != null && progress.TryGetComponent(out Image progressImage))
        {
            float fill = ResearchProgressFillAmount(state, status);
            if (Mathf.Abs(progressImage.fillAmount - fill) > 0.0001f)
                progressImage.fillAmount = fill;
            progressImage.enabled = true;
            progress.gameObject.SetActive(true);
        }

        Transform progressLabel = button.transform.Find("Progress");
        if (progressLabel != null && progressLabel.TryGetComponent(out TMP_Text progressText))
            SetTextIfChanged(progressText, ResearchProgressText(state, status));
    }

    private static void SetTextIfChanged(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private static void SetColorIfChanged(Graphic graphic, Color value)
    {
        if (graphic != null && graphic.color != value)
            graphic.color = value;
    }

    private static string GetResearchTextureName(TechLevel techLevel)
    {
        return techLevel switch
        {
            TechLevel.Animal => "Animal",
            TechLevel.Neolithic => "Neolithic",
            TechLevel.Medieval => "Medieval",
            TechLevel.Industrial => "Industrial",
            TechLevel.Spacer => "Spacer",
            TechLevel.Ultra => "Ultra",
            TechLevel.Archotech => "Archotech",
            _ => "Animal"
        };
    }

    private static string GetResearchEraTextureName(TechLevel techLevel) =>
        "ResearchTree/ResearchEra" + GetResearchTextureName(techLevel);

    private static string ResearchProgressText(ResearchState state, ResearchStatus status)
    {
        if (status == ResearchStatus.Completed)
            return "100.0%";
        if (state == null)
            return "0%";
        return (state.ProgressRatio.ToDouble() * 100d).ToString("0.0") + "%";
    }

    private static string FormatResearchBaseCost(Research research)
    {
        if (research == null || string.IsNullOrWhiteSpace(research.BaseCost))
            return ExpantaNum.Zero.ToGameString();
        return ExpantaNum.TryParse(research.BaseCost, out ExpantaNum cost)
            ? cost.ToGameString()
            : research.BaseCost;
    }

    private static float ResearchProgressFillAmount(ResearchState state, ResearchStatus status)
    {
        if (status == ResearchStatus.Completed)
            return 1f;
        if (state == null)
            return 0f;
        return Mathf.Clamp01((float)state.ProgressRatio.ToDouble());
    }

    private static string GetResearchProgressTextureName(TechLevel techLevel) =>
        "ResearchTree/Progress" + GetResearchTextureName(techLevel);

    private static Sprite LoadResearchTreeSprite(string path)
    {
        if (researchTreeSprites.TryGetValue(path, out Sprite cached))
            return cached;

        // ResearchTree contains both importer kinds: the era backgrounds are
        // regular textures while Progress*.png and the line assets are often
        // imported as Sprite. Loading only Texture2D returns null for the
        // latter, and Unity renders an Image without a sprite as a white
        // rectangle. Prefer the authored Sprite so its import settings are
        // preserved, then support the regular texture form as a fallback.
        Sprite importedSprite = Resources.Load<Sprite>("Texture/" + path);
        if (importedSprite != null)
        {
            researchTreeSprites[path] = importedSprite;
            return importedSprite;
        }

        Texture2D texture = Resources.Load<Texture2D>("Texture/" + path);
        if (texture == null)
        {
            Debug.LogWarning($"[王国界面] ResearchTree sprite missing: Resources/Texture/{path}");
            return null;
        }
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f);
        researchTreeSprites[path] = sprite;
        return sprite;
    }

    private static Sprite LoadResearchCurveSprite(ResearchCurveType type)
    {
        string key = "ResearchTree/ResearchLineCircle/" + (int)type;
        if (researchTreeSprites.TryGetValue(key, out Sprite cached))
            return cached;
        Texture2D texture = Resources.Load<Texture2D>("Texture/ResearchTree/ResearchLineCircle");
        int halfWidth = texture.width / 2;
        int halfHeight = texture.height / 2;
        int x = type == ResearchCurveType.RightTop || type == ResearchCurveType.RightBottom
            ? halfWidth : 0;
        // Exact ResearchTreeSK Curve.TexRect values from the reference DLL:
        // LeftTop=(0,.5), LeftBottom=(0,0), RightTop=(.5,.5),
        // RightBottom=(.5,0). Unity Sprite.Create also uses a bottom-left
        // texture origin, so these are copied directly rather than inferred
        // from the enum names.
        int y = type == ResearchCurveType.LeftTop || type == ResearchCurveType.RightTop
            ? halfHeight : 0;
        Sprite sprite = Sprite.Create(texture,
            new Rect(x, y, halfWidth, halfHeight), new Vector2(.5f, .5f), 100f);
        researchTreeSprites[key] = sprite;
        return sprite;
    }
}
