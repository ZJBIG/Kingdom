using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private readonly StringBuilder eraPageSignatureBuilder = new(96);
    private readonly List<EraGoalConditionEvaluation> eraConditions = new();

    private void BuildEraPage(RectTransform parent, EraGoalEvaluation preparedGoal = null)
    {
        if (parent == null) return;
        ClearEraCards(parent);
        CacheRuntimeManagers();
        GameState state = gameManagerCache == null ? null : gameManagerCache.State;
        if (state == null)
        {
            CreateEraCard(parent, "EraArchiveHeader", "时代档案", "正在读取王国状态……", PanelRaised, false, null, null);
            nextRowTop = 260f;
            return;
        }

        EraGoalEvaluation goal = preparedGoal ?? EraGoalEvaluator.Evaluate(state.TechLevel, researchManagerCache, resourceManagerCache);
        Research transition = goal.Transition;
        float width = Mathf.Max(480f, pageHost == null ? 1200f : pageHost.rect.width);
        float y = 24f;

        RectTransform current = BeginEraSection(parent, "EraCurrentArchive", "当前时代档案", PanelRaised, width, y, out float currentY);
        currentY += CreateEraCard(current, "EraIdentity", "时代身份", state.TechLevel.GetDescription() + " · " + GetEraSubtitle(state.TechLevel) + "\n第 " + ((int)state.TechLevel + 1) + " / " + Enum.GetValues(typeof(TechLevel)).Length + " 阶段", Panel, false, null, null, width, currentY, 100f) + 10f;
        currentY += CreateEraCard(current, "EraDefinition", "时代定义", GetEraDefinition(state.TechLevel), PanelRaised, false, null, null, width, currentY, 100f) + 10f;
        currentY += CreateEraCard(current, "EraCapabilities", "本时代能力", BuildCurrentEraCapabilities(state.TechLevel), Panel, false, null, null, width, currentY, 130f) + 16f;
        y += EndEraSection(current, currentY, width) + 18f;

        if (transition == null)
        {
            RectTransform terminal = BeginEraSection(parent, "EraTransitionSection", "时代状态", Panel, width, y, out float terminalY);
            terminalY += CreateEraCard(terminal, "EraTerminal", "当前内容终点", "当前内容已到达最后阶段。\n继续完成研究树中的剩余内容。", PanelRaised, true, () => SetPage("Research"), null, width, terminalY, 150f);
            y += EndEraSection(terminal, terminalY, width);
            SetEraPageHeight(parent, y + 24f, state, goal);
            return;
        }

        eraConditions.Clear();
        for (int i = 0; i < goal.Conditions.Count; i++) eraConditions.Add(goal.Conditions[i]);
        int completed = 0;
        int blockerIndex = -1;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            if (eraConditions[i].Met) completed++; else if (blockerIndex < 0) blockerIndex = i;
        }
        float progress = eraConditions.Count == 0 ? 1f : completed / (float)eraConditions.Count;
        bool showQueueAction = !IsEraTransitionResearchCompleted(transition);

        RectTransform transitionSection = BeginEraSection(parent, "EraTransitionSection", "下一时代跃迁", Panel, width, y, out float transitionY);
        transitionY += CreateEraCard(transitionSection, "EraTransitionTarget", "跃迁目标", goal.TargetEra.GetDescription() + " · " + transition.Label + "\n" + transition.Description, PanelRaised, true,
            () => { SetPage("Research"); ShowResearchDetails(transition); }, showQueueAction ? transition : null, width, transitionY, 180f) + 10f;
        transitionY += CreateEraCard(transitionSection, "EraTransitionStatus", "跃迁状态", GetTransitionStatus(transition, progress) + "\n硬条件：" + completed + "/" + eraConditions.Count + "（" + (progress * 100f).ToString("0") + "%）", Panel, false, null, null, width, transitionY, 110f) + 10f;
        transitionY += CreateEraCard(transitionSection, "EraNextImpact", "进入后变化", BuildNextEraImpact(goal.TargetEra), PanelRaised, false, null, null, width, transitionY, 130f) + 16f;
        y += EndEraSection(transitionSection, transitionY, width) + 18f;

        RectTransform requirements = BeginEraSection(parent, "EraRequirementSection", "跃迁条件与阻碍", PanelRaised, width, y, out float requirementY);
        int researchCount = 0, researchCompleted = 0, resourceCount = 0, resourceMet = 0;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            if (eraConditions[i].Kind == EraGoalConditionKind.PrerequisiteResearch) { researchCount++; if (eraConditions[i].Met) researchCompleted++; }
            else { resourceCount++; if (eraConditions[i].Met) resourceMet++; }
        }
        requirementY += CreateEraCard(requirements, "EraResearchSummary", "研究前置统计", researchCount == 0 ? "没有额外研究前置。" : "已完成 " + researchCompleted + "/" + researchCount + "；" + (researchCount - researchCompleted) + " 项仍待完成。", Panel, false, null, null, width, requirementY, 100f) + 10f;
        int researchIndex = 0;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraConditions[i];
            if (condition.Kind != EraGoalConditionKind.PrerequisiteResearch) continue;
            Research research = condition.Research;
            ResearchState researchState = condition.ResearchState;
            requirementY += CreateEraCard(requirements, "EraResearch_" + research.Id, (condition.Met ? "✓ " : "○ ") + research.Label,
                condition.Met ? "已完成" : ResearchStateLabel(research, researchState == null ? ResearchStatus.Locked : researchState.Status),
                GetEraAlternatePanelColor(researchIndex++), true, () => { SetPage("Research"); ShowResearchDetails(research); }, null, width, requirementY, 92f) + 10f;
        }
        requirementY += CreateEraCard(requirements, "EraResourceSummary", "资源储备统计", resourceCount == 0 ? "没有额外资源储备要求。" : "已满足 " + resourceMet + "/" + resourceCount + "；" + (resourceCount - resourceMet) + " 项仍需准备。", PanelRaised, false, null, null, width, requirementY, 100f) + 10f;
        int resourceIndex = 0;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraConditions[i];
            if (condition.Kind != EraGoalConditionKind.Resource) continue;
            Resource resource = condition.Resource;
            requirementY += CreateEraCard(requirements, "EraResource_" + resource.Id, (condition.Met ? "✓ " : "○ ") + resource.Label,
                FormatResourceCondition(condition), GetEraAlternatePanelColor(resourceIndex++), true, () => ShowResourceDetails(resource), null, width, requirementY, 120f) + 10f;
        }
        string blockerTitle = blockerIndex < 0 ? "全部硬条件已满足" : BuildConditionTitle(eraConditions[blockerIndex]);
        string blockerBody = blockerIndex < 0 ? "可开始或继续跃迁研究。" : BuildConditionCategory(eraConditions[blockerIndex]) + "\n" + BuildConditionDetail(eraConditions[blockerIndex]);
        Action blockerAction = blockerIndex < 0 ? () => { SetPage("Research"); ShowResearchDetails(transition); } : BuildConditionNavigation(eraConditions[blockerIndex]);
        requirementY += CreateEraCard(requirements, "EraPrimaryBlocker", "当前首要阻碍", blockerTitle + "\n" + blockerBody,
            blockerIndex < 0 ? PanelRaised : Panel, true, blockerAction, null, width, requirementY, 150f);
        y += EndEraSection(requirements, requirementY, width);
        SetEraPageHeight(parent, y + 24f, state, goal);
        Debug.Log($"[王国界面] Era sections rendered: current={state.TechLevel}, target={goal.TargetEra}, progress={completed}/{eraConditions.Count}, blocker={blockerTitle}");
    }

    private RectTransform BeginEraSection(RectTransform parent, string name, string title, Color color, float width, float y, out float contentY)
    {
        RectTransform section = CreatePanel(parent, name, color);
        // EraRows is the outer page's authored data surface. Forward drags from
        // the section shell itself so a gesture starting in its header/background
        // still reaches the shared PageHost ScrollRect.
        section.gameObject.AddComponent<UIPageScrollDragForwarder>();
        SetTop(section, y, 200f);
        TMP_Text titleText = CreateText(section, "Title", title, TextPrimary, TextAnchor.UpperLeft, Mathf.Max(320f, width - 80f), true);
        titleText.rectTransform.anchoredPosition = new Vector2(20f, -16f);
        titleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(48f, titleText.preferredHeight));
        RectTransform content = CreatePanel(section, "Content", Background);
        content.GetComponent<Image>().raycastTarget = false;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = new Vector2(0f, -70f); content.sizeDelta = new Vector2(-24f, 100f);
        contentY = 12f;
        return content;
    }

    private float EndEraSection(RectTransform content, float contentY, float width)
    {
        float contentHeight = contentY + 12f;
        content.sizeDelta = new Vector2(-24f, contentHeight);
        RectTransform section = content.parent as RectTransform;
        float height = 70f + contentHeight + 16f;
        section.sizeDelta = new Vector2(-24f, height);
        return height;
    }

    private static Color GetEraAlternatePanelColor(int index) => index % 2 == 0 ? Panel : PanelRaised;

    private float CreateEraCard(RectTransform parent, string name, string title, string body, Color color,
        bool hasAction, Action action, Research queueResearch, float width = 1200f, float y = 0f, float minimumHeight = 140f)
    {
        RectTransform card = CreatePanel(parent, name, color);
        card.gameObject.AddComponent<UIPageScrollDragForwarder>();
        SetTop(card, y, minimumHeight);
        const float cardPadding = 20f;
        const float titleBodyGap = 14f;
        float cardWidth = Mathf.Max(320f, width - 48f);
        float titleWidth = hasAction ? Mathf.Max(240f, cardWidth - 540f) : cardWidth;
        TMP_Text titleText = CreateText(card, "Title", title, TextPrimary, TextAnchor.UpperLeft, titleWidth, true);
        titleText.rectTransform.anchoredPosition = new Vector2(cardPadding, -cardPadding);
        float titleHeight = Mathf.Max(48f, titleText.preferredHeight);
        titleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, titleHeight);
        // Reserve the right side for the detail/queue actions.  Keeping the
        // body inside the remaining column prevents long transition
        // descriptions from flowing underneath the buttons on narrow
        // landscape layouts.
        float bodyWidth = hasAction ? Mathf.Max(320f, cardWidth - 540f) : cardWidth;
        TMP_Text bodyText = CreateText(card, "Body", body, TextPrimary, TextAnchor.UpperLeft, bodyWidth, false);
        bodyText.rectTransform.anchoredPosition = new Vector2(cardPadding, -(cardPadding + titleHeight + titleBodyGap));
        float bodyHeight = Mathf.Max(48f, bodyText.preferredHeight);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
        float height = Mathf.Max(minimumHeight,
            cardPadding + titleHeight + titleBodyGap + bodyHeight + cardPadding);
        card.sizeDelta = new Vector2(-24f, height);
        if (hasAction && action != null) AddEraActions(card, action, queueResearch);
        return height;
    }

    private void AddEraActions(RectTransform card, Action detailAction, Research queueResearch)
    {
        AddEraButton(card, "DetailAction", "查看详情", Copper, detailAction, 250f, -12f);
        if (queueResearch == null) return;
        ResearchManager currentManager = ResearchManager.Instance;
        ResearchState currentState = null;
        currentManager?.States.TryGetValue(queueResearch, out currentState);
        if (currentState?.Status == ResearchStatus.Completed) return;
        AddEraButton(card, "QueueAction", GetResearchQueueActionLabel(queueResearch, currentState), Positive, () =>
        {
            ResearchManager manager = ResearchManager.Instance;
            if (manager == null) return;
            HandleResearchUiAction(queueResearch);
            RefreshEraPageIfChanged();
        }, 250f, -274f);
    }

    private void AddEraButton(RectTransform card, string name, string label, Color color, Action action, float width, float rightOffset)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(rightOffset, -12f); rect.sizeDelta = new Vector2(width, 58f);
        Image image = buttonObject.GetComponent<Image>(); image.color = color; image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
        ApplyButtonColors(button, image.color);
        TMP_Text text = CreateText(rect, "Label", label, TextPrimary, TextAnchor.MiddleCenter, width - 20f, true);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.pivot = new Vector2(.5f, .5f); text.rectTransform.anchoredPosition = Vector2.zero; text.rectTransform.sizeDelta = Vector2.zero; text.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => { UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail); action(); });
    }

    private static void ClearEraCards(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    private void SetEraPageHeight(RectTransform parent, float height, GameState state, EraGoalEvaluation goal)
    {
        parent.sizeDelta = new Vector2(0f, height);
        nextRowTop = height;
        eraPageStateSignature = BuildEraPageStateSignature(state, goal);
    }

    private static string GetTransitionStatus(Research transition, float progress)
    {
        ResearchManager manager = ResearchManager.Instance;
        if (manager != null && manager.States.TryGetValue(transition, out ResearchState state))
        {
            if (state.Status == ResearchStatus.Researching) return "跃迁研究进行中";
            if (state.Status == ResearchStatus.Completed) return "已完成";
        }
        if (progress >= 1f) return "全部条件满足，等待研究";
        return progress <= 0f ? "尚未满足跃迁条件" : "部分条件已满足";
    }

    private static string BuildCurrentEraCapabilities(TechLevel era)
    {
        ResearchManager manager = ResearchManager.Instance;
        var lines = new List<string>();
        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        for (int i = 0; i < definitions.Count && lines.Count < 3; i++)
        {
            Research research = definitions[i];
            if (research == null || research.TechLevel != era || manager == null || !manager.States.TryGetValue(research, out ResearchState state) || state.Status != ResearchStatus.Completed) continue;
            if (research.AdvancesTechLevel) lines.Add("已完成跃迁：" + research.Label);
            for (int j = 0; j < research.Effects.Count && lines.Count < 3; j++)
            {
                ResearchEffectDefinition effect = research.Effects[j];
                if (effect == null) continue;
                string target = effect.Building != null ? "（" + effect.Building.Label + "）" : effect.Resource != null ? "（" + effect.Resource.Label + "）" : string.Empty;
                lines.Add(effect.Type.GetDescription() + target + "：" + effect.NumericValue.ToGameString());
            }
        }
        return lines.Count == 0 ? "当前时代能力由已完成研究构成。" : string.Join("\n", lines);
    }

    private static string BuildNextEraImpact(TechLevel targetEra)
    {
        var lines = new List<string>();
        Research transition = EraGoalEvaluator.FindTransition(targetEra);
        if (transition != null)
        {
            for (int i = 0; i < transition.Effects.Count; i++)
            {
                ResearchEffectDefinition effect = transition.Effects[i];
                if (effect == null) continue;
                lines.Add("跃迁即得：" + effect.Type.GetDescription() + " " + effect.NumericValue.ToGameString());
            }
        }
        string[] signatureBuildings = targetEra switch
        {
            TechLevel.StoneAge => new[] { "IrrigationWorks", "ScribeHut" },
            TechLevel.Medieval => new[] { "SteelForge", "Library" },
            TechLevel.Industrial => new[] { "MachineFactory", "CentralPowerStation" },
            TechLevel.Spacer => new[] { "LaunchCenter", "OrbitalResourceExtractionArray" },
            TechLevel.Ultra => new[] { "PhaseEnergyArray", "AutonomousMatterFabricator" },
            _ => System.Array.Empty<string>()
        };
        for (int i = 0; i < signatureBuildings.Length; i++)
        {
            Building building = DataBase<Building>.Find(signatureBuildings[i]);
            if (building == null) continue;
            var prerequisites = new List<string>();
            for (int j = 0; j < building.RequiredResearch.Count; j++)
                if (building.RequiredResearch[j] != null) prerequisites.Add(building.RequiredResearch[j].Label);
            lines.Add("时代代表能力（仍需建设）：" + building.Label +
                (prerequisites.Count > 0 ? "；先研究 " + string.Join("、", prerequisites) : string.Empty));
        }
        return lines.Count == 0 ? "该时代的详细内容将在研究与建筑页面中逐步展开。" : string.Join("\n", lines);
    }

    private static string BuildConditionCategory(EraGoalConditionEvaluation condition)
    {
        if (condition.Kind == EraGoalConditionKind.PrerequisiteResearch) return "阻碍类别：研究前置";
        ExpantaNum net = condition.ProductionRate - condition.ConsumptionRate;
        return condition.ProductionRate <= ExpantaNum.Zero ? "阻碍类别：无生产来源" : net <= ExpantaNum.Zero ? "阻碍类别：净产出不足" : "阻碍类别：资源缺口";
    }

    private static string BuildConditionTitle(EraGoalConditionEvaluation condition) => condition.Kind == EraGoalConditionKind.PrerequisiteResearch ? "研究：" + condition.Research.Label : "资源：" + condition.Resource.Label;
    private static string BuildConditionDetail(EraGoalConditionEvaluation condition) => condition.Kind == EraGoalConditionKind.PrerequisiteResearch ? "尚未完成" : FormatResourceCondition(condition);
    private Action BuildConditionNavigation(EraGoalConditionEvaluation condition) => condition.Kind == EraGoalConditionKind.PrerequisiteResearch ? (Action)(() => { SetPage("Research"); ShowResearchDetails(condition.Research); }) : () => ShowResourceDetails(condition.Resource);

    private static string FormatResourceCondition(EraGoalConditionEvaluation condition)
    {
        string payment = "库存 " + condition.AvailableAmount.ToGameString() + " / 剩余应付 " + condition.RemainingAmount.ToGameString();
        if (condition.PaidAmount > ExpantaNum.Zero) payment += "（已付 " + condition.PaidAmount.ToGameString() + "）";
        if (condition.Met) return payment + "（已满足）";
        ExpantaNum net = condition.ProductionRate - condition.ConsumptionRate;
        string shortfall = payment + "，尚欠库存 " + condition.MissingAmount.ToGameString();
        if (net > ExpantaNum.Zero) return shortfall + "，净产出 +" + net.ToGameString() + "/s，按当前净流预计 " + (condition.MissingAmount / net).ToGameString() + " 秒备齐材料";
        if (condition.ProductionRate <= ExpantaNum.Zero) return shortfall + "，无当前生产来源";
        return shortfall + "，净产出 " + net.ToGameString() + "/s，无法估算";
    }

    private static bool IsEraTransitionResearchCompleted(Research transition) => transition != null && ResearchManager.Instance != null && ResearchManager.Instance.States.TryGetValue(transition, out ResearchState state) && state.Status == ResearchStatus.Completed;

    private static string GetEraDefinition(TechLevel era) => era switch
    {
        TechLevel.Animal => "以火种、聚落和基础生存为核心。", TechLevel.StoneAge => "以定居、农业和早期加工为核心。", TechLevel.Medieval => "以城市、制度和专业分工为核心。", TechLevel.Industrial => "以机器、电力和规模化生产为核心。", TechLevel.Spacer => "以轨道、舰队和星区建设为核心。", TechLevel.Ultra => "以技术奇点和极限自动化为核心。", TechLevel.Archotech => "远期文明框架。", _ => string.Empty
    };

    private static string GetEraSubtitle(TechLevel era) => era switch
    {
        TechLevel.Animal => "火种与聚落", TechLevel.StoneAge => "定居与早期金属", TechLevel.Medieval => "城市、制度与机械萌芽", TechLevel.Industrial => "机器、电力与规模化生产", TechLevel.Spacer => "轨道、舰队与星区", TechLevel.Ultra => "技术奇点", TechLevel.Archotech => "远期文明框架", _ => string.Empty
    };

    private string BuildEraPageStateSignature(GameState state, EraGoalEvaluation goal = null)
    {
        if (state == null) return string.Empty;
        goal ??= EraGoalEvaluator.Evaluate(state.TechLevel, researchManagerCache, resourceManagerCache);
        StringBuilder signature = eraPageSignatureBuilder; signature.Clear(); signature.Append((int)state.TechLevel).Append('|').Append(goal.Transition?.Id ?? string.Empty);
        for (int i = 0; i < goal.Conditions.Count; i++) { EraGoalConditionEvaluation c = goal.Conditions[i]; signature.Append('|').Append(c.Kind).Append(':').Append(c.Met ? '1' : '0').Append(':').Append(c.Research?.Id ?? c.Resource?.Id ?? string.Empty).Append(':').Append(c.AvailableAmount).Append(':').Append(c.PaidAmount).Append(':').Append(c.RemainingAmount).Append(':').Append(c.ProductionRate).Append(':').Append(c.ConsumptionRate).Append(':').Append(c.ResearchState?.Version ?? 0); }
        return signature.ToString();
    }
}
