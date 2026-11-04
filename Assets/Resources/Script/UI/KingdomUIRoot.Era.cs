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
            CreateEraCard(parent, "EraArchiveHeader", "时代档案", "正在读取王国状态……", PanelRaised, null, null);
            nextRowTop = 260f;
            return;
        }

        EraGoalEvaluation goal = preparedGoal ?? EraGoalEvaluator.Evaluate(state.TechLevel, researchManagerCache, resourceManagerCache);
        ResearchDef transition = goal.Transition;
        float width = Mathf.Max(480f, pageHost == null ? 1200f : pageHost.rect.width);
        float y = 24f;

        RectTransform current = BeginEraSection(parent, "EraCurrentArchive", "文明阶段", width, y, out float currentY);
        currentY += CreateEraCard(current, "EraIdentity", state.TechLevel.GetDescription() + " · 第 " + ((int)state.TechLevel + 1) + " / " + Enum.GetValues(typeof(TechLevel)).Length + " 阶段",
            GetEraSubtitle(state.TechLevel) + "\n" + GetEraDefinition(state.TechLevel), Panel, null, null, width, currentY, 180f);
        y += EndEraSection(current, currentY) + 18f;

        if (transition == null)
        {
            RectTransform terminal = BeginEraSection(parent, "EraTransitionSection", "时代状态", width, y, out float terminalY);
            terminalY += CreateEraCard(terminal, "EraTerminal", "当前内容终点", "当前内容已到达最后阶段。\n继续完成研究树中的剩余内容。", PanelRaised, () => SetPage("Research"), null, width, terminalY, 150f);
            y += EndEraSection(terminal, terminalY);
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

        RectTransform transitionSection = BeginEraSection(parent, "EraTransitionSection", "下一时代跃迁", width, y, out float transitionY);
        transitionY += CreateEraCard(transitionSection, "EraTransitionTarget", "迈向 " + goal.TargetEra.GetDescription(), transition.Label + "\n" + GetTransitionStatus(transition, progress) + " · 已满足 " + completed + "/" + eraConditions.Count + " 项条件\n完成该研究后进入新时代。", PanelRaised,
            () => ShowResearchDetails(transition), showQueueAction ? transition : null, width, transitionY, 180f) + 10f;
        string impact = BuildNextEraImpact(goal.TargetEra);
        if (!string.IsNullOrEmpty(impact))
            transitionY += CreateEraCard(transitionSection, "EraNextImpact", "进入后变化", impact, PanelRaised, null, null, width, transitionY, 130f) + 16f;
        y += EndEraSection(transitionSection, transitionY) + 18f;

        RectTransform requirements = BeginEraSection(parent, "EraRequirementSection", "跃迁条件与阻碍", width, y, out float requirementY);
        int researchIndex = 0;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraConditions[i];
            if (condition.Kind != EraGoalConditionKind.PrerequisiteResearch) continue;
            ResearchDef research = condition.Research;
            ResearchState researchState = condition.ResearchState;
            requirementY += CreateEraCard(requirements, "EraResearch_" + research.Id, (condition.Met ? "✓ " : "○ ") + research.Label,
                condition.Met ? "前置研究已完成" : "必须先完成这项研究 · " + ResearchStateLabel(research, researchState == null ? ResearchStatus.Locked : researchState.Status),
                GetEraAlternatePanelColor(researchIndex++), () => ShowResearchDetails(research), null, width, requirementY, 92f) + 10f;
        }
        int resourceIndex = 0;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraConditions[i];
            if (condition.Kind != EraGoalConditionKind.Resource) continue;
            ResourceDef resource = condition.Resource;
            requirementY += CreateEraCard(requirements, "EraResource_" + resource.Id, (condition.Met ? "✓ " : "○ ") + resource.Label,
                FormatResourceCondition(condition), GetEraAlternatePanelColor(resourceIndex++), () => ShowResourceDetails(resource), null, width, requirementY, 120f) + 10f;
        }
        string blockerTitle = blockerIndex < 0 ? "全部条件已满足" : BuildConditionTitle(eraConditions[blockerIndex]);
        if (eraConditions.Count == 0)
            requirementY += CreateEraCard(requirements, "EraRequirementsMet", "可以推进", "将上方跃迁课题加入队列，完成研究后进入新时代。", Panel, null, null, width, requirementY, 100f);
        y += EndEraSection(requirements, requirementY);
        SetEraPageHeight(parent, y + 24f, state, goal);
        Debug.Log($"[王国界面] Era sections rendered: current={state.TechLevel}, target={goal.TargetEra}, progress={completed}/{eraConditions.Count}, blocker={blockerTitle}");
    }

    private RectTransform BeginEraSection(RectTransform parent, string name, string title, float width, float y, out float contentY)
    {
        RectTransform section = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.EraSection, parent).GetComponent<RectTransform>();
        section.name = name;
        // EraRows is the outer page's authored data surface. Forward drags from
        // the section shell itself so a gesture starting in its header/background
        // still reaches the shared PageHost ScrollRect.
        SetTop(section, y, 200f);
        TMP_Text titleText = section.Find("Title").GetComponent<TMP_Text>();
        titleText.text = title;
        titleText.font = sharedFontAsset;
        titleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(320f, width - 80f));
        titleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(48f, titleText.preferredHeight));
        RectTransform content = section.Find("Content") as RectTransform;
        contentY = 12f;
        return content;
    }

    private float EndEraSection(RectTransform content, float contentY)
    {
        float contentHeight = contentY + 12f;
        content.sizeDelta = new Vector2(-24f, contentHeight);
        RectTransform section = content.parent as RectTransform;
        float height = -content.anchoredPosition.y + contentHeight + 16f;
        section.sizeDelta = new Vector2(-24f, height);
        return height;
    }

    private static Color GetEraAlternatePanelColor(int index) => index % 2 == 0 ? ListRowEven : ListRowOdd;

    private float CreateEraCard(RectTransform parent, string name, string title, string body, Color color,
        Action action, ResearchDef queueResearch, float width = 1200f, float y = 0f, float minimumHeight = 140f)
    {
        RectTransform card = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.EraCard, parent).GetComponent<RectTransform>();
        card.name = name;
        card.GetComponent<Image>().color = color == PanelRaised || color == ListRowOdd
            ? new Color(.17f, .20f, .17f, .94f) : new Color(.12f, .16f, .14f, .94f);
        SetTop(card, y, minimumHeight);
        const float cardPadding = 24f;
        const float titleBodyGap = 24f;
        float cardWidth = Mathf.Max(320f, width - 96f);
        float titleWidth = queueResearch != null ? Mathf.Max(240f, cardWidth - 360f) : cardWidth;
        TMP_Text titleText = card.Find("Title").GetComponent<TMP_Text>();
        titleText.text = title;
        titleText.font = sharedFontAsset;
        titleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, titleWidth);
        titleText.rectTransform.anchoredPosition = new Vector2(cardPadding, -cardPadding);
        float titleHeight = Mathf.Max(48f, titleText.preferredHeight);
        titleText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, titleHeight);
        // Reserve the right side for the detail/queue actions.  Keeping the
        // body inside the remaining column prevents long transition
        // descriptions from flowing underneath the buttons on narrow
        // landscape layouts.
        float bodyWidth = queueResearch != null ? Mathf.Max(320f, cardWidth - 360f) : cardWidth;
        TMP_Text bodyText = card.Find("Body").GetComponent<TMP_Text>();
        bodyText.text = body;
        bodyText.font = sharedFontAsset;
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);
        ((RectTransform)card.Find("Divider")).anchoredPosition = new Vector2(0, -(cardPadding + titleHeight + 10f));
        bodyText.rectTransform.anchoredPosition = new Vector2(cardPadding, -(cardPadding + titleHeight + titleBodyGap));
        float bodyHeight = Mathf.Max(48f, bodyText.preferredHeight);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
        float height = Mathf.Max(minimumHeight,
            cardPadding + titleHeight + titleBodyGap + bodyHeight + cardPadding);
        card.sizeDelta = new Vector2(-24f, height);
        Button selection = card.GetComponent<Button>();
        if (selection != null)
        {
            selection.interactable = action != null;
            if (selection.interactable)
                selection.onClick.AddListener(() => { action(); });
        }
        if (action != null) AddEraActions(card, queueResearch);
        return height;
    }

    private void AddEraActions(RectTransform card, ResearchDef queueResearch)
    {
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
            ShowResearchDetails(queueResearch);
            RefreshEraPageIfChanged();
        });
    }

    private void AddEraButton(RectTransform card, string name, string label, Color color, Action action)
    {
        Button button = card.Find(name).GetComponent<Button>();
        button.gameObject.SetActive(true);
        button.targetGraphic.color = color;
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.GetComponentInChildren<TMP_Text>().font = sharedFontAsset;
        button.onClick.AddListener(() => { action(); });
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

    private static string GetTransitionStatus(ResearchDef transition, float progress)
    {
        ResearchManager manager = ResearchManager.Instance;
        if (manager != null && manager.States.TryGetValue(transition, out ResearchState state))
        {
            if (state.Status == ResearchStatus.Researching) return "跃迁研究进行中";
            if (state.Status == ResearchStatus.Completed) return "已完成";
            if (manager.IsQueued(transition)) return state.Status == ResearchStatus.WaitingResources ? "已排队，等待材料" : "已排队，等待开研";
        }
        if (progress >= 1f) return "全部条件满足，等待研究";
        return progress <= 0f ? "尚未满足跃迁条件" : "部分条件已满足";
    }

    private static string BuildNextEraImpact(TechLevel targetEra)
    {
        var lines = new List<string>();
        ResearchDef transition = EraGoalEvaluator.FindTransition(targetEra);
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
            BuildingDef building = DataBase<BuildingDef>.Find(signatureBuildings[i]);
            if (building == null) continue;
            var prerequisites = new List<string>();
            for (int j = 0; j < building.RequiredResearch.Count; j++)
                if (building.RequiredResearch[j] != null) prerequisites.Add(building.RequiredResearch[j].Label);
            lines.Add("时代代表能力（仍需建设）：" + building.Label +
                (prerequisites.Count > 0 ? "；先研究 " + string.Join("、", prerequisites) : string.Empty));
        }
        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }

    private static string BuildConditionTitle(EraGoalConditionEvaluation condition) => condition.Kind == EraGoalConditionKind.PrerequisiteResearch ? "研究：" + condition.Research.Label : "资源：" + condition.Resource.Label;
    private static string FormatResourceCondition(EraGoalConditionEvaluation condition)
    {
        string payment = "本次需备齐 " + condition.RemainingAmount.ToGameString() + "；现有库存 " + condition.AvailableAmount.ToGameString();
        if (condition.PaidAmount > ExpantaNum.Zero) payment += "（已付 " + condition.PaidAmount.ToGameString() + "）";
        if (condition.Met) return payment + "（已满足）";
        ExpantaNum net = condition.ProductionRate - condition.ConsumptionRate;
        string shortfall = payment + "，尚欠库存 " + condition.MissingAmount.ToGameString();
        if (net > ExpantaNum.Zero) return shortfall + "，净产出 +" + net.ToGameString() + "/s，按当前净产出预计 " + (condition.MissingAmount / net).ToGameString() + " 秒备齐材料";
        if (condition.ProductionRate <= ExpantaNum.Zero) return shortfall + "，无当前生产来源";
        return shortfall + "，净产出 " + net.ToGameString() + "/s，无法估算";
    }

    private static bool IsEraTransitionResearchCompleted(ResearchDef transition) => transition != null && ResearchManager.Instance != null && ResearchManager.Instance.States.TryGetValue(transition, out ResearchState state) && state.Status == ResearchStatus.Completed;

    private static string GetEraDefinition(TechLevel era) => era switch
    {
        TechLevel.Animal => "鼠族还在树根与岩隙间寻找容身之所。拾来的枝木点起火堆，挖出的黏土围住灶台，第一座木屋让流散的同族有了归处。食物先要填饱肚子，剩余劳力才能开垦田地、收集石料，记下长者口耳相传的知识。此时还没有可靠的历法，季节藏在风向与草籽之中。聚落要学会储粮、分工和研究，才能把偶然的丰收变成能延续到下一代的生活。",
        TechLevel.StoneAge => "石斧凿开山壁，炉火照亮鼠族的第一批金属工具。散落的洞穴逐渐连成聚落，农田与粮仓把居民留在土地上，陶器让食物和原料有了更可靠的容器。矿石、木料和粮食开始沿着固定的道路交换，每一座新工坊都需要有人照料。首领不再只数过冬的口粮，也要安排开采、冶炼与研究的先后，让有限的劳力支撑更长的生产链。",
        TechLevel.Medieval => "鼠族城镇的屋顶越过围墙，锻炉、集市和学堂沿街而立。钢材让工具更坚固，水利与轮作支持更多居民，商队把不同聚落的短缺连成交换的机会。知识不再只藏在长者记忆里，抄写与教学使工艺能够传承。繁荣也带来新的协调难题：矿井需要粮食，工坊需要钢材，研究需要稳定的供给。城市必须学会制度与分工，才能迎接机器的时代。",
        TechLevel.Industrial => "蒸汽越过鼠族工厂的屋脊，电线与铁轨把矿山、田野和城市连成整体。机器放大了劳作的成果，也把煤炭、原油和金属变成每日不能中断的需求。炼油、化工与精密加工相互依存，基础采集设施仍为复杂产业提供启动的根基。此时扩张不只是多造厂房：劳力、电力、物流和口粮必须一起安排，才能让规模庞大的生产链持续运转。",
        TechLevel.Spacer => "第一批鼠族航行者离开母星，在轨道上回望祖先仰望过的夜空。船坞、观测站与星区据点将文明的道路延伸到行星之外，但舰队仍要依靠家园的粮食、燃料与材料。勘探带来新来源，远航也拉长了补给线。每一处占领都需要慎重维护，轨道工业则把旧有工坊的成果送向新的用途。星际生活从来不是遗忘故乡，而是让故乡能够支持更远的探索。",
        TechLevel.Ultra => "自动化设施与自主制造巨构接过繁重的劳作，鼠族开始以整个文明的尺度安排资源。曾经分散的研究、工业与远征供给汇成共同的工程目标，技术奇点带来新的能力，也要求更加稳定的基础生产。居民的生活依旧离不开食物与居所，远方舰队依旧需要可靠补给。文明工程的每一阶段认证，都在检验这套庞大体系能否将新知识转化为长期可用的力量。",
        TechLevel.Archotech => "鼠族文明已走到现有技术的边缘，远古遗迹的结构却仍超出熟悉的解释。完成文明工程认证后，研究者与探险者把目光投向更遥远的星区，尝试从残存装置中理解失落文明的选择。强大的制造能力并未消除供给与组织的重要性，远星据点仍依靠持续的生产维系。每一次发现都让鼠族重新审视自己的道路：如何带着聚落时代留下的记忆，走向尚未命名的未来。",
        _ => string.Empty
    };

    private static string GetEraSubtitle(TechLevel era) => era switch
    {
        TechLevel.Animal => "火种与聚落", TechLevel.StoneAge => "定居与早期金属", TechLevel.Medieval => "城市、制度与机械萌芽", TechLevel.Industrial => "机器、电力与规模化生产", TechLevel.Spacer => "轨道、舰队与星区", TechLevel.Ultra => "技术奇点", TechLevel.Archotech => "文明认证与远古遗迹", _ => string.Empty
    };

    private string BuildEraPageStateSignature(GameState state, EraGoalEvaluation goal = null)
    {
        if (state == null) return string.Empty;
        goal ??= EraGoalEvaluator.Evaluate(state.TechLevel, researchManagerCache, resourceManagerCache);
        StringBuilder signature = eraPageSignatureBuilder; signature.Clear(); signature.Append((int)state.TechLevel).Append('|').Append(goal.Transition?.Id ?? string.Empty);
        if (goal.Transition != null && researchManagerCache != null)
        {
            ResearchState transitionState = researchManagerCache.GetState(goal.Transition);
            signature.Append("|transition=").Append((int)(transitionState?.Status ?? ResearchStatus.Locked))
                .Append(':').Append(transitionState?.CostPaid ?? false)
                .Append(':').Append(researchManagerCache.IsQueued(goal.Transition));
        }
        for (int i = 0; i < goal.Conditions.Count; i++) { EraGoalConditionEvaluation c = goal.Conditions[i]; signature.Append('|').Append(c.Kind).Append(':').Append(c.Met ? '1' : '0').Append(':').Append(c.Research?.Id ?? c.Resource?.Id ?? string.Empty).Append(':').Append(c.AvailableAmount).Append(':').Append(c.PaidAmount).Append(':').Append(c.RemainingAmount).Append(':').Append(c.ProductionRate).Append(':').Append(c.ConsumptionRate).Append(':').Append(c.ResearchState?.Version ?? 0); }
        return signature.ToString();
    }
}
