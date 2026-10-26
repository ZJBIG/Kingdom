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
    private SectorDef selectedSectorDefinition;
    private int lastSelectedSectorActionSignature = int.MinValue;
    private float sectorPageRefreshTimer;
    private sealed class SectorRowView
    {
        public SectorDef Definition;
        public RectTransform Row;
        public LayoutElement Layout;
        public Button Expand;
        public TMP_Text ExpandLabel;
        public RectTransform Menu;
        public TMP_Text Label;
        public TMP_Text Metadata;
        public Button Pin;
        public int Order;
        public float CollapsedHeight;
        public bool Expanded;
        public string DiagnosticSignature;
    }
    private readonly List<SectorRowView> sectorRowViews = new();
    private readonly Dictionary<string, bool> sectorBuildingExpanded = new(StringComparer.Ordinal);
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
        IReadOnlyList<SectorDef> definitions = DataBase<SectorDef>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDef definition = definitions[i];
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
            ApplyEraRowStyle(objectRow, definition.IsHomeSystem ? TechLevel.Spacer : TechLevel.Ultra, sectorRowViews.Count);
            BindRowPin(objectRow, UIPinPreferences.Sector, definition.Id);
            Image sectorIcon = row.Find("SectorIcon")?.GetComponent<Image>();
            BindArtworkIcon(sectorIcon, definition.Icon, definition.Label);
            objectRow.SetActive(CanDisplaySector(definition, sectors));
            SectorRowView view = new SectorRowView
            {
                Definition = definition,
                Row = row,
                Layout = layout,
                Label = row.Find("Label")?.GetComponent<TMP_Text>(),
                Metadata = row.Find("Metadata")?.GetComponent<TMP_Text>(),
                Pin = row.Find("PinButton")?.GetComponent<Button>(),
                Order = i,
                Expanded = sectorBuildingExpanded.TryGetValue(definition.Id, out bool expanded) && expanded,
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
        SectorDef definition, SectorManager sectorManager)
    {
        if (definition == null)
            return false;
        IReadOnlyList<SectorDef> prerequisites = definition.PrerequisiteSectors;
        for (int i = 0; prerequisites != null && i < prerequisites.Count; i++)
        {
            SectorDef prerequisite = prerequisites[i];
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
        IReadOnlyList<SectorBuildingDef> buildings =
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
        view.ExpandLabel = view.Expand.transform.Find("Label")?.GetComponent<TMP_Text>();
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
        sectorBuildingExpanded[view.Definition.Id] = view.Expanded;
        view.Menu.gameObject.SetActive(view.Expanded);
        RefreshSectorRowsAndLayout();
    }

    private void BuildSectorBuildingMenu(SectorRowView view, IReadOnlyList<SectorBuildingDef> buildings)
    {
        var orderedBuildings = new List<SectorBuildingDef>(buildings);
        orderedBuildings.Sort((left, right) => CompareAuthoredRows(
            left.TechLevel, left.Id, right.TechLevel, right.Id));
        view.Menu = KingdomUIPrefabLibrary.Instantiate("KingdomUISectorBuildingMenu", view.Row)
            .GetComponent<RectTransform>();
        view.Menu.name = "SectorBuildingMenu";
        view.Menu.anchorMin = new Vector2(0f, 1f);
        view.Menu.anchorMax = new Vector2(1f, 1f);
        view.Menu.pivot = new Vector2(.5f, 1f);
        view.Menu.anchoredPosition = new Vector2(0f, -view.CollapsedHeight);
        view.Menu.sizeDelta = new Vector2(0f, 416f);
        float cardTop = 12f;
        for (int i = 0; i < orderedBuildings.Count; i++)
        {
            SectorBuildingDef building = orderedBuildings[i];
            GameObject cardObject = KingdomUIPrefabLibrary.Instantiate("KingdomUISectorBuildingCard", view.Menu);
            if (cardObject == null)
            {
                Debug.LogError("[SectorBuildings] Missing authored SectorBuildingCard.");
                continue;
            }
            RectTransform card = cardObject.GetComponent<RectTransform>();
            LayoutElement layout = cardObject.GetComponent<LayoutElement>();
            if (card == null || layout == null || layout.preferredHeight <= 0f)
            {
                Debug.LogError("[SectorBuildings] SectorBuildingCard needs a RectTransform and preferred height.");
                Destroy(cardObject);
                continue;
            }
            cardObject.name = building.Id;
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(.5f, 1f);
            card.sizeDelta = new Vector2(-192f, layout.preferredHeight);
            card.anchoredPosition = new Vector2(0f, -cardTop);
            cardTop += layout.preferredHeight + 12f;
            ApplyEraRowStyle(cardObject, building.TechLevel, i);
            Image buildingIcon = card.Find("BuildingIcon")?.GetComponent<Image>();
            BindArtworkIcon(buildingIcon, building.Icon, building.Label);
            BuildingState state = BuildingManager.Instance.States.TryGetValue(building, out BuildingState stored)
                ? stored : null;
            Button nameButton = card.Find("Name")?.GetComponent<Button>();
            if (nameButton != null) nameButton.onClick.AddListener(() => ShowBuildingDetails(building));
            Button cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null) cardButton.onClick.AddListener(() => ShowBuildingDetails(building));
            RefreshSectorBuildingCard(card, building, state);
            Button build = RequireChildButton(cardObject, "Build");
            Button toggle = RequireChildButton(cardObject, "ToggleEnabled");
            Button deconstruct = RequireChildButton(cardObject, "Deconstruct");
            if (build == null || toggle == null || deconstruct == null) continue;
            deconstruct.onClick.AddListener(() =>
            {
                ExecuteBuildingDeconstruction(building, ExpantaNum.One);
                RebuildSectorMenu(view);
            });
            build.onClick.AddListener(() =>
            {
                BuildingState current = BuildingManager.Instance.GetState(building);
                GetSectorBuildingStage(building, out WorkshopUpgradeDef next, out _, out _, out _);
                if (current.Amount > ExpantaNum.Zero && next != null)
                {
                    if (WorkshopManager.Instance.TryPurchase(next, out _))
                        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.WorkshopPurchase);
                    else
                    {
                        string availability = GetWorkshopAvailability(next, out _);
                        if (!string.IsNullOrEmpty(availability)) ShowTooltip(availability);
                    }
                }
                else if (BuildingManager.Instance.TryBuild(building, ExpantaNum.One, out _))
                    UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Build);
                RebuildSectorMenu(view);
            });
            toggle.onClick.AddListener(() =>
            {
                BuildingState current = BuildingManager.Instance.GetState(building);
                if (BuildingManager.Instance.SetSectorBuildingEnabled(building, !current.IsEnabled))
                    UIButtonSoundManager.Play(current.IsEnabled
                        ? UIButtonSoundManager.Sound.StrategicStart : UIButtonSoundManager.Sound.StrategicStop);
                RebuildSectorMenu(view);
            });
        }
        view.Menu.sizeDelta = new Vector2(0f, cardTop + 36f);
        view.Menu.gameObject.SetActive(view.Expanded);
    }

    private static void GetSectorBuildingStage(BuildingDef building, out WorkshopUpgradeDef next,
        out WorkshopUpgradeDef latest, out int purchased, out int total)
    {
        next = null;
        latest = null;
        purchased = 0;
        total = 0;
        WorkshopManager workshop = WorkshopManager.Instance;
        foreach (WorkshopUpgradeDef stage in DataBase<WorkshopUpgradeDef>.All)
        {
            if (stage.RequiredBuilding != building) continue;
            total++;
            if (workshop.IsPurchased(stage))
            {
                purchased++;
                if (latest == null || CompareSectorStages(latest, stage) < 0) latest = stage;
            }
            else if (next == null || CompareSectorStages(stage, next) < 0) next = stage;
        }
    }

    private static int CompareSectorStages(WorkshopUpgradeDef left, WorkshopUpgradeDef right)
    {
        int order = left.SortOrder.CompareTo(right.SortOrder);
        return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
    }

    private void RefreshSectorBuildingCard(Transform card, SectorBuildingDef building, BuildingState state)
    {
        bool built = state != null && state.Amount > ExpantaNum.Zero;
        GetSectorBuildingStage(building, out WorkshopUpgradeDef next, out WorkshopUpgradeDef latest, out _, out _);
        bool staged = BuildingManager.IsMegastructure(building);
        SetRowText(card.gameObject, "Name", built && latest != null ? latest.Label : building.Label);
        Button deconstruct = card.Find("Deconstruct")?.GetComponent<Button>();
        if (deconstruct != null)
        {
            deconstruct.gameObject.SetActive(!staged);
            deconstruct.interactable = built;
            deconstruct.GetComponent<UITouchTooltip>()?.SetTooltip("拆除" + building.Label);
        }
        Transform amount = card.Find("Amount");
        amount.gameObject.SetActive(!staged);
        if (!staged)
            SetRowText(card.gameObject, "Amount", (built ? state.Amount.ToGameString() : "0") + "/" + building.MaxAmount);
        Button build = card.Find("Build")?.GetComponent<Button>();
        if (build != null)
        {
            bool canPurchase = false;
            if (next != null) GetWorkshopAvailability(next, out canPurchase);
            build.interactable = built && staged ? next != null && canPurchase
                : BuildingManager.Instance.GetMaxBuildable(building, ExpantaNum.One) >= ExpantaNum.One;
            bool complete = built && (staged ? next == null : state.Amount >= building.MaxAmount);
            build.gameObject.SetActive(!complete);
            TMP_Text label = build.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                label.gameObject.SetActive(!staged);
                if (!staged) label.text = "×1";
            }
            build.GetComponent<UIBuildingActionIcon>()?.SetUpgrade(staged);
            build.GetComponent<UITouchTooltip>()?.SetTooltip(complete ? "已建成" : built && staged ? "建设" + next.Label : "建造" + building.Label);
        }
        Button toggle = card.Find("ToggleEnabled")?.GetComponent<Button>();
        if (toggle != null)
        {
            toggle.interactable = built;
            bool enabled = state == null || state.IsEnabled;
            toggle.GetComponent<UIBuildingActionIcon>()?.SetPaused(!enabled);
            toggle.targetGraphic = toggle.transform.Find(enabled ? "PauseIcon" : "ResumeIcon")?.GetComponent<Image>();
            toggle.GetComponent<UITouchTooltip>()?.SetTooltip(enabled ? "停用设施，保留建筑与工程" : "启用设施");
        }
    }

    private void RebuildSectorMenu(SectorRowView view)
    {
        Destroy(view.Menu.gameObject);
        BuildSectorBuildingMenu(view, BuildingManager.Instance.GetSectorBuildings(view.Definition));
        RefreshSectorRowsAndLayout();
    }

    private void RefreshSectorRowsAndLayout()
    {
        sectorRowViews.Sort((left, right) =>
        {
            int pin = UIPinPreferences.Compare(UIPinPreferences.Sector, left.Definition.Id, right.Definition.Id);
            return pin != 0 ? pin : left.Order.CompareTo(right.Order);
        });
        float top = 0f;
        for (int i = 0; i < sectorRowViews.Count; i++)
        {
            SectorRowView view = sectorRowViews[i];
            if (view.Row.GetSiblingIndex() != i) view.Row.SetSiblingIndex(i);
            bool eligible = CanDisplaySector(view.Definition, gameManagerCache?.Sectors);
            view.Row.gameObject.SetActive(eligible);
            if (!eligible)
                continue;
            bool occupied = gameManagerCache != null && gameManagerCache.Sectors.GetState(view.Definition).Occupied;
            if (view.Pin != null)
                SetPinButtonVisual(view.Pin, UIPinPreferences.IsPinned(UIPinPreferences.Sector, view.Definition.Id));
            if (view.Metadata != null)
            {
                string summary = GetSectorRowSummary(view.Definition, gameManagerCache?.Sectors);
                if (view.Metadata.text != summary) view.Metadata.text = summary;
            }
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
            if (view.ExpandLabel != null)
                view.ExpandLabel.text = view.Expanded && occupied ? "▲" : "▼";
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
            page.sizeDelta = new Vector2(0f, sectorRowsParent.rect.height);
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

    private string GetSectorRowSummary(SectorDef definition, SectorManager manager)
    {
        SectorState state = manager?.GetState(definition);
        string summary = state != null && state.Occupied && definition.IsHomeSystem
            ? "已驻留" : definition.StarSystemId + " · " + GetSectorStateText(manager, definition);
        if (state != null && state.CampaignProgress > ExpantaNum.Zero && !state.Occupied)
            summary += " · 进度 " + (state.CampaignProgress * 100).ToGameString() + "%";
        if (state != null && state.Occupied && definition.InteriorExploration != null)
        {
            var preview = manager.GetInteriorExplorationPreview(definition);
            summary += preview.Status == SectorInteriorExplorationStatus.Completed
                ? " · 深入探索完成"
                : " · " + definition.InteriorExploration.Label + " " +
                    (preview.Progress * 100).ToGameString() + "% · " + GetSectorInteriorExplorationStatusText(preview.Status);
            if (preview.MissingResource != null)
                summary += "：" + preview.MissingResource.Label;
        }
        if (state != null && state.Occupied)
            foreach (SectorBuildingDef building in BuildingManager.Instance.GetSectorBuildings(definition))
            {
                if (!BuildingManager.Instance.States.TryGetValue(building, out BuildingState owned) || owned.Amount <= ExpantaNum.Zero)
                    continue;
                GetSectorBuildingStage(building, out _, out WorkshopUpgradeDef latest, out _, out int total);
                if (total > 0) summary += " · " + (latest == null ? building.Label : latest.Label);
            }
        if (state != null && state.Occupied && definition.OccupiedResourceRatesPerSecond.Count > 0)
            return summary + " · 产出 " + FormatResourceCosts(definition.OccupiedResourceRatesPerSecond,
                ProgressionModifierManager.Current) + "/s";
        if (state != null && state.Occupied && definition.IsHomeSystem)
            return summary;
        if (definition.TerritoryReward > ExpantaNum.Zero)
            summary += " · 领土 +" + definition.TerritoryReward.ToGameString();
        if (!definition.IsHomeSystem && definition.EnemyPower > ExpantaNum.Zero)
            summary += " · 敌方战力 " + definition.EnemyPower.ToGameString();
        return summary;
    }

    private void RefreshSectorBuildingMenu(SectorRowView view)
    {
        IReadOnlyList<SectorBuildingDef> buildings =
            BuildingManager.Instance.GetSectorBuildings(view.Definition);
        for (int i = 0; i < buildings.Count; i++)
        {
            SectorBuildingDef building = buildings[i];
            Transform card = view.Menu.Find(building.Id);
            if (card == null)
                continue;
            BuildingState state = BuildingManager.Instance.States.TryGetValue(
                building, out BuildingState stored) ? stored : null;
            RefreshSectorBuildingCard(card, building, state);
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
            SectorDef definition = state == null ? null : state.Definition;
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
                        " 已占领；可在星区详情查看领土奖励与持续产出。");
                }
                else if (observedSectorProgress.TryGetValue(id, out ExpantaNum previousProgress) &&
                    previousProgress < ExpantaNum.One && progress >= ExpantaNum.One)
                {
                    EnqueueRecentNotice("远征完成：" + definition.Label +
                        " 的进度已完成；前往星区详情确认占领。");
                }
                else if (previousCasualties <= ExpantaNum.Zero &&
                    state.CampaignCasualties > ExpantaNum.Zero)
                {
                    EnqueueRecentNotice("战报：" + definition.Label +
                        " 的舰队出现损伤；查看补给余量，必要时撤退后维修。");
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
        SectorDef definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null)
            return;

        // 星区详情不能继承残留的研究节点选择，否则通用按钮会被改回研究队列操作。
        RecordOnboardingDetail(definition.Id);
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
        RefreshReviewControls();
    }

    private string BuildSectorDetailDescription(
        SectorDef definition,
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
        IReadOnlyList<SectorDef> prerequisites = definition.PrerequisiteSectors;
        if (prerequisites == null || prerequisites.Count == 0)
            body.AppendLine("直接前置星区：无");
        else
        {
            body.AppendLine("直接前置星区：");
            for (int i = 0; i < prerequisites.Count; i++)
            {
                SectorDef prerequisite = prerequisites[i];
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
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> occupiedRates =
            definition.OccupiedResourceRatesPerSecond;
        body.AppendLine("当前占领持续产出：" +
            (occupiedRates == null || occupiedRates.Count == 0
                ? "无"
                : FormatResourceCosts(
                    occupiedRates,
                    ProgressionModifierManager.Current) + "/s"));

        if (state == null)
        {
            body.AppendLine("星区数据尚未载入，请稍候。");
        }
        else if (sectorState != null && sectorState.Occupied && definition.InteriorExploration != null)
        {
            AppendInteriorExplorationDetails(body, definition, sectorManager);
        }
        else if (sectorState != null && sectorState.Occupied)
        {
            body.AppendLine("已建立驻留，可展开星区建筑进行建设。");
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
            AppendCampaignBudget(body, preview);
            if (GameManager.Instance.UltraProject.IsCampaignDoctrineUnlocked)
            {
                body.AppendLine("战役姿态预览（按当前舰队与供给估算，选择后生效）：");
                foreach (CampaignDoctrine doctrine in new[] { CampaignDoctrine.Stable, CampaignDoctrine.Surge })
                {
                    body.AppendLine("战役姿态：" + (doctrine == CampaignDoctrine.Stable ? "稳健" : "突进"));
                    AppendCampaignBudget(body, sectorManager.GetCampaignPreview(definition, state, resourceManager, doctrine));
                }
            }
            body.AppendLine("战斗比率：" + preview.CombatRatio.ToGameString());
            body.AppendLine("舰队生存倍率：" + preview.FleetSurvivalFactor.ToGameString());
            body.AppendLine("舰队整备度：" + preview.FleetReadiness.ToGameString());
            body.AppendLine("当前伤亡：" + preview.CurrentCasualties.ToGameString());
            body.AppendLine("补给满足率（0—1）：" + preview.SupplySatisfaction.ToGameString());
            body.AppendLine("电力满足率（0—1）：" + preview.PowerSatisfaction.ToGameString());
            body.AppendLine("物流满足率（0—1）：" + preview.LogisticsSatisfaction.ToGameString());
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
        SectorDef definition = selectedSectorDefinition;
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
        SectorDef definition,
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
        if (definition.InteriorExploration != null)
        {
            var interior = sectorManager.GetInteriorExplorationPreview(definition);
            signature = unchecked(signature * 31 + (int)interior.Status);
            signature = unchecked(signature * 31 + (int)interior.Failure);
        }
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
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs)
    {
        for (int i = 0; costs != null && i < costs.Count; i++)
        {
            ResourceDef resource = costs[i].First;
            int version = -1;
            if (resource != null && resourceManager.States.TryGetValue(
                    resource, out ResourceState resourceState))
                version = resourceState.Version;
            signature = unchecked(signature * 31 + version);
        }
    }

    private void ConfigureSectorAction(
        SectorDef definition,
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
            if (definition.InteriorExploration != null)
            {
                var preview = sectorManager.GetInteriorExplorationPreview(definition);
                if (preview.Status != SectorInteriorExplorationStatus.Completed)
                {
                    bool active = sectorState.InteriorExplorationActive;
                    ConfigureActionButton(active ? "暂停勘探" : preview.Status == SectorInteriorExplorationStatus.Paused ? "继续勘探" : "开始勘探",
                        () => ToggleInteriorExploration(definition));
                    if (detailActionButton != null)
                        detailActionButton.interactable = preview.Status != SectorInteriorExplorationStatus.Locked;
                    return;
                }
            }
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

        // An active campaign always keeps its retreat action reachable; repair is a separate authored action.
        if (sectorState.CampaignActive)
        {
            ConfigureActionButton("撤退 / 终止远星战役", () => PauseCampaign(definition));
            ConfigureCampaignDoctrineAction(state);
            return;
        }
        if (sectorState.CampaignCasualties > ExpantaNum.Zero && resourceManager != null)
        {
            ConfigureActionButton("\u7ef4\u4fee\u8230\u961f", PreviewFleetRepair);
            if (detailActionButton != null)
                detailActionButton.interactable = sectorManager.GetMaxAffordableFleetRepair(
                    definition, state, resourceManager) > ExpantaNum.Zero;
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

    private static string GetSectorInteriorExplorationStatusText(SectorInteriorExplorationStatus status)
    {
        return status switch
        {
            SectorInteriorExplorationStatus.Locked => "条件未满足",
            SectorInteriorExplorationStatus.Ready => "待开始",
            SectorInteriorExplorationStatus.Running => "勘探中",
            SectorInteriorExplorationStatus.WaitingForSupply => "等待补给",
            SectorInteriorExplorationStatus.Paused => "已手动暂停",
            SectorInteriorExplorationStatus.Completed => "探索完成",
            _ => "状态未知"
        };
    }

    private void AppendInteriorExplorationDetails(StringBuilder body, SectorDef definition, SectorManager manager)
    {
        var exploration = definition.InteriorExploration;
        var preview = manager.GetInteriorExplorationPreview(definition);
        body.AppendLine();
        body.AppendLine("星球探索：" + exploration.Label);
        body.AppendLine(exploration.Description);
        body.AppendLine("勘探状态：" + GetSectorInteriorExplorationStatusText(preview.Status));
        body.AppendLine("勘探进度：" + (preview.Progress * 100).ToGameString() + "%");
        if (preview.MissingResearch != null) body.AppendLine("需要研究：" + preview.MissingResearch.Label);
        if (preview.MissingResource != null) body.AppendLine("等待补给：" + preview.MissingResource.Label);
        if (preview.Status != SectorInteriorExplorationStatus.Completed)
        {
            body.AppendLine("持续投入：" + FormatResourceCostsPerMinute(exploration.ResourceRatesPerSecond));
            body.AppendLine("供给充足时剩余：" + preview.EstimatedSecondsRemaining.ToGameString() + " 秒");
            body.AppendLine("缺料自动等待，补足后继续；手动暂停保留进度。完成后停止消耗。");
        }
        for (int i = 0; i < exploration.Milestones.Count; i++)
        {
            if (preview.Progress * exploration.Milestones.Count < new ExpantaNum(i + 1))
            {
                body.AppendLine("当前目标：" + exploration.Milestones[i]);
                break;
            }
            body.AppendLine("发现：" + exploration.Milestones[i]);
        }
        if (preview.Status == SectorInteriorExplorationStatus.Completed)
            body.AppendLine(exploration.CompletionDescription);
        body.AppendLine("探索完成后可建设：");
        foreach (SectorBuildingDef building in BuildingManager.Instance.GetSectorBuildings(definition))
            if (building.RequiresInteriorExploration) body.AppendLine("  " + building.Label);
    }

    private void ToggleInteriorExploration(SectorDef definition)
    {
        CacheRuntimeManagers();
        SectorManager sectors = gameManagerCache?.Sectors;
        if (sectors == null) return;
        if (sectors.GetState(definition).InteriorExplorationActive)
        {
            sectors.PauseInteriorExploration(definition);
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStop);
        }
        else if (sectors.TryStartInteriorExploration(definition, out SectorOperationFailure failure))
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStart);
        else ShowTooltip("开始勘探失败：" + failure.GetDescription());
        RefreshSectorDetails(definition);
        RefreshSectorRowsAndLayout();
    }

    private void UnlockSector(SectorDef definition)
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

    private void StartColonization(SectorDef definition)
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

        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStart);
        ShowTooltip("\u661f\u533a\u63a2\u7d22\u5df2\u5f00\u59cb");
        RefreshSectorDetails(definition);
    }

    private void PauseColonization(SectorDef definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        if (gameManager == null || !gameManager.Sectors.CancelColonization(definition))
        {
            ShowTooltip("\u6682\u505c\u63a2\u7d22\u5931\u8d25");
            return;
        }

        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStop);
        ShowTooltip("\u661f\u533a\u63a2\u7d22\u5df2\u6682\u505c");
        RefreshSectorDetails(definition);
    }

    private void StartCampaign(SectorDef definition)
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

        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStart);
        ShowTooltip("\u8fdc\u661f\u6218\u5f79\u5df2\u5f00\u59cb");
        RefreshSectorDetails(definition);
    }

    private void PauseCampaign(SectorDef definition)
    {
        if (GameManager.Instance?.Relic?.State.SupportedSectorId == definition.Id)
        {
            ConfirmReviewAction("终止 " + definition.Label + " 的活动战役。伤亡保留；未占领星区进度会缓慢回退。已经分配的遗迹支援将清除，不返还。之后需重新启动战役。", () => ExecuteCampaignRetreat(definition));
            return;
        }
        ExecuteCampaignRetreat(definition);
    }

    private void ExecuteCampaignRetreat(SectorDef definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        if (gameManager == null || !gameManager.Sectors.CancelCampaign(definition, gameManager.State))
        {
            ShowTooltip("终止战役失败");
            return;
        }

        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStop);
        ShowTooltip("远星战役已终止；未占领星区进度会缓慢回退");
        RefreshUI();
    }

    private void OccupySector(SectorDef definition)
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

    private void RefreshSectorDetails(SectorDef definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        if (gameManager != null)
            ShowSectorDetails(definition, gameManager.Sectors, gameManager.State, resourceManager);
    }

    private void RepairSectorFleet(SectorDef definition)
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
            gameManager.Sectors.GetMaxAffordableFleetRepair(definition, gameManager.State, resourceManager),
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

    private static string GetSectorStateText(SectorManager manager, SectorDef definition)
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

    private string FormatResourceCosts(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs) =>
        FormatResourceCosts(costs, ExpantaNum.One);

    private string FormatResourceCostsPerMinute(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs) =>
        FormatResourceCosts(costs, new ExpantaNum(60d)) + "/min";

    private static string FormatPerMinute(ExpantaNum perSecond) =>
        (perSecond * new ExpantaNum(60d)).ToGameString();

    private string FormatResourceCosts(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs,
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
            ResourceDef resource = costs[i].First;
            result.Append(resource == null ? "未知资源" : resource.Label)
                .Append(" ")
                .Append((costs[i].Second * multiplier).ToGameString());
        }
        return result.ToString();
    }

    private string FormatResourceCosts(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs,
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
            ResourceDef resource = costs[i].First;
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
