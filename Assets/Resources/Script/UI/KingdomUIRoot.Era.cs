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

        RectTransform current = BeginEraSection(parent, "EraCurrentArchive", "文明阶段", width, y, out float currentY);
        currentY += CreateEraCard(current, "EraIdentity", state.TechLevel.GetDescription() + " · 第 " + ((int)state.TechLevel + 1) + " / " + Enum.GetValues(typeof(TechLevel)).Length + " 阶段",
            GetEraSubtitle(state.TechLevel) + "\n" + GetEraDefinition(state.TechLevel), Panel, false, null, null, width, currentY, 100f);
        y += EndEraSection(current, currentY, width) + 18f;

        if (transition == null)
        {
            RectTransform terminal = BeginEraSection(parent, "EraTransitionSection", "时代状态", width, y, out float terminalY);
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

        RectTransform transitionSection = BeginEraSection(parent, "EraTransitionSection", "下一时代跃迁", width, y, out float transitionY);
        transitionY += CreateEraCard(transitionSection, "EraTransitionTarget", "迈向 " + goal.TargetEra.GetDescription(), transition.Label + "\n" + transition.Description + "\n\n" + GetTransitionStatus(transition, progress) + " · 所需条件 " + completed + "/" + eraConditions.Count, PanelRaised, true,
            () => { SetPage("Research"); ShowResearchDetails(transition); }, showQueueAction ? transition : null, width, transitionY, 180f) + 10f;
        string impact = BuildNextEraImpact(goal.TargetEra);
        if (!string.IsNullOrEmpty(impact))
            transitionY += CreateEraCard(transitionSection, "EraNextImpact", "进入后变化", impact, PanelRaised, false, null, null, width, transitionY, 130f) + 16f;
        y += EndEraSection(transitionSection, transitionY, width) + 18f;

        RectTransform requirements = BeginEraSection(parent, "EraRequirementSection", "跃迁条件与阻碍", width, y, out float requirementY);
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
        int resourceIndex = 0;
        for (int i = 0; i < eraConditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraConditions[i];
            if (condition.Kind != EraGoalConditionKind.Resource) continue;
            Resource resource = condition.Resource;
            requirementY += CreateEraCard(requirements, "EraResource_" + resource.Id, (condition.Met ? "✓ " : "○ ") + resource.Label,
                FormatResourceCondition(condition), GetEraAlternatePanelColor(resourceIndex++), true, () => ShowResourceDetails(resource), null, width, requirementY, 120f) + 10f;
        }
        string blockerTitle = blockerIndex < 0 ? "全部条件已满足" : BuildConditionTitle(eraConditions[blockerIndex]);
        if (eraConditions.Count == 0)
            requirementY += CreateEraCard(requirements, "EraRequirementsMet", "可以推进", "前置研究与材料均已备齐，前往研究页开始时代跃迁。", Panel, false, null, null, width, requirementY, 100f);
        y += EndEraSection(requirements, requirementY, width);
        SetEraPageHeight(parent, y + 24f, state, goal);
        Debug.Log($"[王国界面] Era sections rendered: current={state.TechLevel}, target={goal.TargetEra}, progress={completed}/{eraConditions.Count}, blocker={blockerTitle}");
    }

    private RectTransform BeginEraSection(RectTransform parent, string name, string title, float width, float y, out float contentY)
    {
        RectTransform section = Instantiate(KingdomUIPrefabLibrary.Load(KingdomUIPrefabLibrary.EraSection), parent, false).GetComponent<RectTransform>();
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

    private float EndEraSection(RectTransform content, float contentY, float width)
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
        bool hasAction, Action action, Research queueResearch, float width = 1200f, float y = 0f, float minimumHeight = 140f)
    {
        RectTransform card = Instantiate(KingdomUIPrefabLibrary.Load(KingdomUIPrefabLibrary.EraCard), parent, false).GetComponent<RectTransform>();
        card.name = name;
        card.GetComponent<Image>().color = color == PanelRaised || color == ListRowOdd
            ? new Color(.17f, .20f, .17f, .94f) : new Color(.12f, .16f, .14f, .94f);
        SetTop(card, y, minimumHeight);
        const float cardPadding = 24f;
        const float titleBodyGap = 24f;
        float cardWidth = Mathf.Max(320f, width - 96f);
        float titleWidth = hasAction ? Mathf.Max(240f, cardWidth - 540f) : cardWidth;
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
        float bodyWidth = hasAction ? Mathf.Max(320f, cardWidth - 540f) : cardWidth;
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
        if (hasAction && action != null) AddEraActions(card, action, queueResearch);
        return height;
    }

    private void AddEraActions(RectTransform card, Action detailAction, Research queueResearch)
    {
        AddEraButton(card, "DetailAction", "查看详情", BasicMetalIngot, detailAction);
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
        });
    }

    private void AddEraButton(RectTransform card, string name, string label, Color color, Action action)
    {
        Button button = card.Find(name).GetComponent<Button>();
        button.gameObject.SetActive(true);
        button.targetGraphic.color = color;
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.GetComponentInChildren<TMP_Text>().font = sharedFontAsset;
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
        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }

    private static string BuildConditionTitle(EraGoalConditionEvaluation condition) => condition.Kind == EraGoalConditionKind.PrerequisiteResearch ? "研究：" + condition.Research.Label : "资源：" + condition.Resource.Label;
    private static string FormatResourceCondition(EraGoalConditionEvaluation condition)
    {
        string payment = "库存 " + condition.AvailableAmount.ToGameString() + " / 剩余应付 " + condition.RemainingAmount.ToGameString();
        if (condition.PaidAmount > ExpantaNum.Zero) payment += "（已付 " + condition.PaidAmount.ToGameString() + "）";
        if (condition.Met) return payment + "（已满足）";
        ExpantaNum net = condition.ProductionRate - condition.ConsumptionRate;
        string shortfall = payment + "，尚欠库存 " + condition.MissingAmount.ToGameString();
        if (net > ExpantaNum.Zero) return shortfall + "，净产出 +" + net.ToGameString() + "/s，按当前净产出预计 " + (condition.MissingAmount / net).ToGameString() + " 秒备齐材料";
        if (condition.ProductionRate <= ExpantaNum.Zero) return shortfall + "，无当前生产来源";
        return shortfall + "，净产出 " + net.ToGameString() + "/s，无法估算";
    }

    private static bool IsEraTransitionResearchCompleted(Research transition) => transition != null && ResearchManager.Instance != null && ResearchManager.Instance.States.TryGetValue(transition, out ResearchState state) && state.Status == ResearchStatus.Completed;

    private static string GetEraDefinition(TechLevel era) => era switch
    {
        TechLevel.Animal => "采集原木、储备食物并建造住房；居民入住后提供生产力。", TechLevel.StoneAge => "开采矿石、冶炼金属，用农田和粮仓支撑更多居民。", TechLevel.Medieval => "冶炼钢材，建立贸易与学术设施，为机器生产准备材料和研究。", TechLevel.Industrial => "建设电网与铁路，将矿石、石油加工成机器和精密材料。", TechLevel.Spacer => "建造轨道设施与舰队；维持食物和材料补给，探索并占领星区。", TechLevel.Ultra => "建造自主制造巨构，完成三阶段文明工程认证，解锁远征供给姿态。", TechLevel.Archotech => "完成文明工程认证，探索远古遗迹，继续占领远星据点。", _ => string.Empty
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
        for (int i = 0; i < goal.Conditions.Count; i++) { EraGoalConditionEvaluation c = goal.Conditions[i]; signature.Append('|').Append(c.Kind).Append(':').Append(c.Met ? '1' : '0').Append(':').Append(c.Research?.Id ?? c.Resource?.Id ?? string.Empty).Append(':').Append(c.AvailableAmount).Append(':').Append(c.PaidAmount).Append(':').Append(c.RemainingAmount).Append(':').Append(c.ProductionRate).Append(':').Append(c.ConsumptionRate).Append(':').Append(c.ResearchState?.Version ?? 0); }
        return signature.ToString();
    }
}
