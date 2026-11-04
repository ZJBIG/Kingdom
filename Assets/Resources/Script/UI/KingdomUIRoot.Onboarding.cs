using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private RectTransform onboardingOverlay;
    private RectTransform onboardingHighlight;
    private RectTransform onboardingPanel;
    private RectTransform onboardingArrow;
    private readonly RectTransform[] onboardingDims = new RectTransform[4];
    private TMP_Text onboardingTitle;
    private TMP_Text onboardingBody;
    private Button onboardingNext;
    private Button onboardingSkip;
    private OnboardingInputGate onboardingGate;
    private RectTransform onboardingTarget;
    private TutorialStageSnapshot onboardingStage;
    private float nextOnboardingRefresh;
    private Canvas onboardingCanvas;
    private readonly Vector3[] onboardingCorners = new Vector3[4];
    private RectTransform lastOnboardingFramedTarget;
    private string onboardingCompletionFeedback;
    private string onboardingCompletionNextGoal;
    private float onboardingCompletionUntil;
    private bool onboardingShowingCompletion;

#if UNITY_EDITOR
    public RectTransform OnboardingTargetForEditor => onboardingTarget;
    public TutorialStageSnapshot OnboardingStageForEditor => onboardingStage;
    public bool OnboardingCompletionVisibleForEditor => Time.unscaledTime < onboardingCompletionUntil;
    public void RefreshOnboardingForEditor() => RefreshOnboarding();
    public void TickOnboardingForEditor() => TickOnboarding();
#endif

    private void TickOnboarding()
    {
        if (Time.unscaledTime >= nextOnboardingRefresh)
        {
            nextOnboardingRefresh = Time.unscaledTime + .2f;
            RefreshOnboarding();
        }
        // Scroll inertia and graph framing continue between state refreshes.
        SyncOnboardingGeometry();
    }

    private bool BindOnboarding()
    {
        if (onboardingOverlay != null) return true;
        if (safeArea == null) return false;
        onboardingOverlay = safeArea.Find("OnboardingOverlay") as RectTransform;
        onboardingGate = GetComponent<OnboardingInputGate>();
        onboardingCanvas = GetComponentInParent<Canvas>();
        if (onboardingOverlay == null || onboardingGate == null) return false;
        onboardingHighlight = onboardingOverlay.Find("Highlight") as RectTransform;
        onboardingPanel = onboardingOverlay.Find("Panel") as RectTransform;
        onboardingArrow = onboardingOverlay.Find("Arrow") as RectTransform;
        string[] names = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
        for (int i = 0; i < names.Length; i++)
            onboardingDims[i] = onboardingOverlay.Find(names[i]) as RectTransform;
        onboardingTitle = onboardingPanel.Find("Title").GetComponent<TMP_Text>();
        onboardingBody = onboardingPanel.Find("Body").GetComponent<TMP_Text>();
        onboardingNext = onboardingPanel.Find("Next").GetComponent<Button>();
        onboardingSkip = onboardingPanel.Find("Skip").GetComponent<Button>();
        onboardingTitle.font = sharedFontAsset;
        onboardingBody.font = sharedFontAsset;
        onboardingArrow.GetComponent<TMP_Text>().font = sharedFontAsset;
        onboardingNext.GetComponentInChildren<TMP_Text>().font = sharedFontAsset;
        onboardingSkip.GetComponentInChildren<TMP_Text>().font = sharedFontAsset;
        onboardingNext.onClick.AddListener(() =>
        {
            if (onboardingShowingCompletion)
                onboardingCompletionUntil = 0f;
            else
                TutorialManager.Current?.AcknowledgeCurrentStage();
            HideOnboarding();
            RefreshOnboarding();
        });
        onboardingSkip.onClick.AddListener(() =>
        {
            TutorialManager.Current?.SkipCurrentStage();
            HideOnboarding();
            RefreshOnboarding();
        });
        // Layout and explicit canvas refreshes can move a target after LateUpdate.
        Canvas.willRenderCanvases += SyncOnboardingGeometry;
        return true;
    }

    private void SyncOnboardingGeometry()
    {
        if (!isActiveAndEnabled || onboardingTarget == null || onboardingOverlay == null ||
            !onboardingTarget.gameObject.activeInHierarchy || !onboardingOverlay.gameObject.activeSelf) return;
        PlaceOnboardingOverlay(onboardingTarget);
    }

    private void RefreshOnboarding()
    {
        if (SaveManager.Instance.IsLoading) { HideOnboarding(); return; }
        if (!BindOnboarding()) return;
        onboardingShowingCompletion = false;
        if (storyIllustrationPreview != null && storyIllustrationPreview.IsOpen)
        {
            HideOnboarding();
            return;
        }
        CacheRuntimeManagers();
        bool firstSession = gameManagerCache != null && gameManagerCache.State != null &&
            (gameManagerCache.State.TechLevel == TechLevel.Animal || !string.IsNullOrEmpty(tutorialSnapshot?.ActionId));
        TutorialManager manager = RefreshTutorialSnapshot(firstSession);
        onboardingStage = manager == null ? null : manager.EvaluateStage();
        if (manager != null && firstSession && populatedPage == "Overview" &&
            (!developmentGuidanceInitialized || developmentGuidanceRefreshTimer >= 2f))
        {
            RefreshDevelopmentGuidance();
            developmentGuidanceRefreshTimer = 0f;
        }
        if (manager != null && Time.unscaledTime < onboardingCompletionUntil)
        {
            ShowOnboardingMessage("新进展", onboardingCompletionFeedback +
                (string.IsNullOrEmpty(onboardingCompletionNextGoal) ? string.Empty : "\n下一步：" + onboardingCompletionNextGoal), false);
            onboardingShowingCompletion = true;
            onboardingNext.gameObject.SetActive(true);
            return;
        }
        if (onboardingStage == null)
        {
            HideOnboarding();
            return;
        }
        if (!onboardingStage.CanInteract)
        {
            ShowOnboardingWaiting(onboardingStage.Instruction);
            return;
        }
        if ((onboardingStage.Action == TutorialStageAction.Build && populatedPage != onboardingStage.Page) ||
            (onboardingStage.Action == TutorialStageAction.StartResearch &&
            (populatedPage != onboardingStage.Page || selectedResearchNode == null ||
             selectedResearchNode.Id != onboardingStage.TargetId)))
        {
            manager.ReopenCurrentStage();
            onboardingStage = manager.EvaluateStage();
        }
        if (onboardingStage == null)
        {
            HideOnboarding();
            return;
        }
        // A system can unlock while its page is already being viewed.
        if (onboardingStage.Action == TutorialStageAction.Navigate && populatedPage == onboardingStage.Page)
        {
            manager.RecordStagePageVisited(populatedPage);
            onboardingStage = manager.EvaluateStage();
        }
        if (onboardingStage == null)
        {
            HideOnboarding();
            return;
        }
        RectTransform target = ResolveOnboardingTarget(onboardingStage);
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            HideOnboarding();
            return;
        }
        Button targetButton = target.GetComponentInParent<Button>();
        bool selectSingleQuantity = false;
        if (onboardingStage.Action == TutorialStageAction.Build && buildingQuantityMode == BuildingQuantityMode.Ten &&
            targetButton != null && !targetButton.IsInteractable() &&
            buildingQuantityButtons.TryGetValue(BuildingQuantityMode.One, out Button singleQuantityButton))
        {
            target = singleQuantityButton.transform as RectTransform;
            targetButton = singleQuantityButton;
            selectSingleQuantity = true;
        }
        if (onboardingStage.Action != TutorialStageAction.Inspect &&
            targetButton != null && !targetButton.IsInteractable())
        {
            HideOnboarding();
            return;
        }
        if (lastOnboardingFramedTarget != target)
        {
            FrameOnboardingTarget(target);
            lastOnboardingFramedTarget = target;
        }
        if (!IsOnboardingTargetVisible(target))
        {
            HideOnboarding();
            return;
        }
        onboardingTarget = target;
        string instruction = selectSingleQuantity ? "选择「1个」。" : OnboardingInstruction(onboardingStage);
        bool layoutChanged = !onboardingOverlay.gameObject.activeSelf ||
            onboardingTitle.text != onboardingStage.Title || onboardingBody.text != instruction;
        onboardingTitle.text = onboardingStage.Title;
        onboardingBody.text = instruction;
        onboardingSkip.gameObject.SetActive(true);
        onboardingHighlight.gameObject.SetActive(true);
        onboardingArrow.gameObject.SetActive(true);
        for (int i = 0; i < onboardingDims.Length; i++) onboardingDims[i].gameObject.SetActive(true);
        onboardingNext.gameObject.SetActive(onboardingStage.Action == TutorialStageAction.Inspect);
        onboardingOverlay.gameObject.SetActive(true);
        onboardingOverlay.SetAsLastSibling();
        if (layoutChanged) Canvas.ForceUpdateCanvases();
        PlaceOnboardingOverlay(target);
        onboardingGate.Configure(target, onboardingOverlay, onboardingNext, onboardingSkip);
    }

    private static string OnboardingInstruction(TutorialStageSnapshot stage)
    {
        if (stage.Action != TutorialStageAction.Select) return stage.Instruction;
        string label = stage.TargetId;
        if ((stage.Page == "Resources" || stage.Page == "Production") &&
            DataBase<ResourceDef>.TryFind(stage.TargetId, out ResourceDef resource)) label = resource.Label;
        else if (stage.Page == "Buildings" && DataBase<BuildingDef>.TryFind(stage.TargetId, out BuildingDef building)) label = building.Label;
        else if ((stage.Page == "Research" || stage.Page == "Era") && DataBase<ResearchDef>.TryFind(stage.TargetId, out ResearchDef research)) label = research.Label;
        else if (stage.Page == "Workshop" && DataBase<WorkshopUpgradeDef>.TryFind(stage.TargetId, out WorkshopUpgradeDef upgrade)) label = upgrade.Label;
        else if (stage.Page == "Sectors" && DataBase<SectorDef>.TryFind(stage.TargetId, out SectorDef sector)) label = sector.Label;
        return "点击「" + label + "」，查看详情与条件。";
    }

    private void ShowOnboardingWaiting(string instruction)
    {
        ShowOnboardingMessage(onboardingStage.Title, instruction);
    }

    private void ShowOnboardingMessage(string title, string instruction, bool allowSkip = true)
    {
        onboardingGate.Clear();
        onboardingTarget = null;
        lastOnboardingFramedTarget = null;
        onboardingOverlay.gameObject.SetActive(true);
        onboardingOverlay.SetAsLastSibling();
        onboardingHighlight.gameObject.SetActive(false);
        onboardingArrow.gameObject.SetActive(false);
        for (int i = 0; i < onboardingDims.Length; i++) onboardingDims[i].gameObject.SetActive(false);
        onboardingTitle.text = title;
        onboardingBody.text = instruction;
        onboardingNext.gameObject.SetActive(false);
        onboardingSkip.gameObject.SetActive(allowSkip);
        onboardingPanel.anchoredPosition = new Vector2(0, 32);
    }

    private RectTransform ResolveOnboardingTarget(TutorialStageSnapshot stage)
    {
        if (stage == null) return null;
        if (stage.StageId == "orientation")
        {
            switch (stage.TargetId)
            {
                case "mission": return safeArea.Find("TopStatusBar/TopInfoShell/InfoGrid") as RectTransform;
                case "food": return topFoodValue?.transform.parent as RectTransform;
                case "population": return topPopulationValue?.transform.parent as RectTransform;
                case "productivity": return topCurrentResearchValue?.transform.parent as RectTransform;
                case "research": return topResearchPowerValue?.transform.parent as RectTransform;
                case "territory": return topTerritoryValue?.transform.parent as RectTransform;
                case "happiness": return topHappinessValue?.transform.parent as RectTransform;
                case "materials": return FindNavigationButton("Resources")?.transform as RectTransform;
            }
        }
        if (stage.Action == TutorialStageAction.Navigate)
            return FindNavigationButton(stage.Page)?.transform as RectTransform;
        if (stage.Action == TutorialStageAction.Build && DataBase<BuildingDef>.TryFind(stage.TargetId, out BuildingDef buildTarget) &&
            buildingActionButtons.TryGetValue(buildTarget, out Button buildButton))
            return buildButton.transform as RectTransform;
        if (stage.Action == TutorialStageAction.StartResearch)
            return detailActionButton == null ? null : detailActionButton.transform as RectTransform;
        if (stage.Page == "Era" && stage.Action == TutorialStageAction.Select &&
            pages.TryGetValue("Era", out RectTransform eraPage))
            return eraPage.Find("DataRows/EraTransitionSection/Content/EraTransitionTarget/Title") as RectTransform;
        if (stage.Action == TutorialStageAction.Inspect)
        {
            if (stage.StageId == "population") return topPopulationValue?.rectTransform;
            if (stage.StageId == "era-goal")
                return pages["Era"].Find("DataRows/EraTransitionSection/Content/EraTransitionTarget/Title") as RectTransform;
        }
        if (stage.Page == "Resources" && DataBase<ResourceDef>.TryFind(stage.TargetId, out ResourceDef resource) &&
            resourceAmountLabels.TryGetValue(resource, out TMP_Text resourceLabel) && resourceLabel != null)
            return resourceLabel.transform.parent.Find("Label") as RectTransform;
        if (stage.Page == "Buildings" && DataBase<BuildingDef>.TryFind(stage.TargetId, out BuildingDef building) &&
            buildingAmountLabels.TryGetValue(building, out TMP_Text buildingLabel) && buildingLabel != null)
            return buildingLabel.transform.parent.Find("Label") as RectTransform;
        if (stage.Page == "Research" && DataBase<ResearchDef>.TryFind(stage.TargetId, out ResearchDef research) &&
            researchTreeNodes.TryGetValue(research, out Button researchButton))
            return researchButton.transform as RectTransform;
        if (stage.Page == "Workshop" && DataBase<WorkshopUpgradeDef>.TryFind(stage.TargetId, out WorkshopUpgradeDef upgrade) &&
            workshopPurchaseButtons.TryGetValue(upgrade, out Button workshopButton) && workshopButton != null)
            return workshopButton.transform.parent.Find("Label") as RectTransform;
        if (stage.Page == "Sectors")
            foreach (SectorRowView view in sectorRowViews)
                if (view.Definition.Id == stage.TargetId) return view.Label.rectTransform;
        if (stage.Page == "Production" && DataBase<ResourceDef>.TryFind(stage.TargetId, out ResourceDef chainResource))
        {
            if (productionTree == null || productionTree.Root.Resource != chainResource)
            {
                RefreshProductionVisibility();
                if (!productionResources.Contains(chainResource)) return null;
                productionTree = new ResourceProductionTree(chainResource, DataBase<BuildingDef>.All,
                    DataBase<SectorDef>.All, productionResources.Contains,
                    productionBuildings.Contains, productionSectors.Contains);
                RebuildProductionGraph();
                CenterProductionRoot();
            }
            foreach (var pair in productionStatuses)
                if (pair.Key == productionTree.Root && pair.Value != null)
                    return pair.Value.transform.parent.Find("Label") as RectTransform;
        }
        return null;
    }

    private void FrameOnboardingTarget(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();
        if (populatedPage == "Research" && target.IsChildOf(researchGraphContent))
        {
            // Framing is not selection: the player still has to click the node.
            FocusResearchRect(target, null);
            return;
        }
        ScrollRect scroll = target.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        Vector3 targetCenter = scroll.viewport.InverseTransformPoint(target.TransformPoint(target.rect.center));
        Vector3 viewportCenter = scroll.viewport.rect.center;
        Vector3 delta = scroll.viewport.TransformVector(viewportCenter - targetCenter);
        Vector3 local = scroll.content.parent.InverseTransformVector(delta);
        Vector2 position = scroll.content.anchoredPosition;
        if (scroll.horizontal) position.x += local.x;
        if (scroll.vertical) position.y += local.y;
        scroll.StopMovement();
        scroll.content.anchoredPosition = position;
        Canvas.ForceUpdateCanvases();
    }

    private bool IsOnboardingTargetVisible(RectTransform target)
    {
        Camera camera = onboardingCanvas == null || onboardingCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : onboardingCanvas.worldCamera;
        target.GetWorldCorners(onboardingCorners);
        RectMask2D[] masks = target.GetComponentsInParent<RectMask2D>();
        Vector3 center = target.TransformPoint(target.rect.center);
        // A partially clipped target must never open a hole over another control.
        for (int i = 0; i < onboardingCorners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera,
                Vector3.Lerp(onboardingCorners[i], center, .001f));
            if (!RectTransformUtility.RectangleContainsScreenPoint(safeArea, point, camera)) return false;
            for (int j = 0; j < masks.Length; j++)
                if (masks[j].isActiveAndEnabled && !masks[j].IsRaycastLocationValid(point, camera)) return false;
        }
        return true;
    }

    private void PlaceOnboardingOverlay(RectTransform target)
    {
        target.GetWorldCorners(onboardingCorners);
        Vector2 min = onboardingOverlay.InverseTransformPoint(onboardingCorners[0]);
        Vector2 max = onboardingOverlay.InverseTransformPoint(onboardingCorners[2]);
        Rect bounds = onboardingOverlay.rect;
        min = Vector2.Max(min, bounds.min);
        max = Vector2.Min(max, bounds.max);
        PlaceOnboardingRect(onboardingHighlight, min - new Vector2(4, 4), max + new Vector2(4, 4));
        PlaceOnboardingRect(onboardingDims[0], new Vector2(bounds.xMin, max.y), bounds.max);
        PlaceOnboardingRect(onboardingDims[1], bounds.min, new Vector2(bounds.xMax, min.y));
        PlaceOnboardingRect(onboardingDims[2], new Vector2(bounds.xMin, min.y), new Vector2(min.x, max.y));
        PlaceOnboardingRect(onboardingDims[3], new Vector2(max.x, min.y), new Vector2(bounds.xMax, max.y));
        // Position authored objects from measured geometry, away from the highlighted control.
        float panelY = min.y > bounds.center.y ? bounds.yMin + 32 : bounds.yMax - onboardingPanel.rect.height - 32;
        float panelX = Mathf.Clamp((min.x + max.x) * .5f,
            bounds.xMin + onboardingPanel.rect.width * .5f + 24,
            bounds.xMax - onboardingPanel.rect.width * .5f - 24);
        onboardingPanel.anchoredPosition = new Vector2(panelX, panelY - bounds.yMin);
        onboardingArrow.anchorMin = onboardingArrow.anchorMax = new Vector2(.5f, .5f);
        onboardingArrow.anchoredPosition = new Vector2((min.x + max.x) * .5f,
            Mathf.Min(bounds.yMax - 32, max.y + 28));
    }

    private static void PlaceOnboardingRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = min;
        rect.sizeDelta = Vector2.Max(Vector2.zero, max - min);
    }

    private void HideOnboarding()
    {
        if (onboardingGate != null) onboardingGate.Clear();
        if (onboardingOverlay != null) onboardingOverlay.gameObject.SetActive(false);
        onboardingTarget = null;
        lastOnboardingFramedTarget = null;
    }

    private void RecordOnboardingDetail(string targetId)
    {
        TutorialManager.Current?.RecordStageTargetViewed(populatedPage, targetId);
    }
}
