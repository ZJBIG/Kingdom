using System.Text;

public sealed partial class KingdomUIRoot
{
    private OfflineProgressSummary renderedOfflineSummary;
    private string offlineSummaryText;

    private void AppendOfflineSummary(StringBuilder body)
    {
        if (!SaveManager.TryGetInstance(out SaveManager save) || save.LastOfflineSummary == null)
            return;
        OfflineProgressSummary summary = save.LastOfflineSummary;
        if (!ReferenceEquals(renderedOfflineSummary, summary))
        {
            renderedOfflineSummary = summary;
            var text = new StringBuilder("\n\n离线结算摘要");
            text.Append("\n离开时长：").Append(summary.WallClockSeconds.ToString("0.##"))
                .Append(" 秒；结算跨度：").Append(summary.SettledSeconds.ToString("0.##"))
                .Append(" 秒（含分段折减）");
            if (summary.WallClockSeconds > summary.SettledSeconds)
                text.Append("；超过离线上限的时段未结算");
            AppendOfflineDifference(text, "食物", summary.FoodBefore, summary.FoodAfter);
            if (summary.FoodAtCapacity) text.Append("（结算结束时已满）");
            AppendOfflineDifference(text, "人口", summary.PopulationBefore, summary.PopulationAfter);
            int count = System.Math.Min(5, summary.Resources.Count);
            for (int i = 0; i < count; i++)
            {
                OfflineResourceChange change = summary.Resources[i];
                AppendOfflineDifference(text, change.Resource.Label, change.Before, change.After);
            }
            if (summary.Resources.Count > count)
                text.Append("\n另有 ").Append(summary.Resources.Count - count).Append(" 项资源发生实际变化");
            foreach (Research research in summary.CompletedResearches)
                text.Append("\n完成研究：").Append(research.Label);
            foreach (SectorDefinition sector in summary.OccupiedSectors)
                text.Append("\n新增据点：").Append(sector.Label);
            if (summary.StageBefore != summary.StageAfter || summary.StageProgressBefore != summary.StageProgressAfter ||
                summary.ProjectStatusBefore != summary.ProjectStatusAfter)
                text.Append("\n文明工程：").Append(GetUltraStageLabel(summary.StageBefore))
                    .Append(' ').Append((summary.StageProgressBefore * new ExpantaNum(100)).ToGameString())
                    .Append("% → ").Append(GetUltraStageLabel(summary.StageAfter)).Append(' ')
                    .Append((summary.StageProgressAfter * new ExpantaNum(100)).ToGameString()).Append("%（")
                    .Append(GetUltraStatusLabel(summary.ProjectStatusAfter)).Append('）');
            if (summary.WaitingResearch != null)
                text.Append("\n结算结束时：").Append(summary.WaitingResearch.Label).Append("等待研究材料");
            if (summary.ResearchPowerBlocked)
                text.Append("\n结算结束时：研究力不足");
            foreach (SectorDefinition sector in summary.SupplyBlockedSectors)
                text.Append("\n结算结束时：").Append(sector.Label).Append("供给不足");
            if (summary.ProjectPauseReason != UltraProjectPauseReason.None)
                text.Append("\n结算结束时：文明工程")
                    .Append(summary.ProjectPauseReason == UltraProjectPauseReason.Manual ? "手动暂停" :
                        summary.ProjectPauseReason == UltraProjectPauseReason.InsufficientSupply ? "供给不足而暂停" : "定义缺失而暂停");
            offlineSummaryText = text.ToString();
        }
        body.Append(offlineSummaryText);
    }

    private static void AppendOfflineDifference(StringBuilder text, string label, ExpantaNum before, ExpantaNum after)
    {
        ExpantaNum change = after - before;
        text.Append('\n').Append(label).Append("：").Append(before.ToGameString()).Append(" → ")
            .Append(after.ToGameString()).Append("（")
            .Append(change > ExpantaNum.Zero ? "+" : string.Empty).Append(change.ToGameString()).Append('）');
    }
}
