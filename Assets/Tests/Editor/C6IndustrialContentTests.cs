using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class C6IndustrialContentTests
{
    [Test]
    public void WorkshopPrerequisiteCyclesAreRejected()
    {
        WorkshopUpgrade first = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        WorkshopUpgrade second = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        first.SetIdForEditor("CycleWorkshopA");
        second.SetIdForEditor("CycleWorkshopB");
        first.ConfigureForEditor(new List<Research>(), new List<WorkshopUpgrade> { second },
            new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
        second.ConfigureForEditor(new List<Research>(), new List<WorkshopUpgrade> { first },
            new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());

        try
        {
            bool valid = EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { first, second }, out string error);
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("CycleWorkshopA"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void WorkshopPrerequisiteNullAndDuplicateEntriesAreRejected()
    {
        WorkshopUpgrade upgrade = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        WorkshopUpgrade prerequisite = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        Research research = ScriptableObject.CreateInstance<Research>();
        upgrade.SetIdForEditor("InvalidWorkshop");
        prerequisite.SetIdForEditor("WorkshopPrerequisite");
        research.SetIdForEditor("WorkshopResearch");

        try
        {
            upgrade.ConfigureForEditor(new List<Research>(), new List<WorkshopUpgrade> { null },
                new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            Assert.That(EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { upgrade, prerequisite }, out _), Is.False);

            prerequisite.ConfigureForEditor(new List<Research> { research }, new List<WorkshopUpgrade>(),
                new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            upgrade.ConfigureForEditor(new List<Research>(),
                new List<WorkshopUpgrade> { prerequisite, prerequisite },
                new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            Assert.That(EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { upgrade, prerequisite }, out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("重复"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(upgrade);
            UnityEngine.Object.DestroyImmediate(prerequisite);
            UnityEngine.Object.DestroyImmediate(research);
        }
    }

    [Test]
    public void WorkshopResearchPrerequisiteNullAndDuplicateEntriesAreRejected()
    {
        WorkshopUpgrade upgrade = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        Research first = ScriptableObject.CreateInstance<Research>();
        upgrade.SetIdForEditor("InvalidResearchPrerequisites");
        first.SetIdForEditor("ResearchPrerequisiteA");

        try
        {
            upgrade.ConfigureForEditor(new List<Research> { null }, new List<WorkshopUpgrade>(),
                new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            Assert.That(EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { upgrade }, out _), Is.False);

            upgrade.ConfigureForEditor(new List<Research> { first, first }, new List<WorkshopUpgrade>(),
                new List<Pair<Resource, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            Assert.That(EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { upgrade }, out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("重复"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(upgrade);
            UnityEngine.Object.DestroyImmediate(first);
        }
    }

    [Test]
    public void EconomyDependencyValidationRejectsResearchCycles()
    {
        Research first = ScriptableObject.CreateInstance<Research>();
        Research second = ScriptableObject.CreateInstance<Research>();
        first.SetIdForEditor("EconomyCycleResearchA");
        second.SetIdForEditor("EconomyCycleResearchB");
        first.SetPrerequisitesForEditor(new List<Research> { second });
        second.SetPrerequisitesForEditor(new List<Research> { first });

        try
        {
            bool valid = EconomyDependencyValidator.Validate(
                new List<Resource>(), new List<Building>(),
                new List<Research> { first, second },
                new List<WorkshopUpgrade>(), out string error);
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("EconomyCycleResearchA"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    private static readonly string[] IndustrialResourceIds =
    {
        "Machinery",
        "Chemical",
        "Electronics",
        "CrudeOil",
        "Coke",
        "Glass",
        "Ceramic",
        "RefinedFuel",
        "Lubricant",
        "Rubber",
        "CopperWire",
        "Engine",
        "Concrete",
        "BauxiteOre",
        "Aluminum",
        "Explosives"
    };

    private static readonly string[] IndustrialBuildingIds =
    {
        "SteamPlant",
        "OilDerrick",
        "CokeOven",
        "BuildingMaterialsComplex",
        "MachineFactory",
        "ChemicalPlant",
        "OilRefinery",
        "WireMill",
        "University",
        "RailHub",
        "IndustrialMetalSmelter",
        "AluminumSmelter",
        "CentralPowerStation",
        "NickelRefinery",
        "RareMetalMine",
        "MechanizedCoalMine",
        "IndustrialStoneworks",
        "IndustrialOilExtractionComplex",
        "IntegratedPetrochemicalComplex",
        "AdvancedCeramicsPlant",
        "MechanizedTextileMill",
        "TitaniumMetallurgicalComplex"
        , "IndustrialCarbonizationRetort", "IndustrialHabitationComplex"
    };

    [Test]
    public void C601_IndustrialResourcesHavePlayerFacingMetadata()
    {
        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Resource resource = DataBase<Resource>.Find(IndustrialResourceIds[i]);
            Assert.That(resource, Is.Not.Null);
            Assert.That(string.IsNullOrWhiteSpace(resource.Label), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(resource.Description), Is.False);
        }
    }

    [Test]
    public void C601_IndustrialBuildingsExistAtIndustrialTechLevel()
    {
        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(IndustrialBuildingIds[i]);
            Assert.That(building, Is.Not.Null, $"Missing industrial building '{IndustrialBuildingIds[i]}'.");
            Assert.That(building.TechLevel, Is.EqualTo(TechLevel.Industrial));
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
        }
    }

    [Test]
    public void C602_IndustrialBuildingsDeclarePowerAndLogisticsFlows()
    {
        Assert.That(DataBase<Building>.Find("SteamPlant").PowerProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("SteamPlant").LogisticsProductionRate,
            Is.EqualTo(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("MachineFactory").PowerConsumptionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("RailHub").LogisticsProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void C602_IndustrialChemistryFollowsThePowerGridFoundation()
    {
        Research chemistry = DataBase<Research>.Find("IndustrialChemistry");
        Research powerGrid = DataBase<Research>.Find("PowerGridEngineering");

        Assert.That(chemistry, Is.Not.Null);
        Assert.That(powerGrid, Is.Not.Null);
        Assert.That(chemistry.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building != null &&
            effect.Building.Id == "ChemicalPlant" &&
            effect.NumericValue.ToDouble() >= 1.10d));
        Assert.That(chemistry.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building != null &&
            effect.Building.Id == "BuildingMaterialsComplex" &&
            effect.NumericValue.ToDouble() >= 1.05d));

        Research electrical = DataBase<Research>.Find("ElectricalEngineering");
        Research standardization = DataBase<Research>.Find("Standardization");
        Assert.That(electrical, Is.Not.Null);
        Assert.That(standardization, Is.Not.Null);
        Assert.That(ContainsResearch(electrical.Prerequisites, chemistry), Is.False);
        Assert.That(ContainsResearch(standardization.Prerequisites, chemistry), Is.False);
        bool standardizationImprovesRailHub = false;
        for (int i = 0; i < standardization.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = standardization.Effects[i];
            if (effect != null &&
                effect.Type == ResearchEffectType.BuildingLogisticsProductionMultiplier &&
                effect.Building != null &&
                effect.Building.Id == "RailHub" &&
            effect.NumericValue.ToDouble() >= 1.15d)
            {
                standardizationImprovesRailHub = true;
                break;
            }
        }
        Assert.That(standardizationImprovesRailHub, Is.True);

        Assert.That(DataBase<Building>.Find("OilDerrick").RequiredResearch,
            Does.Contain(powerGrid));
        Assert.That(DataBase<Building>.Find("IndustrialMetalSmelter").RequiredResearch,
            Does.Contain(powerGrid));

        Building powerStation = DataBase<Building>.Find("CentralPowerStation");
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Steel"), Is.True);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Ceramic"), Is.True);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Coal"), Is.True);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Machinery"), Is.False);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "CopperWire"), Is.False);
        Assert.That(ContainsResource(powerStation.ResourceConsumptionRates, "Lubricant"), Is.False);
        Assert.That(powerStation.LogisticsConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C603_IndustrialResearchCoordinationExtendsModernUniversity()
    {
        Research university = DataBase<Research>.Find("ModernUniversity");
        Research coordination = DataBase<Research>.Find("IndustrialResearchCoordination");
        WorkshopUpgrade center = DataBase<WorkshopUpgrade>.Find("IndustrialResearchCoordinationCenter");

        Assert.That(university, Is.Not.Null);
        Assert.That(coordination, Is.Not.Null);
        Assert.That(center, Is.Not.Null);
        Assert.That(coordination.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(ExpantaNum.TryParse(university.BaseCost, out ExpantaNum universityCost), Is.True);
        Assert.That(ExpantaNum.TryParse(coordination.BaseCost, out ExpantaNum coordinationCost), Is.True);
        Assert.That(coordinationCost, Is.GreaterThan(universityCost));
        Assert.That(coordination.Prerequisites, Does.Contain(university));
        Assert.That(coordination.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.GlobalResearchMultiplier &&
            System.Math.Abs(effect.NumericValue.ToDouble() - 1.5d) <= 0.000001d));

        Assert.That(center.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(center.RequiredResearch, Does.Contain(coordination));
        Assert.That(center.RequiredUpgrades.Select(upgrade => upgrade.Id),
            Is.EquivalentTo(new[] { "LaboratoryGlassware", "ElectricalInstrumentation" }));
        Assert.That(center.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null &&
            effect.Type == WorkshopEffectType.GlobalResearchMultiplier &&
            System.Math.Abs(effect.NumericValue.ToDouble() - 2d) <= 0.000001d));
    }

    [Test]
    public void ChemicalPlantConsumesCrudeOilAndCokeForIndustrialChemistry()
    {
        Building chemicalPlant = DataBase<Building>.Find("ChemicalPlant");
        Assert.That(chemicalPlant, Is.Not.Null);
        Assert.That(FindRate(chemicalPlant.ResourceConsumptionRates, "CrudeOil"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(chemicalPlant.ResourceConsumptionRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(chemicalPlant.ResourceGenerationRates, "Chemical"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(chemicalPlant.ResourceGenerationRates, "Explosives"),
            Is.GreaterThan(0d));
    }

    [Test]
    public void C605_IndustrialMetalSmelterProducesCopperTinAndIron()
    {
        Building smelter = DataBase<Building>.Find("IndustrialMetalSmelter");
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Copper"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Tin"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Bronze"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Steel"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "CopperOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "TinOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Iron"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "IronOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "Steel"), Is.LessThanOrEqualTo(0d));
    }

    [Test]
    public void MetallurgicalBuildings_ConserveMaterialMass()
    {
        string[] buildingIds =
        {
            "MetalSmelter", "SteelForge", "IndustrialMetalSmelter",
            "AluminumSmelter", "NickelRefinery", "TitaniumMetallurgicalComplex"
        };

        foreach (string buildingId in buildingIds)
        {
            Building building = DataBase<Building>.Find(buildingId);
            double input = building.ResourceConsumptionRates.Sum(x => x.Second.ToDouble());
            double output = building.ResourceGenerationRates.Sum(x => x.Second.ToDouble());

            Assert.That(output, Is.LessThanOrEqualTo(input + 0.0001d),
                $"{buildingId} 产出总质量 {output} 不得超过输入总质量 {input}");
        }

        Building early = DataBase<Building>.Find("MetalSmelter");
        Assert.That(FindRate(early.ResourceGenerationRates, "Copper") +
            FindRate(early.ResourceGenerationRates, "Bronze") / 2d,
            Is.LessThanOrEqualTo(FindRate(early.ResourceConsumptionRates, "CopperOre") + 0.0001d));
        Assert.That(FindRate(early.ResourceGenerationRates, "Tin") +
            FindRate(early.ResourceGenerationRates, "Bronze") / 2d,
            Is.LessThanOrEqualTo(FindRate(early.ResourceConsumptionRates, "TinOre") + 0.0001d));

        Building industrial = DataBase<Building>.Find("IndustrialMetalSmelter");
        Assert.That(FindRate(industrial.ResourceGenerationRates, "Tin") +
            FindRate(industrial.ResourceGenerationRates, "Bronze") / 2d,
            Is.LessThanOrEqualTo(FindRate(industrial.ResourceConsumptionRates, "TinOre") + 0.0001d));
        Assert.That(FindRate(industrial.ResourceGenerationRates, "Iron") +
            FindRate(industrial.ResourceGenerationRates, "Steel"),
            Is.LessThanOrEqualTo(FindRate(industrial.ResourceConsumptionRates, "IronOre") + 0.0001d));
    }

    [Test]
    public void C612_IndustrialMultiMetalMineRecoversCommonOres()
    {
        Building mine = DataBase<Building>.Find("RareMetalMine");
        WorkshopUpgrade separation =
            DataBase<WorkshopUpgrade>.Find("HeavyMineralSeparationSystem");
        Assert.That(separation, Is.Not.Null);
        Assert.That(mine.RequiredWorkshopUpgrades, Does.Contain(separation));
        Assert.That(mine.Label, Does.Contain("多金属"));
        Assert.That(FindRate(mine.ResourceGenerationRates, "BauxiteOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "CopperOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "TinOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "IronOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "TitaniumConcentrate"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "NickelConcentrate"), Is.GreaterThan(0d));
    }

    [Test]
    public void 石器时代多金属矿场同时供应铜锡铁矿()
    {
        Building mine = DataBase<Building>.Find("MetalMine");
        Assert.That(mine, Is.Not.Null);
        Assert.That(FindRate(mine.ResourceGenerationRates, "CopperOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "TinOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "IronOre"), Is.GreaterThan(0d));
    }

    [Test]
    public void C613_PoweredMiningImprovesTheUnifiedIndustrialMine()
    {
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find("PoweredMining");
        Assert.That(workshop, Is.Not.Null);
        Assert.That(HasBuildingEffect(workshop, "RareMetalMine", 1.2d), Is.True);
        Assert.That(HasBuildingEffect(workshop, "MechanizedCoalMine", 1.2d), Is.True);
    }

    [Test]
    public void 工业多金属矿的研究与分离工艺都必须作用于同一上位矿场()
    {
        Building mine = DataBase<Building>.Find("RareMetalMine");
        Research development = DataBase<Research>.Find("RareMetalResourceDevelopment");
        WorkshopUpgrade separation = DataBase<WorkshopUpgrade>.Find("HeavyMineralSeparationSystem");

        Assert.That(mine, Is.Not.Null);
        Assert.That(development, Is.Not.Null);
        Assert.That(separation, Is.Not.Null);
        Assert.That(development.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building == mine &&
            effect.NumericValue.ToDouble() >= 1.1d));
        Assert.That(separation.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
            effect.Building == mine &&
            effect.NumericValue.ToDouble() >= 1.25d));
    }

    [Test]
    public void 工业多金属矿必须真正替代基础铜锡铁矿场()
    {
        Building lower = DataBase<Building>.Find("MetalMine");
        Building upper = DataBase<Building>.Find("RareMetalMine");

        Assert.That(lower, Is.Not.Null);
        Assert.That(upper, Is.Not.Null);
        Assert.That(lower.UpgradeTo, Is.EqualTo(upper));
        Assert.That(FindRate(upper.ResourceGenerationRates, "CopperOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "CopperOre")));
        Assert.That(FindRate(upper.ResourceGenerationRates, "TinOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "TinOre")));
        Assert.That(FindRate(upper.ResourceGenerationRates, "IronOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "IronOre")));
        Building smelter = DataBase<Building>.Find("IndustrialMetalSmelter");
        Assert.That(smelter, Is.Not.Null);
        Assert.That(FindRate(upper.ResourceGenerationRates, "CopperOre"),
            Is.GreaterThanOrEqualTo(FindRate(smelter.ResourceConsumptionRates, "CopperOre")));
        Assert.That(FindRate(upper.ResourceGenerationRates, "TinOre"),
            Is.GreaterThanOrEqualTo(FindRate(smelter.ResourceConsumptionRates, "TinOre")));
        Assert.That(FindRate(upper.ResourceGenerationRates, "IronOre"),
            Is.GreaterThanOrEqualTo(FindRate(smelter.ResourceConsumptionRates, "IronOre")));
        Assert.That(FindRate(upper.ResourceGenerationRates, "NickelConcentrate"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(upper.ResourceGenerationRates, "TitaniumConcentrate"),
            Is.GreaterThan(0d));
    }

    [Test]
    public void C612_AllUnifiedMineOutputsRetainDownstreamUses()
    {
        string[] outputIds =
        {
            "BauxiteOre",
            "CopperOre",
            "TinOre",
            "IronOre",
            "TitaniumConcentrate",
            "NickelConcentrate"
        };

        for (int i = 0; i < outputIds.Length; i++)
        {
            Assert.That(CountConsumerUses(outputIds[i]), Is.GreaterThan(0),
                $"统一多金属矿产出 {outputIds[i]} 必须存在后续建筑、研究或工坊用途。");
        }
    }

    [Test]
    public void 工业机械化煤矿替代基础煤矿并持续消耗炸药()
    {
        Building coalMine = DataBase<Building>.Find("CoalMine");
        Building mechanizedMine = DataBase<Building>.Find("MechanizedCoalMine");
        Assert.That(coalMine, Is.Not.Null);
        Assert.That(mechanizedMine, Is.Not.Null);
        Assert.That(mechanizedMine.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(FindRate(mechanizedMine.ResourceGenerationRates, "Coal"), Is.GreaterThan(0d));
        Assert.That(FindRate(mechanizedMine.ResourceConsumptionRates, "Explosives"), Is.GreaterThan(0d));
        Assert.That(mechanizedMine.RequiredResearch, Does.Contain(DataBase<Research>.Find("IndustrialExplosives")));
        Assert.That(mechanizedMine.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgrade>.Find("ControlledBlasting")));
    }

    [Test]
    public void 工业石材联合厂合并采石与切石并持续供给工业建筑()
    {
        Building quarry = DataBase<Building>.Find("Quarry");
        Building cutter = DataBase<Building>.Find("StoneCuttingWorkshop");
        Building stoneworks = DataBase<Building>.Find("IndustrialStoneworks");
        Assert.That(quarry.UpgradeTo, Is.EqualTo(stoneworks));
        Assert.That(cutter.UpgradeTo, Is.EqualTo(stoneworks));
        Assert.That(FindRate(stoneworks.ResourceGenerationRates, "StoneChunk"), Is.GreaterThan(0d));
        Assert.That(FindRate(stoneworks.ResourceGenerationRates, "StoneBrick"), Is.GreaterThan(0d));
        Assert.That(FindRate(stoneworks.ResourceConsumptionRates, "Lubricant"), Is.GreaterThan(0d));
        Assert.That(FindRate(stoneworks.ResourceConsumptionRates, "Explosives"), Is.GreaterThan(0d));
        Assert.That(DataBase<Research>.Find("ConcreteEngineering").Effects,
            Has.Some.Matches<ResearchEffectDefinition>(effect =>
                effect != null && effect.Building == stoneworks && effect.NumericValue.ToDouble() >= 1.1d));
    }

    [Test]
    public void AdvancedCeramicsPlantUsesItsClayExtractionAndCeramicPrerequisites()
    {
        Building clayPit = DataBase<Building>.Find("ClayPit");
        Building processingWorks = DataBase<Building>.Find("AdvancedCeramicsPlant");

        Assert.That(clayPit, Is.Not.Null);
        Assert.That(processingWorks, Is.Not.Null);
        Assert.That(clayPit.UpgradeTo, Is.EqualTo(processingWorks));
        Assert.That(processingWorks.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(processingWorks.RequiredResearch,
            Does.Contain(DataBase<Research>.Find("Industrialization")));
        Assert.That(processingWorks.RequiredResearch,
            Does.Contain(DataBase<Research>.Find("AdvancedCeramicEngineering")));
        Assert.That(processingWorks.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgrade>.Find("RotaryKilns")));
        Assert.That(processingWorks.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgrade>.Find("ControlledBlasting")));
        Assert.That(FindRate(processingWorks.ResourceGenerationRates, "Clay"),
            Is.GreaterThan(FindRate(clayPit.ResourceGenerationRates, "Clay")));
        Assert.That(FindRate(processingWorks.ResourceConsumptionRates, "Explosives"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(processingWorks.ResourceConsumptionRates, "Lubricant"),
            Is.GreaterThan(0d));
    }

    [Test]
    public void 工业石油开采综合体替代油井并持续维护石化供给()
    {
        Building derrick = DataBase<Building>.Find("OilDerrick");
        Building complex = DataBase<Building>.Find("IndustrialOilExtractionComplex");
        Assert.That(derrick, Is.Not.Null);
        Assert.That(complex, Is.Not.Null);
        Assert.That(derrick.UpgradeTo, Is.EqualTo(complex));
        Assert.That(FindRate(complex.ResourceGenerationRates, "CrudeOil"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "Lubricant"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "Explosives"), Is.GreaterThan(0d));
        Assert.That(complex.RequiredResearch, Does.Contain(DataBase<Research>.Find("DeepOilDrilling")));
        Assert.That(complex.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgrade>.Find("RotaryDrillingHeads")));
    }

    [Test]
    public void 一体化石油化工联合体集中输出燃料润滑剂与橡胶()
    {
        Building refinery = DataBase<Building>.Find("OilRefinery");
        Building complex = DataBase<Building>.Find("IntegratedPetrochemicalComplex");
        Assert.That(refinery, Is.Not.Null);
        Assert.That(complex, Is.Not.Null);
        Assert.That(refinery.UpgradeTo, Is.EqualTo(complex));
        Assert.That(FindRate(complex.ResourceGenerationRates, "RefinedFuel"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceGenerationRates, "Lubricant"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceGenerationRates, "Rubber"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceGenerationRates, "Chemical"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "CrudeOil"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "Chemical"), Is.LessThanOrEqualTo(0d));
        Assert.That(complex.RequiredResearch, Does.Contain(DataBase<Research>.Find("IndustrialChemistry")));
        Assert.That(complex.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgrade>.Find("ContinuousDistillation")));
    }

    [Test]
    public void C614_SeparateBauxiteMineWasRemovedAfterUnifiedMineMigration()
    {
        Assert.That(
            DataBase<Building>.TryFind("BauxiteMine", out Building bauxiteMine),
            Is.False);
        Assert.That(bauxiteMine, Is.Null);
        Assert.That(DataBase<Building>.Find("RareMetalMine"), Is.Not.Null);
    }

    [Test]
    public void C609_MachineFactoryConsumesRubberForEngineProduction()
    {
        Building factory = DataBase<Building>.Find("MachineFactory");
        Assert.That(factory, Is.Not.Null, "机器制造厂定义不能为空。");
        Assert.That(FindRate(factory.ResourceGenerationRates, "Engine"), Is.GreaterThan(0d));
        Assert.That(FindRate(factory.ResourceConsumptionRates, "Rubber"),
            Is.GreaterThan(0d), "机器制造厂应消耗橡胶来生产发动机。");
    }

    [Test]
    public void SpacerDemandReusesIndustrialWorkshopEfficiencyChains()
    {
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgrade>.Find("PressurizedReactors"), "ChemicalPlant", 1.35d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgrade>.Find("ContinuousDistillation"), "IntegratedPetrochemicalComplex", 1.30d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgrade>.Find("InsulatedWindings"), "WireMill", 1.25d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgrade>.Find("PrecisionTooling"), "MachineFactory", 1.25d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgrade>.Find("FuelInjection"), "MachineFactory", 1.25d), Is.True);
    }

    [Test]
    public void C610_RailHubConsumesEnginesForLogistics()
    {
        Building railHub = DataBase<Building>.Find("RailHub");
        Assert.That(railHub, Is.Not.Null, "铁路枢纽定义不能为空。");
        Assert.That(FindRate(railHub.ResourceConsumptionRates, "Engine"),
            Is.GreaterThan(0d), "铁路枢纽应持续消耗发动机来维持运输能力。");
        Assert.That(FindRate(railHub.ResourceConsumptionRates, "Machinery"),
            Is.GreaterThan(0d), "铁路枢纽应持续消耗机械设备来维护运输能力。");
    }

    [Test]
    public void IndustrialWorkshopEffectsMatchTheirTargetBuildingSystems()
    {
        Assert.That(HasTypedBuildingEffect("AluminumBusbars", "CentralPowerStation",
            WorkshopEffectType.BuildingPowerProductionMultiplier), Is.True);
        Assert.That(HasTypedBuildingEffect("ElectricalInstrumentation", "CentralPowerStation",
            WorkshopEffectType.BuildingPowerProductionMultiplier), Is.True);
        Assert.That(HasTypedBuildingEffect("ElectricalInstrumentation", "University",
            WorkshopEffectType.BuildingResearchPowerMultiplier), Is.True);
        Assert.That(HasTypedBuildingEffect("HighPressureTurbines", "CentralPowerStation",
            WorkshopEffectType.BuildingPowerProductionMultiplier), Is.True);
        Assert.That(HasTypedBuildingEffect("ReinforcedBoilers", "SteamPlant",
            WorkshopEffectType.BuildingPowerProductionMultiplier), Is.True);
        Assert.That(HasTypedBuildingEffect("BlockSignalling", "RailHub",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
    }

    [Test]
    public void IndustrialResearchAndWorkshopEffectsKeepDifferentRoles()
    {
        AssertResearchWorkshopValuesDiffer(
            "MechanizedForestry", "MechanizedForestryEquipment", "MechanizedLumberyard");
        AssertResearchWorkshopValuesDiffer(
            "RareMetalResourceDevelopment", "HeavyMineralSeparationSystem", "RareMetalMine");
        AssertResearchWorkshopValuesDiffer(
            "AdvancedCeramicEngineering", "AdvancedCeramicFiring", "AdvancedCeramicsPlant");
        AssertResearchWorkshopValuesDiffer(
            "IndustrialAgriculture", "AgriculturalMachinery", "PlantingField");
    }

    [Test]
    public void IndustrialResearchGraphRetainsMeaningfulBranches()
    {
        Research[] industrialResearch = DataBase<Research>.All
            .Where(research => research != null && research.TechLevel == TechLevel.Industrial)
            .ToArray();

        // Industrial entry points may deliberately inherit a late-Medieval
        // prerequisite.  Count those as roots of the Industrial subgraph.
        int rootCount = industrialResearch.Count(research =>
            research.Prerequisites == null || research.Prerequisites.Count == 0 ||
            research.Prerequisites.All(prerequisite =>
                prerequisite == null || prerequisite.TechLevel < TechLevel.Industrial));
        int branchingCount = industrialResearch.Count(research =>
            industrialResearch.Count(next =>
                next.Prerequisites != null && next.Prerequisites.Contains(research)) >= 2);

        Assert.That(rootCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(branchingCount, Is.GreaterThanOrEqualTo(5));
        Assert.That(industrialResearch.Any(research => research.Id == "Industrialization"), Is.True);
        Assert.That(industrialResearch.Any(research => research.Id == "IndustrialAgriculture"), Is.True);
        Assert.That(industrialResearch.Any(research => research.Id == "IndustrialChemistry"), Is.True);
    }

    [Test]
    public void AgriculturalMachineryImprovesFoodBuildings()
    {
        Assert.That(DataBase<WorkshopUpgrade>.Find("AgriculturalMachinery").Effects.Any(
            effect => effect.Type == WorkshopEffectType.GlobalFoodProductionMultiplier), Is.True);
    }

    [Test]
    public void IndustrialFoodProcessWorkshopsUseDistinctGlobalRolesAndEntryAccess()
    {
        WorkshopUpgrade first = DataBase<WorkshopUpgrade>.Find(
            "IndustrialFoodProcessEngineering");
        WorkshopUpgrade second = DataBase<WorkshopUpgrade>.Find(
            "EnzymaticConversionSystems");
        WorkshopUpgrade third = DataBase<WorkshopUpgrade>.Find(
            "ContinuousCultureBioreactors");
        Research industrialWorkshop = DataBase<Research>.Find("IndustrialWorkshop");

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(third, Is.Not.Null);
        Assert.That(first.RequiredResearch, Does.Contain(industrialWorkshop));
        Assert.That(first.RequiredUpgrades, Is.Empty);
        Assert.That(second.RequiredUpgrades, Does.Contain(first));
        Assert.That(third.RequiredUpgrades, Does.Contain(second));

        WorkshopUpgrade[] upgrades = { first, second, third };
        for (int i = 0; i < upgrades.Length; i++)
        {
            Assert.That(upgrades[i].Effects, Has.Count.EqualTo(1));
            WorkshopEffectDefinition effect = upgrades[i].Effects[0];
            Assert.That(effect.Type, Is.EqualTo(
                WorkshopEffectType.GlobalFoodProductionMultiplier));
            Assert.That(effect.Building, Is.Null);
            Assert.That(upgrades[i].ResourceRequirements, Has.Count.GreaterThanOrEqualTo(3));
        }

        Assert.That(first.Label, Is.Not.EqualTo(second.Label));
        Assert.That(first.Label, Is.Not.EqualTo(third.Label));
        Assert.That(second.Label, Is.Not.EqualTo(third.Label));
        Assert.That(1.5d * 1.25d * 1.25d, Is.EqualTo(2.34375d).Within(0.000001d));
    }

    [Test]
    public void AgriculturalMachineryAlsoSupportsIndustrialFiberSupply()
    {
        Assert.That(HasTypedBuildingEffect("AgriculturalMachinery", "PlantingField",
            WorkshopEffectType.BuildingProductionMultiplier), Is.True);
    }

    [Test]
    public void PlantingFieldMustOutproduceIrrigationWorksInBothAgriculturalOutputs()
    {
        Building field = DataBase<Building>.Find("PlantingField");
        Building irrigation = DataBase<Building>.Find("IrrigationWorks");

        Assert.That(field, Is.Not.Null);
        Assert.That(irrigation, Is.Not.Null);
        Assert.That(field.FoodProductionRate, Is.GreaterThan(irrigation.FoodProductionRate));
        Assert.That(FindRate(field.ResourceGenerationRates, "Biomass"),
            Is.GreaterThan(FindRate(irrigation.ResourceGenerationRates, "Biomass")));
    }

    [Test]
    public void 种植田必须在农业理论和农业机械工坊都完成后建造()
    {
        Building field = DataBase<Building>.Find("PlantingField");
        Research agriculture = DataBase<Research>.Find("IndustrialAgriculture");
        WorkshopUpgrade machinery = DataBase<WorkshopUpgrade>.Find("AgriculturalMachinery");

        Assert.That(field, Is.Not.Null);
        Assert.That(agriculture, Is.Not.Null);
        Assert.That(machinery, Is.Not.Null);
        Assert.That(field.RequiredResearch, Does.Contain(agriculture));
        Assert.That(field.RequiredWorkshopUpgrades, Does.Contain(machinery));
    }

    [Test]
    public void 医疗建筑删除后医学研究仍保留人口生产力效果()
    {
        bool hospitalExists = DataBase<Building>.TryFind("Hospital", out Building hospital);
        bool medicalCenterExists = DataBase<Building>.TryFind(
            "IndustrialMedicalCenter", out Building medicalCenter);
        Research modernMedicine = DataBase<Research>.Find("ModernMedicine");

        Assert.That(hospitalExists, Is.False);
        Assert.That(medicalCenterExists, Is.False);
        Assert.That(hospital, Is.Null);
        Assert.That(medicalCenter, Is.Null);
        Assert.That(modernMedicine, Is.Not.Null);
        Assert.That(modernMedicine.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.PopulationProductivityMultiplier &&
            effect.NumericValue == new ExpantaNum("1.35")));
    }

    [Test]
    public void 无明确生产职责的退休建筑与工坊不得重新出现()
    {
        string[] retiredBuildingIds =
        {
            "CraftShelter",
            "HealerHut",
            "MeetingGround",
            "Pasture",
            "GuildHall",
            "Hospital",
            "IndustrialMedicalCenter",
            "RoyalWorkshop",
            "VillageWorkshop",
            "LivestockYard",
            "BronzeFoundry",
            "IndustrialBronzeFoundry",
            "BlastFurnace",
            "CouncilHall"
        };

        for (int i = 0; i < retiredBuildingIds.Length; i++)
        {
            bool exists = DataBase<Building>.TryFind(retiredBuildingIds[i], out Building retired);
            Assert.That(exists, Is.False, retiredBuildingIds[i]);
            Assert.That(retired, Is.Null, retiredBuildingIds[i]);
        }

        Assert.That(
            DataBase<WorkshopUpgrade>.TryFind("RemoteSurgicalSystems", out WorkshopUpgrade retiredUpgrade),
            Is.False);
        Assert.That(retiredUpgrade, Is.Null);
    }

    [Test]
    public void C606_IndustrialPowerAndLogisticsRolesRemainDistinct()
    {
        Building powerStation = DataBase<Building>.Find("CentralPowerStation");
        Building railHub = DataBase<Building>.Find("RailHub");

        Assert.That(powerStation.PowerProductionRate, Is.GreaterThan(ExpantaNum.Zero),
            "中央电站必须提供电力。");
        Assert.That(railHub.LogisticsProductionRate, Is.GreaterThan(ExpantaNum.Zero),
            "铁路枢纽必须提供物流。");
        Assert.That(powerStation.LogisticsProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(railHub.PowerProductionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C603_IndustrialBuildingsUseProductivityAndFlowsInsteadOfFood()
    {
        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(IndustrialBuildingIds[i]);
            Assert.That(building.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero),
                $"Industrial building '{building.Id}' must not directly consume Food.");
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(
                building.PowerProductionRate > ExpantaNum.Zero ||
                building.PowerConsumptionRate > ExpantaNum.Zero ||
                building.LogisticsProductionRate > ExpantaNum.Zero ||
                building.LogisticsConsumptionRate > ExpantaNum.Zero,
                $"Industrial building '{building.Id}' must declare a Power or Logistics flow.");
        }
    }

    [Test]
    public void C603_IndustrialAndSpacerBuildingsHaveAnExplicitGameplayRole()
    {
        IReadOnlyList<Building> buildings = DataBase<Building>.All;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null || building.TechLevel < TechLevel.Industrial)
                continue;

            bool hasRole = building.ResourceGenerationRates.Count > 0 ||
                building.ResourceConsumptionRates.Count > 0 ||
                building.PowerProductionRate > ExpantaNum.Zero ||
                building.PowerConsumptionRate > ExpantaNum.Zero ||
                building.LogisticsProductionRate > ExpantaNum.Zero ||
                building.LogisticsConsumptionRate > ExpantaNum.Zero ||
                building.PopulationCapacityGranted > ExpantaNum.Zero ||
                building.ResearchPowerGranted > ExpantaNum.Zero ||
                building.FoodProductionRate > ExpantaNum.Zero ||
                building.FoodConsumptionRate > ExpantaNum.Zero ||
                building.FleetPowerGranted > ExpantaNum.Zero ||
                building.AttackPowerGranted > ExpantaNum.Zero ||
                building.DefensePowerGranted > ExpantaNum.Zero ||
                building.MilitaryManpowerGranted > ExpantaNum.Zero;

            Assert.That(
                hasRole,
                Is.True,
                $"工业及太空建筑“{building.Id}”必须声明生产、维护、人口、研究或战略职责。");
        }
    }

    [Test]
    public void C603_IndustrialEraRetainsCoalCopperIronAndSteelUses()
    {
        var legacyIndustrialResources = new[] { "Coal", "Copper", "Iron", "Steel" };
        for (int i = 0; i < legacyIndustrialResources.Length; i++)
        {
            Assert.That(CountConsumerBuildings(legacyIndustrialResources[i]), Is.GreaterThan(0),
                $"Industrial layer lost its use for '{legacyIndustrialResources[i]}'.");
        }
    }

    [Test]
    public void C603_IndustrialEnergyChainRetainsContinuousSourcesAndSinks()
    {
        Building cokeOven = DataBase<Building>.Find("CokeOven");
        Building retort = DataBase<Building>.Find("IndustrialCarbonizationRetort");
        Building mechanizedLumberyard = DataBase<Building>.Find("MechanizedLumberyard");
        Building mechanizedCoalMine = DataBase<Building>.Find("MechanizedCoalMine");

        Assert.That(cokeOven, Is.Not.Null);
        Assert.That(retort, Is.Not.Null);
        Assert.That(mechanizedLumberyard, Is.Not.Null);
        Assert.That(mechanizedCoalMine, Is.Not.Null);
        Assert.That(FindRate(cokeOven.ResourceConsumptionRates, "Coal"), Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceConsumptionRates, "WoodLog"), Is.GreaterThan(0d));
        Assert.That(FindRate(mechanizedLumberyard.ResourceGenerationRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(mechanizedCoalMine.ResourceGenerationRates, "Coal"),
            Is.GreaterThan(0d));

        bool cokeHasContinuousSink = DataBase<Building>.All.Any(building =>
            FindRate(building.ResourceConsumptionRates, "Coke") > 0d);
        bool coalHasContinuousSink = DataBase<Building>.All.Any(building =>
            FindRate(building.ResourceConsumptionRates, "Coal") > 0d);
        bool woodHasContinuousSink = DataBase<Building>.All.Any(building =>
            FindRate(building.ResourceConsumptionRates, "WoodLog") > 0d);

        Assert.That(cokeHasContinuousSink, Is.True, "焦炭必须有持续消费建筑。");
        Assert.That(coalHasContinuousSink, Is.True, "煤炭必须有持续消费建筑。");
        Assert.That(woodHasContinuousSink, Is.True, "木材必须有持续消费建筑。");
    }

    [Test]
    public void C604_IndustrialUpgradeChainsAreExplicitAndAcyclic()
    {
        var expected = new Dictionary<string, string>
        {
            ["Lumberyard"] = "MechanizedLumberyard",
            ["Quarry"] = "IndustrialStoneworks",
            ["StoneCuttingWorkshop"] = "IndustrialStoneworks",
            ["CoalMine"] = "MechanizedCoalMine",
            ["MetalMine"] = "RareMetalMine",
            ["ClayPit"] = "AdvancedCeramicsPlant",
            ["CeramicKiln"] = "AdvancedCeramicsPlant",
            ["CharcoalKiln"] = "IndustrialCarbonizationRetort",
            ["CokeOven"] = "IndustrialCarbonizationRetort",
            ["OilDerrick"] = "IndustrialOilExtractionComplex",
            ["FiberGatheringCamp"] = "PlantingField",
            ["Farm"] = "IrrigationWorks",
            ["IrrigationWorks"] = "PlantingField",
            ["MetalSmelter"] = "IndustrialMetalSmelter",
            ["SteelForge"] = "IndustrialMetalSmelter",
            ["SteamPlant"] = "CentralPowerStation"
        };

        foreach (var pair in expected)
        {
            Building source = DataBase<Building>.Find(pair.Key);
            Building target = DataBase<Building>.Find(pair.Value);
            Assert.That(source.UpgradeTo, Is.SameAs(target),
                $"Building chain '{pair.Key}' must point to '{pair.Value}'.");
        }

        BuildingManager.ValidateBuildingChains(DataBase<Building>.All);
    }

    [Test]
    public void IndustrialBaseReplacementsCarryIndustrialOperatingBurden()
    {
        string[,] chains =
        {
            { "Lumberyard", "MechanizedLumberyard" },
            { "Quarry", "IndustrialStoneworks" },
            { "CoalMine", "MechanizedCoalMine" },
            { "MetalMine", "RareMetalMine" },
            { "ClayPit", "AdvancedCeramicsPlant" },
            { "CeramicKiln", "AdvancedCeramicsPlant" },
            { "FiberGatheringCamp", "PlantingField" },
            { "IrrigationWorks", "PlantingField" }
        };

        for (int i = 0; i < chains.GetLength(0); i++)
        {
            Building lower = DataBase<Building>.Find(chains[i, 0]);
            Building upper = DataBase<Building>.Find(chains[i, 1]);

            Assert.That(lower, Is.Not.Null, $"找不到基础建筑 {chains[i, 0]}。");
            Assert.That(upper, Is.Not.Null, $"找不到工业上位建筑 {chains[i, 1]}。");
            Assert.That(upper.TechLevel, Is.EqualTo(TechLevel.Industrial));
            Assert.That(upper.SpaceCost, Is.GreaterThan(lower.SpaceCost),
                $"上位建筑 {chains[i, 1]} 的土地需求必须高于 {chains[i, 0]}。");
            Assert.That(upper.ProductivityConsumption,
                Is.GreaterThan(lower.ProductivityConsumption),
                $"上位建筑 {chains[i, 1]} 的生产力需求必须高于 {chains[i, 0]}。");
        }
    }

    [Test]
    public void C607_IndustrialProductionGraphHasNoRecipeCycle()
    {
        string error;
        bool valid = EconomyDependencyValidator.Validate(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            DataBase<WorkshopUpgrade>.All,
            out error);

        Assert.That(valid, Is.True, error);
    }

    [Test]
    public void C608_PrecisionManufacturingDoesNotRequireDownstreamGlass()
    {
        Research precision = DataBase<Research>.Find("PrecisionManufacturing");

        Assert.That(ContainsResource(precision.ResourceRequirements, "Glass"), Is.False);
        Assert.That(ContainsResource(precision.ResourceRequirements, "CopperWire"), Is.True);
    }

    [Test]
    public void C610_IndustrialEntryResearchDoesNotLoopThroughItsOutputBuildings()
    {
        Research precision = DataBase<Research>.Find("PrecisionManufacturing");
        Research concrete = DataBase<Research>.Find("ConcreteEngineering");
        Research metal = DataBase<Research>.Find("IndustrialMetalSmelting");
        Research workshop = DataBase<Research>.Find("IndustrialWorkshop");

        Assert.That(precision, Is.Not.Null);
        Assert.That(concrete, Is.Not.Null);
        Assert.That(metal, Is.Not.Null);
        Assert.That(workshop, Is.Not.Null);
        Assert.That(precision.Prerequisites,
            Has.None.Matches<Research>(item => item != null && item.Id == concrete.Id));
        Assert.That(precision.Prerequisites,
            Has.None.Matches<Research>(item => item != null && item.Id == metal.Id));
        Assert.That(precision.Prerequisites,
            Has.Some.Matches<Research>(item => item != null && item.Id == workshop.Id));
        Assert.That(metal.Prerequisites,
            Has.Some.Matches<Research>(item => item != null && item.Id == workshop.Id));
        Assert.That(DataBase<Building>.Find("MachineFactory").RequiredResearch,
            Has.Some.Matches<Research>(item => item != null && item.Id == precision.Id));
        Assert.That(DataBase<Building>.Find("BuildingMaterialsComplex").RequiredResearch,
            Has.Some.Matches<Research>(item => item != null && item.Id == concrete.Id));
        Assert.That(DataBase<Building>.Find("IndustrialMetalSmelter").RequiredResearch,
            Has.Some.Matches<Research>(item => item != null && item.Id == metal.Id));
    }

    [Test]
    public void C601_IndustrialBuildingsOwnTheirResearchPrerequisites()
    {
        Research industrialization = DataBase<Research>.Find("Industrialization");
        var expected = new HashSet<string>(IndustrialBuildingIds);
        var actual = new HashSet<string>();

        foreach (Building building in DataBase<Building>.All)
        {
            if (building.TechLevel != TechLevel.Industrial)
                continue;
            Assert.That(building.RequiredResearch, Is.Not.Empty, building.Id);
            for (int i = 0; i < building.RequiredResearch.Count; i++)
                if (building.RequiredResearch[i] == industrialization)
                    actual.Add(building.Id);
        }

        Assert.That(actual, Is.SubsetOf(expected));
        Assert.That(actual, Does.Contain("SteamPlant"));
    }

    [Test]
    public void 机械化纺织厂必须作为布料工坊的工业上位替代()
    {
        Building lower = DataBase<Building>.Find("WeavingWorkshop");
        Building upper = DataBase<Building>.Find("MechanizedTextileMill");
        Research theory = DataBase<Research>.Find("PrecisionManufacturing");
        WorkshopUpgrade looms = DataBase<WorkshopUpgrade>.Find("MechanicalLooms");

        Assert.That(lower, Is.Not.Null);
        Assert.That(upper, Is.Not.Null);
        Assert.That(theory, Is.Not.Null);
        Assert.That(looms, Is.Not.Null);
        Assert.That(lower.UpgradeTo, Is.EqualTo(upper));
        Assert.That(upper.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(FindRate(upper.ResourceGenerationRates, "Cloth"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "Cloth") * 5d));
        Assert.That(FindRate(upper.ResourceConsumptionRates, "Biomass"),
            Is.GreaterThan(FindRate(lower.ResourceConsumptionRates, "Biomass") * 3d));
        Assert.That(FindRate(upper.ResourceConsumptionRates, "Lubricant"),
            Is.GreaterThan(0d));
        Assert.That(upper.RequiredResearch, Does.Contain(theory));
        Assert.That(upper.RequiredWorkshopUpgrades, Does.Contain(looms));
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Building == upper &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier));
        Assert.That(looms.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == upper &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier));
    }

    [Test]
    public void C601_IndustrialResourcesHaveSourcesAndMultipleUses()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Assert.That(result.ResourcesWithoutSource, Does.Not.Contain(IndustrialResourceIds[i]),
                string.Join(", ", result.ResourcesWithoutSource));
            Assert.That(result.ResourcesWithoutSink, Does.Not.Contain(IndustrialResourceIds[i]),
                string.Join(", ", result.ResourcesWithoutSink));
            Assert.That(CountConsumerUses(IndustrialResourceIds[i]), Is.GreaterThanOrEqualTo(2),
                $"Industrial resource '{IndustrialResourceIds[i]}' must have at least two independent uses.");
        }
    }

    [Test]
    public void C608_NewIndustrialResourcesHavePlayerFacingMetadata()
    {
        AssertResourceMetadata("Concrete");
        AssertResourceMetadata("Explosives");
        AssertResourceMetadata("BauxiteOre");
        AssertResourceMetadata("Aluminum");
    }

    [Test]
    public void C611_IndustrialConstructionMaterialsRemainUsefulInSpaceEra()
    {
        Building launchCenter = DataBase<Building>.Find("LaunchCenter");
        Building orbitalStation = DataBase<Building>.Find("OrbitalStation");
        Building shipyard = DataBase<Building>.Find("Shipyard");

        Assert.That(ContainsResource(launchCenter.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(launchCenter.ResourceRequirements, "Explosives"), Is.False);
        Assert.That(ContainsResource(orbitalStation.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(shipyard.ResourceRequirements, "Concrete"), Is.True);
    }

    private static int CountConsumerBuildings(string resourceId)
    {
        int count = 0;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
        {
            Building building = DataBase<Building>.All[i];
            if (ContainsResource(building.ResourceRequirements, resourceId) ||
                ContainsResource(building.ResourceConsumptionRates, resourceId))
                count++;
        }
        return count;
    }

    [Test]
    public void 机械化林场必须成为伐木场的工业上位替代()
    {
        Building lower = DataBase<Building>.Find("Lumberyard");
        Building upper = DataBase<Building>.Find("MechanizedLumberyard");
        Research research = DataBase<Research>.Find("MechanizedForestry");
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find("MechanizedForestryEquipment");

        Assert.That(lower.UpgradeTo, Is.EqualTo(upper));
        Assert.That(upper.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(FindRate(upper.ResourceGenerationRates, "WoodLog"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "WoodLog")));
        Assert.That(FindRate(upper.ResourceConsumptionRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(upper.ProductivityConsumption,
            Is.GreaterThan(lower.ProductivityConsumption));
        Assert.That(upper.PowerConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(research.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building == upper));
        Assert.That(workshop.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
            effect.Building == upper));
    }

    [Test]
    public void 工业炭化干馏炉必须把原木转化为焦炭()
    {
        Building lower = DataBase<Building>.Find("CharcoalKiln");
        Building retort = DataBase<Building>.Find("IndustrialCarbonizationRetort");
        Research industrialization = DataBase<Research>.Find("Industrialization");
        Research mechanizedForestry = DataBase<Research>.Find("MechanizedForestry");
        Research coking = DataBase<Research>.Find("Coking");

        Assert.That(lower.UpgradeTo, Is.EqualTo(retort));
        Assert.That(retort.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(retort.RequiredResearch, Does.Contain(industrialization));
        Assert.That(retort.RequiredResearch, Does.Contain(coking));
        bool requiresForestryTheory = false;
        for (int i = 0; i < retort.RequiredResearch.Count; i++)
            requiresForestryTheory |= retort.RequiredResearch[i] == mechanizedForestry;
        Assert.That(requiresForestryTheory, Is.False);
        Assert.That(FindRate(retort.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "Coal")));
    }

    [Test]
    public void CokeOvenSharesTheIndustrialCarbonizationUpgrade()
    {
        Building cokeOven = DataBase<Building>.Find("CokeOven");
        Building retort = DataBase<Building>.Find("IndustrialCarbonizationRetort");
        Research coking = DataBase<Research>.Find("Coking");
        WorkshopUpgrade optimization = DataBase<WorkshopUpgrade>.Find("CokeOvenOptimization");
        WorkshopUpgrade forestry = DataBase<WorkshopUpgrade>.Find("MechanizedForestryEquipment");

        Assert.That(cokeOven, Is.Not.Null);
        Assert.That(retort, Is.Not.Null);
        Assert.That(coking, Is.Not.Null);
        Assert.That(optimization, Is.Not.Null);
        Assert.That(forestry, Is.Not.Null);
        Assert.That(cokeOven.UpgradeTo, Is.EqualTo(retort));
        Assert.That(retort.RequiredWorkshopUpgrades, Does.Contain(optimization));
        bool usesForestryEquipment = false;
        for (int i = 0; i < retort.RequiredWorkshopUpgrades.Count; i++)
            usesForestryEquipment |= retort.RequiredWorkshopUpgrades[i] == forestry;
        Assert.That(usesForestryEquipment, Is.False);
        Assert.That(FindRate(retort.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(FindRate(cokeOven.ResourceGenerationRates, "Coke")));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(FindRate(cokeOven.ResourceGenerationRates, "Coke") * 3d));
        Assert.That(coking.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building == retort && effect.NumericValue.ToDouble() >= 1.1d));
    }

    [Test]
    public void IndustrialMetalSmelterRequiresItsPhysicalFurnaceWorkshop()
    {
        Building smelter = DataBase<Building>.Find("IndustrialMetalSmelter");
        WorkshopUpgrade furnaces = DataBase<WorkshopUpgrade>.Find("IntegratedFurnaces");

        Assert.That(smelter, Is.Not.Null);
        Assert.That(furnaces, Is.Not.Null);
        Assert.That(smelter.RequiredWorkshopUpgrades, Does.Contain(furnaces));
        Assert.That(furnaces.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == smelter && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void BuildingMaterialsComplexRequiresItsRotaryKilnWorkshop()
    {
        Building buildingMaterialsComplex = DataBase<Building>.Find("BuildingMaterialsComplex");
        WorkshopUpgrade rotaryKilns = DataBase<WorkshopUpgrade>.Find("RotaryKilns");

        Assert.That(buildingMaterialsComplex, Is.Not.Null);
        Assert.That(rotaryKilns, Is.Not.Null);
        Assert.That(buildingMaterialsComplex.RequiredWorkshopUpgrades, Does.Contain(rotaryKilns));
        Assert.That(FindRate(buildingMaterialsComplex.ResourceGenerationRates, "Concrete"),
            Is.GreaterThan(0d));
        Assert.That(rotaryKilns.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == buildingMaterialsComplex && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void MachineFactoryRequiresItsPrecisionToolingWorkshop()
    {
        Building machineFactory = DataBase<Building>.Find("MachineFactory");
        WorkshopUpgrade precisionTooling = DataBase<WorkshopUpgrade>.Find("PrecisionTooling");

        Assert.That(machineFactory, Is.Not.Null);
        Assert.That(precisionTooling, Is.Not.Null);
        Assert.That(machineFactory.RequiredWorkshopUpgrades, Does.Contain(precisionTooling));
        Assert.That(precisionTooling.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == machineFactory && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void AdvancedCeramicsPlant_HasOnlySmallClayExtractionSurplus()
    {
        Building plant = DataBase<Building>.Find("AdvancedCeramicsPlant");

        Assert.That(plant, Is.Not.Null);
        Assert.That(FindRate(plant.ResourceGenerationRates, "Ceramic"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(DataBase<Building>.Find("ClayPit").ResourceGenerationRates, "Clay"),
            Is.GreaterThan(0d));
        Assert.That(CountConsumerUses("Clay"), Is.GreaterThan(0));
    }

    [Test]
    public void IndustrialResearchMaterialCosts_RequireTheirProductionEntrances()
    {
        Research chemicalResearch = DataBase<Research>.Find("IndustrialChemistry");
        Research machineryResearch = DataBase<Research>.Find("PrecisionManufacturing");
        Research coking = DataBase<Research>.Find("Coking");
        Research concrete = DataBase<Research>.Find("ConcreteEngineering");

        Assert.That(chemicalResearch, Is.Not.Null);
        Assert.That(machineryResearch, Is.Not.Null);
        Assert.That(coking, Is.Not.Null);
        Assert.That(concrete, Is.Not.Null);

        for (int i = 0; i < DataBase<Research>.All.Count; i++)
        {
            Research research = DataBase<Research>.All[i];
            if (research == null || research.TechLevel != TechLevel.Industrial)
                continue;

            if (ContainsResource(research.ResourceRequirements, "Chemical"))
                Assert.That(HasResearchPrerequisiteTransitively(research, chemicalResearch), Is.True,
                    research.Id + " consumes Chemical without IndustrialChemistry in its prerequisite closure.");

            if (ContainsResource(research.ResourceRequirements, "Machinery"))
                Assert.That(HasResearchPrerequisiteTransitively(research, machineryResearch), Is.True,
                    research.Id + " consumes Machinery without PrecisionManufacturing in its prerequisite closure.");

            if (ContainsResource(research.ResourceRequirements, "Glass"))
            {
                Assert.That(HasResearchPrerequisiteTransitively(research, chemicalResearch), Is.True,
                    research.Id + " consumes Glass without IndustrialChemistry in its prerequisite closure.");
                Assert.That(HasResearchPrerequisiteTransitively(research, coking), Is.True,
                    research.Id + " consumes Glass without Coking in its prerequisite closure.");
                Assert.That(HasResearchPrerequisiteTransitively(research, concrete), Is.True,
                    research.Id + " consumes Glass without ConcreteEngineering in its prerequisite closure.");
            }
        }
    }

    [Test]
    public void IndustrialWorkshopMaterialCosts_RequireTheirProductionEntrances()
    {
        Research chemicalResearch = DataBase<Research>.Find("IndustrialChemistry");
        Research machineryResearch = DataBase<Research>.Find("PrecisionManufacturing");
        Research coking = DataBase<Research>.Find("Coking");
        Research concrete = DataBase<Research>.Find("ConcreteEngineering");

        Assert.That(chemicalResearch, Is.Not.Null);
        Assert.That(machineryResearch, Is.Not.Null);
        Assert.That(coking, Is.Not.Null);
        Assert.That(concrete, Is.Not.Null);

        for (int i = 0; i < DataBase<WorkshopUpgrade>.All.Count; i++)
        {
            WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.All[i];
            if (workshop == null || workshop.TechLevel != TechLevel.Industrial)
                continue;

            if (ContainsResource(workshop.ResourceRequirements, "Chemical"))
                Assert.That(HasWorkshopPrerequisiteTransitively(workshop, chemicalResearch), Is.True,
                    workshop.Id + " consumes Chemical without IndustrialChemistry in its prerequisite closure.");

            if (ContainsResource(workshop.ResourceRequirements, "Machinery"))
                Assert.That(HasWorkshopPrerequisiteTransitively(workshop, machineryResearch), Is.True,
                    workshop.Id + " consumes Machinery without PrecisionManufacturing in its prerequisite closure.");

            if (ContainsResource(workshop.ResourceRequirements, "Coke"))
                Assert.That(HasWorkshopPrerequisiteTransitively(workshop, coking), Is.True,
                    workshop.Id + " consumes Coke without Coking in its prerequisite closure.");

            if (ContainsResource(workshop.ResourceRequirements, "Glass"))
            {
                Assert.That(HasWorkshopPrerequisiteTransitively(workshop, chemicalResearch), Is.True,
                    workshop.Id + " consumes Glass without IndustrialChemistry in its prerequisite closure.");
                Assert.That(HasWorkshopPrerequisiteTransitively(workshop, coking), Is.True,
                    workshop.Id + " consumes Glass without Coking in its prerequisite closure.");
                Assert.That(HasWorkshopPrerequisiteTransitively(workshop, concrete), Is.True,
                    workshop.Id + " consumes Glass without ConcreteEngineering in its prerequisite closure.");
            }
        }
    }

    [Test]
    public void IndustrialMaterialEntrances_HaveSourcesAndSinks()
    {
        Assert.That(FindRate(DataBase<Building>.Find("ChemicalPlant").ResourceGenerationRates, "Chemical"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(DataBase<Building>.Find("MachineFactory").ResourceGenerationRates, "Machinery"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(DataBase<Building>.Find("BuildingMaterialsComplex").ResourceGenerationRates, "Glass"),
            Is.GreaterThan(0d));
        Assert.That(CountConsumerUses("Chemical"), Is.GreaterThan(0));
        Assert.That(CountConsumerUses("Machinery"), Is.GreaterThan(0));
        Assert.That(CountConsumerUses("Glass"), Is.GreaterThan(0));
    }

    private static bool HasResearchPrerequisiteTransitively(Research research, Research target)
    {
        return HasResearchPrerequisiteTransitively(
            research, target, new HashSet<Research>());
    }

    private static bool HasResearchPrerequisiteTransitively(
        Research research,
        Research target,
        HashSet<Research> visited)
    {
        if (research == null || target == null || !visited.Add(research))
            return false;

        for (int i = 0; i < research.Prerequisites.Count; i++)
        {
            Research prerequisite = research.Prerequisites[i];
            if (prerequisite == target ||
                HasResearchPrerequisiteTransitively(prerequisite, target, visited))
                return true;
        }

        return false;
    }

    private static bool HasWorkshopPrerequisiteTransitively(
        WorkshopUpgrade workshop,
        Research target)
    {
        return HasWorkshopPrerequisiteTransitively(
            workshop, target, new HashSet<WorkshopUpgrade>());
    }

    private static bool HasWorkshopPrerequisiteTransitively(
        WorkshopUpgrade workshop,
        Research target,
        HashSet<WorkshopUpgrade> visited)
    {
        if (workshop == null || target == null || !visited.Add(workshop))
            return false;

        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
        {
            Research prerequisite = workshop.RequiredResearch[i];
            if (prerequisite == target ||
                HasResearchPrerequisiteTransitively(prerequisite, target))
                return true;
        }

        for (int i = 0; i < workshop.RequiredUpgrades.Count; i++)
        {
            if (HasWorkshopPrerequisiteTransitively(
                workshop.RequiredUpgrades[i], target, visited))
                return true;
        }

        return false;
    }

    private static int CountConsumerUses(string resourceId)
    {
        int count = CountConsumerBuildings(resourceId);
        for (int i = 0; i < DataBase<Research>.All.Count; i++)
            if (ContainsResource(DataBase<Research>.All[i].ResourceRequirements, resourceId))
                count++;
        for (int i = 0; i < DataBase<WorkshopUpgrade>.All.Count; i++)
            if (ContainsResource(DataBase<WorkshopUpgrade>.All[i].ResourceRequirements, resourceId))
                count++;
        return count;
    }

    private static bool ContainsResource(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return true;
        }
        return false;
    }

    private static double FindRate(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second.ToDouble();
        }
        return 0d;
    }

    private static bool HasBuildingEffect(
        WorkshopUpgrade workshop,
        string buildingId,
        double minimumMultiplier)
    {
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
                effect.Building != null && effect.Building.Id == buildingId &&
                effect.NumericValue.ToDouble() >= minimumMultiplier)
                return true;
        }
        return false;
    }

    private static bool HasTypedBuildingEffect(
        string workshopId,
        string buildingId,
        WorkshopEffectType type)
    {
        WorkshopUpgrade workshop =
            DataBase<WorkshopUpgrade>.Find(workshopId);
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null && effect.Type == type &&
                effect.Building != null && effect.Building.Id == buildingId)
                return true;
        }
        return false;
    }

    private static void AssertResearchWorkshopValuesDiffer(
        string researchId,
        string workshopId,
        string buildingId)
    {
        Research research = DataBase<Research>.Find(researchId);
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find(workshopId);
        Assert.That(research, Is.Not.Null, researchId);
        Assert.That(workshop, Is.Not.Null, workshopId);

        ResearchEffectDefinition researchEffect = null;
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Building != null && effect.Building.Id == buildingId)
            {
                researchEffect = effect;
                break;
            }
        }

        WorkshopEffectDefinition workshopEffect = null;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null && effect.Building != null && effect.Building.Id == buildingId)
            {
                workshopEffect = effect;
                break;
            }
        }

        Assert.That(researchEffect, Is.Not.Null,
            $"研究 {researchId} 必须作用于 {buildingId}。");
        Assert.That(workshopEffect, Is.Not.Null,
            $"工坊 {workshopId} 必须作用于 {buildingId}。");
        Assert.That(researchEffect.NumericValue, Is.Not.EqualTo(workshopEffect.NumericValue),
            $"研究 {researchId} 与工坊 {workshopId} 不应复制完全相同的数值效果。");
    }

    private static bool ContainsResearch(
        System.Collections.Generic.IReadOnlyList<Research> prerequisites,
        Research target)
    {
        if (prerequisites == null || target == null)
            return false;
        for (int i = 0; i < prerequisites.Count; i++)
            if (prerequisites[i] == target)
                return true;
        return false;
    }

    private static void AssertResourceMetadata(string resourceId)
    {
        Resource resource = DataBase<Resource>.Find(resourceId);
        Assert.That(resource, Is.Not.Null, $"资源“{resourceId}”必须存在。");
        Assert.That(string.IsNullOrWhiteSpace(resource.Label), Is.False,
            $"资源“{resourceId}”必须有中文名称。");
        Assert.That(string.IsNullOrWhiteSpace(resource.Description), Is.False,
            $"资源“{resourceId}”必须有中文描述。");
        Assert.That(resource.Sprite, Is.Not.Null,
            $"资源“{resourceId}”必须有 UI 图标。");
    }

    [Test]
    public void 生产循环验证必须一次性报告所有独立循环()
    {
        Resource[] resources =
        {
            CreateTestResource("CycleA"), CreateTestResource("CycleB"), CreateTestResource("CycleC"),
            CreateTestResource("CycleD"), CreateTestResource("CycleE")
        };
        Building[] buildings =
        {
            CreateCycleBuilding("CycleAB", resources[0], resources[1]),
            CreateCycleBuilding("CycleBC", resources[1], resources[2]),
            CreateCycleBuilding("CycleCA", resources[2], resources[0]),
            CreateCycleBuilding("CycleDE", resources[3], resources[4]),
            CreateCycleBuilding("CycleED", resources[4], resources[3])
        };

        try
        {
            bool valid = EconomyDependencyValidator.ValidateProductionGraphForEditor(
                resources, buildings, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("CycleA -> CycleB -> CycleC -> CycleA"));
            Assert.That(error, Does.Contain("CycleD -> CycleE -> CycleD"));
        }
        finally
        {
            for (int i = 0; i < buildings.Length; i++)
                UnityEngine.Object.DestroyImmediate(buildings[i]);
            for (int i = 0; i < resources.Length; i++)
                UnityEngine.Object.DestroyImmediate(resources[i]);
        }
    }

    private static Resource CreateTestResource(string id)
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.SetIdForEditor(id);
        return resource;
    }

    private static Building CreateCycleBuilding(string id, Resource input, Resource output)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor(id);
        building.ConfigureEconomyForEditor(
            new ExpantaNum("1.15"), ExpantaNum.One, ExpantaNum.One, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(output, ExpantaNum.One)
            },
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(input, ExpantaNum.One)
            });
        return building;
    }
}
