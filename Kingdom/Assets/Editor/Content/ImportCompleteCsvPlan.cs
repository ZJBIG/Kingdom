using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ImportCompleteCsvPlan
{
    private const string Root = @"C:\Users\19603\Downloads\Kingdom_全时代精简_完整CSV策划案";
    private sealed class Row : Dictionary<string, string> { public string G(string key) => TryGetValue(key, out var v) ? v : ""; }
    private static double N(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0d;
    private static string Id(string s) { var p = s.IndexOf('（'); return (p >= 0 ? s.Substring(0, p) : s).Trim(); }
    private static IEnumerable<string> Ids(string s) => string.IsNullOrWhiteSpace(s) ? Enumerable.Empty<string>() : s.Split(new[] { '；', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(Id);

    [MenuItem("Codex/Content/Import Complete CSV Plan")]
    public static void Import()
    {
        var res = Load<Resource>(); var research = Load<Research>(); var buildings = Load<Building>(); var workshops = Load<WorkshopUpgradeDefinition>();
        var rRows = Read("03_资源完整表.csv");
        var researchRows = Read("02_研究完整总表.csv"); var rp = Group("02A_研究_前置研究.csv", "研究ID"); var rr = Group("02B_研究_资源需求.csv", "研究ID"); var re = Group("02C_研究_效果.csv", "研究ID");
        var buildingRows = Read("01_建筑完整总表.csv"); var bc = Group("01A_建筑_建造资源需求.csv", "建筑ID"); var bg = Group("01B_建筑_每周期生成.csv", "建筑ID"); var bu = Group("01C_建筑_每周期消耗.csv", "建筑ID"); var br = Group("01D_建筑_前置研究.csv", "建筑ID"); var bw = Group("01E_建筑_前置工坊器件.csv", "建筑ID"); var bx = Group("01F_建筑_升级链.csv", "建筑ID");
        var workshopRows = Read("04_工坊器件完整总表.csv"); var wr = Group("04A_工坊器件_前置研究.csv", "工坊器件ID"); var wx = Group("04B_工坊器件_前置器件.csv", "工坊器件ID"); var wc = Group("04C_工坊器件_制造资源需求.csv", "工坊器件ID"); var we = Group("04D_工坊器件_效果.csv", "工坊器件ID");

        foreach (var x in rRows) ApplyResource(x, res);
        foreach (var x in researchRows) ApplyResearch(x, research, res, buildings, rp, rr, re);
        foreach (var x in buildingRows) ApplyBuilding(x, buildings, research, res, workshops, bc, bg, bu, br, bw, bx);
        foreach (var x in workshopRows) ApplyWorkshop(x, workshops, research, res, buildings, wr, wx, wc, we);
        ApplyClosureRepairs(research, buildings, res);
        DeleteOutside<Resource>(rRows, res.Keys, "ID"); DeleteOutside<Research>(researchRows, research.Keys, "ID"); DeleteOutside<Building>(buildingRows, buildings.Keys, "ID"); DeleteOutside<WorkshopUpgradeDefinition>(workshopRows, workshops.Keys, "ID");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ValidateCounts(rRows, researchRows, buildingRows, workshopRows);
        Debug.Log("Complete CSV plan imported successfully.");
    }

    [MenuItem("Codex/Content/Repair Complete CSV Closure")]
    public static void RepairClosureOnly()
    {
        var steelmaking = AssetDatabase.LoadAssetAtPath<Research>("Assets/Resources/Datas/Research/Medieval/Steelmaking.asset");
        var feudal = AssetDatabase.LoadAssetAtPath<Research>("Assets/Resources/Datas/Research/Medieval/FeudalAdministration.asset");
        if (steelmaking != null) steelmaking.SetResourceRequirementsForEditor(steelmaking.ResourceRequirements.Where(x => x.First != null && !AssetDatabase.GetAssetPath(x.First).Replace('\\', '/').EndsWith("/Steel.asset", StringComparison.OrdinalIgnoreCase)).ToList());
        if (feudal != null) feudal.AdvancesTechLevel = true;
        if (steelmaking != null) EditorUtility.SetDirty(steelmaking); if (feudal != null) EditorUtility.SetDirty(feudal); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); Debug.Log("Complete CSV closure repairs saved.");
    }

    private static List<Row> Read(string file)
    {
        var path = Path.Combine(Root, file); if (!File.Exists(path)) throw new FileNotFoundException(path);
        var lines = File.ReadAllLines(path, new UTF8Encoding(false)); if (lines.Length == 0) return new List<Row>();
        var headers = ParseLine(lines[0]); var result = new List<Row>();
        for (var i = 1; i < lines.Length; i++) { if (string.IsNullOrWhiteSpace(lines[i])) continue; var values = ParseLine(lines[i]); var row = new Row(); for (var j = 0; j < headers.Count; j++) row[headers[j]] = j < values.Count ? values[j].Trim() : ""; result.Add(row); }
        return result;
    }
    private static List<string> ParseLine(string line)
    {
        var result = new List<string>(); var sb = new StringBuilder(); var quote = false;
        for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"') { if (quote && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else quote = !quote; } else if (c == ',' && !quote) { result.Add(sb.ToString()); sb.Clear(); } else sb.Append(c); }
        result.Add(sb.ToString()); return result;
    }
    private static Dictionary<string, List<Row>> Group(string file, string key) => Read(file).GroupBy(x => x.G(key)).ToDictionary(x => x.Key, x => x.ToList());
    private static List<Row> Get(Dictionary<string, List<Row>> map, string id) => map.TryGetValue(id, out var rows) ? rows : new List<Row>();
    private static Dictionary<string, T> Load<T>() where T : GameDefinition => AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First());
    private static List<Pair<Resource, ExpantaNum>> Pairs(IEnumerable<Row> rows, string idKey, string amountKey, Dictionary<string, Resource> res) => rows.Where(x => res.ContainsKey(x.G(idKey))).Select(x => new Pair<Resource, ExpantaNum>(res[x.G(idKey)], new ExpantaNum(N(x.G(amountKey))))).ToList();
    private static List<Research> ResearchRefs(IEnumerable<Row> rows, string key, Dictionary<string, Research> all) => rows.Where(x => all.ContainsKey(x.G(key))).Select(x => all[x.G(key)]).ToList();
    private static List<WorkshopUpgradeDefinition> WorkshopRefs(IEnumerable<Row> rows, string key, Dictionary<string, WorkshopUpgradeDefinition> all) => rows.Where(x => all.ContainsKey(x.G(key))).Select(x => all[x.G(key)]).ToList();

    private static void ApplyResource(Row x, Dictionary<string, Resource> all)
    {
        if (!all.TryGetValue(x.G("ID"), out var a)) return; a.Label = x.G("Label"); a.Description = x.G("Description"); a.Color = new Color((float)N(x.G("Color_r")), (float)N(x.G("Color_g")), (float)N(x.G("Color_b")), (float)N(x.G("Color_a"))); EditorUtility.SetDirty(a);
    }
    private static void ApplyResearch(Row x, Dictionary<string, Research> all, Dictionary<string, Resource> res, Dictionary<string, Building> buildings, Dictionary<string, List<Row>> pre, Dictionary<string, List<Row>> costs, Dictionary<string, List<Row>> effects)
    {
        if (!all.TryGetValue(x.G("ID"), out var a)) return; a.Label = x.G("Label"); a.Description = x.G("Description"); a.BaseCost = x.G("BaseCost"); a.TechLevel = (TechLevel)(int)N(x.G("TechLevel")); a.AdvancesTechLevel = N(x.G("AdvancesTechLevel")) != 0;
        a.SetPrerequisitesForEditor(ResearchRefs(Get(pre, a.Id), "前置研究ID", all)); a.SetResourceRequirementsForEditor(Pairs(Get(costs, a.Id), "资源ID", "数量", res));
        var list = new List<ResearchEffectDefinition>(); foreach (var e in Get(effects, a.Id)) { var z = new ResearchEffectDefinition { Type = (ResearchEffectType)(int)N(e.G("Type")), Value = new ExpantaNum(N(e.G("Value数值"))) }; if (buildings.TryGetValue(e.G("目标建筑ID"), out var b)) z.Building = b; if (res.TryGetValue(e.G("目标资源ID"), out var r)) z.Resource = r; list.Add(z); } a.SetEffectsForEditor(list); EditorUtility.SetDirty(a);
    }
    private static void ApplyBuilding(Row x, Dictionary<string, Building> all, Dictionary<string, Research> research, Dictionary<string, Resource> res, Dictionary<string, WorkshopUpgradeDefinition> workshops, Dictionary<string, List<Row>> costs, Dictionary<string, List<Row>> generated, Dictionary<string, List<Row>> consumed, Dictionary<string, List<Row>> prereq, Dictionary<string, List<Row>> workshopPrereq, Dictionary<string, List<Row>> upgrades)
    {
        if (!all.TryGetValue(x.G("ID"), out var a)) return; a.Label = x.G("Label"); a.Description = x.G("Description"); a.TechLevel = (TechLevel)(int)N(x.G("TechLevel"));
        a.ConfigureEconomyForEditor(new ExpantaNum(N(x.G("costGrowth_数值"))), new ExpantaNum(N(x.G("spaceCost_数值"))), new ExpantaNum(N(x.G("productivityConsumption_数值"))), new ExpantaNum(N(x.G("productivityGranted_数值"))), new ExpantaNum(N(x.G("populationCapacityGranted_数值"))), new ExpantaNum(N(x.G("researchPowerGranted_数值"))), new ExpantaNum(N(x.G("foodProductionRate_数值"))), new ExpantaNum(N(x.G("foodConsumptionRate_数值"))), new ExpantaNum(N(x.G("foodCapacityGranted_数值"))), new ExpantaNum(N(x.G("powerProductionRate_数值"))), new ExpantaNum(N(x.G("powerConsumptionRate_数值"))), new ExpantaNum(N(x.G("logisticsProductionRate_数值"))), new ExpantaNum(N(x.G("logisticsConsumptionRate_数值"))), new ExpantaNum(N(x.G("attackPowerGranted_数值"))), new ExpantaNum(N(x.G("defensePowerGranted_数值"))), new ExpantaNum(N(x.G("militaryManpowerGranted_数值"))), Pairs(Get(costs, a.Id), "资源ID", "数量", res), Pairs(Get(generated, a.Id), "资源ID", "数量", res), Pairs(Get(consumed, a.Id), "资源ID", "数量", res));
        a.SetRequiredResearchForEditor(ResearchRefs(Get(prereq, a.Id), "研究ID", research)); a.SetRequiredWorkshopUpgradesForEditor(WorkshopRefs(Get(workshopPrereq, a.Id), "工坊器件ID", workshops)); var up = Get(upgrades, a.Id).FirstOrDefault(); if (up != null && all.TryGetValue(up.G("升级目标ID"), out var target)) a.SetUpgradeToForEditor(target); EditorUtility.SetDirty(a);
    }
    private static void ApplyWorkshop(Row x, Dictionary<string, WorkshopUpgradeDefinition> all, Dictionary<string, Research> research, Dictionary<string, Resource> res, Dictionary<string, Building> buildings, Dictionary<string, List<Row>> prereq, Dictionary<string, List<Row>> upgradePrereq, Dictionary<string, List<Row>> costs, Dictionary<string, List<Row>> effects)
    {
        if (!all.TryGetValue(x.G("ID"), out var a)) return; var es = new List<WorkshopEffectDefinition>(); foreach (var e in Get(effects, a.Id)) { var z = new WorkshopEffectDefinition { Type = (WorkshopEffectType)(int)N(e.G("Type")), Value = new ExpantaNum(N(e.G("Value数值"))) }; if (buildings.TryGetValue(e.G("目标建筑ID"), out var b)) z.Building = b; if (res.TryGetValue(e.G("目标资源ID"), out var r)) z.Resource = r; es.Add(z); } a.Label = x.G("Label"); a.Description = x.G("Description"); a.SortOrder = (int)N(x.G("SortOrder")); a.TechLevel = (TechLevel)(int)N(x.G("TechLevel")); a.ConfigureForEditor(ResearchRefs(Get(prereq, a.Id), "研究ID", research), WorkshopRefs(Get(upgradePrereq, a.Id), "前置器件ID", all), Pairs(Get(costs, a.Id), "资源ID", "数量", res), es); EditorUtility.SetDirty(a);
    }
    private static void ApplyClosureRepairs(Dictionary<string, Research> research, Dictionary<string, Building> buildings, Dictionary<string, Resource> res)
    {
        if (research.TryGetValue("Steelmaking", out var steelmaking) && res.TryGetValue("Steel", out var steel))
            steelmaking.SetResourceRequirementsForEditor(steelmaking.ResourceRequirements.Where(x => x.First != null && !AssetDatabase.GetAssetPath(x.First).Replace('\\', '/').EndsWith("/Steel.asset", StringComparison.OrdinalIgnoreCase)).ToList());
        if (research.ContainsKey("Steelmaking")) EditorUtility.SetDirty(research["Steelmaking"]);
        if (research.ContainsKey("FeudalAdministration")) EditorUtility.SetDirty(research["FeudalAdministration"]);
    }
    private static void DeleteOutside<T>(List<Row> rows, ICollection<string> loaded, string key) where T : GameDefinition { var keep = new HashSet<string>(rows.Select(x => x.G(key))); foreach (var id in loaded.Where(x => !keep.Contains(x)).ToList()) foreach (var path in AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Where(p => AssetDatabase.LoadAssetAtPath<T>(p)?.Id == id)) AssetDatabase.DeleteAsset(path); }
    private static void ValidateCounts(List<Row> r, List<Row> research, List<Row> buildings, List<Row> workshops) { if (r.Count != 33 || research.Count != 72 || buildings.Count != 66 || workshops.Count != 24) throw new InvalidDataException($"Unexpected final counts: {r.Count}/{research.Count}/{buildings.Count}/{workshops.Count}"); }
}
