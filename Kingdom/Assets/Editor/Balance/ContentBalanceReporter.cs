using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    public static class ContentBalanceReporter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly double[] Horizons = { 600d, 3600d, 14400d, 43200d, 86400d };
        private static readonly HashSet<string> ReleasedResourceIds =
            new HashSet<string>(new[]
            {
                "WoodLog", "StoneChunk", "StoneBrick", "Clay", "PlantFiber", "Pottery",
                "Cloth", "Coal", "CopperOre", "Copper", "TinOre", "Tin", "IronOre",
                "Iron", "Bronze", "Steel", "Chemical", "Machinery", "Electronics",
                "CrudeOil", "Silica", "Coke", "Glass", "IndustrialCeramic",
                "RefinedFuel", "Lubricant", "Rubber", "CopperWire", "PrecisionParts", "Engine"
            }, StringComparer.Ordinal);

        [MenuItem("Tools/Kingdom/Balance/Export Current Snapshot And Pacing")]
        public static void Export()
        {
            string repositoryRoot = Directory.GetParent(
                Directory.GetParent(Application.dataPath).FullName).FullName;
            string dataRoot = Path.Combine(repositoryRoot, "data");
            string reportRoot = Path.Combine(repositoryRoot, "TestResults", "C10-28", "balance");
            Directory.CreateDirectory(dataRoot);
            Directory.CreateDirectory(reportRoot);

            List<Resource> resources = LoadAll<Resource>();
            List<Resource> allResourceAssets = LoadAllAssets<Resource>();
            List<Building> buildings = LoadAll<Building>();
            List<Research> researches = LoadAll<Research>();
            List<WorkshopUpgradeDefinition> workshops = LoadAll<WorkshopUpgradeDefinition>();

            WriteResources(Path.Combine(dataRoot, "current_resources.csv"), resources);
            WriteResourceInventory(
                Path.Combine(dataRoot, "current_resource_asset_inventory.csv"),
                allResourceAssets);
            WriteBuildings(Path.Combine(dataRoot, "current_buildings.csv"), buildings);
            WriteResearches(Path.Combine(dataRoot, "current_researches.csv"), researches);
            WriteWorkshops(Path.Combine(dataRoot, "current_workshop_upgrades.csv"), workshops);
            WriteSourceSink(
                Path.Combine(dataRoot, "current_resource_source_sink_audit.csv"),
                resources,
                buildings,
                researches,
                workshops);
            WritePacing(
                Path.Combine(reportRoot, "vertical-slice-pacing-after.csv"),
                researches);
            WritePayback(
                Path.Combine(reportRoot, "building-first-copy-payback-after.csv"),
                buildings);
            DeterministicBalanceSimulator.Export(
                reportRoot, resources, buildings, researches, workshops);

            AssetDatabase.Refresh();
            Debug.Log(
                $"Kingdom content export complete: {allResourceAssets.Count} resource assets, " +
                $"{buildings.Count} buildings, {researches.Count} researches, " +
                $"{workshops.Count} workshop upgrades. Reports: {reportRoot}");
        }

        public static void RunFromCommandLine()
        {
            Export();
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Resources/Datas" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToList();
        }

        private static List<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToList();
        }

        private static void WriteResources(string path, IReadOnlyList<Resource> resources)
        {
            var rows = new List<string>
            {
                Csv("Id", "Label", "DisplaySet", "Description", "Path")
            };
            for (int i = 0; i < resources.Count; i++)
            {
                Resource resource = resources[i];
                if (!ReleasedResourceIds.Contains(resource.Id))
                    continue;
                rows.Add(Csv(
                    resource.Id,
                    resource.Label,
                    resource.DisplayerSet.ToString(),
                    resource.Description,
                    AssetDatabase.GetAssetPath(resource)));
            }
            WriteLines(path, rows);
        }

        private static void WriteResourceInventory(
            string path, IReadOnlyList<Resource> resources)
        {
            var rows = new List<string>
            {
                Csv("Id", "Scope", "Label", "Path")
            };
            for (int i = 0; i < resources.Count; i++)
            {
                Resource resource = resources[i];
                string assetPath = AssetDatabase.GetAssetPath(resource);
                string scope = ReleasedResourceIds.Contains(resource.Id)
                    ? "ReleasedAnimalToIndustrial"
                    : assetPath.StartsWith("Assets/ContentBacklog/", StringComparison.Ordinal)
                        ? "Deferred"
                        : "FrozenSpace";
                rows.Add(Csv(resource.Id, scope, resource.Label, assetPath));
            }
            WriteLines(path, rows);
        }

        private static void WriteBuildings(string path, IReadOnlyList<Building> buildings)
        {
            var rows = new List<string>
            {
                Csv(
                    "Id", "Label", "TechLevel", "CostGrowth",
                    "SpaceCost", "ProductivityCost", "PopulationCapacity", "ResearchPower",
                    "FoodProductionPerSec", "FoodConsumptionPerSec", "FoodCapacity",
                    "PowerProduction", "PowerConsumption", "LogisticsProduction",
                    "LogisticsConsumption", "RequiredResearch", "RequiredWorkshop",
                    "Requirements", "Generation", "Consumption", "Path")
            };
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                rows.Add(Csv(
                    building.Id,
                    building.Label,
                    building.TechLevel.ToString(),
                    Number(building.CostGrowth),
                    Number(building.SpaceCost),
                    Number(building.ProductivityConsumption),
                    Number(building.PopulationCapacityGranted),
                    Number(building.ResearchPowerGranted),
                    Number(building.FoodProductionRate),
                    Number(building.FoodConsumptionRate),
                    Number(building.FoodCapacityGranted),
                    Number(building.PowerProductionRate),
                    Number(building.PowerConsumptionRate),
                    Number(building.LogisticsProductionRate),
                    Number(building.LogisticsConsumptionRate),
                    Ids(building.RequiredResearch),
                    Ids(building.RequiredWorkshopUpgrades),
                    Pairs(building.ResourceRequirements),
                    Pairs(building.ResourceGenerationRates, "/s"),
                    Pairs(building.ResourceConsumptionRates, "/s"),
                    AssetDatabase.GetAssetPath(building)));
            }
            WriteLines(path, rows);
        }

        private static void WriteWorkshops(
            string path, IReadOnlyList<WorkshopUpgradeDefinition> workshops)
        {
            var rows = new List<string>
            {
                Csv("Id", "Label", "Category", "SortOrder", "TechLevel",
                    "RequiredResearch", "RequiredUpgrades", "ResourceRequirements",
                    "Effects", "ReferencedByBuildings", "Path")
            };
            for (int i = 0; i < workshops.Count; i++)
            {
                WorkshopUpgradeDefinition workshop = workshops[i];
                string referencedBy = string.Join("; ", LoadAll<Building>()
                    .Where(building => building.RequiredWorkshopUpgrades.Contains(workshop))
                    .Select(building => building.Id));
                rows.Add(Csv(
                    workshop.Id, workshop.Label, workshop.Category,
                    workshop.SortOrder.ToString(Invariant), workshop.TechLevel.ToString(),
                    Ids(workshop.RequiredResearch), Ids(workshop.RequiredUpgrades),
                    Pairs(workshop.ResourceRequirements),
                    string.Join("; ", workshop.Effects.Select(effect =>
                        $"{effect.Type}:{Number(effect.Value)}")),
                    referencedBy, AssetDatabase.GetAssetPath(workshop)));
            }
            WriteLines(path, rows);
        }

        private static void WriteResearches(string path, IReadOnlyList<Research> researches)
        {
            var rows = new List<string>
            {
                Csv(
                    "Id", "Label", "TechLevel", "BaseCost", "AdvancesTechLevel",
                    "Prerequisites", "ResourceRequirements",
                    "Effects", "Path")
            };
            for (int i = 0; i < researches.Count; i++)
            {
                Research research = researches[i];
                rows.Add(Csv(
                    research.Id,
                    research.Label,
                    research.TechLevel.ToString(),
                    research.BaseCost,
                    research.AdvancesTechLevel.ToString(),
                    Ids(research.Prerequisites),
                    Pairs(research.ResourceRequirements),
                    Effects(research.Effects),
                    AssetDatabase.GetAssetPath(research)));
            }
            WriteLines(path, rows);
        }

        private static void WriteSourceSink(
            string path,
            IReadOnlyList<Resource> resources,
            IReadOnlyList<Building> buildings,
            IReadOnlyList<Research> researches,
            IReadOnlyList<WorkshopUpgradeDefinition> workshops)
        {
            var rows = new List<string>
            {
                Csv("Id", "HasProducer", "BuildingSinks", "ResearchSinks",
                    "WorkshopSinks", "TotalIndependentSinks", "HasStrategicSink")
            };
            for (int i = 0; i < resources.Count; i++)
            {
                Resource resource = resources[i];
                if (!ReleasedResourceIds.Contains(resource.Id))
                    continue;
                int producers = buildings.Count(building =>
                    Contains(building.ResourceGenerationRates, resource));
                int buildingSinks = buildings.Count(building =>
                    Contains(building.ResourceRequirements, resource) ||
                    Contains(building.ResourceConsumptionRates, resource));
                int researchSinks = researches.Count(research =>
                    Contains(research.ResourceRequirements, resource));
                int workshopSinks = workshops.Count(workshop =>
                    Contains(workshop.ResourceRequirements, resource));
                bool strategic = researches.Any(research =>
                    research.AdvancesTechLevel &&
                    Contains(research.ResourceRequirements, resource));
                rows.Add(Csv(
                    resource.Id,
                    (resource.Id == "WoodLog" || producers > 0).ToString(),
                    buildingSinks.ToString(Invariant),
                    researchSinks.ToString(Invariant),
                    workshopSinks.ToString(Invariant),
                    (buildingSinks + researchSinks + workshopSinks).ToString(Invariant),
                    strategic.ToString()));
            }
            WriteLines(path, rows);
        }

        private static void WritePacing(string path, IReadOnlyList<Research> researches)
        {
            var scenarios = new[]
            {
                new Scenario("FastestEra", 2d, 10d, 25d, 500d, 15d),
                new Scenario("Balanced", 1d, 8d, 20d, 250d, 45d),
                new Scenario("LowFrequency", 1d, 5d, 15d, 150d, 300d)
            };
            var rows = new List<string>
            {
                Csv(
                    "Scenario", "HorizonSeconds", "CompletedResearch",
                    "HighestTechLevel", "NeolithicAtSeconds", "MedievalAtSeconds",
                    "IndustrialAtSeconds", "IndustrialCompleteAtSeconds",
                    "LongestDecisionGapSeconds", "UnreachableResearch", "Finite", "Model")
            };

            for (int i = 0; i < scenarios.Length; i++)
            {
                ScenarioResult result = SimulateResearch(researches, scenarios[i], 86400d);
                for (int j = 0; j < Horizons.Length; j++)
                {
                    double horizon = Horizons[j];
                    rows.Add(Csv(
                        scenarios[i].Name,
                        horizon.ToString("0", Invariant),
                        result.CompletedAt.Count(pair => pair.Value <= horizon).ToString(Invariant),
                        HighestTechAt(result, horizon).ToString(),
                        Time(result, "NeolithicSettlement"),
                        Time(result, "SmithingRevolution"),
                        Time(result, "Industrialization"),
                        Time(result, "IndustrialAdministration"),
                        scenarios[i].DecisionInterval.ToString("0.###", Invariant),
                        string.Join("; ", result.Unreachable),
                        result.Finite.ToString(),
                        "Deterministic research graph; resource-chain reachability audited separately"));
                }
            }
            WriteLines(path, rows);
        }

        private static ScenarioResult SimulateResearch(
            IReadOnlyList<Research> researches,
            Scenario scenario,
            double horizon)
        {
            var result = new ScenarioResult();
            var remaining = new HashSet<Research>(researches.Where(
                research => research.TechLevel <= TechLevel.Industrial));
            double elapsed = 0d;
            TechLevel tech = TechLevel.Animal;

            while (remaining.Count > 0 && elapsed <= horizon)
            {
                Research next = remaining
                    .Where(research =>
                        research.TechLevel <= tech ||
                        research.AdvancesTechLevel && research.TechLevel == tech + 1)
                    .Where(research => research.Prerequisites.All(
                        prerequisite => result.CompletedAt.ContainsKey(prerequisite.Id)))
                    .OrderBy(research => research.AdvancesTechLevel ? 0 : 1)
                    .ThenBy(research => research.TechLevel)
                    .ThenBy(research => ParseCost(research))
                    .FirstOrDefault();
                if (next == null)
                    break;

                double power = scenario.PowerFor(tech);
                double duration = ParseCost(next) / Math.Max(1d, power);
                duration = Math.Ceiling(duration / scenario.DecisionInterval)
                    * scenario.DecisionInterval;
                elapsed += duration;
                if (double.IsNaN(elapsed) || double.IsInfinity(elapsed))
                {
                    result.Finite = false;
                    break;
                }

                result.CompletedAt[next.Id] = elapsed;
                remaining.Remove(next);
                if (next.AdvancesTechLevel && next.TechLevel > tech)
                    tech = next.TechLevel;
            }
            result.Unreachable = remaining.Select(research => research.Id).ToList();
            return result;
        }

        private static void WritePayback(string path, IReadOnlyList<Building> buildings)
        {
            var rows = new List<string>
            {
                Csv(
                    "BuildingId", "TechLevel", "CostGrowth", "FirstCopyMaterialUnits",
                    "GrossOutputPerSecond", "NominalPaybackSeconds", "TargetBand", "Status")
            };
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building.TechLevel > TechLevel.Industrial)
                    continue;
                double materialUnits = Sum(building.ResourceRequirements);
                double output = Sum(building.ResourceGenerationRates) +
                    Math.Max(0d, building.FoodProductionRate.ToDouble()) +
                    Math.Max(0d, building.ResearchPowerGranted.ToDouble());
                double payback = output > 0d ? materialUnits / output : double.NaN;
                bool processor = building.ResourceConsumptionRates.Count > 0;
                double minimum = processor ? 90d : 45d;
                double maximum = processor ? 240d : 120d;
                string status = double.IsNaN(payback)
                    ? "Infrastructure/strategic"
                    : payback >= minimum && payback <= maximum ? "InBand" : "Review";
                rows.Add(Csv(
                    building.Id,
                    building.TechLevel.ToString(),
                    Number(building.CostGrowth),
                    materialUnits.ToString("0.###", Invariant),
                    output.ToString("0.###", Invariant),
                    double.IsNaN(payback) ? string.Empty : payback.ToString("0.###", Invariant),
                    $"{minimum:0}-{maximum:0}",
                    status));
            }
            WriteLines(path, rows);
        }

        private static TechLevel HighestTechAt(ScenarioResult result, double horizon)
        {
            if (result.CompletedAt.TryGetValue("Industrialization", out double industrial) &&
                industrial <= horizon)
                return TechLevel.Industrial;
            if (result.CompletedAt.TryGetValue("SmithingRevolution", out double medieval) &&
                medieval <= horizon)
                return TechLevel.Medieval;
            if (result.CompletedAt.TryGetValue("NeolithicSettlement", out double neolithic) &&
                neolithic <= horizon)
                return TechLevel.Neolithic;
            return TechLevel.Animal;
        }

        private static string Time(ScenarioResult result, string id) =>
            result.CompletedAt.TryGetValue(id, out double value)
                ? value.ToString("0.###", Invariant)
                : string.Empty;

        private static double ParseCost(Research research) =>
            ExpantaNum.TryParse(research.BaseCost, out ExpantaNum value)
                ? Math.Max(0d, value.ToDouble())
                : double.PositiveInfinity;

        private static double Sum(IReadOnlyList<Pair<Resource, ExpantaNum>> pairs)
        {
            double result = 0d;
            for (int i = 0; i < pairs.Count; i++)
                result += Math.Max(0d, pairs[i].Second.ToDouble());
            return result;
        }

        private static bool Contains(
            IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
            Resource resource)
        {
            for (int i = 0; i < pairs.Count; i++)
                if (pairs[i].First == resource && pairs[i].Second > ExpantaNum.Zero)
                    return true;
            return false;
        }

        private static string Pairs(
            IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
            string suffix = "")
        {
            var values = new List<string>();
            for (int i = 0; i < pairs.Count; i++)
                if (pairs[i].First != null && pairs[i].Second > ExpantaNum.Zero)
                    values.Add($"{pairs[i].First.Id}:{Number(pairs[i].Second)}{suffix}");
            return string.Join("; ", values);
        }

        private static string Ids<T>(IReadOnlyList<T> values) where T : GameDefinition =>
            values == null
                ? string.Empty
                : string.Join("; ", values.Where(value => value != null).Select(value => value.Id));

        private static string Effects(IReadOnlyList<ResearchEffectDefinition> effects) =>
            effects == null
                ? string.Empty
                : string.Join(
                    "; ",
                    effects.Where(effect => effect != null).Select(effect =>
                        $"{effect.Type}:{Number(effect.Value)}"));

        private static string Number(ExpantaNum value) => value.ToString();

        private static string Csv(params string[] values) =>
            string.Join(",", values.Select(value =>
                "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));

        private static void WriteLines(string path, IReadOnlyList<string> lines)
        {
            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }

        private sealed class Scenario
        {
            public Scenario(
                string name,
                double animalPower,
                double neolithicPower,
                double medievalPower,
                double industrialPower,
                double decisionInterval)
            {
                Name = name;
                AnimalPower = animalPower;
                NeolithicPower = neolithicPower;
                MedievalPower = medievalPower;
                IndustrialPower = industrialPower;
                DecisionInterval = decisionInterval;
            }

            public string Name { get; }
            public double AnimalPower { get; }
            public double NeolithicPower { get; }
            public double MedievalPower { get; }
            public double IndustrialPower { get; }
            public double DecisionInterval { get; }

            public double PowerFor(TechLevel tech)
            {
                if (tech >= TechLevel.Industrial)
                    return IndustrialPower;
                if (tech >= TechLevel.Medieval)
                    return MedievalPower;
                return tech >= TechLevel.Neolithic ? NeolithicPower : AnimalPower;
            }
        }

        private sealed class ScenarioResult
        {
            public Dictionary<string, double> CompletedAt { get; } =
                new Dictionary<string, double>(StringComparer.Ordinal);
            public List<string> Unreachable { get; set; } = new List<string>();
            public bool Finite { get; set; } = true;
        }
    }
}
