using System.Text;

public sealed partial class KingdomUIRoot
{
    private OfflineProgressSummary renderedOfflineSummary;
    private string offlineSummaryText;
    private bool offlineSummaryExpanded;

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
                .Append(" 秒；折减后经济时长：").Append(summary.EffectiveEconomySeconds.ToString("0.##")).Append(" 秒");
            if (summary.WallClockSeconds > summary.SettledSeconds)
                text.Append("；超过离线上限的时段未结算");
            AppendOfflineDifference(text, "食物", summary.FoodBefore, summary.FoodAfter);
            if (summary.FoodAtCapacity) text.Append("（结算结束时已满）");
            AppendOfflineDifference(text, "人口", summary.PopulationBefore, summary.PopulationAfter);
            int count = offlineSummaryExpanded ? summary.Resources.Count : System.Math.Min(5, summary.Resources.Count);
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
                text.Append("\n结算结束时：").Append(ReviewLink("research", summary.WaitingResearch.Id, summary.WaitingResearch.Label)).Append("等待研究材料");
            if (summary.ResearchPowerBlocked)
                text.Append("\n结算结束时：科研当前无法推进，检查供粮与有效科研速度");
            foreach (SectorDefinition sector in summary.SupplyBlockedSectors)
                text.Append("\n结算结束时：").Append(ReviewLink("sector", sector.Id, sector.Label)).Append("供给不足");
            if (summary.ProjectPauseReason != UltraProjectPauseReason.None)
                text.Append("\n结算结束时：文明工程")
                    .Append(summary.ProjectPauseReason == UltraProjectPauseReason.Manual ? "手动暂停" :
                        summary.ProjectPauseReason == UltraProjectPauseReason.InsufficientSupply ? "供给不足而暂停" : "定义缺失而暂停");
            if (summary.RelicStatusBefore != summary.RelicStatusAfter || summary.RelicProgressBefore != summary.RelicProgressAfter ||
                summary.RelicCommissionActiveBefore != summary.RelicCommissionActiveAfter ||
                summary.RelicSupportReadyBefore != summary.RelicSupportReadyAfter ||
                summary.RelicCompletedCommissionsBefore != summary.RelicCompletedCommissionsAfter ||
                summary.RelicSupportedSectorIdBefore != summary.RelicSupportedSectorIdAfter)
            {
                text.Append("\n遗迹工作：").Append(OfflineRelicStatusLabel(summary.RelicStatusBefore)).Append(" → ").Append(OfflineRelicStatusLabel(summary.RelicStatusAfter))
                    .Append("；进度 ").Append((summary.RelicProgressBefore * 100).ToGameString()).Append("% → ")
                    .Append((summary.RelicProgressAfter * 100).ToGameString()).Append("%；支援制备完成 ")
                    .Append(summary.RelicCompletedCommissionsAfter - summary.RelicCompletedCommissionsBefore).Append(" 份")
                    .Append(summary.RelicSupportReadyAfter ? "；现有待用支援" : string.Empty);
                if (summary.RelicSupportedSectorIdBefore != summary.RelicSupportedSectorIdAfter)
                    text.Append("\n遗迹支援目标：").Append(OfflineSupportTargetLabel(summary.RelicSupportedSectorIdBefore)).Append(" → ").Append(OfflineSupportTargetLabel(summary.RelicSupportedSectorIdAfter));
            }
            if (summary.RelicPauseReasonAfter != RelicPauseReason.None)
            {
                SectorDefinition site = GameManager.Instance.Relic.Definition?.Sector;
                text.Append("\n结算结束时：").Append(site == null ? "遗迹工作" : ReviewLink("sector", site.Id, "遗迹工作"))
                    .Append(summary.RelicPauseReasonAfter == RelicPauseReason.Manual ? "手动暂停" : "供给不足而暂停");
            }
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

    private static string OfflineRelicStatusLabel(RelicStatus status) => status switch
    {
        RelicStatus.Investigating => "调查中", RelicStatus.AwaitingChoice => "待选路线",
        RelicStatus.Repairing => "修复认证", RelicStatus.ReverseEngineering => "自主认证",
        RelicStatus.Operational => "认证完成", _ => "已发现"
    };

    private static string OfflineSupportTargetLabel(string id) => string.IsNullOrEmpty(id) ? "无服役目标" :
        DataBase<SectorDefinition>.TryFind(id, out SectorDefinition sector) ? sector.Label : "当前远星战役";
}
