using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kingdom.EconomySimulation;

public static class UnityAssetSnapshotReader
{
    private sealed class Raw
    {
        public string Id = "", Guid = "", Text = "", Path = "";
        public DefinitionKind Kind;
    }

    private static readonly IReadOnlyDictionary<int, SimEffectKind> WorkshopEffects =
        new Dictionary<int, SimEffectKind>
        {
            [0] = SimEffectKind.BuildingProductionMultiplier,
            [1] = SimEffectKind.BuildingFoodProductionMultiplier,
            [2] = SimEffectKind.ResourceProductionMultiplier,
            [3] = SimEffectKind.GlobalResearchMultiplier,
            [4] = SimEffectKind.GlobalConstructionMultiplier,
            [5] = SimEffectKind.TerritoryGranted,
            [6] = SimEffectKind.MilitaryMultiplier,
            [7] = SimEffectKind.PowerMultiplier,
            [8] = SimEffectKind.GlobalBuildingProductionMultiplier,
            [9] = SimEffectKind.BuildingResearchPowerMultiplier,
            [10] = SimEffectKind.BuildingPowerProductionMultiplier,
            [11] = SimEffectKind.BuildingLogisticsProductionMultiplier,
            [12] = SimEffectKind.GlobalLogisticsMultiplier
        };

    public static EconomySnapshot Read(string repositoryRoot)
    {
        string data = Path.Combine(repositoryRoot, "Kingdom", "Assets", "Resources", "Datas");
        var raw = new List<Raw>();
        foreach ((DefinitionKind kind, string folder) in new[]
        {
            (DefinitionKind.Resource, "Resource"),
            (DefinitionKind.Building, "Building"),
            (DefinitionKind.Research, "Research"),
            (DefinitionKind.Workshop, "Workshop")
        })
        {
            string directory = Path.Combine(data, folder);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Definition directory is missing: {directory}");
            foreach (string path in Directory.EnumerateFiles(
                         directory, "*.asset", SearchOption.AllDirectories))
                raw.Add(ReadRaw(path, kind));
        }

        foreach (IGrouping<DefinitionKind, Raw> group in raw.GroupBy(x => x.Kind))
            EnsureUnique(group, x => x.Id, $"{group.Key} stable ID");
        EnsureUnique(raw, x => x.Guid, "GUID");
        Dictionary<string, string> guidToId = raw.ToDictionary(
            x => x.Guid, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var parsed = new List<Definition>(raw.Count);
        bool trace = Environment.GetEnvironmentVariable("KINGDOM_SIM_TRACE_INPUT") == "1";
        foreach (Raw item in raw)
        {
            if (trace)
                Console.Error.WriteLine("Reading " + item.Path);
            parsed.Add(Parse(item, guidToId));
        }
        return new EconomySnapshot(parsed);
    }

    private static Raw ReadRaw(string path, DefinitionKind kind)
    {
        string text = File.ReadAllText(path);
        Match id = Regex.Match(text, @"(?m)^\s*id:\s*(\S+)\s*$");
        string metaPath = path + ".meta";
        if (!id.Success)
            throw new InvalidDataException($"Definition has no stable ID: {path}");
        if (!File.Exists(metaPath))
            throw new InvalidDataException($"Definition has no meta file: {path}");
        Match guid = Regex.Match(File.ReadAllText(metaPath), @"(?m)^guid:\s*(\S+)");
        if (!guid.Success)
            throw new InvalidDataException($"Definition meta has no GUID: {metaPath}");
        return new Raw
        {
            Id = id.Groups[1].Value,
            Guid = guid.Groups[1].Value,
            Text = text,
            Path = path,
            Kind = kind
        };
    }

    private static void EnsureUnique(
        IEnumerable<Raw> definitions,
        Func<Raw, string> selector,
        string label)
    {
        string duplicate = definitions.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1)?.Key ?? "";
        if (!string.IsNullOrEmpty(duplicate))
            throw new InvalidDataException($"Duplicate {label}: {duplicate}");
    }

    private static Definition Parse(Raw raw, IReadOnlyDictionary<string, string> guidToId)
    {
        var definition = new Definition
        {
            Id = raw.Id,
            Kind = raw.Kind,
            UpgradeTo = Reference(raw.Text, "upgradeTo", guidToId, raw.Path, false),
            TechLevel = (SimTechLevel)Math.Clamp((int)Number(raw.Text, "TechLevel", 0), 0, 6),
            AdvancesTechLevel = Number(raw.Text, "AdvancesTechLevel", 0) > .5,
            BaseCost = Number(raw.Text, "BaseCost", 0),
            CostGrowth = Number(raw.Text, "costGrowth", 1.15),
            SpaceCost = Number(raw.Text, "spaceCost", 0),
            ResearchPower = Number(raw.Text, "researchPowerGranted", 0),
            FoodProduction = Number(raw.Text, "foodProductionRate", 0),
            FoodConsumption = Number(raw.Text, "foodConsumptionRate", 0),
            PowerProduction = Number(raw.Text, "powerProductionRate", 0),
            PowerConsumption = Number(raw.Text, "powerConsumptionRate", 0),
            LogisticsProduction = Number(raw.Text, "logisticsProductionRate", 0),
            LogisticsConsumption = Number(raw.Text, "logisticsConsumptionRate", 0),
            ProductivityConsumption = Number(raw.Text, "productivityConsumption", 0),
            ProductivityGranted = Number(raw.Text, "productivityGranted", 0),
            PopulationCapacity = Number(raw.Text, "populationCapacityGranted", 0),
            FoodCapacity = Number(raw.Text, "foodCapacityGranted", 0)
        };
        References(Section(raw.Text, "prerequisites"), guidToId,
            definition.Prerequisites, raw.Path);
        References(Section(raw.Text, "requiredResearch"), guidToId,
            definition.RequiredResearch, raw.Path);
        References(Section(raw.Text, "requiredWorkshopUpgrades"), guidToId,
            definition.RequiredWorkshop, raw.Path);
        References(Section(raw.Text, "requiredUpgrades"), guidToId,
            definition.RequiredUpgrades, raw.Path);
        Pairs(Section(raw.Text, "resourceRequirements"), guidToId,
            definition.ResourceRequirements, raw.Path);
        Pairs(Section(raw.Text, "resourceGenerationRates"), guidToId,
            definition.Generation, raw.Path);
        Pairs(Section(raw.Text, "resourceConsumptionRates"), guidToId,
            definition.Consumption, raw.Path);
        Effects(Section(raw.Text, "effects"), raw.Kind, guidToId,
            definition.Effects, raw.Path);
        return definition;
    }

    private static string Section(string text, string field)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        string header = "  " + field + ":";
        int start = Array.FindIndex(lines, x => x.StartsWith(
            header, StringComparison.Ordinal));
        if (start < 0 || lines[start].Length > header.Length &&
            !string.IsNullOrWhiteSpace(lines[start][header.Length..]))
            return "";
        int end = start + 1;
        while (end < lines.Length)
        {
            string line = lines[end];
            if (line.Length >= 3 && line[0] == ' ' && line[1] == ' ' &&
                line[2] != ' ' && line[2] != '-' &&
                Regex.IsMatch(line, @"^  [A-Za-z][A-Za-z0-9_]*:"))
                break;
            end++;
        }
        return string.Join("\n", lines[(start + 1)..end]);
    }

    private static double Number(string text, string field, double fallback)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        string prefix = "  " + field + ":";
        string? direct = lines.FirstOrDefault(x => x.StartsWith(
            prefix, StringComparison.Ordinal));
        string valueText = direct == null ? "" : direct[prefix.Length..].Trim();
        if (double.TryParse(valueText,
                NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return value;
        Match scalar = Regex.Match(Section(text, field),
            @"(?m)^\s*scalar:\s*([-+0-9.eE]+)");
        return scalar.Success && double.TryParse(scalar.Groups[1].Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            ? value
            : fallback;
    }

    private static void References(
        string section,
        IReadOnlyDictionary<string, string> guidToId,
        ICollection<string> destination,
        string path)
    {
        foreach (Match match in Regex.Matches(
                     section, @"guid:\s*([0-9a-f]+)", RegexOptions.IgnoreCase))
            destination.Add(Resolve(match.Groups[1].Value, guidToId, path));
    }

    private static string Reference(
        string text,
        string field,
        IReadOnlyDictionary<string, string> guidToId,
        string path,
        bool required)
    {
        Match match = Regex.Match(text,
            @"(?m)^\s{2}" + Regex.Escape(field) +
            @":\s*\{[^}]*guid:\s*([0-9a-f]+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            if (required)
                throw new InvalidDataException($"Missing {field} reference in {path}");
            return "";
        }
        return Resolve(match.Groups[1].Value, guidToId, path);
    }

    private static void Pairs(
        string section,
        IReadOnlyDictionary<string, string> guidToId,
        IDictionary<string, double> destination,
        string path)
    {
        foreach (Match match in Regex.Matches(section,
                     @"- first:.*?guid:\s*([0-9a-f]+).*?scalar:\s*([-+0-9.eE]+)",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            string id = Resolve(match.Groups[1].Value, guidToId, path);
            if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double value))
                throw new InvalidDataException($"Invalid numeric value for {id} in {path}");
            if (!destination.TryAdd(id, value))
                throw new InvalidDataException($"Duplicate resource {id} in {path}");
        }
    }

    private static void Effects(
        string section,
        DefinitionKind ownerKind,
        IReadOnlyDictionary<string, string> guidToId,
        ICollection<SimEffect> destination,
        string path)
    {
        foreach (Match match in Regex.Matches(section,
                     @"- Type:\s*(\d+)(.*?)(?=\r?\n\s*- Type:|\z)",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            int serializedType = int.Parse(match.Groups[1].Value,
                CultureInfo.InvariantCulture);
            SimEffectKind kind = ownerKind == DefinitionKind.Workshop
                ? WorkshopEffects.TryGetValue(serializedType, out SimEffectKind mapped)
                    ? mapped
                    : throw new InvalidDataException(
                        $"Unknown Workshop effect type {serializedType} in {path}")
                : Enum.IsDefined(typeof(SimEffectKind), serializedType)
                    ? (SimEffectKind)serializedType
                    : throw new InvalidDataException(
                        $"Unknown Research effect type {serializedType} in {path}");
            string body = match.Groups[2].Value;
            Match valueMatch = Regex.Match(body,
                @"(?m)^\s*scalar:\s*([-+0-9.eE]+)");
            var effect = new SimEffect
            {
                Kind = kind,
                Value = valueMatch.Success
                    ? double.Parse(valueMatch.Groups[1].Value,
                        CultureInfo.InvariantCulture)
                    : 1d,
                SystemId = Regex.Match(body,
                    @"(?m)^\s*SystemId:\s*(.*?)\s*$").Groups[1].Value
            };
            Match building = Regex.Match(body,
                @"Building:\s*\{[^}]*guid:\s*([0-9a-f]+)",
                RegexOptions.IgnoreCase);
            Match resource = Regex.Match(body,
                @"Resource:\s*\{[^}]*guid:\s*([0-9a-f]+)",
                RegexOptions.IgnoreCase);
            if (building.Success)
                effect.Target = Resolve(building.Groups[1].Value, guidToId, path);
            else if (resource.Success)
                effect.Target = Resolve(resource.Groups[1].Value, guidToId, path);
            destination.Add(effect);
        }
    }

    private static string Resolve(
        string guid,
        IReadOnlyDictionary<string, string> guidToId,
        string path) => guidToId.TryGetValue(guid, out string? id)
        ? id
        : throw new InvalidDataException($"Unresolved GUID {guid} in {path}");
}
