using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the sector loop without changing sector rules. Home-system sectors
/// are shown as exploration objectives; interstellar sectors are shown as
/// supply-heavy campaigns. The manager remains the only authority for actions.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private readonly StringBuilder sectorDetailTextBuilder = new(1024);
    private readonly StringBuilder sectorCostTextBuilder = new(256);
    private SectorDefinition selectedSectorDefinition;
    private int lastSelectedSectorActionSignature = int.MinValue;
    private float sectorPageRefreshTimer;
    private sealed class SectorRowView
    {
        public SectorDefinition Definition;
        public RectTransform Row;
        public LayoutElement Layout;
        public Button Expand;
        public RectTransform Menu;
        public TMP_Text Label;
        public float CollapsedHeight;
        public bool Expanded;
        public string DiagnosticSignature;
    }
    private readonly List<SectorRowView> sectorRowViews = new();
    private RectTransform sectorRowsParent;

    private void BuildSectorRows(RectTransform parent)
    {
        BuildSectorRowsWithMenus(parent);
    }

    private void BuildSectorRowsWithMenus(RectTransform parent)
    {
        sectorRowsParent = parent;
        sectorRowViews.Clear();
        CacheRuntimeManagers();
        SectorManager sectors = gameManagerCache == null ? null : gameManagerCache.Sectors;
        IReadOnlyList<SectorDefinition> definitions = DataBase<SectorDefinition>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition definition = definitions[i];
            if (definition == null)
                continue;
            GameObject objectRow = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.SectorRow, parent);
            RectTransform row = objectRow.GetComponent<RectTransform>();
            LayoutElement layout = objectRow.GetComponent<LayoutElement>();
            Button rootButton = objectRow.GetComponent<Button>();
            if (row == null || layout == null || rootButton == null)
            {
                Debug.LogError("[SectorBuildings] Sector row prefab is incomplete.");
                Destroy(objectRow);
                continue;
            }
            objectRow.name = "SectorRow_" + definition.Id;
            ApplyListRowStyle(objectRow, sectorRowViews.Count);
            objectRow.SetActive(CanDisplaySector(definition, sectors));
            SectorRowView view = new SectorRowView
            {
                Definition = definition,
                Row = row,
                Layout = layout,
                Label = row.Find("Label")?.GetComponent<TMP_Text>(),
                CollapsedHeight = Mathf.Max(1f, layout.preferredHeight)
            };
            if (view.Label == null)
            {
                Debug.LogError("[SectorBuildings] Sector row prefab is missing Label.");
                Destroy(objectRow);
                continue;
            }
            view.Label.text = definition.Label ?? definition.Id;
            Transform expand = row.Find("Buildings");
            if (expand != null)
                expand.gameObject.SetActive(false);
            if (sectors != null && sectors.GetState(definition).Occupied)
                EnsureSectorBuildingControls(view);
            rootButton.onClick.AddListener(() => ShowSectorDetails(
                definition, sectors, gameManagerCache == null ? null : gameManagerCache.State,
                resourceManagerCache));
            sectorRowViews.Add(view);
        }
        RefreshSectorRowsAndLayout();
    }

    private static bool CanDisplaySector(
        SectorDefinition definition, SectorManager sectorManager)
    {
        if (definition == null)
            return false;
        IReadOnlyList<SectorDefinition> prerequisites = definition.PrerequisiteSectors;
        for (int i = 0; prerequisites != null && i < prerequisites.Count; i++)
        {
            SectorDefinition prerequisite = prerequisites[i];
            if (prerequisite == null || sectorManager == null ||
                !sectorManager.GetState(prerequisite).Occupied)
                return false;
        }
        return true;
    }

    private void EnsureSectorBuildingControls(SectorRowView view)
    {
        if (view == null || view.Expand != null)
            return;
        IReadOnlyList<SectorBuilding> buildings =
            BuildingManager.Instance.GetSectorBuildings(view.Definition);
        if (buildings.Count == 0)
            return;

        view.Expand = view.Row.Find("Buildings")?.GetComponent<Button>();
        if (view.Expand == null)
        {
            Debug.LogError("[SectorBuildings] Sector row prefab is missing its authored Buildings button.");
            return;
        }
        view.Expand.gameObject.SetActive(true);
        view.Expand.onClick.RemoveAllListeners();
        view.Expand.onClick.AddListener(() => ToggleSectorBuildingMenu(view));
        BuildSectorBuildingMenu(view, buildings);
    }

    private void ToggleSectorBuildingMenu(SectorRowView view)
    {
        if (view == null || view.Menu == null || gameManagerCache == null ||
            !gameManagerCache.Sectors.GetState(view.Definition).Occupied)
            return;
        view.Expanded = !view.Expanded;
        view.Menu.gameObject.SetActive(view.Expanded);
        RefreshSectorRowsAndLayout();
    }

    private void BuildSectorBuildingMenu(SectorRowView view, IReadOnlyList<SectorBuilding> buildings)
    {
        view.Menu = CreatePanel(view.Row, "SectorBuildingMenu", PanelRaised);
        view.Menu.anchorMin = new Vector2(0f, 0f);
        view.Menu.anchorMax = new Vector2(1f, 0f);
        view.Menu.pivot = new Vector2(.5f, 1f);
        view.Menu.anchoredPosition = new Vector2(0f, -view.CollapsedHeight);
        view.Menu.sizeDelta = new Vector2(0f, 416f);
        CreateText("Heading", view.Menu, "星区工程", Copper, TextAlignmentOptions.MidlineLeft)
            .rectTransform.offsetMin = new Vector2(28f, -58f);
        for (int i = 0; i < buildings.Count; i++)
        {
            SectorBuilding building = buildings[i];
            RectTransform card = CreatePanel(view.Menu, building.Id, Panel);
            card.gameObject.AddComponent<UIPageScrollDragForwarder>();
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.sizeDelta = new Vector2(-40f, 100f);
            card.anchoredPosition = new Vector2(0f, -76f - i * 112f);
            BuildingState state = BuildingManager.Instance.States.TryGetValue(building, out BuildingState stored)
                ? stored : null;
            CreateText("Name", card, building.Label ?? building.Id, TextPrimary,
                TextAlignmentOptions.MidlineLeft).rectTransform.offsetMin = new Vector2(20f, 50f);
            TMP_Text nameText = card.Find("Name").GetComponent<TMP_Text>();
            nameText.raycastTarget = true;
            Button nameButton = nameText.gameObject.AddComponent<Button>();
            nameButton.targetGraphic = nameText;
            nameButton.gameObject.AddComponent<UIPageScrollDragForwarder>();
            nameButton.onClick.AddListener(() => ShowBuildingDetails(building));
            CreateText("Amount", card, (state == null ? "0" : state.Amount.ToGameString()) + "/" + building.MaxAmount,
                TextSecondary, TextAlignmentOptions.MidlineLeft).rectTransform.offsetMin = new Vector2(360f, 50f);
            CreateText("Effect", card, "Logistics +" +
                (building.LogisticsProductionRate - building.LogisticsConsumptionRate).ToGameString() +
                "/s  Fleet +" + building.FleetPowerGranted.ToGameString() +
                "  Defense +" + building.DefensePowerGranted.ToGameString(),
                TextSecondary, TextAlignmentOptions.MidlineLeft).rectTransform.offsetMin = new Vector2(500f, 50f);
            CreateText("Cost", card, FormatResourceCosts(building.ResourceRequirements), Copper,
                TextAlignmentOptions.MidlineLeft).rectTransform.offsetMin = new Vector2(900f, 50f);
            Button build = CreateButton("Build", card, "建造", BuildableActionColor);
            Button demolish = CreateButton("Deconstruct", card, "拆除", new Color(.35f, .20f, .18f, 1f));
            build.gameObject.AddComponent<UIPageScrollDragForwarder>();
            demolish.gameObject.AddComponent<UIPageScrollDragForwarder>();
            PositionSectorAction(build, 1f);
            PositionSectorAction(demolish, 2f);
            build.interactable = BuildingManager.Instance.GetMaxBuildable(building, ExpantaNum.One) >= ExpantaNum.One;
            demolish.interactable = state != null && state.Amount >= ExpantaNum.One;
            build.onClick.AddListener(() =>
            {
                if (BuildingManager.Instance.TryBuild(building, ExpantaNum.One, out _))
                    UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Build);
                RebuildSectorMenu(view);
            });
            demolish.onClick.AddListener(() =>
            {
                if (BuildingManager.Instance.TryDeconstruct(building, ExpantaNum.One, out _))
                    UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Deconstruct);
                RebuildSectorMenu(view);
            });
        }
        view.Menu.gameObject.SetActive(view.Expanded);
    }

    private static void PositionSectorAction(Button button, float index)
    {
        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = new Vector2(128f, -20f);
        rect.anchoredPosition = new Vector2(-18f - (index - 1f) * 140f, 0f);
    }

    private void RebuildSectorMenu(SectorRowView view)
    {
        Destroy(view.Menu.gameObject);
        BuildSectorBuildingMenu(view, BuildingManager.Instance.GetSectorBuildings(view.Definition));
        RefreshSectorRowsAndLayout();
    }

    private void RefreshSectorRowsAndLayout()
    {
        float top = 0f;
        for (int i = 0; i < sectorRowViews.Count; i++)
        {
            SectorRowView view = sectorRowViews[i];
            bool eligible = CanDisplaySector(view.Definition, gameManagerCache?.Sectors);
            view.Row.gameObject.SetActive(eligible);
            if (!eligible)
                continue;
            bool occupied = gameManagerCache != null && gameManagerCache.Sectors.GetState(view.Definition).Occupied;
            if (occupied)
                EnsureSectorBuildingControls(view);
            else if (view.Expand != null)
            {
                view.Expanded = false;
                if (view.Menu != null)
                {
                    view.Menu.gameObject.SetActive(false);
                    Destroy(view.Menu.gameObject);
                }
                view.Expand.gameObject.SetActive(false);
            }
            if (view.Menu != null)
            {
                view.Menu.gameObject.SetActive(view.Expanded && occupied);
                RefreshSectorBuildingMenu(view);
            }
            float menuHeight = view.Menu == null ? 0f : view.Menu.sizeDelta.y;
            float height = view.CollapsedHeight +
                (view.Expanded && occupied ? menuHeight : 0f);
            view.Layout.preferredHeight = height;
            view.Row.sizeDelta = new Vector2(0f, height);
            view.Row.anchoredPosition = new Vector2(0f, -top);
            top += height;
        }
        nextRowTop = top;
        if (sectorRowsParent == null)
            return;
        float minimumHeight = sectorRowViews.Count == 0
            ? 1f
            : sectorRowViews[0].CollapsedHeight;
        sectorRowsParent.sizeDelta = new Vector2(0f, Mathf.Max(minimumHeight, top));
        RectTransform page = sectorRowsParent.parent as RectTransform;
        if (page != null)
            page.sizeDelta = new Vector2(0f, Mathf.Max(1400f, top + 180f));
        Canvas.ForceUpdateCanvases();
        if (pageScroll != null)
            ConfigureOuterPageScroll("Sectors", false, true);
        Vector2 viewportSize = pageScroll?.viewport == null
            ? Vector2.zero
            : pageScroll.viewport.rect.size;
        Vector2 contentSize = pageScroll?.content == null
            ? sectorRowsParent.rect.size
            : pageScroll.content.rect.size;
        bool hasPositiveBounds = viewportSize.x > 0f && viewportSize.y > 0f &&
            contentSize.x > 0f && contentSize.y > 0f;
        for (int i = 0; i < sectorRowViews.Count; i++)
        {
            SectorRowView view = sectorRowViews[i];
            string message = $"[SectorBuildings] sector={view.Definition.Id} expanded={view.Expanded} " +
                $"collapsed={view.CollapsedHeight} expandedHeight={view.CollapsedHeight + (view.Menu == null ? 0f : view.Menu.sizeDelta.y)} " +
                $"cards={(view.Menu == null ? 0 : Mathf.Max(0, view.Menu.childCount - 1))} " +
                $"viewport={viewportSize} content={contentSize}";
            if (hasPositiveBounds)
            {
                // This layout rebuild runs on a timer, so report a row only
                // when its measured layout actually changes; logging every
                // pass would flood the console while the page stays open.
                if (message != view.DiagnosticSignature)
                {
                    view.DiagnosticSignature = message;
                    Debug.Log(message);
                }
            }
            else
                Debug.LogError(message + " bounds must be positive.");
        }
    }

    private static void RefreshSectorBuildingMenu(SectorRowView view)
    {
        IReadOnlyList<SectorBuilding> buildings =
            BuildingManager.Instance.GetSectorBuildings(view.Definition);
        for (int i = 0; i < buildings.Count; i++)
        {
            SectorBuilding building = buildings[i];
            Transform card = view.Menu.Find(building.Id);
            if (card == null)
                continue;
            BuildingState state = BuildingManager.Instance.States.TryGetValue(
                building, out BuildingState stored) ? stored : null;
            TMP_Text amount = card.Find("Amount")?.GetComponent<TMP_Text>();
            if (amount != null)
                amount.text = (state == null ? "0" : state.Amount.ToGameString()) + "/" + building.MaxAmount;
            Button build = card.Find("Build")?.GetComponent<Button>();
            if (build != null)
                build.interactable = BuildingManager.Instance.GetMaxBuildable(
                    building, ExpantaNum.One) >= ExpantaNum.One;
            Button deconstruct = card.Find("Deconstruct")?.GetComponent<Button>();
            if (deconstruct != null)
                deconstruct.interactable = state != null && state.Amount >= ExpantaNum.One;
        }
    }

    private void RefreshSectorRowSummaries()
    {
        if (gameManagerCache == null)
            return;
        SectorManager sectorManager = gameManagerCache.Sectors;
        GameState state = gameManagerCache.State;
        ObserveSectorMilestones(sectorManager);
        RefreshSelectedSectorDetails(sectorManager, state, resourceManagerCache);
        RefreshSectorRowsAndLayout();
    }

#if UNITY_EDITOR
    public void RefreshSectorRowSummariesForEditor() => RefreshSectorRowSummaries();
#endif

    private void ObserveSectorMilestones(SectorManager sectorManager)
    {
        if (sectorManager == null)
            return;

        foreach (SectorState state in sectorManager.OrderedStates)
        {
            SectorDefinition definition = state == null ? null : state.Definition;
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                continue;

            string id = definition.Id;
            ExpantaNum progress = state.CampaignProgress;
            ExpantaNum previousCasualties = observedSectorCasualties.TryGetValue(
                id, out ExpantaNum recordedCasualties)
                ? recordedCasualties
                : ExpantaNum.Zero;
            bool wasOccupied = observedOccupiedSectorIds.Contains(id);
            if (sectorObservationInitialized)
            {
                if (!wasOccupied && state.Occupied)
                {
                    EnqueueRecentNotice("星区纳入王国：" + definition.Label +
                        " 已成为鼠族文明新的责任与资源来源。");
                }
                else if (observedSectorProgress.TryGetValue(id, out ExpantaNum previousProgress) &&
                    previousProgress < ExpantaNum.One && progress >= ExpantaNum.One)
                {
                    EnqueueRecentNotice("远征完成：" + definition.Label +
                        " 的道路已经打通，现在可以决定是否接纳这片星区。");
                }
                else if (previousCasualties <= ExpantaNum.Zero &&
                    state.CampaignCasualties > ExpantaNum.Zero)
                {
                    EnqueueRecentNotice("战报：" + definition.Label +
                        " 的远征出现伤亡；补给与维修是继续前进的代价。");
                }
            }

            observedSectorProgress[id] = progress;
            observedSectorCasualties[id] = state.CampaignCasualties;
            if (state.Occupied)
                observedOccupiedSectorIds.Add(id);
        }

        sectorObservationInitialized = true;
    }

    private void ShowSectorDetails(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null)
            return;

        // 星区详情不能继承残留的研究节点选择，否则通用按钮会被改回研究队列操作。
        selectedResearchNode = null;
        ShowDetails(
            definition.Label,
            BuildSectorDetailDescription(
                definition, sectorManager, state, resourceManager),
            definition.Id);
        selectedSectorDefinition = definition;
        ConfigureSectorAction(definition, sectorManager, state, resourceManager);
        ConfigureRelicDetails(definition);
        lastSelectedSectorActionSignature = GetSectorActionSignature(
            definition, sectorManager, state, resourceManager);
    }

    private string BuildSectorDetailDescription(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        StringBuilder body = sectorDetailTextBuilder;
        body.Clear();
        SectorState sectorState = sectorManager == null
            ? null
            : sectorManager.GetState(definition);
        body.AppendLine(definition.Description ?? string.Empty);
        body.AppendLine();
        body.AppendLine(definition.IsHomeSystem ? "本星系探索" : "远星星区战役");
        body.AppendLine("状态：" + GetSectorStateText(sectorManager, definition));
        if (GameManager.TryGetInstance(out GameManager gameManager) &&
            gameManager.UltraProject != null &&
            gameManager.UltraProject.IsCampaignDoctrineUnlocked)
        {
            body.AppendLine("文明工程姿态：" +
                GetUltraDoctrineLabel(gameManager.UltraProject.State.Doctrine));
        }
        if (sectorManager != null && sectorState != null &&
            !sectorState.Unlocked && !sectorState.Occupied)
            body.AppendLine("\u5f53\u524d\u963b\u788d\uff1a" +
                sectorManager.GetUnlockFailure(definition).GetDescription());
        IReadOnlyList<SectorDefinition> prerequisites = definition.PrerequisiteSectors;
        if (prerequisites == null || prerequisites.Count == 0)
            body.AppendLine("直接前置星区：无");
        else
        {
            body.AppendLine("直接前置星区：");
            for (int i = 0; i < prerequisites.Count; i++)
            {
                SectorDefinition prerequisite = prerequisites[i];
                if (prerequisite == null)
                    continue;
                string prerequisiteState = sectorManager == null
                    ? "状态未知"
                    : sectorManager.GetState(prerequisite).Occupied ? "已占领" : "未占领";
                body.Append("  ")
                    .Append(string.IsNullOrEmpty(prerequisite.Label) ? prerequisite.Id : prerequisite.Label)
                    .Append(" / ")
                    .AppendLine(prerequisiteState);
            }
        }
        IReadOnlyList<Pair<Resource, ExpantaNum>> occupiedRates =
            definition.OccupiedResourceRatesPerSecond;
        body.AppendLine("当前占领持续产出：" +
            (occupiedRates == null || occupiedRates.Count == 0
                ? "无"
                : FormatResourceCosts(
                    occupiedRates,
                    ProgressionModifierManager.Current) + "/s"));

        if (state == null)
        {
            body.AppendLine("运行时状态尚未初始化。");
        }
        else if (definition.IsHomeSystem)
        {
            SectorExplorationPreview explorationPreview = sectorManager == null
                ? null
                : sectorManager.GetExplorationPreview(definition, state, resourceManager);
            if (explorationPreview != null)
            {
                body.AppendLine("探索方式：仅按持续资源供给推进");
                body.AppendLine("预计剩余：" + explorationPreview.EstimatedSecondsRemaining.ToGameString() + " 秒");
                body.AppendLine("食物补给：" + FormatPerMinute(explorationPreview.FoodCostPerSecond) + "/min");
                body.AppendLine("战略资源补给：" +
                    FormatResourceCostsPerMinute(explorationPreview.ResourceCostsPerSecond));
                body.AppendLine(explorationPreview.HasSupply ? "当前补给：充足" : "当前补给：不足");
                if (!sectorState.ColonizationActive)
                {
                    body.AppendLine(explorationPreview.HasSupply
                        ? "当前阻碍：无，可开始星区探索"
                        : "当前阻碍：持续补给不足");
                }
            }
            body.AppendLine("探索不需要战斗力，仅消耗本页列出的持续资源。");
        }
        else if (sectorManager != null)
        {
            SectorCampaignPreview preview = sectorManager.GetCampaignPreview(definition, state, resourceManager);
            body.AppendLine("领土回报：" + definition.TerritoryReward.ToGameString());
            body.AppendLine("占领资源回报：" + FormatResourceCosts(definition.ResourceRewards));
            body.AppendLine("战斗比率：" + preview.CombatRatio.ToGameString());
            body.AppendLine("舰队生存倍率：" + preview.FleetSurvivalFactor.ToGameString());
            body.AppendLine("舰队整备度：" + preview.FleetReadiness.ToGameString());
            body.AppendLine("当前伤亡：" + preview.CurrentCasualties.ToGameString());
            body.AppendLine("补给满意度：" + preview.SupplySatisfaction.ToGameString());
            body.AppendLine("电力满意度：" + preview.PowerSatisfaction.ToGameString());
            body.AppendLine("物流满意度：" + preview.LogisticsSatisfaction.ToGameString());
                body.AppendLine("推进速度：" + preview.ProgressPerSecond.ToGameString() + "/s");
            body.AppendLine(preview.EstimatedSecondsRemaining > ExpantaNum.Zero
                ? "预计完成：" + preview.EstimatedSecondsRemaining.ToGameString() + " 秒"
                : "预计完成：无法估算（当前条件不支持推进）");
            body.AppendLine("预计伤亡：" + preview.CasualtiesPerSecond.ToGameString() + "/s");
            body.AppendLine("食物补给：" + FormatPerMinute(preview.FoodCostPerSecond) + "/min");
            body.AppendLine("战略资源补给：" +
                FormatResourceCostsPerMinute(preview.ResourceCostsPerSecond));
            body.AppendLine(preview.EstimatedRepairAmount > ExpantaNum.Zero
                ? "预计维修材料：" + FormatResourceCosts(preview.FleetRepairCosts)
                : "预计维修材料：无");
            body.AppendLine(preview.HasOngoingSupplyCost
                ? "按当前库存可维持：" + preview.EstimatedSupplySeconds.ToGameString() + " 秒"
                : "按当前库存可维持：无限（无持续补给成本）");
            body.AppendLine(preview.HasSupply ? "当前补给：足够" : "当前补给：不足");
            if (!sectorState.CampaignActive)
            {
                string blocker = sectorState.CampaignCasualties > ExpantaNum.Zero
                    ? "舰队受损，需先维修"
                    : !preview.HasSupply
                        ? "持续远征补给不足"
                        : preview.ProgressPerSecond <= ExpantaNum.Zero
                            ? "战斗或后勤条件不足，远征无法推进"
                            : "无，可开始远星战役";
                body.AppendLine("当前阻碍：" + blocker);
            }
            if (preview.ProgressPerSecond <= ExpantaNum.Zero)
                body.AppendLine("警告：当前战斗或后勤条件不足，战役不会推进，继续行动只会增加伤亡。");
            else if (preview.LogisticsSatisfaction < ExpantaNum.One ||
                preview.SupplySatisfaction < ExpantaNum.One ||
                preview.PowerSatisfaction < ExpantaNum.One)
                body.AppendLine("提示：补给尚未完善，战役推进速度和舰队安全性低于最佳状态。");
            body.AppendLine("提示：远星战役是长期后勤行动，建议先完成舰队、燃料和先进材料准备。");
        }
        return body.ToString();
    }

    private void RefreshSelectedSectorDetails(
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        SectorDefinition definition = selectedSectorDefinition;
        if (definition == null || detailBody == null)
            return;
        float preservedScrollPosition = requirementGesture == null
            ? 1f
            : requirementGesture.GetNormalizedPosition();

        string value = definition.Label + "\n\n" +
            BuildSectorDetailDescription(
                definition, sectorManager, state, resourceManager) +
            "\n\n标识：" + definition.Id;
        if (detailBody.text != value)
        {
            detailBody.text = value;
            LayoutResourceDetailsBody();
            if (requirementGesture != null)
                requirementGesture.SetNormalizedPosition(preservedScrollPosition);
        }

        int actionSignature = GetSectorActionSignature(
            definition, sectorManager, state, resourceManager);
        ConfigureRelicDetails(definition);
        if (actionSignature == lastSelectedSectorActionSignature)
            return;
        ConfigureSectorAction(definition, sectorManager, state, resourceManager);
        lastSelectedSectorActionSignature = actionSignature;
    }

    private static int GetSectorActionSignature(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null || sectorManager == null || state == null)
            return 0;
        SectorState sectorState = sectorManager.GetState(definition);
        int signature = sectorState.Unlocked ? 1 : 0;
        signature = unchecked(signature * 31 + state.Version);
        if (sectorState.Occupied) signature |= 2;
        if (sectorState.ColonizationActive) signature |= 4;
        if (sectorState.CampaignActive) signature |= 8;
        if (sectorState.CampaignProgress >= ExpantaNum.One) signature |= 16;
        if (sectorState.CampaignCasualties > ExpantaNum.Zero) signature |= 32;
        if (resourceManager != null) signature |= 64;
        if (!sectorState.Unlocked)
            signature |= (int)sectorManager.GetUnlockFailure(definition) << 7;
        if (resourceManager != null)
        {
            if (definition.IsHomeSystem)
            {
                AppendResourceStateVersions(
                    ref signature,
                    resourceManager,
                    definition.ColonizationResourceRatesPerSecond);
            }
            else
            {
                SectorCampaignPreview preview = sectorManager.GetCampaignPreview(
                    definition, state, resourceManager);
                if (preview != null)
                {
                    AppendResourceStateVersions(
                        ref signature,
                        resourceManager,
                        preview.ResourceCostsPerSecond);
                    AppendResourceStateVersions(
                        ref signature,
                        resourceManager,
                        preview.FleetRepairCosts);
                }
            }
        }
        if (GameManager.TryGetInstance(out GameManager gameManager) &&
            gameManager.UltraProject != null)
        {
            signature = unchecked(signature * 31 + gameManager.UltraProject.State.Version);
            signature = unchecked(signature * 31 + gameManager.Relic.State.Version);
        }
        return signature;
    }

    private static void AppendResourceStateVersions(
        ref int signature,
        ResourceManager resourceManager,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        for (int i = 0; costs != null && i < costs.Count; i++)
        {
            Resource resource = costs[i].First;
            int version = -1;
            if (resource != null && resourceManager.States.TryGetValue(
                    resource, out ResourceState resourceState))
                version = resourceState.Version;
            signature = unchecked(signature * 31 + version);
        }
    }

    private void ConfigureSectorAction(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null || sectorManager == null || state == null)
            return;

        SectorState sectorState = sectorManager.GetState(definition);
        if (sectorState == null)
            return;
        HideUltraDoctrineButton();
        if (sectorState.Occupied)
        {
            if (detailActionButton != null)
                detailActionButton.gameObject.SetActive(false);
            if (!definition.IsHomeSystem)
                ConfigureCampaignDoctrineAction(state);
            return;
        }

        // 已完成的远星战役必须直接显示占领操作。
        if (sectorState.CampaignProgress >= ExpantaNum.One)
        {
            ConfigureActionButton("\u5360\u9886\u661f\u533a", () => OccupySector(definition));
            ConfigureCampaignDoctrineAction(state);
            return;
        }

        // 保持现有单按钮详情结构。舰队有伤亡时优先维修，避免损坏舰队被带入新战役。
        if (sectorState.CampaignCasualties > ExpantaNum.Zero && resourceManager != null)
        {
            ConfigureActionButton("\u7ef4\u4fee\u8230\u961f", () => RepairSectorFleet(definition));
            ConfigureCampaignDoctrineAction(state);
            return;
        }

        if (!sectorState.Unlocked)
        {
            SectorOperationFailure failure = sectorManager.GetUnlockFailure(definition);
            bool canUnlock = failure == SectorOperationFailure.None;
            ConfigureActionButton(
                canUnlock ? "\u89e3\u9501\u661f\u533a" : failure.GetDescription(),
                () => UnlockSector(definition));
            if (detailActionButton != null)
                detailActionButton.interactable = canUnlock;
            if (!definition.IsHomeSystem)
                ConfigureCampaignDoctrineAction(state);
            return;
        }

        if (definition.IsHomeSystem)
        {
            SectorExplorationPreview explorationPreview = sectorManager.GetExplorationPreview(
                definition, state, resourceManager);
            bool canExplore = explorationPreview != null && explorationPreview.HasSupply;
            ConfigureActionButton(
                sectorState.ColonizationActive ? "\u6682\u505c\u661f\u533a\u63a2\u7d22" : "\u5f00\u59cb\u661f\u533a\u63a2\u7d22",
                sectorState.ColonizationActive
                    ? (UnityEngine.Events.UnityAction)(() => PauseColonization(definition))
                    : () => StartColonization(definition));
            if (detailActionButton != null && !sectorState.ColonizationActive)
                detailActionButton.interactable = canExplore;
            return;
        }

        if (sectorState.CampaignActive)
        {
            ConfigureActionButton("\u6682\u505c\u8fdc\u661f\u6218\u5f79", () => PauseCampaign(definition));
            ConfigureCampaignDoctrineAction(state);
            return;
        }

        SectorCampaignPreview campaignPreview = sectorManager.GetCampaignPreview(
            definition, state, resourceManager);
        bool anotherCampaignActive = state.Campaign.Active &&
            !string.Equals(
                state.Campaign.TargetSectorId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase);
        bool canStartCampaign = campaignPreview != null &&
            campaignPreview.HasSupply && campaignPreview.ProgressPerSecond > ExpantaNum.Zero &&
            !anotherCampaignActive;
        string campaignLabel = canStartCampaign
            ? "\u5f00\u59cb\u8fdc\u661f\u6218\u5f79"
            : anotherCampaignActive
                ? "\u5df2\u6709\u5176\u4ed6\u8fdc\u661f\u6218\u5f79\u8fdb\u884c\u4e2d"
                : campaignPreview == null || !campaignPreview.HasSupply
                    ? "\u6301\u7eed\u8fdc\u5f81\u8865\u7ed9\u4e0d\u8db3"
                    : "\u6218\u6597\u6216\u540e\u52e4\u6761\u4ef6\u4e0d\u8db3";
        ConfigureActionButton(campaignLabel, () => StartCampaign(definition));
        if (detailActionButton != null)
            detailActionButton.interactable = canStartCampaign;
        ConfigureCampaignDoctrineAction(state);
    }

    private void UnlockSector(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryUnlock(definition, out failure))
        {
            ShowTooltip("\u661f\u533a\u89e3\u9501\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        ShowTooltip("\u661f\u533a\u5df2\u89e3\u9501");
        RefreshSectorDetails(definition);
    }

    private void StartColonization(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryAdvanceColonization(
                definition, 0d, gameManager.State, resourceManager, out failure))
        {
            ShowTooltip("\u5f00\u59cb\u63a2\u7d22\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        ShowTooltip("\u661f\u533a\u63a2\u7d22\u5df2\u5f00\u59cb");
        RefreshSectorDetails(definition);
    }

    private void PauseColonization(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        if (gameManager == null || !gameManager.Sectors.CancelColonization(definition))
        {
            ShowTooltip("\u6682\u505c\u63a2\u7d22\u5931\u8d25");
            return;
        }

        ShowTooltip("\u661f\u533a\u63a2\u7d22\u5df2\u6682\u505c");
        RefreshSectorDetails(definition);
    }

    private void StartCampaign(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryAdvanceCampaign(
                definition, 0d, gameManager.State, resourceManager, out failure))
        {
            ShowTooltip("\u5f00\u59cb\u6218\u5f79\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        ShowTooltip("\u8fdc\u661f\u6218\u5f79\u5df2\u5f00\u59cb");
        RefreshSectorDetails(definition);
    }

    private void PauseCampaign(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        if (gameManager == null || !gameManager.Sectors.CancelCampaign(definition, gameManager.State))
        {
            ShowTooltip("\u6682\u505c\u6218\u5f79\u5931\u8d25");
            return;
        }

        ShowTooltip("\u8fdc\u661f\u6218\u5f79\u5df2\u6682\u505c");
        RefreshUI();
    }

    private void OccupySector(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryOccupy(definition, out failure))
        {
            ShowTooltip("\u5360\u9886\u661f\u533a\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        if (definition.IsHomeSystem)
        {
            EnqueueRecentNotice("\u672c\u661f\u7cfb\u63a2\u7d22\u5b8c\u6210\uff1a" + definition.Label +
                " \u5df2\u7eb3\u5165\u9f20\u65cf\u6587\u660e\uff1b\u9886\u571f +" +
                definition.TerritoryReward.ToGameString() + "\u3002");
            ShowTooltip("\u661f\u533a\u5df2\u5360\u9886");
            RefreshSectorDetails(definition);
            return;
        }

        EnqueueRecentNotice("占领完成：" + definition.Label +
            " 已纳入鼠族文明；领土 +" + definition.TerritoryReward.ToGameString() +
            "，资源奖励：" + FormatResourceCosts(definition.ResourceRewards) + "。");
        ShowTooltip("\u661f\u533a\u5df2\u5360\u9886");
        RefreshSectorDetails(definition);
    }

    private void RefreshSectorDetails(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        if (gameManager != null)
            ShowSectorDetails(definition, gameManager.Sectors, gameManager.State, resourceManager);
    }

    private void RepairSectorFleet(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        if (gameManager == null || resourceManager == null || definition == null)
            return;

        bool repaired = gameManager.Sectors.TryRepairFleet(
            definition,
            gameManager.State,
            resourceManager,
            gameManager.Sectors.GetState(definition).CampaignCasualties,
            out ExpantaNum repairedAmount,
            out SectorOperationFailure failure);
        if (!repaired)
        {
            ShowTooltip("舰队维修失败：" + failure.GetDescription());
            return;
        }

        ShowTooltip("舰队已维修：" + repairedAmount.ToGameString());
        EnqueueRecentNotice("舰队维修完成：" + definition.Label +
            " 已修复 " + repairedAmount.ToGameString() +
            " 点损伤，可以继续承担这场远征。");
        ShowSectorDetails(
            definition,
            gameManager.Sectors,
            gameManager.State,
            resourceManager);
    }

    private static string GetSectorStateText(SectorManager manager, SectorDefinition definition)
    {
        if (manager == null)
            return "未初始化";
        SectorState state = manager.GetState(definition);
        if (state.Occupied)
            return "已占领";
        if (!state.Unlocked)
            return "未解锁";
        if (state.ColonizationActive)
            return "探索中";
        if (state.CampaignActive)
            return "远星战役中";
        if (state.CampaignCasualties > ExpantaNum.Zero)
            return "等待维修";
        if (state.CampaignProgress >= ExpantaNum.One)
            return "等待占领";
        return state.CampaignProgress > ExpantaNum.Zero ? "已暂停" : "已解锁";
    }

    private string FormatResourceCosts(IReadOnlyList<Pair<Resource, ExpantaNum>> costs) =>
        FormatResourceCosts(costs, ExpantaNum.One);

    private string FormatResourceCostsPerMinute(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs) =>
        FormatResourceCosts(costs, new ExpantaNum(60d)) + "/min";

    private static string FormatPerMinute(ExpantaNum perSecond) =>
        (perSecond * new ExpantaNum(60d)).ToGameString();

    private string FormatResourceCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        ExpantaNum multiplier)
    {
        if (costs == null || costs.Count == 0)
            return "无";
        StringBuilder result = sectorCostTextBuilder;
        result.Clear();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0)
                result.Append("、");
            Resource resource = costs[i].First;
            result.Append(resource == null ? "未知资源" : resource.Label)
                .Append(" ")
                .Append((costs[i].Second * multiplier).ToGameString());
        }
        return result.ToString();
    }

    private string FormatResourceCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        ProgressionModifierState modifiers)
    {
        if (costs == null || costs.Count == 0)
            return "无";
        StringBuilder result = sectorCostTextBuilder;
        result.Clear();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0)
                result.Append("、");
            Resource resource = costs[i].First;
            ExpantaNum multiplier = modifiers == null
                ? ExpantaNum.One
                : modifiers.OccupiedResourceProductionMultiplier *
                  modifiers.GetOccupiedResourceProductionMultiplier(resource);
            result.Append(resource == null ? "未知资源" : resource.Label)
                .Append(" ")
                .Append((costs[i].Second * multiplier).ToGameString());
        }
        return result.ToString();
    }
}
