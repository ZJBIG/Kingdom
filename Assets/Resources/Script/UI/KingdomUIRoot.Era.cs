using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
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
            state.TechLevel.GetDescription() + "\n" + GetEraSubtitle(state.TechLevel),
            transition == null
                ? "已到达当前内容的最后时代"
                : "下一时代：" + nextEra.GetDescription(),
            Copper,
            null);

        AddEraTextRow(
            parent,
            "当前引导",
            "当前目标、阻碍和推荐行动统一显示在 Overview。",
            TextSecondary,
            pages.ContainsKey("Overview") ? () => SetPage("Overview") : null);

        AddEraTextRow(
            parent,
            "本时代能力",
            "点击查看当前时代的研究与建筑详情",
            TextSecondary,
            GetEraContentNavigation(state.TechLevel));

        string nextEraPromise = BuildNextEraPromise(nextEra);
        if (!string.IsNullOrEmpty(nextEraPromise))
            AddEraTextRow(parent, "进入后可用能力", nextEraPromise, TextPrimary,
                GetEraContentNavigation(nextEra));

        if (transition == null)
        {
            AddEraTextRow(parent, "时代进度", "100%  |  当前内容已完成", Positive, null);
            AddEraTextRow(parent, "下一步", "继续扩张生产链，或在研究页查看尚未完成的研究。", TextPrimary,
                () => SetPage("Research"));
            eraPageStateSignature = BuildEraPageStateSignature(state, eraGoal);
            FinishEraTextRows();
            return;
        }

        AddEraTextRow(
            parent,
            "\u65f6\u4ee3\u63a8\u8fdb\u786c\u6761\u4ef6",
            "\u53ea\u6709\u4ee5\u4e0b\u771f\u5b9e\u6761\u4ef6\u4f1a\u51b3\u5b9a\u65f6\u4ee3\u7814\u7a76\u662f\u5426\u53ef\u63a8\u8fdb",
            TextSecondary,
            null);
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

        Debug.Log($"[王国界面] Era page rendered: current={state.TechLevel}, next={nextEra}, transition={transition.Id}, progress={completed}/{conditions.Count}, blocker={(hasBlocker ? blocker.Title : "none")}");
        bool hasProductionChain = buildingManagerCache != null &&
            TutorialManager.HasOwnedProductionChain(
                buildingManagerCache.States.Values);
        bool foodReady = state.FoodNetRate > ExpantaNum.Zero;
        bool populationReady = state.Population.PopulationCapacity > state.Population.Population;
        bool productivityReady = buildingManagerCache != null &&
            buildingManagerCache.AvailableProductivity > ExpantaNum.Zero;
        bool researchReady = researchManagerCache != null &&
            researchManagerCache.ResearchPower > ExpantaNum.Zero;

        AddEraTextRow(
            parent,
            "\u53d1\u5c55\u51c6\u5907\u5ea6\uff08\u5efa\u8bae\uff09",
            "\u4ee5\u4e0b\u4fe1\u606f\u4ec5\u7528\u4e8e\u9884\u5224\u8fdb\u5165\u4e0b\u4e00\u65f6\u4ee3\u540e\u662f\u5426\u4f1a\u7acb\u5373\u5361\u4f4f\uff0c\u4e0d\u4f1a\u963b\u6b62\u65f6\u4ee3\u8dc3\u8fc1",
            TextSecondary,
            null);
        AddEraTextRow(
            parent,
            (foodReady ? "[x] " : "[ ] ") + "\u98df\u7269\u51c0\u4ea7\u51fa",
            (state.FoodNetRate >= ExpantaNum.Zero ? "+" : "") +
            state.FoodNetRate.ToGameString() + "/s",
            foodReady ? Positive : Copper,
            null);
        AddEraTextRow(
            parent,
            (populationReady ? "[x] " : "[ ] ") + "\u4eba\u53e3\u5bb9\u91cf\u4f59\u91cf",
            state.Population.Population.ToGameString() + "/" +
            state.Population.PopulationCapacity.ToGameString(),
            populationReady ? Positive : Copper,
            null);
        AddEraTextRow(
            parent,
            (productivityReady ? "[x] " : "[ ] ") + "\u53ef\u7528\u751f\u4ea7\u529b",
            buildingManagerCache == null
                ? "0"
                : buildingManagerCache.AvailableProductivity.ToGameString(),
            productivityReady ? Positive : Copper,
            null);
        AddEraTextRow(
            parent,
            (hasProductionChain ? "[x] " : "[ ] ") + "\u8fde\u7eed\u751f\u4ea7\u94fe",
            hasProductionChain ? "\u5df2\u5f62\u6210\u539f\u6599\u2192\u52a0\u5de5\u2192\u4ea7\u51fa\u94fe" : "\u5c1a\u672a\u5f62\u6210\u8fde\u7eed\u751f\u4ea7\u94fe",
            hasProductionChain ? Positive : Copper,
            pages.ContainsKey("Buildings") ? () => SetPage("Buildings") : null);
        AddEraTextRow(
            parent,
            (researchReady ? "[x] " : "[ ] ") + "\u7814\u7a76\u529b",
            researchManagerCache == null
                ? "0/s"
                : researchManagerCache.ResearchPower.ToGameString() + "/s",
            researchReady ? Positive : Copper,
            null);

        AddEraTextRow(
            parent,
            "\u57fa\u7840\u4ea7\u4e1a\u51c6\u5907",
            hasProductionChain
                ? "\u5df2\u5f62\u6210\u771f\u5b9e\u7684\u539f\u6599\u2192\u52a0\u5de5\u2192\u4ea7\u51fa\u94fe\uff0c\u53ef\u4ee5\u7ee7\u7eed\u89c2\u5bdf\u5b83\u5982\u4f55\u652f\u6491\u4e0b\u4e00\u65f6\u4ee3\u3002"
                : "\u5c1a\u672a\u89c2\u5bdf\u5230\u8fde\u7eed\u7684\u539f\u6599\u3001\u52a0\u5de5\u4e0e\u4ea7\u51fa\u94fe\uff1b\u8fd9\u662f\u53d1\u5c55\u51c6\u5907\uff0c\u4e0d\u662f\u65b0\u7684\u65f6\u4ee3\u89c4\u5219\u3002",
            hasProductionChain ? Positive : Copper,
            pages.ContainsKey("Buildings") ? () => SetPage("Buildings") : null);

        eraPageStateSignature = BuildEraPageStateSignature(state, eraGoal);
        FinishEraTextRows();
    }

    private static string GetEraSubtitle(TechLevel era) => era switch
    {
        TechLevel.Animal => "\u706b\u79cd\u4e0e\u805a\u843d",
        TechLevel.Neolithic => "\u5b9a\u5c45\u4e0e\u65e9\u671f\u91d1\u5c5e",
        TechLevel.Medieval => "\u57ce\u5e02\u3001\u5236\u5ea6\u4e0e\u673a\u68b0\u840c\u82bd",
        TechLevel.Industrial => "\u673a\u5668\u3001\u7535\u529b\u4e0e\u89c4\u6a21\u5316\u751f\u4ea7",
        TechLevel.Spacer => "\u8f68\u9053\u3001\u8230\u961f\u4e0e\u661f\u533a",
        TechLevel.Ultra => "\u6280\u672f\u5947\u70b9\uff08\u5f53\u524d\u6846\u67b6\uff09",
        TechLevel.Archotech => "\u8fdc\u671f\u6587\u660e\u6846\u67b6",
        _ => string.Empty
    };

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

    private Action GetEraContentNavigation(TechLevel era)
    {
        Research research = DataBase<Research>.All.FirstOrDefault(value =>
            value != null && value.TechLevel == era && !value.AdvancesTechLevel);
        if (research != null && pages.ContainsKey("Research"))
            return () =>
            {
                SetPage("Research");
                ShowResearchDetails(research);
            };

        Building building = DataBase<Building>.All.FirstOrDefault(value =>
            value != null && !(value is SectorBuilding) && value.TechLevel == era);
        if (building != null && pages.ContainsKey("Buildings"))
            return () =>
            {
                SetPage("Buildings");
                ShowBuildingDetails(building);
            };
        return null;
    }

    private static string BuildNextEraPromise(TechLevel targetEra)
    {
        if (!Enum.IsDefined(typeof(TechLevel), targetEra))
            return string.Empty;

        Research transition = EraGoalEvaluator.FindTransition(targetEra);

        var researchNames = new List<string>();
        IReadOnlyList<Research> researches = DataBase<Research>.All;
        for (int i = 0; i < researches.Count && researchNames.Count < 2; i++)
        {
            Research research = researches[i];
            if (research != null && research.TechLevel == targetEra &&
                !research.AdvancesTechLevel && !string.IsNullOrEmpty(research.Label))
                researchNames.Add(research.Label);
        }

        var buildingNames = new List<string>();
        IReadOnlyList<Building> buildings = DataBase<Building>.All;
        for (int i = 0; i < buildings.Count && buildingNames.Count < 2; i++)
        {
            Building building = buildings[i];
            if (building != null && !(building is SectorBuilding) &&
                building.TechLevel == targetEra &&
                !string.IsNullOrEmpty(building.Label))
                buildingNames.Add(building.Label);
        }

        if (researchNames.Count == 0 && buildingNames.Count == 0)
            return string.Empty;

        StringBuilder promise = new StringBuilder(128);
        if (transition != null)
            promise.Append("关键研究“").Append(transition.Label).Append("”完成后，");
        promise.Append("进入").Append(targetEra.GetDescription()).Append("后，");
        if (researchNames.Count > 0)
            promise.Append("研究可继续理解：").Append(string.Join("、", researchNames));
        if (buildingNames.Count > 0)
        {
            if (researchNames.Count > 0)
                promise.Append("；");
            promise.Append("建设可扩展：").Append(string.Join("、", buildingNames));
        }
        promise.Append("。这些能力会把当前的生产与人口基础带入下一阶段。");
        return promise.ToString();
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
        StringBuilder signature = eraPageSignatureBuilder;
        signature.Clear();
        signature.Append((int)state.TechLevel).Append('|').Append(transition?.Id ?? string.Empty);
        if (TutorialManager.Current != null)
            signature.Append("|tutorial=").Append(TutorialManager.Current.Version);
        bool hasProductionChain = buildingManagerCache != null &&
            TutorialManager.HasOwnedProductionChain(
                buildingManagerCache.States.Values);
        signature.Append("|production-chain=").Append(
            hasProductionChain ? '1' : '0');
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
                string resourceDetail = evaluation.Met
                    ? "已满足，点击查看资源详情"
                    : "尚未满足，点击查看资源详情";
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
        SetRowText(row, "Subtitle", CompactEraSummary(subtitle), TextSecondary);
        TMP_Text subtitleText = row.transform.Find("Subtitle")?.GetComponent<TMP_Text>();
        if (subtitleText != null)
        {
            subtitleText.rectTransform.offsetMax = new Vector2(-164f, 0f);
            subtitleText.enableWordWrapping = false;
            subtitleText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            subtitleText.enableAutoSizing = true;
            subtitleText.fontSizeMin = 18f;
            subtitleText.fontSizeMax = 30f;
        }

        Button rowButton = RequireRowButton(row);
        if (rowButton != null)
        {
            rowButton.onClick.RemoveAllListeners();
            rowButton.interactable = false;
        }

        Button detailButton = row.transform.Find("Detail")?.GetComponent<Button>();
        if (detailButton == null)
        {
            Debug.LogError("[王国界面] Era text row is missing authored Detail button.");
            return null;
        }
        RectTransform detailRect = detailButton.transform as RectTransform;
        detailRect.anchorMin = new Vector2(1f, .5f);
        detailRect.anchorMax = new Vector2(1f, .5f);
        detailRect.pivot = new Vector2(1f, .5f);
        detailRect.sizeDelta = new Vector2(128f, 72f);
        detailRect.anchoredPosition = new Vector2(-18f, 0f);
        detailButton.onClick.RemoveAllListeners();
        detailButton.gameObject.SetActive(navigate != null);
        detailButton.interactable = navigate != null;
        if (detailButton.GetComponent<UIPageScrollDragForwarder>() == null)
            detailButton.gameObject.AddComponent<UIPageScrollDragForwarder>();
        if (navigate != null)
            detailButton.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                navigate();
            });
        Image rowImage = row.GetComponent<Image>();
        if (rowImage != null)
            rowImage.raycastTarget = false;
        return row;
    }

    private static string CompactEraSummary(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        text = text.Replace('\n', ' ');
        if (text.Length > 28)
            text = text.Substring(0, 28);
        return text.Length <= 56 ? text : text.Substring(0, 55) + "… 点击查看详情";
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

}
