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
        WorkshopUpgradeDef first = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        WorkshopUpgradeDef second = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        first.SetIdForEditor("CycleWorkshopA");
        second.SetIdForEditor("CycleWorkshopB");
        first.ConfigureForEditor(new List<ResearchDef>(), new List<WorkshopUpgradeDef> { second },
            new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
        second.ConfigureForEditor(new List<ResearchDef>(), new List<WorkshopUpgradeDef> { first },
            new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());

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
        WorkshopUpgradeDef upgrade = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        WorkshopUpgradeDef prerequisite = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        ResearchDef research = ScriptableObject.CreateInstance<ResearchDef>();
        upgrade.SetIdForEditor("InvalidWorkshop");
        prerequisite.SetIdForEditor("WorkshopPrerequisite");
        research.SetIdForEditor("WorkshopResearch");

        try
        {
            upgrade.ConfigureForEditor(new List<ResearchDef>(), new List<WorkshopUpgradeDef> { null },
                new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            Assert.That(EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { upgrade, prerequisite }, out _), Is.False);

            prerequisite.ConfigureForEditor(new List<ResearchDef> { research }, new List<WorkshopUpgradeDef>(),
                new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            upgrade.ConfigureForEditor(new List<ResearchDef>(),
                new List<WorkshopUpgradeDef> { prerequisite, prerequisite },
                new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
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
        WorkshopUpgradeDef upgrade = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        ResearchDef first = ScriptableObject.CreateInstance<ResearchDef>();
        upgrade.SetIdForEditor("InvalidResearchPrerequisites");
        first.SetIdForEditor("ResearchPrerequisiteA");

        try
        {
            upgrade.ConfigureForEditor(new List<ResearchDef> { null }, new List<WorkshopUpgradeDef>(),
                new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
            Assert.That(EconomyDependencyValidator.ValidateWorkshopPrerequisitesForEditor(
                new[] { upgrade }, out _), Is.False);

            upgrade.ConfigureForEditor(new List<ResearchDef> { first, first }, new List<WorkshopUpgradeDef>(),
                new List<Pair<ResourceDef, ExpantaNum>>(), new List<WorkshopEffectDefinition>());
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
        ResearchDef first = ScriptableObject.CreateInstance<ResearchDef>();
        ResearchDef second = ScriptableObject.CreateInstance<ResearchDef>();
        first.SetIdForEditor("EconomyCycleResearchA");
        second.SetIdForEditor("EconomyCycleResearchB");
        first.SetPrerequisitesForEditor(new List<ResearchDef> { second });
        second.SetPrerequisitesForEditor(new List<ResearchDef> { first });

        try
        {
            bool valid = EconomyDependencyValidator.Validate(
                new List<ResourceDef>(), new List<BuildingDef>(),
                new List<ResearchDef> { first, second },
                new List<WorkshopUpgradeDef>(), out string error);
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
        "AdvancedMetalOre",
        "AdvancedMetalIngot",
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
        "AdvancedMetalSmelter",
        "CentralPowerStation",
        "AdvancedMetalRefinery",
        "IntegratedMetalMine",
        "MechanizedCoalMine",
        "IndustrialStoneworks",
        "IndustrialOilExtractionComplex",
        "IntegratedPetrochemicalComplex",
        "AdvancedCeramicsPlant",
        "MechanizedTextileMill",
        "TitaniumAlloySmelter"
        , "IndustrialCarbonizationRetort", "IndustrialHabitationComplex"
    };

    [Test]
    public void C602_IndustrialChemistryFollowsThePowerGridFoundation()
    {
        ResearchDef chemistry = DataBase<ResearchDef>.Find("IndustrialChemistry");
        ResearchDef powerGrid = DataBase<ResearchDef>.Find("PowerGridEngineering");

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

        ResearchDef electrical = DataBase<ResearchDef>.Find("ElectricalEngineering");
        ResearchDef standardization = DataBase<ResearchDef>.Find("Standardization");
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

        Assert.That(DataBase<BuildingDef>.Find("OilDerrick").RequiredResearch,
            Does.Contain(powerGrid));
        Assert.That(DataBase<BuildingDef>.Find("IndustrialMetalSmelter").RequiredResearch,
            Does.Contain(powerGrid));

        BuildingDef powerStation = DataBase<BuildingDef>.Find("CentralPowerStation");
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
        ResearchDef university = DataBase<ResearchDef>.Find("ModernUniversity");
        ResearchDef coordination = DataBase<ResearchDef>.Find("IndustrialResearchCoordination");
        WorkshopUpgradeDef center = DataBase<WorkshopUpgradeDef>.Find("IndustrialResearchCoordinationCenter");

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
        BuildingDef chemicalPlant = DataBase<BuildingDef>.Find("ChemicalPlant");
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
    public void C605_IndustrialMetalSmelterProducesBasicIngotsAndSteel()
    {
        BuildingDef smelter = DataBase<BuildingDef>.Find("IndustrialMetalSmelter");
        Assert.That(FindRate(smelter.ResourceGenerationRates, "BasicMetalIngot"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Steel"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "BasicMetalOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "Steel"), Is.LessThanOrEqualTo(0d));
        Assert.That(smelter.ResourceGenerationRates.Count, Is.EqualTo(2),
            "The industrial furnace exposes only basic ingots and steel.");
    }

    [Test]
    public void MetallurgicalBuildings_ConserveMaterialMass()
    {
        string[] buildingIds =
        {
            "MetalSmelter", "SteelForge", "IndustrialMetalSmelter",
            "AdvancedMetalSmelter", "AdvancedMetalRefinery", "TitaniumAlloySmelter"
        };

        foreach (string buildingId in buildingIds)
        {
            BuildingDef building = DataBase<BuildingDef>.Find(buildingId);
            double input = building.ResourceConsumptionRates.Sum(x => x.Second.ToDouble());
            double output = building.ResourceGenerationRates.Sum(x => x.Second.ToDouble());
            Assert.That(output, Is.LessThanOrEqualTo(input + 0.0001d),
                $"{buildingId} output {output} exceeds its material input {input}.");
        }

        foreach (string buildingId in new[] { "MetalSmelter", "IndustrialMetalSmelter" })
        {
            BuildingDef building = DataBase<BuildingDef>.Find(buildingId);
            double metals = FindRate(building.ResourceGenerationRates, "BasicMetalIngot") +
                FindRate(building.ResourceGenerationRates, "Steel");
            Assert.That(metals, Is.GreaterThan(0d));
            Assert.That(metals,
                Is.LessThanOrEqualTo(FindRate(building.ResourceConsumptionRates, "BasicMetalOre") + 0.0001d),
                "Fuel cannot stand in for the metal ore mass balance.");
        }
    }

    [Test]
    public void C612_IndustrialMetalMineSuppliesThreeOreTiers()
    {
        BuildingDef mine = DataBase<BuildingDef>.Find("IntegratedMetalMine");
        WorkshopUpgradeDef separation = DataBase<WorkshopUpgradeDef>.Find("MetalOreSeparationSystem");
        Assert.That(mine.RequiredWorkshopUpgrades, Does.Contain(separation));
        foreach (string id in new[] { "BasicMetalOre", "AdvancedMetalOre", "SpecialMetalOre" })
            Assert.That(FindRate(mine.ResourceGenerationRates, id), Is.GreaterThan(0d), id);
        Assert.That(mine.ResourceGenerationRates.Count, Is.EqualTo(3),
            "The consolidated mine supplies exactly three metal ore tiers.");
    }

    [Test]
    public void 石器时代矿场统一供应低级金属矿()
    {
        BuildingDef mine = DataBase<BuildingDef>.Find("MetalMine");
        Assert.That(FindRate(mine.ResourceGenerationRates, "BasicMetalOre"), Is.GreaterThan(0d));
        Assert.That(mine.ResourceGenerationRates.Count, Is.EqualTo(1),
            "Early mining supplies one shared basic ore inventory.");
    }

    [Test]
    public void AdvancedMetalRefiningUpgradeKeepsOneInventoryAndProductivityEfficiency()
    {
        BuildingDef lower = DataBase<BuildingDef>.Find("AdvancedMetalSmelter");
        BuildingDef upper = DataBase<BuildingDef>.Find("AdvancedMetalRefinery");
        Assert.That(lower.UpgradeTo, Is.SameAs(upper));
        double lowerOutput = FindRate(lower.ResourceGenerationRates, "AdvancedMetalIngot");
        double upperOutput = FindRate(upper.ResourceGenerationRates, "AdvancedMetalIngot");
        Assert.That(lowerOutput, Is.GreaterThan(0d));
        Assert.That(upperOutput, Is.GreaterThan(lowerOutput));
        Assert.That(upper.ResourceGenerationRates.Count, Is.EqualTo(1));
        Assert.That(FindRate(upper.ResourceConsumptionRates, "AdvancedMetalOre"), Is.GreaterThan(0d));
        Assert.That(upper.ProductivityConsumption.ToDouble() / upperOutput,
            Is.LessThanOrEqualTo(lower.ProductivityConsumption.ToDouble() / lowerOutput + 0.0001d));
    }

    [Test]
    public void C613_PoweredMiningImprovesTheUnifiedIndustrialMine()
    {
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.Find("PoweredMining");
        Assert.That(workshop, Is.Not.Null);
        Assert.That(HasBuildingEffect(workshop, "IntegratedMetalMine", 1.2d), Is.True);
        Assert.That(HasBuildingEffect(workshop, "MechanizedCoalMine", 1.2d), Is.True);
    }

    [Test]
    public void 工业多金属矿的研究与分离工艺都必须作用于同一上位矿场()
    {
        BuildingDef mine = DataBase<BuildingDef>.Find("IntegratedMetalMine");
        ResearchDef development = DataBase<ResearchDef>.Find("IntegratedMetalResourceDevelopment");
        WorkshopUpgradeDef separation = DataBase<WorkshopUpgradeDef>.Find("MetalOreSeparationSystem");

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
    public void 工业综合矿场必须真正替代低级金属矿场()
    {
        BuildingDef lower = DataBase<BuildingDef>.Find("MetalMine");
        BuildingDef upper = DataBase<BuildingDef>.Find("IntegratedMetalMine");
        Assert.That(lower.UpgradeTo, Is.SameAs(upper));
        Assert.That(FindRate(upper.ResourceGenerationRates, "BasicMetalOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "BasicMetalOre")));
        BuildingDef smelter = DataBase<BuildingDef>.Find("IndustrialMetalSmelter");
        Assert.That(FindRate(upper.ResourceGenerationRates, "BasicMetalOre"),
            Is.GreaterThanOrEqualTo(FindRate(smelter.ResourceConsumptionRates, "BasicMetalOre")));
        Assert.That(FindRate(upper.ResourceGenerationRates, "AdvancedMetalOre"), Is.GreaterThan(0d));
        Assert.That(FindRate(upper.ResourceGenerationRates, "SpecialMetalOre"), Is.GreaterThan(0d));
    }

    [Test]
    public void C612_AllUnifiedMineOutputsRetainDownstreamUses()
    {
        string[] outputIds =
        {
            "AdvancedMetalOre",
            "BasicMetalOre",
            "SpecialMetalOre"
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
        BuildingDef coalMine = DataBase<BuildingDef>.Find("CoalMine");
        BuildingDef mechanizedMine = DataBase<BuildingDef>.Find("MechanizedCoalMine");
        Assert.That(coalMine, Is.Not.Null);
        Assert.That(mechanizedMine.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(FindRate(mechanizedMine.ResourceGenerationRates, "Coal"), Is.GreaterThan(0d));
        Assert.That(FindRate(mechanizedMine.ResourceConsumptionRates, "Explosives"), Is.GreaterThan(0d));
        Assert.That(mechanizedMine.RequiredResearch, Does.Contain(DataBase<ResearchDef>.Find("IndustrialExplosives")));
        Assert.That(mechanizedMine.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgradeDef>.Find("ControlledBlasting")));
    }

    [Test]
    public void 工业石材联合厂合并采石与切石并持续供给工业建筑()
    {
        BuildingDef quarry = DataBase<BuildingDef>.Find("Quarry");
        BuildingDef cutter = DataBase<BuildingDef>.Find("StoneCuttingWorkshop");
        BuildingDef stoneworks = DataBase<BuildingDef>.Find("IndustrialStoneworks");
        Assert.That(quarry.UpgradeTo, Is.EqualTo(stoneworks));
        Assert.That(cutter.UpgradeTo, Is.EqualTo(stoneworks));
        Assert.That(FindRate(stoneworks.ResourceGenerationRates, "StoneChunk"), Is.GreaterThan(0d));
        Assert.That(FindRate(stoneworks.ResourceGenerationRates, "StoneBrick"), Is.GreaterThan(0d));
        Assert.That(FindRate(stoneworks.ResourceConsumptionRates, "Lubricant"), Is.GreaterThan(0d));
        Assert.That(FindRate(stoneworks.ResourceConsumptionRates, "Explosives"), Is.GreaterThan(0d));
        Assert.That(DataBase<ResearchDef>.Find("ConcreteEngineering").Effects,
            Has.Some.Matches<ResearchEffectDefinition>(effect =>
                effect != null && effect.Building == stoneworks && effect.NumericValue.ToDouble() >= 1.1d));
    }

    [Test]
    public void AdvancedCeramicsPlantUsesItsClayExtractionAndCeramicPrerequisites()
    {
        BuildingDef clayPit = DataBase<BuildingDef>.Find("ClayPit");
        BuildingDef processingWorks = DataBase<BuildingDef>.Find("AdvancedCeramicsPlant");

        Assert.That(clayPit, Is.Not.Null);
        Assert.That(processingWorks, Is.Not.Null);
        Assert.That(clayPit.UpgradeTo, Is.EqualTo(processingWorks));
        Assert.That(processingWorks.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(processingWorks.RequiredResearch,
            Does.Contain(DataBase<ResearchDef>.Find("Industrialization")));
        Assert.That(processingWorks.RequiredResearch,
            Does.Contain(DataBase<ResearchDef>.Find("AdvancedCeramicEngineering")));
        Assert.That(processingWorks.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgradeDef>.Find("RotaryKilns")));
        Assert.That(processingWorks.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgradeDef>.Find("ControlledBlasting")));
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
        BuildingDef derrick = DataBase<BuildingDef>.Find("OilDerrick");
        BuildingDef complex = DataBase<BuildingDef>.Find("IndustrialOilExtractionComplex");
        Assert.That(derrick, Is.Not.Null);
        Assert.That(complex, Is.Not.Null);
        Assert.That(derrick.UpgradeTo, Is.EqualTo(complex));
        Assert.That(FindRate(complex.ResourceGenerationRates, "CrudeOil"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "Lubricant"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "Explosives"), Is.GreaterThan(0d));
        Assert.That(complex.RequiredResearch, Does.Contain(DataBase<ResearchDef>.Find("DeepOilDrilling")));
        Assert.That(complex.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgradeDef>.Find("RotaryDrillingHeads")));
    }

    [Test]
    public void 一体化石油化工联合体集中输出燃料润滑剂与橡胶()
    {
        BuildingDef refinery = DataBase<BuildingDef>.Find("OilRefinery");
        BuildingDef complex = DataBase<BuildingDef>.Find("IntegratedPetrochemicalComplex");
        Assert.That(refinery, Is.Not.Null);
        Assert.That(complex, Is.Not.Null);
        Assert.That(refinery.UpgradeTo, Is.EqualTo(complex));
        Assert.That(FindRate(complex.ResourceGenerationRates, "RefinedFuel"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceGenerationRates, "Lubricant"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceGenerationRates, "Rubber"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceGenerationRates, "Chemical"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "CrudeOil"), Is.GreaterThan(0d));
        Assert.That(FindRate(complex.ResourceConsumptionRates, "Chemical"), Is.LessThanOrEqualTo(0d));
        Assert.That(complex.RequiredResearch, Does.Contain(DataBase<ResearchDef>.Find("IndustrialChemistry")));
        Assert.That(complex.RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgradeDef>.Find("ContinuousDistillation")));
    }

    [Test]
    public void C609_MachineFactoryStartsBeforeAdvancedMetalAndPetrochemicalSupply()
    {
        BuildingDef factory = DataBase<BuildingDef>.Find("MachineFactory");
        Assert.That(factory, Is.Not.Null, "机器制造厂定义不能为空。");
        Assert.That(FindRate(factory.ResourceGenerationRates, "Engine"), Is.GreaterThan(0d));
        foreach (string resourceId in new[] { "Rubber", "Lubricant", "AdvancedMetalIngot" })
            Assert.That(FindRate(factory.ResourceConsumptionRates, resourceId),
                Is.LessThanOrEqualTo(0d), "基础机器厂不能依赖需要机器才能建设的上游：" + resourceId);
    }

    [Test]
    public void SpacerDemandReusesIndustrialWorkshopEfficiencyChains()
    {
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgradeDef>.Find("PressurizedReactors"), "ChemicalPlant", 1.35d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgradeDef>.Find("ContinuousDistillation"), "IntegratedPetrochemicalComplex", 1.30d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgradeDef>.Find("InsulatedWindings"), "WireMill", 1.25d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgradeDef>.Find("PrecisionTooling"), "MachineFactory", 1.25d), Is.True);
        Assert.That(HasBuildingEffect(DataBase<WorkshopUpgradeDef>.Find("FuelInjection"), "MachineFactory", 1.25d), Is.True);
    }

    [Test]
    public void C610_RailHubConsumesEnginesForLogistics()
    {
        BuildingDef railHub = DataBase<BuildingDef>.Find("RailHub");
        Assert.That(railHub, Is.Not.Null, "铁路枢纽定义不能为空。");
        Assert.That(FindRate(railHub.ResourceConsumptionRates, "Engine"),
            Is.GreaterThan(0d), "铁路枢纽应持续消耗发动机来维持运输能力。");
        Assert.That(FindRate(railHub.ResourceConsumptionRates, "Machinery"),
            Is.GreaterThan(0d), "铁路枢纽应持续消耗机械设备来维护运输能力。");
    }

    [Test]
    public void IndustrialWorkshopEffectsMatchTheirTargetBuildingSystems()
    {
        Assert.That(HasTypedBuildingEffect("ConductiveBusbars", "CentralPowerStation",
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
            "IntegratedMetalResourceDevelopment", "MetalOreSeparationSystem", "IntegratedMetalMine");
        AssertResearchWorkshopValuesDiffer(
            "AdvancedCeramicEngineering", "AdvancedCeramicFiring", "AdvancedCeramicsPlant");
        AssertResearchWorkshopValuesDiffer(
            "IndustrialAgriculture", "AgriculturalMachinery", "PlantingField");
    }

    [Test]
    public void IndustrialResearchGraphRetainsMeaningfulBranches()
    {
        ResearchDef[] industrialResearch = DataBase<ResearchDef>.All
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
        Assert.That(DataBase<WorkshopUpgradeDef>.Find("AgriculturalMachinery").Effects.Any(
            effect => effect.Type == WorkshopEffectType.GlobalFoodProductionMultiplier), Is.True);
    }

    [Test]
    public void IndustrialFoodProcessWorkshopsUseDistinctGlobalRolesAndEntryAccess()
    {
        WorkshopUpgradeDef first = DataBase<WorkshopUpgradeDef>.Find(
            "IndustrialFoodProcessEngineering");
        WorkshopUpgradeDef second = DataBase<WorkshopUpgradeDef>.Find(
            "EnzymaticConversionSystems");
        WorkshopUpgradeDef third = DataBase<WorkshopUpgradeDef>.Find(
            "ContinuousCultureBioreactors");
        ResearchDef industrialWorkshop = DataBase<ResearchDef>.Find("IndustrialWorkshop");

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(third, Is.Not.Null);
        Assert.That(first.RequiredResearch, Does.Contain(industrialWorkshop));
        Assert.That(first.RequiredUpgrades, Is.Empty);
        Assert.That(second.RequiredUpgrades, Does.Contain(first));
        Assert.That(third.RequiredUpgrades, Does.Contain(second));

        WorkshopUpgradeDef[] upgrades = { first, second, third };
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
        BuildingDef field = DataBase<BuildingDef>.Find("PlantingField");
        BuildingDef irrigation = DataBase<BuildingDef>.Find("IrrigationWorks");

        Assert.That(field, Is.Not.Null);
        Assert.That(irrigation, Is.Not.Null);
        Assert.That(field.FoodProductionRate, Is.GreaterThan(irrigation.FoodProductionRate));
        Assert.That(FindRate(field.ResourceGenerationRates, "Biomass"),
            Is.GreaterThan(FindRate(irrigation.ResourceGenerationRates, "Biomass")));
    }

    [Test]
    public void 种植田必须在农业理论和农业机械工坊都完成后建造()
    {
        BuildingDef field = DataBase<BuildingDef>.Find("PlantingField");
        ResearchDef agriculture = DataBase<ResearchDef>.Find("IndustrialAgriculture");
        WorkshopUpgradeDef machinery = DataBase<WorkshopUpgradeDef>.Find("AgriculturalMachinery");

        Assert.That(field, Is.Not.Null);
        Assert.That(agriculture, Is.Not.Null);
        Assert.That(machinery, Is.Not.Null);
        Assert.That(field.RequiredResearch, Does.Contain(agriculture));
        Assert.That(field.RequiredWorkshopUpgrades, Does.Contain(machinery));
    }

    [Test]
    public void 医疗建筑删除后医学研究仍保留人口生产力效果()
    {
        bool hospitalExists = DataBase<BuildingDef>.TryFind("Hospital", out BuildingDef hospital);
        bool medicalCenterExists = DataBase<BuildingDef>.TryFind(
            "IndustrialMedicalCenter", out BuildingDef medicalCenter);
        ResearchDef modernMedicine = DataBase<ResearchDef>.Find("ModernMedicine");

        Assert.That(hospitalExists, Is.False);
        Assert.That(medicalCenterExists, Is.False);
        Assert.That(hospital, Is.Null);
        Assert.That(medicalCenter, Is.Null);
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
            bool exists = DataBase<BuildingDef>.TryFind(retiredBuildingIds[i], out BuildingDef retired);
            Assert.That(exists, Is.False, retiredBuildingIds[i]);
            Assert.That(retired, Is.Null, retiredBuildingIds[i]);
        }

        Assert.That(
            DataBase<WorkshopUpgradeDef>.TryFind("RemoteSurgicalSystems", out WorkshopUpgradeDef retiredUpgrade),
            Is.False);
        Assert.That(retiredUpgrade, Is.Null);
    }

    [Test]
    public void C606_IndustrialPowerAndLogisticsRolesRemainDistinct()
    {
        BuildingDef powerStation = DataBase<BuildingDef>.Find("CentralPowerStation");
        BuildingDef railHub = DataBase<BuildingDef>.Find("RailHub");

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
            BuildingDef building = DataBase<BuildingDef>.Find(IndustrialBuildingIds[i]);
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
        IReadOnlyList<BuildingDef> buildings = DataBase<BuildingDef>.All;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingDef building = buildings[i];
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
    public void C603_IndustrialEraRetainsCoalBasicMetalAndSteelUses()
    {
        var legacyIndustrialResources = new[] { "Coal", "BasicMetalIngot", "Steel" };
        for (int i = 0; i < legacyIndustrialResources.Length; i++)
        {
            Assert.That(CountConsumerBuildings(legacyIndustrialResources[i]), Is.GreaterThan(0),
                $"Industrial layer lost its use for '{legacyIndustrialResources[i]}'.");
        }
    }

    [Test]
    public void C603_IndustrialEnergyChainRetainsContinuousSourcesAndSinks()
    {
        BuildingDef cokeOven = DataBase<BuildingDef>.Find("CokeOven");
        BuildingDef retort = DataBase<BuildingDef>.Find("IndustrialCarbonizationRetort");
        BuildingDef mechanizedLumberyard = DataBase<BuildingDef>.Find("MechanizedLumberyard");
        BuildingDef mechanizedCoalMine = DataBase<BuildingDef>.Find("MechanizedCoalMine");

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

        bool cokeHasContinuousSink = DataBase<BuildingDef>.All.Any(building =>
            FindRate(building.ResourceConsumptionRates, "Coke") > 0d);
        bool coalHasContinuousSink = DataBase<BuildingDef>.All.Any(building =>
            FindRate(building.ResourceConsumptionRates, "Coal") > 0d);
        bool woodHasContinuousSink = DataBase<BuildingDef>.All.Any(building =>
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
            ["MetalMine"] = "IntegratedMetalMine",
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
            ["SteamPlant"] = "CentralPowerStation",
            ["AdvancedMetalSmelter"] = "AdvancedMetalRefinery"
        };

        foreach (var pair in expected)
        {
            BuildingDef source = DataBase<BuildingDef>.Find(pair.Key);
            BuildingDef target = DataBase<BuildingDef>.Find(pair.Value);
            Assert.That(source.UpgradeTo, Is.SameAs(target),
                $"Building chain '{pair.Key}' must point to '{pair.Value}'.");
        }

        BuildingManager.ValidateBuildingChains(DataBase<BuildingDef>.All);
    }

    [Test]
    public void IndustrialBaseReplacementsCarryIndustrialOperatingBurden()
    {
        string[,] chains =
        {
            { "Lumberyard", "MechanizedLumberyard" },
            { "Quarry", "IndustrialStoneworks" },
            { "CoalMine", "MechanizedCoalMine" },
            { "MetalMine", "IntegratedMetalMine" },
            { "ClayPit", "AdvancedCeramicsPlant" },
            { "CeramicKiln", "AdvancedCeramicsPlant" },
            { "FiberGatheringCamp", "PlantingField" },
            { "IrrigationWorks", "PlantingField" }
        };

        for (int i = 0; i < chains.GetLength(0); i++)
        {
            BuildingDef lower = DataBase<BuildingDef>.Find(chains[i, 0]);
            BuildingDef upper = DataBase<BuildingDef>.Find(chains[i, 1]);

            Assert.That(lower, Is.Not.Null, $"找不到基础建筑 {chains[i, 0]}。");
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
            DataBase<ResourceDef>.All,
            DataBase<BuildingDef>.All,
            DataBase<ResearchDef>.All,
            DataBase<WorkshopUpgradeDef>.All,
            out error);

        Assert.That(valid, Is.True, error);
    }

    [Test]
    public void C608_PrecisionManufacturingDoesNotRequireDownstreamGlass()
    {
        ResearchDef precision = DataBase<ResearchDef>.Find("PrecisionManufacturing");

        Assert.That(ContainsResource(precision.ResourceRequirements, "Glass"), Is.False);
        Assert.That(ContainsResource(precision.ResourceRequirements, "CopperWire"), Is.True);
    }

    [Test]
    public void C610_IndustrialEntryResearchDoesNotLoopThroughItsOutputBuildings()
    {
        ResearchDef precision = DataBase<ResearchDef>.Find("PrecisionManufacturing");
        ResearchDef concrete = DataBase<ResearchDef>.Find("ConcreteEngineering");
        ResearchDef metal = DataBase<ResearchDef>.Find("IndustrialMetalSmelting");
        ResearchDef workshop = DataBase<ResearchDef>.Find("IndustrialWorkshop");

        Assert.That(precision, Is.Not.Null);
        Assert.That(concrete, Is.Not.Null);
        Assert.That(metal, Is.Not.Null);
        Assert.That(workshop, Is.Not.Null);
        Assert.That(precision.Prerequisites,
            Has.None.Matches<ResearchDef>(item => item != null && item.Id == concrete.Id));
        Assert.That(precision.Prerequisites,
            Has.None.Matches<ResearchDef>(item => item != null && item.Id == metal.Id));
        Assert.That(precision.Prerequisites,
            Has.Some.Matches<ResearchDef>(item => item != null && item.Id == workshop.Id));
        Assert.That(metal.Prerequisites,
            Has.Some.Matches<ResearchDef>(item => item != null && item.Id == workshop.Id));
        Assert.That(DataBase<BuildingDef>.Find("MachineFactory").RequiredResearch,
            Has.Some.Matches<ResearchDef>(item => item != null && item.Id == precision.Id));
        Assert.That(DataBase<BuildingDef>.Find("BuildingMaterialsComplex").RequiredResearch,
            Has.Some.Matches<ResearchDef>(item => item != null && item.Id == concrete.Id));
        Assert.That(DataBase<BuildingDef>.Find("IndustrialMetalSmelter").RequiredResearch,
            Has.Some.Matches<ResearchDef>(item => item != null && item.Id == metal.Id));
    }

    [Test]
    public void C601_IndustrialBuildingsOwnTheirResearchPrerequisites()
    {
        ResearchDef industrialization = DataBase<ResearchDef>.Find("Industrialization");
        var expected = new HashSet<string>(IndustrialBuildingIds);
        var actual = new HashSet<string>();

        foreach (BuildingDef building in DataBase<BuildingDef>.All)
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
        BuildingDef lower = DataBase<BuildingDef>.Find("WeavingWorkshop");
        BuildingDef upper = DataBase<BuildingDef>.Find("MechanizedTextileMill");
        ResearchDef theory = DataBase<ResearchDef>.Find("PrecisionManufacturing");
        WorkshopUpgradeDef looms = DataBase<WorkshopUpgradeDef>.Find("MechanicalLooms");

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
            DataBase<ResourceDef>.All,
            DataBase<BuildingDef>.All,
            DataBase<ResearchDef>.All,
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
    public void C611_IndustrialConstructionMaterialsRemainUsefulInSpaceEra()
    {
        BuildingDef launchCenter = DataBase<BuildingDef>.Find("LaunchCenter");
        BuildingDef orbitalStation = DataBase<BuildingDef>.Find("OrbitalStation");
        BuildingDef shipyard = DataBase<BuildingDef>.Find("Shipyard");

        Assert.That(ContainsResource(launchCenter.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(launchCenter.ResourceRequirements, "Explosives"), Is.False);
        Assert.That(ContainsResource(orbitalStation.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(shipyard.ResourceRequirements, "Concrete"), Is.True);
    }

    private static int CountConsumerBuildings(string resourceId)
    {
        int count = 0;
        for (int i = 0; i < DataBase<BuildingDef>.All.Count; i++)
        {
            BuildingDef building = DataBase<BuildingDef>.All[i];
            if (ContainsResource(building.ResourceRequirements, resourceId) ||
                ContainsResource(building.ResourceConsumptionRates, resourceId))
                count++;
        }
        return count;
    }

    [Test]
    public void 机械化林场必须成为伐木场的工业上位替代()
    {
        BuildingDef lower = DataBase<BuildingDef>.Find("Lumberyard");
        BuildingDef upper = DataBase<BuildingDef>.Find("MechanizedLumberyard");
        ResearchDef research = DataBase<ResearchDef>.Find("MechanizedForestry");
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.Find("MechanizedForestryEquipment");

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
        BuildingDef lower = DataBase<BuildingDef>.Find("CharcoalKiln");
        BuildingDef retort = DataBase<BuildingDef>.Find("IndustrialCarbonizationRetort");
        ResearchDef industrialization = DataBase<ResearchDef>.Find("Industrialization");
        ResearchDef mechanizedForestry = DataBase<ResearchDef>.Find("MechanizedForestry");
        ResearchDef coking = DataBase<ResearchDef>.Find("Coking");

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
        BuildingDef cokeOven = DataBase<BuildingDef>.Find("CokeOven");
        BuildingDef retort = DataBase<BuildingDef>.Find("IndustrialCarbonizationRetort");
        ResearchDef coking = DataBase<ResearchDef>.Find("Coking");
        WorkshopUpgradeDef optimization = DataBase<WorkshopUpgradeDef>.Find("CokeOvenOptimization");
        WorkshopUpgradeDef forestry = DataBase<WorkshopUpgradeDef>.Find("MechanizedForestryEquipment");

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
        BuildingDef smelter = DataBase<BuildingDef>.Find("IndustrialMetalSmelter");
        WorkshopUpgradeDef furnaces = DataBase<WorkshopUpgradeDef>.Find("IntegratedFurnaces");

        Assert.That(smelter, Is.Not.Null);
        Assert.That(furnaces, Is.Not.Null);
        Assert.That(smelter.RequiredWorkshopUpgrades, Does.Contain(furnaces));
        Assert.That(furnaces.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == smelter && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void BuildingMaterialsComplexRequiresItsRotaryKilnWorkshop()
    {
        BuildingDef buildingMaterialsComplex = DataBase<BuildingDef>.Find("BuildingMaterialsComplex");
        WorkshopUpgradeDef rotaryKilns = DataBase<WorkshopUpgradeDef>.Find("RotaryKilns");

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
        BuildingDef machineFactory = DataBase<BuildingDef>.Find("MachineFactory");
        WorkshopUpgradeDef precisionTooling = DataBase<WorkshopUpgradeDef>.Find("PrecisionTooling");

        Assert.That(machineFactory, Is.Not.Null);
        Assert.That(precisionTooling, Is.Not.Null);
        Assert.That(machineFactory.RequiredWorkshopUpgrades, Does.Contain(precisionTooling));
        Assert.That(precisionTooling.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == machineFactory && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void AdvancedCeramicsPlant_HasOnlySmallClayExtractionSurplus()
    {
        BuildingDef plant = DataBase<BuildingDef>.Find("AdvancedCeramicsPlant");

        Assert.That(plant, Is.Not.Null);
        Assert.That(FindRate(plant.ResourceGenerationRates, "Ceramic"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(DataBase<BuildingDef>.Find("ClayPit").ResourceGenerationRates, "Clay"),
            Is.GreaterThan(0d));
        Assert.That(CountConsumerUses("Clay"), Is.GreaterThan(0));
    }

    [Test]
    public void IndustrialResearchMaterialCosts_RequireTheirProductionEntrances()
    {
        ResearchDef chemicalResearch = DataBase<ResearchDef>.Find("IndustrialChemistry");
        ResearchDef machineryResearch = DataBase<ResearchDef>.Find("PrecisionManufacturing");
        ResearchDef coking = DataBase<ResearchDef>.Find("Coking");
        ResearchDef concrete = DataBase<ResearchDef>.Find("ConcreteEngineering");

        Assert.That(chemicalResearch, Is.Not.Null);
        Assert.That(machineryResearch, Is.Not.Null);
        Assert.That(coking, Is.Not.Null);
        Assert.That(concrete, Is.Not.Null);

        for (int i = 0; i < DataBase<ResearchDef>.All.Count; i++)
        {
            ResearchDef research = DataBase<ResearchDef>.All[i];
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
        ResearchDef chemicalResearch = DataBase<ResearchDef>.Find("IndustrialChemistry");
        ResearchDef machineryResearch = DataBase<ResearchDef>.Find("PrecisionManufacturing");
        ResearchDef coking = DataBase<ResearchDef>.Find("Coking");
        ResearchDef concrete = DataBase<ResearchDef>.Find("ConcreteEngineering");

        Assert.That(chemicalResearch, Is.Not.Null);
        Assert.That(machineryResearch, Is.Not.Null);
        Assert.That(coking, Is.Not.Null);
        Assert.That(concrete, Is.Not.Null);

        for (int i = 0; i < DataBase<WorkshopUpgradeDef>.All.Count; i++)
        {
            WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.All[i];
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
        Assert.That(FindRate(DataBase<BuildingDef>.Find("ChemicalPlant").ResourceGenerationRates, "Chemical"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(DataBase<BuildingDef>.Find("MachineFactory").ResourceGenerationRates, "Machinery"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(DataBase<BuildingDef>.Find("BuildingMaterialsComplex").ResourceGenerationRates, "Glass"),
            Is.GreaterThan(0d));
        Assert.That(CountConsumerUses("Chemical"), Is.GreaterThan(0));
        Assert.That(CountConsumerUses("Machinery"), Is.GreaterThan(0));
        Assert.That(CountConsumerUses("Glass"), Is.GreaterThan(0));
    }

    private static bool HasResearchPrerequisiteTransitively(ResearchDef research, ResearchDef target)
    {
        return HasResearchPrerequisiteTransitively(
            research, target, new HashSet<ResearchDef>());
    }

    private static bool HasResearchPrerequisiteTransitively(
        ResearchDef research,
        ResearchDef target,
        HashSet<ResearchDef> visited)
    {
        if (research == null || target == null || !visited.Add(research))
            return false;

        for (int i = 0; i < research.Prerequisites.Count; i++)
        {
            ResearchDef prerequisite = research.Prerequisites[i];
            if (prerequisite == target ||
                HasResearchPrerequisiteTransitively(prerequisite, target, visited))
                return true;
        }

        return false;
    }

    private static bool HasWorkshopPrerequisiteTransitively(
        WorkshopUpgradeDef workshop,
        ResearchDef target)
    {
        return HasWorkshopPrerequisiteTransitively(
            workshop, target, new HashSet<WorkshopUpgradeDef>());
    }

    private static bool HasWorkshopPrerequisiteTransitively(
        WorkshopUpgradeDef workshop,
        ResearchDef target,
        HashSet<WorkshopUpgradeDef> visited)
    {
        if (workshop == null || target == null || !visited.Add(workshop))
            return false;

        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
        {
            ResearchDef prerequisite = workshop.RequiredResearch[i];
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
        for (int i = 0; i < DataBase<ResearchDef>.All.Count; i++)
            if (ContainsResource(DataBase<ResearchDef>.All[i].ResourceRequirements, resourceId))
                count++;
        for (int i = 0; i < DataBase<WorkshopUpgradeDef>.All.Count; i++)
            if (ContainsResource(DataBase<WorkshopUpgradeDef>.All[i].ResourceRequirements, resourceId))
                count++;
        return count;
    }

    private static bool ContainsResource(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return true;
        }
        return false;
    }

    private static double FindRate(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second.ToDouble();
        }
        return 0d;
    }

    private static bool HasBuildingEffect(
        WorkshopUpgradeDef workshop,
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
        WorkshopUpgradeDef workshop =
            DataBase<WorkshopUpgradeDef>.Find(workshopId);
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
        ResearchDef research = DataBase<ResearchDef>.Find(researchId);
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.Find(workshopId);
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
        System.Collections.Generic.IReadOnlyList<ResearchDef> prerequisites,
        ResearchDef target)
    {
        if (prerequisites == null || target == null)
            return false;
        for (int i = 0; i < prerequisites.Count; i++)
            if (prerequisites[i] == target)
                return true;
        return false;
    }

    [Test]
    public void 生产循环验证必须一次性报告所有独立循环()
    {
        ResourceDef[] resources =
        {
            CreateTestResource("CycleA"), CreateTestResource("CycleB"), CreateTestResource("CycleC"),
            CreateTestResource("CycleD"), CreateTestResource("CycleE")
        };
        BuildingDef[] buildings =
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

    private static ResourceDef CreateTestResource(string id)
    {
        ResourceDef resource = ScriptableObject.CreateInstance<ResourceDef>();
        resource.SetIdForEditor(id);
        return resource;
    }

    private static BuildingDef CreateCycleBuilding(string id, ResourceDef input, ResourceDef output)
    {
        BuildingDef building = ScriptableObject.CreateInstance<BuildingDef>();
        building.SetIdForEditor(id);
        building.ConfigureEconomyForEditor(
            new ExpantaNum("1.15"), ExpantaNum.One, ExpantaNum.One, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>>
            {
                new Pair<ResourceDef, ExpantaNum>(output, ExpantaNum.One)
            },
            new List<Pair<ResourceDef, ExpantaNum>>
            {
                new Pair<ResourceDef, ExpantaNum>(input, ExpantaNum.One)
            });
        return building;
    }
}
