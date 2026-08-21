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
        Research transition = FindEraTransition(nextEra);
        AddEraTextRow(
            parent,
            state.TechLevel.GetDescription(),
            transition == null
                ? "已到达当前内容的最后时代"
                : "下一时代：" + nextEra.GetDescription(),
            Copper,
            null);

        if (transition == null)
        {
            AddEraTextRow(parent, "时代进度", "100%  |  当前内容已完成", Positive, null);
            AddEraTextRow(parent, "下一步", "继续扩张生产链，或在研究页查看尚未完成的研究。", TextPrimary,
                () => SetPage("Research"));
            eraPageStateSignature = BuildEraPageStateSignature(state, transition);
            return;
        }

        List<EraGoalCondition> conditions = BuildEraConditions(transition);
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
        eraPageStateSignature = BuildEraPageStateSignature(state, transition);
    }

    private string BuildEraPageStateSignature(GameState state, Research transition = null)
    {
        if (state == null)
            return string.Empty;
        transition ??= FindEraTransition((TechLevel)((int)state.TechLevel + 1));
        var signature = new StringBuilder(96);
        signature.Append((int)state.TechLevel).Append('|').Append(transition?.Id ?? string.Empty);
        if (transition == null)
            return signature.ToString();

        for (int i = 0; i < transition.Prerequisites.Count; i++)
        {
            Research prerequisite = transition.Prerequisites[i];
            bool met = prerequisite != null && researchManagerCache != null &&
                researchManagerCache.IsResearchCompleted(prerequisite.Id);
            signature.Append('|').Append(prerequisite?.Id ?? string.Empty).Append(':').Append(met ? '1' : '0');
        }
        for (int i = 0; i < transition.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = transition.ResourceRequirements[i];
            bool met = requirement.First != null && resourceManagerCache != null &&
                resourceManagerCache.GetAmount(requirement.First) >= requirement.Second;
            signature.Append('|').Append(requirement.First?.Id ?? string.Empty).Append(':').Append(met ? '1' : '0');
        }
        return signature.ToString();
    }

    private List<EraGoalCondition> BuildEraConditions(Research transition)
    {
        var result = new List<EraGoalCondition>();
        if (transition == null)
            return result;

        for (int i = 0; i < transition.Prerequisites.Count; i++)
        {
            Research prerequisite = transition.Prerequisites[i];
            if (prerequisite == null)
                continue;
            bool met = researchManagerCache != null && researchManagerCache.IsResearchCompleted(prerequisite.Id);
            string researchDetail = met ? "已完成" : "尚未完成";
            if (!met && researchManagerCache != null &&
                researchManagerCache.States.TryGetValue(prerequisite, out ResearchState prerequisiteState))
                researchDetail = ResearchStateLabel(prerequisite, prerequisiteState.Status);
            result.Add(new EraGoalCondition
            {
                Title = "研究：" + prerequisite.Label,
                Detail = researchDetail,
                Met = met,
                Navigate = () =>
                {
                    SetPage("Research");
                    ShowResearchDetails(prerequisite);
                }
            });
        }

        for (int i = 0; i < transition.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = transition.ResourceRequirements[i];
            if (requirement.First == null)
                continue;
            ExpantaNum amount = resourceManagerCache == null
                ? ExpantaNum.Zero
                : resourceManagerCache.GetAmount(requirement.First);
            ExpantaNum required = ExpantaNum.Max(ExpantaNum.Zero, requirement.Second);
            bool met = amount >= required;
            Resource resource = requirement.First;
            string resourceDetail = amount.ToGameString() + " / " + required.ToGameString();
            if (!met && resourceManagerCache != null &&
                resourceManagerCache.States.TryGetValue(resource, out ResourceState resourceState))
            {
                ExpantaNum netRate = resourceState.ProductionRate - resourceState.ConsumptionRate;
                resourceDetail += netRate > ExpantaNum.Zero
                    ? "  |  净产出 +" + netRate.ToGameString() + "/s"
                    : "  |  当前无净产出";
            }
            result.Add(new EraGoalCondition
            {
                Title = "资源：" + resource.Label,
                Detail = resourceDetail,
                Met = met,
                Navigate = () =>
                {
                    SetPage("Resources");
                    ShowResourceDetails(resource);
                }
            });
        }
        return result;
    }

    private static Research FindEraTransition(TechLevel target)
    {
        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        Research result = null;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research candidate = definitions[i];
            if (candidate == null || !candidate.AdvancesTechLevel || candidate.TechLevel != target)
                continue;
            if (result == null || string.CompareOrdinal(candidate.Id, result.Id) < 0)
                result = candidate;
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
}
