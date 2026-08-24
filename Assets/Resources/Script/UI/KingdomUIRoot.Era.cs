using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private readonly List<GameObject> eraTextRows = new();
    private readonly List<EraGoalCondition> eraGoalConditionBuffer = new();
    private readonly StringBuilder eraPageSignatureBuilder = new(96);
    private int eraTextRowCursor;

    private struct EraGoalCondition
    {
        public string Title;
        public string Detail;
        public bool Met;
        public Action Navigate;
    }

    private void BuildEraPage(
        RectTransform parent,
        EraGoalEvaluation preparedGoal = null)
    {
        if (parent == null)
            return;
        eraTextRowCursor = 0;

        CacheRuntimeManagers();
        GameState state = gameManagerCache == null ? null : gameManagerCache.State;
        if (state == null)
        {
            AddEraTextRow(parent, "时代目标", "正在读取王国状态……", TextSecondary, null);
            FinishEraTextRows();
            return;
        }

        TechLevel nextEra = (TechLevel)((int)state.TechLevel + 1);
        if (DataBase<Research>.All.Count == 0)
        {
            AddEraTextRow(parent, "时代目标", "时代定义正在加载……", TextSecondary, null);
            FinishEraTextRows();
            return;
        }
        EraGoalEvaluation eraGoal = preparedGoal ?? EraGoalEvaluator.Evaluate(
            state.TechLevel,
            researchManagerCache,
            resourceManagerCache);
        Research transition = eraGoal.Transition;
        AddEraTextRow(
            parent,
            state.TechLevel.GetDescription(),
            transition == null
                ? "已到达当前内容的最后时代"
                : "下一时代：" + nextEra.GetDescription(),
            Copper,
            null);

        TutorialManager tutorialManager = TutorialManager.Current;
        if (tutorialManager != null)
        {
            TutorialSnapshot tutorial = tutorialManager.Evaluate();
            bool tutorialDestinationIsCurrentPage = tutorial.NavigationPage == "Era";
            AddEraTextRow(
                parent,
                "文明复兴阶段",
                tutorial.CivilizationContext,
                TextSecondary,
                null);
            AddEraTextRow(
                parent,
                "引导目标",
                tutorial.CurrentGoal + "\n" +
                (string.IsNullOrWhiteSpace(tutorial.NarrativeText)
                    ? string.Empty
                    : tutorial.NarrativeText + "\n") +
                tutorial.GoalDescription,
                TextPrimary,
                null);
            AddEraTextRow(
                parent,
                "引导推荐行动",
                tutorialDestinationIsCurrentPage
                    ? "查看下方时代条件与当前主要阻碍。"
                    : tutorial.RecommendedAction,
                Copper,
                tutorialDestinationIsCurrentPage
                    ? null
                    : () => NavigateToTutorialPage(tutorial.NavigationPage));
        }

        AddEraTextRow(
            parent,
            "本时代能力",
            GetEraCapabilitySummary(state.TechLevel),
            TextSecondary,
            null);

        if (transition == null)
        {
            AddEraTextRow(parent, "时代进度", "100%  |  当前内容已完成", Positive, null);
            AddEraTextRow(parent, "下一步", "继续扩张生产链，或在研究页查看尚未完成的研究。", TextPrimary,
                () => SetPage("Research"));
            eraPageStateSignature = BuildEraPageStateSignature(state, eraGoal);
            FinishEraTextRows();
            return;
        }

        List<EraGoalCondition> conditions = BuildEraConditions(eraGoal);
        int completed = 0;
        int blockerIndex = -1;
        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i].Met)
                completed++;
            else if (blockerIndex < 0)
                blockerIndex = i;
        }
        bool hasBlocker = blockerIndex >= 0;
        EraGoalCondition blocker = hasBlocker ? conditions[blockerIndex] : default;
        float progress = conditions.Count == 0 ? 0f : completed / (float)conditions.Count;
        AddEraTextRow(
            parent,
            "时代进度",
            $"{completed}/{conditions.Count} 条件已完成  |  {progress * 100f:0}%",
            progress >= 1f ? Positive : Copper,
            null);
        AddEraTextRow(parent, "时代目标", transition.Label, TextPrimary,
            () =>
            {
                SetPage("Research");
                ShowResearchDetails(transition);
            });

        AddEraTextRow(
            parent,
            "当前任务",
            !hasBlocker
                ? "完成时代目标研究：" + transition.Label
                : blocker.Title + "：" + blocker.Detail,
            hasBlocker ? Copper : Positive,
            hasBlocker
                ? blocker.Navigate
                : () =>
                {
                    SetPage("Research");
                    ShowResearchDetails(transition);
                });

        AddEraTextRow(parent, "达成条件", "完成下列条件后即可推进时代。", TextSecondary, null);
        for (int i = 0; i < conditions.Count; i++)
        {
            EraGoalCondition condition = conditions[i];
            AddEraTextRow(
                parent,
                (condition.Met ? "[x] " : "[ ] ") + condition.Title,
                condition.Detail,
                condition.Met ? Positive : Error,
                condition.Navigate);
        }

        AddEraTextRow(
            parent,
            "当前主要阻碍",
            hasBlocker ? blocker.Title + "：" + blocker.Detail : "所有条件已满足，等待完成时代研究。",
            hasBlocker ? Error : Positive,
            hasBlocker ? blocker.Navigate : null);

        string productivity = buildingManagerCache == null
            ? "0 / 0"
            : buildingManagerCache.UsedProductivity.ToGameString() + " / " + buildingManagerCache.TotalProductivity.ToGameString();
        string researchPower = researchManagerCache == null ? "0" : researchManagerCache.ResearchPower.ToGameString();
        AddEraTextRow(
            parent,
            "国家发展",
            "人口 " + state.Population.Population.ToGameString() + " / " + state.Population.PopulationCapacity.ToGameString() +
            "  |  食物净产出 " + (state.FoodNetRate >= ExpantaNum.Zero ? "+" : "") + state.FoodNetRate.ToGameString() + "/s\n" +
            "生产力 " + productivity + "  |  研究力 " + researchPower + "/s\n" +
            "领土 " + state.TerritoryUsed.ToGameString() + " / " + state.TerritoryTotal.ToGameString(),
            TextPrimary,
            null);

        Debug.Log($"[王国界面] Era page rendered: current={state.TechLevel}, next={nextEra}, transition={transition.Id}, progress={completed}/{conditions.Count}, blocker={(hasBlocker ? blocker.Title : "none")}");
        eraPageStateSignature = BuildEraPageStateSignature(state, eraGoal);
        FinishEraTextRows();
    }

    private static string GetEraCapabilitySummary(TechLevel era) => era switch
    {
        TechLevel.Animal => "采集并加工木材、石材、黏土与纤维，建立食物、住房和知识基础。",
        TechLevel.Neolithic => "发展灌溉储粮、陶瓷纺织、文字治理，以及铜、青铜和铁器生产。",
        TechLevel.Medieval => "建立行政、贸易、学院、城市住宅、炼钢和标准化生产体系。",
        TechLevel.Industrial => "形成电力与铁路物流、机械制造、石油化工、现代大学和规模化农业。",
        TechLevel.Spacer => "建设轨道能源、居住与工业设施，发展量子计算、星际航行、舰队和星区经营。",
        TechLevel.Ultra => "以技术奇点完成当前阶段的文明跃迁；目前没有独立建筑或工坊循环。",
        TechLevel.Archotech => "当前为远期时代框架，尚无独立研究、建筑或工坊内容。",
        _ => "查看研究、建筑和工坊页面了解当前能力。"
    };

    private string BuildEraPageStateSignature(
        GameState state,
        EraGoalEvaluation eraGoal = null)
    {
        if (state == null)
            return string.Empty;
        eraGoal ??= EraGoalEvaluator.Evaluate(
            state.TechLevel,
            researchManagerCache,
            resourceManagerCache);
        Research transition = eraGoal.Transition;
        StringBuilder signature = eraPageSignatureBuilder;
        signature.Clear();
        signature.Append((int)state.TechLevel).Append('|').Append(transition?.Id ?? string.Empty);
        if (TutorialManager.Current != null)
            signature.Append("|tutorial=").Append(TutorialManager.Current.Version);
        if (transition == null)
            return signature.ToString();

        for (int i = 0; i < eraGoal.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraGoal.Conditions[i];
            signature.Append('|').Append(condition.Kind).Append(':');
            if (condition.Kind == EraGoalConditionKind.PrerequisiteResearch)
            {
                signature.Append(condition.Research?.Id ?? string.Empty)
                    .Append(':').Append(condition.ResearchState?.Version ?? 0)
                    .Append(':').Append(condition.Met ? '1' : '0');
            }
            else
            {
                signature.Append(condition.Resource?.Id ?? string.Empty)
                    .Append(':').Append(condition.AvailableAmount)
                    .Append(':').Append(condition.PaidAmount)
                    .Append(':').Append(condition.RemainingAmount)
                    .Append(':').Append(condition.ProductionRate)
                    .Append(':').Append(condition.ConsumptionRate)
                    .Append(':').Append(condition.Met ? '1' : '0');
            }
        }
        return signature.ToString();
    }

    private List<EraGoalCondition> BuildEraConditions(EraGoalEvaluation eraGoal)
    {
        List<EraGoalCondition> result = eraGoalConditionBuffer;
        result.Clear();
        if (eraGoal == null || eraGoal.Transition == null)
            return result;

        for (int i = 0; i < eraGoal.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation evaluation = eraGoal.Conditions[i];
            if (evaluation.Kind == EraGoalConditionKind.PrerequisiteResearch)
            {
                Research prerequisite = evaluation.Research;
                ResearchState prerequisiteState = evaluation.ResearchState;
                result.Add(new EraGoalCondition
                {
                    Title = "研究：" + prerequisite.Label,
                    Detail = evaluation.Met
                        ? "已完成"
                        : ResearchStateLabel(
                            prerequisite,
                            prerequisiteState == null
                                ? ResearchStatus.Locked
                                : prerequisiteState.Status),
                    Met = evaluation.Met,
                    Navigate = () =>
                    {
                        SetPage("Research");
                        ShowResearchDetails(prerequisite);
                    }
                });
            }
            else
            {
                Resource resource = evaluation.Resource;
                string resourceDetail = "剩余需求 " +
                    evaluation.RemainingAmount.ToGameString() +
                    "  |  可用 " + evaluation.AvailableAmount.ToGameString();
                ExpantaNum netRate = evaluation.ProductionRate - evaluation.ConsumptionRate;
                if (!evaluation.Met)
                {
                    resourceDetail += netRate > ExpantaNum.Zero
                        ? "  |  净产出 +" + netRate.ToGameString() + "/s"
                        : "  |  当前无净产出";
                }
                result.Add(new EraGoalCondition
                {
                    Title = "资源：" + resource.Label,
                    Detail = resourceDetail,
                    Met = evaluation.Met,
                    Navigate = () =>
                    {
                        SetPage("Resources");
                        ShowResourceDetails(resource);
                    }
                });
            }
        }
        return result;
    }

    private GameObject AddEraTextRow(RectTransform parent, string title, string subtitle, Color color, Action navigate)
    {
        int index = eraTextRowCursor;
        GameObject reusable = index < eraTextRows.Count ? eraTextRows[index] : null;
        GameObject row = InstantiateAuthoredRow(
            KingdomUIPrefabLibrary.TextRow,
            parent,
            index,
            reusable);
        if (row == null)
            return null;
        if (index < eraTextRows.Count)
            eraTextRows[index] = row;
        else
            eraTextRows.Add(row);
        eraTextRowCursor++;
        ApplyListRowStyle(row, index);
        SetRowText(row, "Title", title, color);
        SetRowText(row, "Subtitle", subtitle, TextSecondary);
        Button button = RequireRowButton(row);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = navigate != null;
            if (navigate != null)
                button.onClick.AddListener(() =>
                {
                    UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                    navigate();
                });
        }
        return row;
    }

    private void FinishEraTextRows()
    {
        for (int i = eraTextRows.Count - 1; i >= eraTextRowCursor; i--)
        {
            if (eraTextRows[i] != null)
                Destroy(eraTextRows[i]);
            eraTextRows.RemoveAt(i);
        }
    }

    private void NavigateToTutorialPage(string pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName) || pageName == "Era" ||
            !pages.ContainsKey(pageName))
            return;
        SetPage(pageName);
    }
}
