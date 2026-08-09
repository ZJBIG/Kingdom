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
    private void BuildSectorRows(RectTransform parent)
    {
        if (parent == null)
            return;

        GameManager gameManager = FindObjectOfType<GameManager>();
        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        SectorManager sectorManager = gameManager == null ? null : gameManager.Sectors;
        GameState state = gameManager == null ? null : gameManager.State;
        IReadOnlyList<SectorDefinition> definitions = DataBase<SectorDefinition>.All;
        int visible = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition definition = definitions[i];
            if (definition == null)
                continue;

            GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.TextRow, parent, visible++);
            if (row == null)
                continue;
            ApplyListRowStyle(row, visible - 1);

            string title = string.IsNullOrEmpty(definition.Label) ? definition.Id : definition.Label;
            string subtitle = BuildSectorSummary(definition, sectorManager, state, resourceManager);
            if (!SetRowText(row, "Title", title) || !SetRowText(row, "Subtitle", subtitle))
                continue;

            Button button = RequireRowButton(row);
            if (button == null)
                continue;
            button.onClick.AddListener(() => ShowSectorDetails(definition, sectorManager, state, resourceManager));
        }

        Debug.Log($"[KingdomUI] Sector rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private static string BuildSectorSummary(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        SectorState sectorState = sectorManager == null ? null : sectorManager.GetState(definition);
        string progress = sectorState == null ? "0%" : (sectorState.CampaignProgress * 100).ToGameString() + "%";
        if (definition.IsHomeSystem)
        {
            string attack = state == null ? "0" : state.AttackPower.ToGameString();
            string defense = state == null ? "0" : state.DefensePower.ToGameString();
            return "本星系探索  |  攻击 " + attack + "/" + definition.EnemyPower.ToGameString() +
                "  防御 " + defense + "/" + definition.EnemyPower.ToGameString() +
                "  | 进度 " + progress;
        }

        if (state == null || sectorManager == null)
            return "远星战役  |  需要深空舰队  |  进度 " + progress;

        SectorCampaignPreview preview = sectorManager.GetCampaignPreview(definition, state, resourceManager);
        return "远星战役  |  战斗比率 " + preview.CombatRatio.ToGameString() +
            "  生存倍率 " + preview.FleetSurvivalFactor.ToGameString() +
            "  | 进度 " + progress;
    }

    private void ShowSectorDetails(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null)
            return;

        StringBuilder body = new StringBuilder();
        body.AppendLine(definition.Label ?? definition.Id);
        body.AppendLine();
        body.AppendLine(definition.Description ?? string.Empty);
        body.AppendLine();
        body.AppendLine(definition.IsHomeSystem ? "本星系探索" : "远星星区战役");
        body.AppendLine("状态：" + GetSectorStateText(sectorManager, definition));

        if (state == null)
        {
            body.AppendLine("运行时状态尚未初始化。");
        }
        else if (definition.IsHomeSystem)
        {
            body.AppendLine("探索要求：攻击力 ≥ " + definition.EnemyPower.ToGameString());
            body.AppendLine("舰队生存要求：防御力 ≥ " + definition.EnemyPower.ToGameString());
            body.AppendLine("当前攻击力：" + state.AttackPower.ToGameString());
            body.AppendLine("当前防御力：" + state.DefensePower.ToGameString());
            body.AppendLine("提示：本星系不进行星区战役。先发展生产和舰队，再回来完成探索。");
        }
        else if (sectorManager != null)
        {
            SectorCampaignPreview preview = sectorManager.GetCampaignPreview(definition, state, resourceManager);
            body.AppendLine("战斗比率：" + preview.CombatRatio.ToGameString());
            body.AppendLine("舰队生存倍率：" + preview.FleetSurvivalFactor.ToGameString());
            body.AppendLine("推进速度：" + preview.ProgressPerMinute.ToGameString() + "/分钟");
            body.AppendLine("预计伤亡：" + preview.CasualtiesPerMinute.ToGameString() + "/分钟");
            body.AppendLine("食物补给：" + preview.FoodCostPerMinute.ToGameString() + "/分钟");
            body.AppendLine("战略资源补给：" + FormatResourceCosts(preview.ResourceCostsPerMinute) + "/分钟");
            body.AppendLine(preview.HasSupply ? "当前补给：足够" : "当前补给：不足");
            body.AppendLine("提示：远星战役是长期后勤行动，建议先完成舰队、燃料和先进材料准备。");
        }

        ShowDetails(definition.Label, body.ToString(), definition.Id);
    }

    private static string GetSectorStateText(SectorManager manager, SectorDefinition definition)
    {
        if (manager == null)
            return "未初始化";
        SectorState state = manager.GetState(definition);
        if (state.Occupied)
            return "已占据";
        if (state.Unlocked)
            return state.CampaignProgress > ExpantaNum.Zero ? "进行中" : "已解锁";
        return "未解锁";
    }

    private static string FormatResourceCosts(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        if (costs == null || costs.Count == 0)
            return "无";
        StringBuilder result = new StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0)
                result.Append("、");
            Resource resource = costs[i].First;
            result.Append(resource == null ? "未知资源" : resource.Label).Append(" ").Append(costs[i].Second.ToGameString());
        }
        return result.ToString();
    }
}
