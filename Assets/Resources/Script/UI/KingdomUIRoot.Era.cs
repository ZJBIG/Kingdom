using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private sealed class EraGoalCondition
    {
        public string Title;
        public string Detail;
        public bool Met;
        public Action Navigate;
    }

    private void BuildEraPage(RectTransform parent)
    {
        if (parent == null)
            return;

        CacheRuntimeManagers();
        GameState state = gameManagerCache == null ? null : gameManagerCache.State;
        if (state == null)
        {
            AddEraTextRow(parent, "时代目标", "正在读取王国状态……", TextSecondary, null);
            return;
        }

        TechLevel nextEra = (TechLevel)((int)state.TechLevel + 1);
        if (DataBase<Research>.All.Count == 0)
        {
            AddEraTextRow(parent, "时代目标", "时代定义正在加载……", TextSecondary, null);
            return;
        }
        EraGoalEvaluation eraGoal = EraGoalEvaluator.Evaluate(
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
                tutorial.RecommendedAction,
                Copper,
                () => NavigateToTutorialPage(tutorial.NavigationPage));
        }

        if (transition == null)
        {
            AddEraTextRow(parent, "时代进度", "100%  |  当前内容已完成", Positive, null);
            AddEraTextRow(parent, "下一步", "继续扩张生产链，或在研究页查看尚未完成的研究。", TextPrimary,
                () => SetPage("Research"));
            eraPageStateSignature = BuildEraPageStateSignature(state, eraGoal);
            return;
        }

        List<EraGoalCondition> conditions = BuildEraConditions(eraGoal);
        int completed = 0;
        for (int i = 0; i < conditions.Count; i++)
            if (conditions[i].Met)
                completed++;
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

        EraGoalCondition nextAction = null;
        for (int i = 0; i < conditions.Count; i++)
            if (!conditions[i].Met)
            {
                nextAction = conditions[i];
                break;
            }
        AddEraTextRow(
            parent,
            "当前任务",
            nextAction == null
                ? "完成时代目标研究：" + transition.Label
                : nextAction.Title + "：" + nextAction.Detail,
            nextAction == null ? Positive : Copper,
            nextAction == null
                ? () =>
                {
                    SetPage("Research");
                    ShowResearchDetails(transition);
                }
                : nextAction.Navigate);

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

        EraGoalCondition blocker = null;
        for (int i = 0; i < conditions.Count; i++)
            if (!conditions[i].Met)
            {
                blocker = conditions[i];
                break;
            }
        AddEraTextRow(
            parent,
            "当前主要阻碍",
            blocker == null ? "所有条件已满足，等待完成时代研究。" : blocker.Title + "：" + blocker.Detail,
            blocker == null ? Positive : Error,
            blocker?.Navigate);

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

        Debug.Log($"[王国界面] Era page rendered: current={state.TechLevel}, next={nextEra}, transition={transition.Id}, progress={completed}/{conditions.Count}, blocker={(blocker == null ? "none" : blocker.Title)}");
        eraPageStateSignature = BuildEraPageStateSignature(state, eraGoal);
    }

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
        var signature = new StringBuilder(96);
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
        var result = new List<EraGoalCondition>();
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
        GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.TextRow, parent, parent.childCount);
        if (row == null)
            return null;
        ApplyListRowStyle(row, parent.childCount - 1);
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

    private void NavigateToTutorialPage(string pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName) || pageName == "Era" ||
            !pages.ContainsKey(pageName))
            return;
        SetPage(pageName);
    }
}
