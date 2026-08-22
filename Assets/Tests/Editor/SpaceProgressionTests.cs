using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class SpaceProgressionTests
{
    [Test]
    public void CoreOrbitalFacilitiesRequireAdvancedMaterialsAndContinuousOperations()
    {
        string[] buildingIds =
        {
            "OrbitalStation",
            "OrbitalHabitatMegastructure",
            "Shipyard"
        };

        for (int i = 0; i < buildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(buildingIds[i]);
            Assert.That(building, Is.Not.Null, buildingIds[i]);
            Assert.That(building.TechLevel, Is.EqualTo(TechLevel.Spacer));
            Assert.That(building.SpaceCost, Is.GreaterThanOrEqualTo(new ExpantaNum(400d)));
            Assert.That(building.ProductivityConsumption,
                Is.GreaterThanOrEqualTo(new ExpantaNum(500d)));
            Assert.That(building.PowerConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(building.LogisticsConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(ContainsResource(building.ResourceRequirements, "TitaniumAlloy"), Is.True);
            Assert.That(ContainsResource(building.ResourceRequirements, "Composite"), Is.True);
            Assert.That(HasBuildingResourceConsumption(building.Id, "TitaniumAlloy"), Is.True);
            Assert.That(HasBuildingResourceConsumption(building.Id, "Composite"), Is.True);
        }
    }

    [Test]
    public void ResearchPowerBuildingsFormOneContinuousUpgradeChain()
    {
        string[] chainIds =
        {
            "KnowledgeCircle", "ScribeHut", "Library", "Academy", "University",
            "DeepSpaceObservatory", "QuantumComputingArray", "InterstellarTheoryNexus"
        };

        for (int i = 0; i < chainIds.Length - 1; i++)
        {
            Building current = DataBase<Building>.Find(chainIds[i]);
            Building next = DataBase<Building>.Find(chainIds[i + 1]);
            Assert.That(current, Is.Not.Null, chainIds[i]);
            Assert.That(next, Is.Not.Null, chainIds[i + 1]);
            Assert.That(current.UpgradeTo, Is.SameAs(next),
                $"研究力建筑链断裂：{chainIds[i]} -> {chainIds[i + 1]}");
        }

        foreach (Building building in DataBase<Building>.All)
        {
            if (building.ResearchPowerGranted <= ExpantaNum.Zero)
                continue;
            Assert.That(chainIds, Does.Contain(building.Id),
                $"非研究力建筑链成员仍提供研究力：{building.Id}");
        }
    }

    [Test]
    public void IndustrialProgressionAuditReachesTheUltraTransition()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.EqualTo(TechLevel.Ultra), result.FormatFailureReport());
        Assert.That(result.UnreachableResearch, Is.Empty, result.FormatFailureReport());
        Assert.That(result.UnreachableBuildings, Is.Empty, result.FormatFailureReport());
    }

    [Test]
    public void SpacerResearchMustHavePhysicalImplementationUnlessItIsTheoryOnly()
    {
        string[] theoryOnlyIds = { "FirstContact", "InterstellarNavigation", "PrecisionMedicine" };
        Research[] spacerResearch = DataBase<Research>.All
            .Where(research => research != null && research.TechLevel == TechLevel.Spacer)
            .ToArray();
        WorkshopUpgrade[] spacerWorkshops = DataBase<WorkshopUpgrade>.All
            .Where(workshop => workshop != null && workshop.TechLevel == TechLevel.Spacer)
            .ToArray();

        for (int i = 0; i < spacerResearch.Length; i++)
        {
            Research research = spacerResearch[i];
            if (research.Id == "GravitationalCommunicationTheory")
                continue;
            if (theoryOnlyIds.Contains(research.Id))
                continue;

            Assert.That(
                spacerWorkshops.Any(workshop =>
                    workshop.RequiredResearch != null &&
                    workshop.RequiredResearch.Contains(research)),
                Is.True,
                research.Id);
        }

        for (int i = 0; i < theoryOnlyIds.Length; i++)
            Assert.That(spacerResearch.Any(research => research.Id == theoryOnlyIds[i]), Is.True, theoryOnlyIds[i]);
    }

    [Test]
    public void 高级材料研究与工坊必须先经过对应理论研究()
    {
        Research phantomMaterials = DataBase<Research>.Find("PhantomMaterials");
        Research phaseMaterialEngineering = DataBase<Research>.Find("PhaseMaterialEngineering");
        Research[] spacerResearch = DataBase<Research>.All
            .Where(research => research != null && research.TechLevel == TechLevel.Spacer)
            .ToArray();
        WorkshopUpgrade[] spacerWorkshops = DataBase<WorkshopUpgrade>.All
            .Where(workshop => workshop != null && workshop.TechLevel == TechLevel.Spacer)
            .ToArray();

        for (int i = 0; i < spacerResearch.Length; i++)
        {
            Research research = spacerResearch[i];
            if (HasResourceRequirement(research, "PhaseMaterial"))
                Assert.That(ResearchDependsOn(
                    research,
                    phaseMaterialEngineering.Id,
                    new System.Collections.Generic.HashSet<Research>()), Is.True, research.Id);
            if (HasResourceRequirement(research, "PhantomAlloy") ||
                HasResourceRequirement(research, "PhantomWeave"))
                Assert.That(ResearchDependsOn(
                    research,
                    phantomMaterials.Id,
                    new System.Collections.Generic.HashSet<Research>()), Is.True, research.Id);
        }

        for (int i = 0; i < spacerWorkshops.Length; i++)
        {
            WorkshopUpgrade workshop = spacerWorkshops[i];
            if (HasWorkshopResourceRequirement(workshop, "PhaseMaterial"))
                Assert.That(WorkshopResearchDependsOn(workshop, phaseMaterialEngineering.Id), Is.True, workshop.Id);
            if (HasWorkshopResourceRequirement(workshop, "PhantomAlloy") ||
                HasWorkshopResourceRequirement(workshop, "PhantomWeave"))
                Assert.That(WorkshopResearchDependsOn(workshop, phantomMaterials.Id), Is.True, workshop.Id);
        }
    }

    [Test]
    public void SpacerResearchGraphMustRetainMeaningfulBranches()
    {
        Research[] spacerResearch = DataBase<Research>.All
            .Where(research => research != null && research.TechLevel == TechLevel.Spacer)
            .ToArray();

        int rootCount = spacerResearch.Count(research =>
            research.Prerequisites == null ||
            research.Prerequisites.All(prerequisite =>
                prerequisite == null || prerequisite.TechLevel != TechLevel.Spacer));
        int branchingCount = spacerResearch.Count(research =>
            spacerResearch.Count(next =>
                next.Prerequisites != null && next.Prerequisites.Contains(research)) >= 2);

        Assert.That(rootCount, Is.GreaterThan(0));
        Assert.That(branchingCount, Is.GreaterThan(0));
        Assert.That(spacerResearch.Any(research => research.Id == "OrbitalEngineering"), Is.True);
        Assert.That(spacerResearch.Any(research => research.Id == "InterstellarNavigation"), Is.True);
    }

    [Test]
    public void SpacerAddsDedicatedUpperReplacementsForWoodAndCoke()
    {
        Building mechanizedLumberyard = DataBase<Building>.Find("MechanizedLumberyard");
        Building agroecologyArray = DataBase<Building>.Find("OrbitalAgroecologyArray");
        Building retort = DataBase<Building>.Find("IndustrialCarbonizationRetort");
        Building carbonizationComplex = DataBase<Building>.Find("OrbitalCarbonizationComplex");

        Assert.That(mechanizedLumberyard, Is.Not.Null);
        Assert.That(agroecologyArray, Is.Not.Null);
        Assert.That(retort, Is.Not.Null);
        Assert.That(carbonizationComplex, Is.Not.Null);
        Assert.That(mechanizedLumberyard.UpgradeTo, Is.EqualTo(agroecologyArray));
        Assert.That(retort.UpgradeTo, Is.EqualTo(carbonizationComplex));
        Assert.That(agroecologyArray.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(carbonizationComplex.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(FindRate(agroecologyArray.ResourceGenerationRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(agroecologyArray.ResourceConsumptionRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(carbonizationComplex.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(FindRate(retort.ResourceGenerationRates, "Coke") * 3d));
        Assert.That(FindRate(carbonizationComplex.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(FindRate(retort.ResourceConsumptionRates, "WoodLog") * 2d));
        Assert.That(ContainsResource(carbonizationComplex.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(carbonizationComplex.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(carbonizationComplex.ResourceRequirements, "WoodLog"), Is.False);
        Assert.That(ContainsResource(carbonizationComplex.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(FindRate(carbonizationComplex.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(HasBuildingResourceConsumption("OrbitalAgroecologyArray", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalAgroecologyArray", "PhaseMaterial"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCarbonizationComplex", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCarbonizationComplex", "PhaseMaterial"), Is.True);

        Research forestryTheory = DataBase<Research>.Find("MechanizedForestry");
        Research cokingTheory = DataBase<Research>.Find("Coking");
        WorkshopUpgrade forestryEquipment = DataBase<WorkshopUpgrade>.Find("MechanizedForestryEquipment");
        Assert.That(forestryTheory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Building == agroecologyArray &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier));
        Assert.That(cokingTheory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Building == carbonizationComplex &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier));
        Assert.That(forestryEquipment.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == agroecologyArray &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier));
    }

    [Test]
    public void 每个太空建筑都必须使用高级结构材料建造并持续维护()
    {
        Building[] spacerBuildings = DataBase<Building>.All
            .Where(building => building != null && building.TechLevel == TechLevel.Spacer)
            .ToArray();

        string[] structuralMaterials =
        {
            "TitaniumAlloy",
            "Composite",
            "PhantomAlloy",
            "PhantomWeave",
            "PhaseMaterial"
        };

        for (int i = 0; i < spacerBuildings.Length; i++)
        {
            Building building = spacerBuildings[i];
            bool hasStructuralConstruction = structuralMaterials.Any(resourceId =>
                HasBuildingResourceRequirement(building.Id, resourceId));
            bool hasStructuralMaintenance = structuralMaterials.Any(resourceId =>
                HasBuildingResourceConsumption(building.Id, resourceId));

            Assert.That(hasStructuralConstruction, Is.True,
                $"太空建筑 {building.Id} 缺少高级结构材料建造成本。");
            Assert.That(hasStructuralMaintenance, Is.True,
                $"太空建筑 {building.Id} 缺少高级结构材料持续维护消耗。");
        }
    }

    [Test]
    public void OrbitalForestryAndCarbonizationRequireAdvancedStructuralMaintenance()
    {
        Building agroecology = DataBase<Building>.Find("OrbitalAgroecologyArray");
        Building carbonization = DataBase<Building>.Find("OrbitalCarbonizationComplex");

        Assert.That(agroecology, Is.Not.Null);
        Assert.That(carbonization, Is.Not.Null);
        Assert.That(HasBuildingResourceConsumption("OrbitalAgroecologyArray", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalAgroecologyArray", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCarbonizationComplex", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCarbonizationComplex", "Composite"), Is.True);
        Assert.That(GetBuildingResourceConsumption("OrbitalAgroecologyArray", "TitaniumAlloy"),
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(GetBuildingResourceConsumption("OrbitalCarbonizationComplex", "Composite"),
            Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void 木材与焦炭必须保留工业到太空的连续上位替代链()
    {
        Building lumberyard = DataBase<Building>.Find("Lumberyard");
        Building mechanizedLumberyard = DataBase<Building>.Find("MechanizedLumberyard");
        Building agroecologyArray = DataBase<Building>.Find("OrbitalAgroecologyArray");
        Building cokeOven = DataBase<Building>.Find("CokeOven");
        Building retort = DataBase<Building>.Find("IndustrialCarbonizationRetort");
        Building carbonizationComplex = DataBase<Building>.Find("OrbitalCarbonizationComplex");

        Assert.That(lumberyard, Is.Not.Null);
        Assert.That(mechanizedLumberyard, Is.Not.Null);
        Assert.That(agroecologyArray, Is.Not.Null);
        Assert.That(cokeOven, Is.Not.Null);
        Assert.That(retort, Is.Not.Null);
        Assert.That(carbonizationComplex, Is.Not.Null);
        Assert.That(lumberyard.UpgradeTo, Is.EqualTo(mechanizedLumberyard));
        Assert.That(mechanizedLumberyard.UpgradeTo, Is.EqualTo(agroecologyArray));
        Assert.That(cokeOven.UpgradeTo, Is.EqualTo(retort));
        Assert.That(retort.UpgradeTo, Is.EqualTo(carbonizationComplex));
        Assert.That(FindRate(mechanizedLumberyard.ResourceGenerationRates, "WoodLog"),
            Is.GreaterThan(FindRate(lumberyard.ResourceGenerationRates, "WoodLog") * 3d));
        Assert.That(FindRate(agroecologyArray.ResourceGenerationRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(FindRate(cokeOven.ResourceGenerationRates, "Coke") * 2d));
        Assert.That(FindRate(carbonizationComplex.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(FindRate(retort.ResourceGenerationRates, "Coke") * 3d));
        Assert.That(FindRate(retort.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(carbonizationComplex.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(FindRate(retort.ResourceConsumptionRates, "WoodLog") * 2d));
        Assert.That(HasBuildingResourceConsumption("CokeOven", "Coal"), Is.True);
        Assert.That(ContainsResource(carbonizationComplex.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(carbonizationComplex.ResourceRequirements, "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalAgroecologyArray", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCarbonizationComplex", "PhaseMaterial"), Is.True);
    }

    [Test]
    public void 轨道纺织制造阵列必须升级工业纺织厂并使用高阶材料()
    {
        Building lower = DataBase<Building>.Find("MechanizedTextileMill");
        Building upper = DataBase<Building>.Find("OrbitalResourceExtractionArray");
        Research theory = DataBase<Research>.Find("OrbitalTextileFabrication");
        WorkshopUpgrade looms = DataBase<WorkshopUpgrade>.Find("OrbitalTextileLooms");

        Assert.That(lower, Is.Not.Null);
        Assert.That(upper, Is.Not.Null);
        Assert.That(theory, Is.Not.Null);
        Assert.That(looms, Is.Not.Null);
        Assert.That(lower.UpgradeTo, Is.EqualTo(upper));
        Assert.That(upper.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(FindRate(upper.ResourceGenerationRates, "Cloth"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "Cloth") * 4d));
        Assert.That(FindRate(upper.ResourceConsumptionRates, "Biomass"),
            Is.GreaterThanOrEqualTo(FindRate(lower.ResourceConsumptionRates, "Biomass") * 4d));
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "PhaseMaterial"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "PhantomWeave"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "PhaseMaterial"), Is.True);
        Assert.That(upper.SpaceCost, Is.GreaterThan(lower.SpaceCost));
        Assert.That(upper.ProductivityConsumption, Is.GreaterThan(lower.ProductivityConsumption));
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Building == upper &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier));
        Assert.That(looms.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == upper &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier));
    }

    [Test]
    public void 基础资源链使用合并后的工业上位建筑()
    {
        Assert.That(DataBase<Building>.Find("Quarry").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("IndustrialStoneworks")));
        Assert.That(DataBase<Building>.Find("StoneCuttingWorkshop").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("IndustrialStoneworks")));
        Assert.That(DataBase<Building>.Find("ClayPit").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("AdvancedCeramicsPlant")));
        Assert.That(DataBase<Building>.Find("MetalMine").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("RareMetalMine")));
        Assert.That(DataBase<Building>.Find("CoalMine").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("MechanizedCoalMine")));
        Assert.That(DataBase<Building>.Find("Farm").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("IrrigationWorks")));
        Assert.That(DataBase<Building>.Find("IrrigationWorks").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("PlantingField")));
        Assert.That(DataBase<Building>.Find("FiberGatheringCamp").UpgradeTo,
            Is.EqualTo(DataBase<Building>.Find("PlantingField")));
    }

    [Test]
    public void 每个太空研究与工坊都必须改变后期玩法()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            if (research == null || research.TechLevel != TechLevel.Spacer)
                continue;

            Assert.That(research.Effects, Is.Not.Null.And.Not.Empty, research.Id);
            for (int i = 0; i < research.Effects.Count; i++)
                Assert.That(research.Effects[i], Is.Not.Null, research.Id);
        }

        foreach (WorkshopUpgrade workshop in DataBase<WorkshopUpgrade>.All)
        {
            if (workshop == null || workshop.TechLevel != TechLevel.Spacer)
                continue;

            Assert.That(workshop.Effects, Is.Not.Null.And.Not.Empty, workshop.Id);
            for (int i = 0; i < workshop.Effects.Count; i++)
                Assert.That(workshop.Effects[i], Is.Not.Null, workshop.Id);
        }
    }

    [Test]
    public void 工业到太空研究链完整()
    {
        Research orbitalEngineering = Resources.Load<Research>("Datas/Research/Spacer/OrbitalEngineering");
        Research orbitalHabitation = Resources.Load<Research>("Datas/Research/Spacer/OrbitalHabitation");
        Research deepSpaceShipbuilding = Resources.Load<Research>("Datas/Research/Spacer/DeepSpaceShipbuilding");
        Research phaseMaterialEngineering = Resources.Load<Research>("Datas/Research/Spacer/PhaseMaterialEngineering");
        Research phaseFieldNavigation = Resources.Load<Research>("Datas/Research/Spacer/PhaseFieldNavigation");

        Assert.That(orbitalEngineering, Is.Not.Null, "轨道工程研究必须存在。");
        Assert.That(orbitalHabitation, Is.Not.Null, "轨道空间站工程研究必须存在。");
        Assert.That(deepSpaceShipbuilding, Is.Not.Null, "深空舰船制造研究必须存在。");
        Assert.That(phaseFieldNavigation, Is.Not.Null, "相位航行研究必须存在。");
        Assert.That(orbitalEngineering.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbitalHabitation.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(deepSpaceShipbuilding.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(phaseMaterialEngineering, Is.Not.Null);
        Assert.That(phaseMaterialEngineering.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Research interstellarNavigation = Resources.Load<Research>("Datas/Research/Spacer/InterstellarNavigation");
        Assert.That(interstellarNavigation, Is.Not.Null);
        Assert.That(interstellarNavigation.AdvancesTechLevel, Is.True);
        Assert.That(HasResourceRequirement(interstellarNavigation, "CopperWire"), Is.True);
        Assert.That(HasResourceRequirement(interstellarNavigation, "Electronics"), Is.True);

        Assert.That(HasPrerequisite(orbitalEngineering, "PowerGridEngineering"), Is.True);
        Assert.That(HasPrerequisite(orbitalEngineering, "CombustionEngines"), Is.True);
        Assert.That(HasPrerequisite(orbitalHabitation, "OrbitalEngineering"), Is.True);
        Assert.That(HasPrerequisite(orbitalHabitation, "FirstContact"), Is.False);
        Assert.That(HasPrerequisite(orbitalHabitation, "IndustrialHabitationEngineering"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("FirstContact"), "Electronics"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("FirstContact"), "CopperWire"), Is.True);
        Research firstContact = DataBase<Research>.Find("FirstContact");
        Assert.That(firstContact, Is.Not.Null);
        Assert.That(firstContact.Effects.Count(effect =>
            effect != null && effect.Type == ResearchEffectType.UnlockFirstContact), Is.EqualTo(1));
        Assert.That(HasPrerequisite(deepSpaceShipbuilding, "OrbitalHabitation"), Is.True);
        Assert.That(HasPrerequisite(deepSpaceShipbuilding, "DeepSpaceFleet"), Is.False);
        Assert.That(HasResourceRequirement(deepSpaceShipbuilding, "Biomass"), Is.False);
        Assert.That(HasPrerequisite(DataBase<Research>.Find("DeepSpaceFleet"), "LogisticsManagement"), Is.True);
        Assert.That(ResearchDependsOn(
            phaseFieldNavigation,
            "PhantomMaterials",
            new System.Collections.Generic.HashSet<Research>()), Is.True);
        Assert.That(ResearchDependsOn(
            phaseFieldNavigation,
            "PhaseMaterialEngineering",
            new System.Collections.Generic.HashSet<Research>()), Is.True);
        Assert.That(HasWorkshopResearch(
            DataBase<WorkshopUpgrade>.Find("PhaseMaterialCalibration"),
            "PhaseMaterialEngineering"), Is.True);
        Assert.That(DataBase<Research>.Find("OrbitalLogisticsInfrastructure").Description, Is.Not.Empty);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Machinery"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Engine"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Chemical"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("OrbitalLogisticsInfrastructure"), "Biomass"), Is.False);
        Assert.That(HasResourceRequirement(deepSpaceShipbuilding, "Chemical"), Is.True);

        Assert.That(HasBuildingEffectType(
            orbitalEngineering,
            "DeepSpaceObservatory",
            ResearchEffectType.BuildingResearchPowerMultiplier), Is.True);
        Assert.That(HasBuildingEffectType(
            deepSpaceShipbuilding,
            "Shipyard",
            ResearchEffectType.MilitaryMultiplier), Is.True);
        Assert.That(HasBuildingEffectType(
            phaseFieldNavigation,
            "DeepSpaceRelay",
            ResearchEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "Steel"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "PhaseMaterial"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "Glass"), Is.True);
        Assert.That(GetBuildingResourceConsumption("DeepSpaceRelay", "Composite"),
            Is.GreaterThanOrEqualTo(new ExpantaNum("0.02")));
        Building relay = DataBase<Building>.Find("DeepSpaceRelay");
        Building station = DataBase<Building>.Find("OrbitalStation");
        Assert.That(relay.SpaceCost, Is.EqualTo(new ExpantaNum(520)));
        Assert.That(relay.ProductivityConsumption, Is.EqualTo(new ExpantaNum(680)));
        Assert.That(relay.PowerConsumptionRate, Is.EqualTo(new ExpantaNum(150)));
        Assert.That(relay.LogisticsConsumptionRate, Is.EqualTo(new ExpantaNum(48)));
    }

    [Test]
    public void OrbitalStationCarriesTheMergedInfrastructureFootprint()
    {
        Building station = DataBase<Building>.Find("OrbitalStation");

        Assert.That(station, Is.Not.Null);
        Assert.That(station.SpaceCost, Is.GreaterThanOrEqualTo(new ExpantaNum(700)));
        Assert.That(station.ProductivityConsumption, Is.GreaterThanOrEqualTo(new ExpantaNum(1200)));
        Assert.That(station.PowerConsumptionRate, Is.GreaterThanOrEqualTo(new ExpantaNum(200)));
        Assert.That(station.LogisticsProductionRate, Is.GreaterThan(new ExpantaNum(200)));
        Assert.That(station.LogisticsConsumptionRate, Is.GreaterThanOrEqualTo(new ExpantaNum(50)));
        Assert.That(station.FoodCapacityGranted, Is.GreaterThanOrEqualTo(new ExpantaNum(30000)));
    }

    [Test]
    public void SpacerEarlyRouteBuildsLogisticsAndShipsBeforeTheFleet()
    {
        Research fleet = DataBase<Research>.Find("DeepSpaceFleet");
        Research logistics = DataBase<Research>.Find("OrbitalLogisticsInfrastructure");
        Research survey = DataBase<Research>.Find("DeepSpaceSurvey");

        Assert.That(fleet, Is.Not.Null);
        Assert.That(logistics, Is.Not.Null);
        Assert.That(survey, Is.Not.Null);
        Assert.That(HasPrerequisite(fleet, "DeepSpaceShipbuilding"), Is.True);
        Assert.That(HasPrerequisite(fleet, "OrbitalLogisticsInfrastructure"), Is.True);
        Assert.That(HasPrerequisite(logistics, "DeepSpaceFleet"), Is.False);
        Assert.That(HasPrerequisite(survey, "QuantumComputing"), Is.False);
        Assert.That(HasPrerequisite(survey, "OrbitalEngineering"), Is.True);
        Assert.That(HasPrerequisite(survey, "OrbitalLogisticsInfrastructure"), Is.False);

        ExpantaNum supportedPower = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(100),
            new ExpantaNum(100),
            new ExpantaNum(100),
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.Zero);
        ExpantaNum undersuppliedPower = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(100),
            new ExpantaNum(100),
            new ExpantaNum(100),
            new ExpantaNum(0.5d),
            new ExpantaNum(0.5d),
            new ExpantaNum(0.5d),
            ExpantaNum.One,
            ExpantaNum.Zero);
        Assert.That(supportedPower, Is.GreaterThan(undersuppliedPower));
    }

    [Test]
    public void 星际补给链理论与自动化模块必须降低持续远征补给消耗()
    {
        Research theory = DataBase<Research>.Find("InterstellarSupplyChainTheory");
        WorkshopUpgrade modules = DataBase<WorkshopUpgrade>.Find("AutomatedFleetResupplyModules");

        Assert.That(theory, Is.Not.Null);
        Assert.That(modules, Is.Not.Null);
        Assert.That(theory.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(modules.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(theory.Effects.Any(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.CampaignSupplyCostMultiplier &&
            effect.NumericValue <= new ExpantaNum(0.9d)), Is.True);
        Assert.That(modules.Effects.Any(effect =>
            effect != null &&
            effect.Type == WorkshopEffectType.CampaignSupplyCostMultiplier &&
            effect.NumericValue <= new ExpantaNum(0.9d)), Is.True);
        Assert.That(HasPrerequisite(theory, "InterstellarLogisticsDoctrine"), Is.True);
        Assert.That(HasPrerequisite(theory, "InterstellarCombatLogistics"), Is.True);
        Assert.That(modules.RequiredResearch.Any(research => research != null &&
            research.Id == "InterstellarSupplyChainTheory"), Is.True);
        Assert.That(modules.RequiredUpgrades.Any(upgrade => upgrade != null &&
            upgrade.Id == "InterstellarSupplyDoctrine"), Is.True);
        Assert.That(HasResourceRequirement(theory, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceRequirement(theory, "PhaseMaterial"), Is.True);
        Assert.That(HasResourceRequirement(theory, "Rubber"), Is.True);
        WorkshopUpgrade doctrine = DataBase<WorkshopUpgrade>.Find("InterstellarSupplyDoctrine");
        Assert.That(doctrine, Is.Not.Null);
        Assert.That(HasWorkshopResourceRequirement(doctrine, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(modules, "PhantomWeave"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(modules, "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(modules, "Machinery"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(modules, "Electronics"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(modules, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(modules, "Lubricant"), Is.True);
        Assert.That(GetWorkshopResourceRequirement(modules, "Machinery"), Is.GreaterThan(
            GetWorkshopResourceRequirement(modules, "TitaniumAlloy") * 2d));
    }

    [Test]
    public void 舰队损伤控制分支必须区分战损与维修成本并使用高级材料()
    {
        Research theory = DataBase<Research>.Find("FleetDamageControlTheory");
        WorkshopUpgrade systems = DataBase<WorkshopUpgrade>.Find("AdaptiveArmorRepairSystems");

        Assert.That(theory, Is.Not.Null);
        Assert.That(systems, Is.Not.Null);
        Assert.That(theory.Effects.Any(effect => effect != null &&
            effect.Type == ResearchEffectType.CampaignCasualtyMultiplier &&
            effect.NumericValue <= new ExpantaNum(0.9d)), Is.True);
        Assert.That(systems.Effects.Any(effect => effect != null &&
            effect.Type == WorkshopEffectType.CampaignCasualtyMultiplier &&
            effect.NumericValue <= new ExpantaNum(0.9d)), Is.True);
        Assert.That(HasPrerequisite(theory, "InterstellarCombatLogistics"), Is.True);
        Assert.That(systems.RequiredResearch.Any(research => research != null &&
            research.Id == "FleetDamageControlTheory"), Is.True);
        Assert.That(HasResourceRequirement(theory, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "PhantomWeave"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "Machinery"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "Lubricant"), Is.True);
        Assert.That(GetWorkshopResourceRequirement(systems, "Machinery"), Is.GreaterThan(
            GetWorkshopResourceRequirement(systems, "TitaniumAlloy")));
    }

    [Test]
    public void 轨道资源提取阵列必须替代工业多金属矿场并持续消耗运行资源()
    {
        Building lower = DataBase<Building>.Find("RareMetalMine");
        Building upper = DataBase<Building>.Find("OrbitalResourceExtractionArray");
        Research theory = DataBase<Research>.Find("AutonomousOrbitalMining");
        WorkshopUpgrade systems = DataBase<WorkshopUpgrade>.Find("AutonomousOrbitalMiningSystems");

        Assert.That(lower, Is.Not.Null);
        Assert.That(upper, Is.Not.Null);
        Assert.That(theory, Is.Not.Null);
        Assert.That(systems, Is.Not.Null);
        Assert.That(lower.UpgradeTo, Is.EqualTo(upper));
        Assert.That(upper.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(FindRate(upper.ResourceGenerationRates, "CopperOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "CopperOre") * 8d));
        Assert.That(FindRate(upper.ResourceGenerationRates, "TinOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "TinOre") * 8d));
        Assert.That(FindRate(upper.ResourceGenerationRates, "IronOre"),
            Is.GreaterThan(FindRate(lower.ResourceGenerationRates, "IronOre") * 8d));
        Assert.That(FindRate(upper.ResourceGenerationRates, "TitaniumConcentrate"),
            Is.EqualTo(9d).Within(0.0001d));
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "Explosives"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "Lubricant"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "Composite"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "Machinery"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "Electronics"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "Ceramic"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "PhantomAlloy"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "PhaseMaterial"), Is.True);
        Assert.That(ContainsResource(upper.ResourceRequirements, "CopperOre"), Is.False);
        Assert.That(ContainsResource(upper.ResourceRequirements, "TinOre"), Is.False);
        Assert.That(ContainsResource(upper.ResourceRequirements, "IronOre"), Is.False);
        Assert.That(ContainsResource(upper.ResourceRequirements, "BauxiteOre"), Is.False);
        Assert.That(ContainsResource(upper.ResourceRequirements, "NickelConcentrate"), Is.False);
        Assert.That(ContainsResource(upper.ResourceRequirements, "TitaniumConcentrate"), Is.False);
        Assert.That(upper.SpaceCost, Is.GreaterThan(lower.SpaceCost));
        Assert.That(upper.ProductivityConsumption, Is.GreaterThan(lower.ProductivityConsumption));
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Building == upper &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier));
        Assert.That(systems.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == upper &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier));
    }

    [Test]
    public void 轨道采出资源必须接入明确的下游冶金节点()
    {
        Building extraction = DataBase<Building>.Find("OrbitalResourceExtractionArray");
        Assert.That(extraction, Is.Not.Null);

        string[,] downstreamRoutes =
        {
            { "TitaniumConcentrate", "TitaniumMetallurgicalComplex" },
            { "NickelConcentrate", "NickelRefinery" },
            { "BauxiteOre", "AluminumSmelter" },
            { "CopperOre", "IndustrialMetalSmelter" },
            { "TinOre", "IndustrialMetalSmelter" },
            { "IronOre", "IndustrialMetalSmelter" }
        };

        for (int i = 0; i < downstreamRoutes.GetLength(0); i++)
        {
            string resourceId = downstreamRoutes[i, 0];
            string buildingId = downstreamRoutes[i, 1];
            Building downstream = DataBase<Building>.Find(buildingId);
            Assert.That(
                FindRate(extraction.ResourceGenerationRates, resourceId),
                Is.GreaterThan(0d),
                $"轨道阵列必须产出 {resourceId}。");
            Assert.That(downstream, Is.Not.Null, buildingId);
            Assert.That(
                FindRate(downstream.ResourceConsumptionRates, resourceId),
                Is.GreaterThan(0d),
                $"{resourceId} 必须进入 {buildingId} 的持续冶金消耗。");
        }
    }

    [Test]
    public void 轨道真空冶金阵列不应使用采出精矿作为建造材料()
    {
        Building metallurgy = DataBase<Building>.Find("OrbitalResourceExtractionArray");
        Assert.That(metallurgy, Is.Not.Null);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "Machinery"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "Ceramic"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "PhantomAlloy"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "PhaseMaterial"), Is.True);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "TitaniumConcentrate"), Is.False);
        Assert.That(ContainsResource(metallurgy.ResourceRequirements, "NickelConcentrate"), Is.False);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "TitaniumConcentrate"), Is.False);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "NickelConcentrate"), Is.False);
    }

    [Test]
    public void 轨道工业设施不得把原矿或精矿作为建造材料()
    {
        string[] orbitalIndustrialBuildings =
        {
            "OrbitalResourceExtractionArray",
            "OrbitalCryogenicPropellantArray",
            "OrbitalCarbonizationComplex"
        };
        string[] rawExtractionResources =
        {
            "CopperOre",
            "TinOre",
            "IronOre",
            "BauxiteOre",
            "NickelConcentrate",
            "TitaniumConcentrate"
        };

        for (int buildingIndex = 0; buildingIndex < orbitalIndustrialBuildings.Length; buildingIndex++)
        {
            Building building = DataBase<Building>.Find(orbitalIndustrialBuildings[buildingIndex]);
            Assert.That(building, Is.Not.Null, orbitalIndustrialBuildings[buildingIndex]);
            for (int resourceIndex = 0; resourceIndex < rawExtractionResources.Length; resourceIndex++)
                Assert.That(
                    ContainsResource(building.ResourceRequirements, rawExtractionResources[resourceIndex]),
                    Is.False,
                    $"{building.Id} 不应使用采出原矿或精矿建造。");
        }
    }

    [Test]
    public void Spacer高级材料必须都有来源和长期战略消耗()
    {
        Building titaniumWorks = DataBase<Building>.Find("TitaniumMetallurgicalComplex");
        Building machineFactory = DataBase<Building>.Find("MachineFactory");
        Building phantomFabricator = DataBase<Building>.Find("PhantomMaterialsFabricator");
        Building phaseSynthesis = DataBase<Building>.Find("PhaseMaterialSynthesisArray");
        Building orbitalStation = DataBase<Building>.Find("OrbitalStation");
        Building orbitalResourceArray = DataBase<Building>.Find("OrbitalResourceExtractionArray");
        Building habitat = DataBase<Building>.Find("OrbitalHabitatMegastructure");
        Research occupationAdministration =
            DataBase<Research>.Find("InterstellarOccupationAdministration");

        Assert.That(titaniumWorks, Is.Not.Null);
        Assert.That(machineFactory, Is.Not.Null);
        Assert.That(phantomFabricator, Is.Not.Null);
        Assert.That(phaseSynthesis, Is.Not.Null);
        Assert.That(orbitalStation, Is.Not.Null);
        Assert.That(orbitalResourceArray, Is.Not.Null);
        Assert.That(habitat, Is.Not.Null);
        Assert.That(occupationAdministration, Is.Not.Null);

        Assert.That(FindRate(titaniumWorks.ResourceGenerationRates, "TitaniumAlloy"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(machineFactory.ResourceGenerationRates, "Composite"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(phantomFabricator.ResourceGenerationRates, "PhantomAlloy"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(phantomFabricator.ResourceGenerationRates, "PhantomWeave"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(phaseSynthesis.ResourceGenerationRates, "PhaseMaterial"),
            Is.GreaterThan(0d));

        Assert.That(ContainsResource(orbitalStation.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(orbitalStation.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(orbitalResourceArray.ResourceRequirements, "PhantomWeave"), Is.True);
        Assert.That(ContainsResource(habitat.ResourceRequirements, "PhaseMaterial"), Is.True);
        Assert.That(HasResourceRequirement(occupationAdministration, "PhantomAlloy"), Is.True);
        Assert.That(HasResourceRequirement(occupationAdministration, "PhaseMaterial"), Is.True);
    }

    [Test]
    public void IndustrialFuelAndChemicalChainKeepsCoalCokeAndExplosivesInContinuousUse()
    {
        Building coalMine = DataBase<Building>.Find("CoalMine");
        Building mechanizedCoalMine = DataBase<Building>.Find("MechanizedCoalMine");
        Building cokeOven = DataBase<Building>.Find("CokeOven");
        Building retort = DataBase<Building>.Find("IndustrialCarbonizationRetort");
        Building chemicalPlant = DataBase<Building>.Find("ChemicalPlant");

        Assert.That(coalMine, Is.Not.Null);
        Assert.That(mechanizedCoalMine, Is.Not.Null);
        Assert.That(cokeOven, Is.Not.Null);
        Assert.That(retort, Is.Not.Null);
        Assert.That(chemicalPlant, Is.Not.Null);
        Assert.That(FindRate(mechanizedCoalMine.ResourceGenerationRates, "Coal"),
            Is.GreaterThan(FindRate(coalMine.ResourceGenerationRates, "Coal") * 4d));
        Assert.That(coalMine.UpgradeTo, Is.EqualTo(mechanizedCoalMine));
        Assert.That(ContainsResource(mechanizedCoalMine.ResourceRequirements, "Coke"), Is.True);
        Assert.That(ContainsResource(mechanizedCoalMine.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(FindRate(mechanizedCoalMine.ResourceConsumptionRates, "Explosives"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(cokeOven.ResourceConsumptionRates, "Coal"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(cokeOven.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(retort.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(0d));
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
    public void 炸药必须作为采掘与土木设备的持续消耗而不是战争建造费()
    {
        string[] extractionConsumers =
        {
            "MechanizedCoalMine",
            "IndustrialStoneworks",
            "AdvancedCeramicsPlant",
            "IndustrialOilExtractionComplex",
            "OrbitalResourceExtractionArray"
        };

        for (int i = 0; i < extractionConsumers.Length; i++)
            Assert.That(HasBuildingResourceConsumption(extractionConsumers[i], "Explosives"), Is.True,
                $"建筑 {extractionConsumers[i]} 应持续消耗炸药。");

        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "Explosives"), Is.False);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceObservatory", "Explosives"), Is.False);
    }

    [Test]
    public void SpacerFrontierStartsBeforePhaseMaterialProduction()
    {
        Research deepSpaceFleet = DataBase<Research>.Find("DeepSpaceFleet");
        Building shipyard = DataBase<Building>.Find("Shipyard");
        Resource phaseMaterial = DataBase<Resource>.Find("PhaseMaterial");
        Resource titaniumAlloy = DataBase<Resource>.Find("TitaniumAlloy");

        Assert.That(deepSpaceFleet, Is.Not.Null);
        Assert.That(shipyard, Is.Not.Null);
        Assert.That(phaseMaterial, Is.Not.Null);
        Assert.That(titaniumAlloy, Is.Not.Null);
        Assert.That(HasResourceRequirement(deepSpaceFleet, "PhaseMaterial"), Is.False);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "PhaseMaterial"), Is.False);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "TitaniumAlloy"), Is.True);
    }

    [Test]
    public void 太空建筑必须经过对应研究()
    {
        Assert.That(HasRequiredResearch("LaunchCenter", "OrbitalEngineering"), Is.True);
        Assert.That(HasRequiredResearch("OrbitalStation", "OrbitalHabitation"), Is.True);
        Assert.That(HasRequiredResearch("Shipyard", "DeepSpaceShipbuilding"), Is.True);
        Assert.That(HasRequiredResearch("DeepSpaceRelay", "PhaseFieldNavigation"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalSolarArray", "CopperWire"), Is.True);
        Assert.That(HasBuildingResourceRequirement("DeepSpaceRelay", "Steel"), Is.True);
        Assert.That(GetBuildingResourceRequirement("DeepSpaceRelay", "Concrete"), Is.GreaterThan(
            GetBuildingResourceRequirement("DeepSpaceRelay", "PhantomWeave") * 10d));
        Assert.That(GetBuildingResourceRequirement("DeepSpaceRelay", "Steel"), Is.GreaterThan(
            GetBuildingResourceRequirement("DeepSpaceRelay", "PhantomWeave") * 6d));

        Assert.That(DataBase<Building>.Find("LaunchCenter").DefensePowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("OrbitalStation").DefensePowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("Shipyard").DefensePowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("LaunchCenter").AttackPowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("OrbitalStation").AttackPowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("Shipyard").AttackPowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("LaunchCenter").MilitaryManpowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("OrbitalStation").MilitaryManpowerGranted.ToDouble(), Is.GreaterThan(0d));
        Assert.That(DataBase<Building>.Find("Shipyard").MilitaryManpowerGranted.ToDouble(), Is.GreaterThan(0d));
    }

    [Test]
    public void ShipyardRequiresItsAutomatedAssemblyWorkshop()
    {
        Building shipyard = DataBase<Building>.Find("Shipyard");
        WorkshopUpgrade assembly = DataBase<WorkshopUpgrade>.Find("AutomatedShipyardAssembly");

        Assert.That(shipyard, Is.Not.Null);
        Assert.That(assembly, Is.Not.Null);
        Assert.That(shipyard.RequiredWorkshopUpgrades, Does.Contain(assembly));
        Assert.That(assembly.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == shipyard && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void OrbitalStationRequiresAutonomousFleetLogistics()
    {
        Building hub = DataBase<Building>.Find("OrbitalStation");
        WorkshopUpgrade logistics = DataBase<WorkshopUpgrade>.Find("AutonomousFleetLogistics");

        Assert.That(hub, Is.Not.Null);
        Assert.That(logistics, Is.Not.Null);
        Assert.That(hub.RequiredWorkshopUpgrades, Does.Contain(logistics));
        Assert.That(logistics.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == hub && effect.NumericValue.ToDouble() > 1d));
    }

    [Test]
    public void 所有太空建筑必须持续消耗高级资源()
    {
        string[] advancedResources =
        {
            "TitaniumAlloy",
            "Composite",
            "PhantomAlloy",
            "PhantomWeave",
            "PhaseMaterial"
        };

        Building[] spacerBuildings = DataBase<Building>.All
            .Where(building => building != null && building.TechLevel == TechLevel.Spacer)
            .ToArray();

        Assert.That(spacerBuildings, Is.Not.Empty);
        foreach (Building building in spacerBuildings)
        {
            Assert.That(
                advancedResources.Any(resourceId =>
                    HasBuildingResourceConsumption(building.Id, resourceId)),
                Is.True,
                $"太空建筑 {building.Id} 必须有高级资源维护消耗。");
        }
    }

    [Test]
    public void 所有太空工坊必须支付高级结构材料()
    {
        string[] advancedResources =
        {
            "TitaniumAlloy",
            "Composite",
            "PhantomAlloy",
            "PhantomWeave",
            "PhaseMaterial"
        };

        WorkshopUpgrade[] spacerWorkshops = DataBase<WorkshopUpgrade>.All
            .Where(workshop => workshop != null && workshop.TechLevel == TechLevel.Spacer)
            .ToArray();

        Assert.That(spacerWorkshops, Is.Not.Empty);
        foreach (WorkshopUpgrade workshop in spacerWorkshops)
        {
            if (workshop.Id == "ClosedLoopBiosecurityModules")
                continue;
            Assert.That(
                advancedResources.Any(resourceId =>
                    HasWorkshopResourceRequirement(workshop, resourceId)),
                Is.True,
                workshop.Id);
        }
    }

    [Test]
    public void 所有太空建筑必须具备宏大土地与生产力规模()
    {
        Building[] spacerBuildings = DataBase<Building>.All
            .Where(building => building != null && building.TechLevel == TechLevel.Spacer)
            .ToArray();

        Assert.That(spacerBuildings, Is.Not.Empty);
        foreach (Building building in spacerBuildings)
        {
            Assert.That(building.SpaceCost, Is.GreaterThanOrEqualTo(new ExpantaNum(300d)), building.Id);
            Assert.That(
                building.ProductivityConsumption,
                Is.GreaterThanOrEqualTo(new ExpantaNum(360d)),
                building.Id);
            Assert.That(
                building.PowerConsumptionRate + building.LogisticsConsumptionRate,
                Is.GreaterThan(ExpantaNum.Zero),
                building.Id);
        }
    }

    [Test]
    public void OrbitalEcologyUsesADedicatedArrayForFoodAndBiomassWithoutFoodCapacity()
    {
        Building plantingField = DataBase<Building>.Find("PlantingField");
        Building agroecologyArray = DataBase<Building>.Find("OrbitalAgroecologyArray");
        Building station = DataBase<Building>.Find("OrbitalStation");
        Building habitat = DataBase<Building>.Find("OrbitalHabitatMegastructure");
        Research agroecology = DataBase<Research>.Find("OrbitalAgroecology");
        WorkshopUpgrade agroponics = DataBase<WorkshopUpgrade>.Find("OrbitalAgroponicSystems");

        Assert.That(plantingField, Is.Not.Null);
        Assert.That(agroecologyArray, Is.Not.Null);
        Assert.That(station, Is.Not.Null);
        Assert.That(habitat, Is.Not.Null);
        Assert.That(agroecology, Is.Not.Null);
        Assert.That(agroponics, Is.Not.Null);
        Assert.That(plantingField.FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(plantingField.UpgradeTo, Is.EqualTo(agroecologyArray));
        Assert.That(agroecologyArray.FoodProductionRate, Is.EqualTo(new ExpantaNum(720d)));
        Assert.That(FindRate(agroecologyArray.ResourceGenerationRates, "Biomass"),
            Is.EqualTo(180d).Within(0.000001d));
        Assert.That(agroecologyArray.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(agroecologyArray.PowerConsumptionRate, Is.EqualTo(new ExpantaNum(335d)));
        Assert.That(agroecologyArray.LogisticsConsumptionRate, Is.EqualTo(new ExpantaNum(48d)));
        Assert.That(agroecologyArray.RequiredResearch, Does.Contain(agroecology));
        Assert.That(agroecologyArray.RequiredWorkshopUpgrades, Does.Contain(agroponics));
        Assert.That(FindRate(plantingField.ResourceGenerationRates, "Biomass"),
            Is.GreaterThan(0d));
        Assert.That(plantingField.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(station.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(habitat.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(FindRate(station.ResourceConsumptionRates, "Biomass"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(habitat.ResourceConsumptionRates, "Biomass"),
            Is.GreaterThan(FindRate(station.ResourceConsumptionRates, "Biomass")));
        Assert.That(habitat.PopulationCapacityGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(habitat.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(agroecology.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Type == ResearchEffectType.ResourceProductionMultiplier &&
            effect.Resource != null && effect.Resource.Id == "Biomass"));
        Assert.That(agroponics.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == plantingField));
    }

    [Test]
    public void OrbitalStationHasARealSpaceEraInfrastructureFootprint()
    {
        Building station = DataBase<Building>.Find("OrbitalStation");
        Assert.That(station, Is.Not.Null);
        Assert.That(station.SpaceCost.ToDouble(), Is.GreaterThanOrEqualTo(420d));
        Assert.That(station.ProductivityConsumption.ToDouble(), Is.GreaterThanOrEqualTo(520d));
        Assert.That(station.ProductivityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(station.ResearchPowerGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(station.LogisticsProductionRate, Is.EqualTo(new ExpantaNum(230)));
        Assert.That(station.FleetPowerGranted, Is.EqualTo(new ExpantaNum(150)));
        Assert.That(station.DefensePowerGranted, Is.EqualTo(new ExpantaNum(100)));
        Assert.That(station.PowerConsumptionRate.ToDouble(), Is.GreaterThanOrEqualTo(80d));
        Assert.That(station.LogisticsConsumptionRate.ToDouble(), Is.GreaterThanOrEqualTo(18d));
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "PhaseMaterial"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalStation", "Biomass"), Is.True);
        Assert.That(GetBuildingResourceRequirement("OrbitalStation", "PhaseMaterial"),
            Is.GreaterThanOrEqualTo(new ExpantaNum(600)));
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Biomass"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Electronics"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Rubber"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Cloth"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalStation", "Cloth"), Is.True);
        Assert.That(
            GetBuildingResourceRequirement("OrbitalStation", "Cloth"),
            Is.GreaterThan(GetBuildingResourceRequirement("OrbitalStation", "TitaniumAlloy") * 0.2d));
        Assert.That(
            GetBuildingResourceConsumption("OrbitalStation", "Electronics").ToDouble(),
            Is.EqualTo(0.08d).Within(0.001d));
        Assert.That(GetBuildingResourceRequirement("OrbitalStation", "Biomass"),
            Is.GreaterThan(GetBuildingResourceRequirement("OrbitalStation", "TitaniumAlloy") * 4d));
    }

    [Test]
    public void ResearchBuildingsSeparateScientificCoreFromDeepSpaceExploration()
    {
        Building university = DataBase<Building>.Find("University");
        Building observatory = DataBase<Building>.Find("DeepSpaceObservatory");
        Building quantum = DataBase<Building>.Find("QuantumComputingArray");

        Assert.That(university, Is.Not.Null);
        Assert.That(observatory, Is.Not.Null);
        Assert.That(quantum, Is.Not.Null);
        Assert.That(university.UpgradeTo, Is.EqualTo(observatory));
        Assert.That(observatory.UpgradeTo, Is.EqualTo(quantum));
        Assert.That(quantum.ResearchPowerGranted, Is.GreaterThan(observatory.ResearchPowerGranted));
        Assert.That(observatory.FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(observatory.AttackPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(observatory.DefensePowerGranted, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void OrbitalStationClothDemandHasAnIndustrialSupplyChain()
    {
        Building weavingWorkshop = DataBase<Building>.Find("WeavingWorkshop");
        WorkshopUpgrade mechanicalLooms = DataBase<WorkshopUpgrade>.Find("MechanicalLooms");

        Assert.That(weavingWorkshop, Is.Not.Null);
        Assert.That(
            weavingWorkshop.ResourceGenerationRates.Any(pair =>
                pair.First != null && pair.First.Id == "Cloth" && pair.Second > ExpantaNum.Zero),
            Is.True);
        Assert.That(
            weavingWorkshop.ResourceConsumptionRates.Any(pair =>
                pair.First != null && pair.First.Id == "Biomass" && pair.Second > ExpantaNum.Zero),
            Is.True);
        Assert.That(mechanicalLooms, Is.Not.Null);
        Assert.That(
            HasWorkshopBuildingEffectType(
                mechanicalLooms,
                "WeavingWorkshop",
                WorkshopEffectType.BuildingProductionMultiplier),
            Is.True);

        WorkshopEffectDefinition loomEffect = mechanicalLooms.Effects.First(effect =>
            effect != null &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
            effect.Building != null &&
            effect.Building.Id == "WeavingWorkshop");
        ExpantaNum clothRate = weavingWorkshop.ResourceGenerationRates
            .First(pair => pair.First != null && pair.First.Id == "Cloth")
            .Second;
        ExpantaNum clothPreparationSeconds =
            GetBuildingResourceRequirement("OrbitalStation", "Cloth") /
            (clothRate * loomEffect.NumericValue);

        Assert.That(loomEffect.NumericValue, Is.GreaterThanOrEqualTo(new ExpantaNum(1.5d)));
        Assert.That(clothPreparationSeconds, Is.LessThanOrEqualTo(new ExpantaNum(4000d)));
    }

    [Test]
    public void 太空工坊形成研究后的持续成长()
    {
        WorkshopUpgrade launchStages =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/ReusableLaunchStages");
        WorkshopUpgrade habitatSystems =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/ModularHabitatSystems");
        WorkshopUpgrade shipyardAssembly =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/AutomatedShipyardAssembly");
        WorkshopUpgrade cryogenicFuel =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/CryogenicFuelSystems");
        WorkshopUpgrade compositeLayup =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/AdvancedCompositeLayup");
        WorkshopUpgrade phaseFieldContainment =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/PhaseFieldContainment");
        WorkshopUpgrade orbitalThermalManagement =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/OrbitalThermalManagement");
        WorkshopUpgrade orbitalPowerBeaming =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/OrbitalPowerBeaming");
        WorkshopUpgrade orbitalLifeSupport =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/OrbitalLifeSupportNetworks");
        WorkshopUpgrade orbitalMining =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/AutonomousOrbitalMiningSystems");

        Assert.That(launchStages, Is.Not.Null, "可复用发射级工坊必须存在。");
        Assert.That(habitatSystems, Is.Not.Null, "模块化空间舱工坊必须存在。");
        Assert.That(shipyardAssembly, Is.Not.Null, "自动化船坞装配工坊必须存在。");
        Assert.That(cryogenicFuel, Is.Not.Null, "低温推进剂系统工坊必须存在。");
        Assert.That(compositeLayup, Is.Not.Null, "先进复合材料铺层工坊必须存在。");
        Assert.That(orbitalThermalManagement, Is.Not.Null, "轨道热管理工坊必须存在。");
        Assert.That(orbitalPowerBeaming, Is.Not.Null, "轨道能量束工坊必须存在。");
        Assert.That(orbitalLifeSupport, Is.Not.Null, "轨道生命保障网络工坊必须存在。");
        Assert.That(orbitalMining, Is.Not.Null, "轨道自主采矿系统工坊必须存在。");
        WorkshopUpgrade industrialHousing =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/IndustrialHousingStandards");
        Assert.That(industrialHousing, Is.Not.Null, "工业住宅标准工坊必须存在。");
        Assert.That(HasWorkshopEffectType(
            industrialHousing, WorkshopEffectType.PopulationGrowthMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(industrialHousing, "Ceramic"), Is.True);
        Assert.That(HasWorkshopEffectType(
            orbitalLifeSupport, WorkshopEffectType.PopulationGrowthMultiplier), Is.True);
        Assert.That(HasWorkshopResearch(orbitalLifeSupport, "OrbitalHabitation"), Is.True);
        Assert.That(HasWorkshopUpgrade(orbitalLifeSupport, "ModularHabitatSystems"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalLifeSupport, "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalLifeSupport, "Chemical"), Is.True);
        Assert.That(HasWorkshopResearch(orbitalMining, "AutonomousOrbitalMining"), Is.True);
        Assert.That(HasWorkshopUpgrade(orbitalMining, "HeavyMineralSeparationSystem"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalMining, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalMining, "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalMining, "BauxiteOre"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalMining, "NickelConcentrate"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalMining, "TitaniumConcentrate"), Is.True);
        Assert.That(
            HasWorkshopBuildingEffectType(
                orbitalMining,
                "OrbitalResourceExtractionArray",
                WorkshopEffectType.BuildingProductionMultiplier),
            Is.True);
        Assert.That(HasWorkshopResearch(launchStages, "OrbitalEngineering"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(launchStages, "Rubber"), Is.True);
        Assert.That(HasWorkshopResearch(habitatSystems, "OrbitalHabitation"), Is.True);
        Assert.That(HasWorkshopUpgrade(habitatSystems, "ReusableLaunchStages"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(habitatSystems, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(habitatSystems, "Cloth"), Is.True);
        Assert.That(HasWorkshopResearch(shipyardAssembly, "DeepSpaceShipbuilding"), Is.True);
        Assert.That(HasWorkshopResearch(shipyardAssembly, "OrbitalConstructionAutomation"), Is.True);
        Assert.That(HasWorkshopUpgrade(shipyardAssembly, "ModularHabitatSystems"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "Lubricant"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "Engine"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "BauxiteOre"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "NickelConcentrate"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "TitaniumConcentrate"), Is.True);
        Assert.That(HasWorkshopEffectType(launchStages, WorkshopEffectType.FleetRepairCostMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            habitatSystems,
            "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(HasWorkshopEffectType(shipyardAssembly, WorkshopEffectType.MilitaryMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            shipyardAssembly,
            "Shipyard",
            WorkshopEffectType.BuildingConstructionMultiplier), Is.True);
        Assert.That(HasWorkshopResearch(orbitalThermalManagement, "DeepSpaceThermalExchangeTheory"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Aluminum"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "CopperWire"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Ceramic"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Glass"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "TitaniumAlloy"), Is.True);
        Assert.That(
            HasWorkshopBuildingEffectType(
                orbitalThermalManagement,
                "OrbitalSolarArray",
                WorkshopEffectType.BuildingPowerProductionMultiplier),
            Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            orbitalPowerBeaming,
            "OrbitalSolarArray",
            WorkshopEffectType.BuildingPowerProductionMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalPowerBeaming, "Machinery"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalPowerBeaming, "Glass"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalPowerBeaming, "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalSolarArray", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalSolarArray", "Aluminum"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalSolarArray", "CopperWire"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalSolarArray", "Electronics"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalSolarArray", "Glass"), Is.True);
        Assert.That(HasBuildingResourceRequirement("LaunchCenter", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("LaunchCenter", "Composite"), Is.True);
        Assert.That(
            GetBuildingResourceConsumption("OrbitalSolarArray", "Electronics").ToDouble(),
            Is.EqualTo(0.08d).Within(0.001d));
        Assert.That(HasResourceEffect(cryogenicFuel, "RocketFuel"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(cryogenicFuel, "Chemical"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(cryogenicFuel, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceEffect(compositeLayup, "Composite"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(compositeLayup, "Nickel"), Is.True);
        Assert.That(phaseFieldContainment, Is.Not.Null);
        Assert.That(HasWorkshopResourceRequirement(phaseFieldContainment, "Machinery"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(phaseFieldContainment, "Electronics"), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            phaseFieldContainment,
            "PhantomMaterialsFabricator",
            WorkshopEffectType.BuildingProductionMultiplier), Is.True);
        Assert.That(phaseFieldContainment.Effects.Count(effect =>
            effect != null &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
            effect.Building != null &&
            effect.Building.Id == "PhantomMaterialsFabricator"), Is.EqualTo(1));

        Building orbitalStation = DataBase<Building>.Find("OrbitalStation");
        Assert.That(orbitalStation, Is.Not.Null);
        Assert.That(GetBuildingResourceRequirement("OrbitalStation", "Biomass"), Is.GreaterThan(
            GetBuildingResourceRequirement("OrbitalStation", "PhantomWeave") * 15d));
        Assert.That(GetBuildingResourceConsumption("OrbitalStation", "Biomass"), Is.GreaterThan(
            GetBuildingResourceConsumption("OrbitalStation", "PhantomWeave") * 20d));
        Assert.That(GetBuildingResourceConsumption("OrbitalHabitatMegastructure", "Biomass"), Is.GreaterThan(
            GetBuildingResourceConsumption("OrbitalStation", "Biomass") * 4d));
        Assert.That(GetBuildingResourceConsumption("OrbitalHabitatMegastructure", "Biomass"), Is.GreaterThan(
            GetBuildingResourceConsumption("OrbitalHabitatMegastructure", "PhantomWeave") * 20d));
        Assert.That(HasWorkshopBuildingEffectType(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/DeepSpaceNetworkAutomation"),
            "DeepSpaceRelay",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarSupplyDoctrine"),
            "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.False);
        Assert.That(HasWorkshopEffectType(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarSupplyDoctrine"),
            WorkshopEffectType.CampaignSupplyCostMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/DeepSpaceNetworkAutomation"),
            "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarCombatSupplySystems"),
            "Nickel"), Is.True);
        WorkshopUpgrade networkAutomation =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/DeepSpaceNetworkAutomation");
        Assert.That(DataBase<Building>.Find("DeepSpaceRelay").RequiredWorkshopUpgrades,
            Does.Contain(networkAutomation));
        Assert.That(HasWorkshopResourceRequirement(networkAutomation, "Machinery"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(networkAutomation, "Glass"), Is.True);
        Assert.That(GetWorkshopResourceRequirement(networkAutomation, "Machinery"), Is.GreaterThan(
            GetWorkshopResourceRequirement(networkAutomation, "TitaniumAlloy") * 5d));
        Assert.That(HasWorkshopEffectType(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarCombatSupplySystems"),
            WorkshopEffectType.FleetRepairCostMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarCombatSupplySystems"),
            "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(HasWorkshopEffectType(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarOccupationGovernance"),
            WorkshopEffectType.OccupiedResourceProductionMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarOccupationGovernance"),
            "Biomass"), Is.True);
        Assert.That(HasResearchEffectType(
            DataBase<Research>.Find("InterstellarOccupationAdministration"),
            ResearchEffectType.OccupiedResourceProductionMultiplier), Is.True);
        Assert.That(HasResourceRequirement(
            DataBase<Research>.Find("InterstellarOccupationAdministration"),
            "Biomass"), Is.True);
        Assert.That(HasResearchEffectType(
            DataBase<Research>.Find("InterstellarCombatLogistics"),
            ResearchEffectType.FleetRepairCostMultiplier), Is.True);
        Assert.That(HasResearchEffectType(
            DataBase<Research>.Find("InterstellarLogisticsDoctrine"),
            ResearchEffectType.CampaignProgressMultiplier), Is.True);
    }

    [Test]
    public void 星际补给研究与实体工坊必须保持职责边界()
    {
        WorkshopUpgrade automation = DataBase<WorkshopUpgrade>.Find("AutonomousFleetLogistics");
        WorkshopUpgrade combat = DataBase<WorkshopUpgrade>.Find("InterstellarCombatSupplySystems");
        WorkshopUpgrade standardization = DataBase<WorkshopUpgrade>.Find("InterstellarSupplyDoctrine");

        Assert.That(automation, Is.Not.Null, "自动化舰队后勤工坊必须存在。");
        Assert.That(combat, Is.Not.Null, "星际战斗补给系统工坊必须存在。");
        Assert.That(standardization, Is.Not.Null, "星际补给标准化模块必须存在。");
        Assert.That(HasWorkshopBuildingEffectType(
            automation, "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            combat, "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            standardization, "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.False);
        Assert.That(HasWorkshopEffectType(
            standardization, WorkshopEffectType.CampaignSupplyCostMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "Lubricant"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(combat, "Nickel"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(standardization, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(standardization, "PhantomAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(standardization, "PhantomWeave"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(standardization, "PhaseMaterial"), Is.True);
        Assert.That(GetWorkshopResourceRequirement(standardization, "PhantomAlloy"),
            Is.GreaterThan(new ExpantaNum(1000d)));
    }

    [Test]
    public void SpacerQuantumComputingSliceUsesAdvancedMaterials()
    {
        Research quantumComputing = DataBase<Research>.Find("QuantumComputing");
        Building quantumArray = DataBase<Building>.Find("QuantumComputingArray");
        WorkshopUpgrade errorCorrection =
            DataBase<WorkshopUpgrade>.Find("QuantumErrorCorrection");

        Assert.That(quantumComputing, Is.Not.Null);
        Assert.That(quantumArray, Is.Not.Null);
        Assert.That(errorCorrection, Is.Not.Null);
        Assert.That(quantumComputing.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(quantumComputing.AdvancesTechLevel, Is.False);
        Assert.That(quantumArray.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(errorCorrection.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(HasResourceRequirement(quantumComputing, "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingEffectType(quantumComputing, "QuantumComputingArray",
            ResearchEffectType.BuildingResearchPowerMultiplier), Is.True);
        Assert.That(HasResearchEffectType(
            quantumComputing, ResearchEffectType.GlobalResearchMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(errorCorrection, "QuantumComputingArray",
            WorkshopEffectType.BuildingResearchPowerMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(errorCorrection, "Nickel"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(errorCorrection, "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(errorCorrection, "Machinery"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(errorCorrection, "Ceramic"), Is.True);
        Assert.That(GetWorkshopResourceRequirement(errorCorrection, "Machinery"), Is.GreaterThan(
            GetWorkshopResourceRequirement(errorCorrection, "TitaniumAlloy") * 4d));
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "Nickel"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "Chemical"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "PhaseMaterial"), Is.True);
    }

    [Test]
    public void 相位材料合成阵列必须持续消耗工业基础材料()
    {
        Assert.That(HasBuildingResourceConsumption("PhaseMaterialSynthesisArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceConsumption("PhaseMaterialSynthesisArray", "Chemical"), Is.True);
        Assert.That(HasBuildingResourceConsumption("PhaseMaterialSynthesisArray", "Composite"), Is.True);
        Assert.That(
            GetBuildingResourceConsumption("PhaseMaterialSynthesisArray", "Composite"),
            Is.GreaterThan(GetBuildingResourceConsumption("PhaseMaterialSynthesisArray", "TitaniumAlloy") * 2d));
    }

    [Test]
    public void 轨道农业生态学必须把生物质理论落实为实体工坊()
    {
        Research theory = DataBase<Research>.Find("OrbitalAgroecology");
        WorkshopUpgrade systems = DataBase<WorkshopUpgrade>.Find("OrbitalAgroponicSystems");

        Assert.That(theory, Is.Not.Null);
        Assert.That(systems, Is.Not.Null);
        Assert.That(theory.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(systems.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(HasPrerequisite(theory, "OrbitalHabitation"), Is.True);
        Assert.That(HasPrerequisite(theory, "BioregenerativeLifeSupport"), Is.True);
        Assert.That(HasWorkshopResearch(systems, "OrbitalAgroecology"), Is.True);
        Assert.That(HasWorkshopUpgrade(systems, "OrbitalLifeSupportNetworks"), Is.True);
        Assert.That(HasResourceRequirement(theory, "Biomass"), Is.True);
        Assert.That(HasResourceRequirement(theory, "Ceramic"), Is.True);
        Assert.That(theory.Effects.Any(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.ResourceProductionMultiplier &&
            effect.Resource != null &&
            effect.Resource.Id == "Biomass"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "Biomass"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "Glass"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(systems, "Chemical"), Is.True);
        Assert.That(HasResourceEffect(systems, "Biomass"), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            systems, "PlantingField", WorkshopEffectType.BuildingFoodProductionMultiplier), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(
            systems, "PlantingField", WorkshopEffectType.BuildingProductionMultiplier), Is.True);
        Assert.That(GetWorkshopResourceRequirement(systems, "Biomass"), Is.GreaterThan(
            GetWorkshopResourceRequirement(systems, "TitaniumAlloy") * 10d));
        Assert.That(GetWorkshopResourceRequirement(systems, "Chemical"), Is.GreaterThan(
            GetWorkshopResourceRequirement(systems, "TitaniumAlloy") * 2d));
    }

    [Test]
    public void CeramicRemainsAUsefulSpacerStructureMaterial()
    {
        Assert.That(HasBuildingResourceRequirement("QuantumComputingArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceRequirement("QuantumComputingArray", "Nickel"), Is.True);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "Ceramic"), Is.True);
    }

    [Test]
    public void SpacerAdvancedResourcesKeepMultipleLongTermSinkCategories()
    {
        string[] resourceIds =
        {
            "Composite",
            "PhantomAlloy",
            "PhantomWeave",
            "PhaseMaterial",
            "RocketFuel"
        };

        for (int i = 0; i < resourceIds.Length; i++)
        {
            string resourceId = resourceIds[i];
            Assert.That(
                CountResourceSinkCategories(resourceId),
                Is.GreaterThanOrEqualTo(3),
                resourceId + " 必须至少同时服务三类后期循环。");
        }
    }

    [Test]
    public void 太空研究与工坊必须持续使用高级结构材料()
    {
        string[] advancedMaterialIds =
        {
            "TitaniumAlloy",
            "Composite",
            "PhantomAlloy",
            "PhantomWeave",
            "PhaseMaterial"
        };

        foreach (Research research in DataBase<Research>.All.Where(item =>
                     item != null && item.TechLevel == TechLevel.Spacer && item.Id != "FirstContact"))
        {
            if (research.Id == "DeepSpaceBiosecurityTheory")
                continue;
            Assert.That(
                advancedMaterialIds.Any(resourceId => HasResourceRequirement(research, resourceId)),
                Is.True,
                research.Id + " 必须消耗至少一种高级结构材料");
        }

        foreach (WorkshopUpgrade workshop in DataBase<WorkshopUpgrade>.All.Where(item =>
                     item != null && item.TechLevel == TechLevel.Spacer))
        {
            if (workshop.Id == "ClosedLoopBiosecurityModules")
                continue;
            Assert.That(
                advancedMaterialIds.Any(resourceId => HasWorkshopResourceRequirement(workshop, resourceId)),
                Is.True,
                workshop.Id + " 必须消耗至少一种高级结构材料");
        }
    }

    [Test]
    public void 每种高级结构材料都必须进入太空建筑研究与工坊()
    {
        string[] advancedMaterialIds =
        {
            "TitaniumAlloy",
            "Composite",
            "PhantomAlloy",
            "PhantomWeave",
            "PhaseMaterial"
        };

        foreach (string resourceId in advancedMaterialIds)
        {
            Assert.That(
                DataBase<Building>.All.Any(building =>
                    building != null && building.TechLevel == TechLevel.Spacer &&
                    (HasResourcePair(building.ResourceRequirements, resourceId) ||
                     HasResourcePair(building.ResourceConsumptionRates, resourceId))),
                Is.True,
                resourceId + " 必须进入至少一座太空建筑的建造或维护链");
            Assert.That(
                DataBase<Research>.All.Any(research =>
                    research != null && research.TechLevel == TechLevel.Spacer &&
                    HasResourcePair(research.ResourceRequirements, resourceId)),
                Is.True,
                resourceId + " 必须进入至少一项太空研究");
            Assert.That(
                DataBase<WorkshopUpgrade>.All.Any(workshop =>
                    workshop != null && workshop.TechLevel == TechLevel.Spacer &&
                    HasResourcePair(workshop.ResourceRequirements, resourceId)),
                Is.True,
                resourceId + " 必须进入至少一项太空工坊升级");
        }
    }

    [Test]
    public void SpacerLogisticsHubTurnsAdvancedMaterialsIntoFleetSupport()
    {
        Building hub = DataBase<Building>.Find("OrbitalStation");
        Research infrastructure = DataBase<Research>.Find("OrbitalLogisticsInfrastructure");
        WorkshopUpgrade automation =
            DataBase<WorkshopUpgrade>.Find("AutonomousFleetLogistics");
        WorkshopUpgrade containers =
            DataBase<WorkshopUpgrade>.Find("StandardizedFreightContainers");

        Assert.That(hub, Is.Not.Null);
        Assert.That(infrastructure, Is.Not.Null);
        Assert.That(automation, Is.Not.Null);
        Assert.That(containers, Is.Not.Null);
        Assert.That(hub.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(hub.LogisticsProductionRate, Is.GreaterThan(hub.LogisticsConsumptionRate));
        Assert.That(HasRequiredResearch("OrbitalStation", "OrbitalLogisticsInfrastructure"), Is.True);
        Assert.That(HasResourceRequirement(infrastructure, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceRequirement(infrastructure, "RocketFuel"), Is.True);
        Assert.That(HasWorkshopBuildingEffectType(automation, "OrbitalStation",
            WorkshopEffectType.BuildingLogisticsProductionMultiplier), Is.True);
        Assert.That(hub.RequiredWorkshopUpgrades, Does.Contain(containers));
        Assert.That(HasWorkshopResourceRequirement(automation, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "Lubricant"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "Rubber"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Rubber"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Lubricant"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalStation", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalStation", "Biomass"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalStation", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalStation", "Biomass"), Is.True);
        Assert.That(
            GetBuildingResourceRequirement("OrbitalStation", "Biomass"),
            Is.GreaterThan(GetBuildingResourceRequirement("OrbitalStation", "TitaniumAlloy") * 5d));
        Assert.That(GetBuildingResourceConsumption("OrbitalStation", "Biomass"), Is.GreaterThan(
            GetBuildingResourceConsumption("OrbitalStation", "TitaniumAlloy") * 2d));
        Assert.That(HasBuildingResourceConsumption("Shipyard", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "Machinery"), Is.True);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "Engine"), Is.True);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "Rubber"), Is.True);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "PhantomAlloy"), Is.False);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "Composite"), Is.True);
        Assert.That(GetBuildingResourceConsumption("Shipyard", "Machinery"), Is.GreaterThan(
            GetBuildingResourceConsumption("Shipyard", "TitaniumAlloy")));
        Assert.That(GetBuildingResourceRequirement("Shipyard", "Steel"), Is.GreaterThan(
            GetBuildingResourceRequirement("Shipyard", "TitaniumAlloy") * 8d));
        Assert.That(GetBuildingResourceRequirement("Shipyard", "Machinery"), Is.GreaterThan(
            GetBuildingResourceRequirement("Shipyard", "TitaniumAlloy") * 4d));
        Assert.That(GetBuildingResourceRequirement("Shipyard", "Concrete"), Is.GreaterThan(
            GetBuildingResourceRequirement("Shipyard", "TitaniumAlloy") * 7d));
        Assert.That(HasBuildingResourceRequirement("Shipyard", "PhantomAlloy"), Is.False);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "PhantomWeave"), Is.False);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "Electronics"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "RocketFuel"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "PhaseMaterial"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "Explosives"), Is.False);
    }

    [Test]
    public void SpacerDeepSpaceSurveyIsAnEarlyExplorationResearch()
    {
        Building observatory = DataBase<Building>.Find("DeepSpaceObservatory");
        Research survey = DataBase<Research>.Find("DeepSpaceSurvey");
        WorkshopUpgrade drones =
            DataBase<WorkshopUpgrade>.Find("AutonomousSurveyDrones");

        Assert.That(observatory, Is.Not.Null);
        Assert.That(survey, Is.Not.Null);
        Assert.That(drones, Is.Not.Null);
        Assert.That(observatory.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(survey.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(drones.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(observatory.RequiredWorkshopUpgrades, Does.Contain(drones));
        Assert.That(HasRequiredResearch("DeepSpaceObservatory", "DeepSpaceSurvey"), Is.True);
        Assert.That(HasPrerequisite(survey, "QuantumComputing"), Is.False);
        Assert.That(HasPrerequisite(survey, "OrbitalEngineering"), Is.True);
        Assert.That(HasResearchEffectType(
            survey, ResearchEffectType.ExplorationPowerMultiplier), Is.True);
        Assert.That(HasWorkshopEffectType(
            drones, WorkshopEffectType.ExplorationPowerMultiplier), Is.True);
        Assert.That(HasResourceRequirement(survey, "PhantomAlloy"), Is.False);
        Assert.That(HasResourceRequirement(survey, "PhantomWeave"), Is.False);
        Assert.That(HasResourceRequirement(survey, "RocketFuel"), Is.True);
        Assert.That(HasResourceRequirement(survey, "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceObservatory", "Explosives"), Is.False);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceObservatory", "Glass"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceObservatory", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceObservatory", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceRequirement("DeepSpaceObservatory", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceObservatory", "Composite"), Is.True);
        Assert.That(GetBuildingResourceRequirement("DeepSpaceObservatory", "Glass"), Is.GreaterThan(
            GetBuildingResourceRequirement("DeepSpaceObservatory", "PhantomWeave") * 5d));
        Assert.That(HasBuildingResourceConsumption("LaunchCenter", "Engine"), Is.True);
        Assert.That(HasBuildingResourceConsumption("LaunchCenter", "Electronics"), Is.True);
        Assert.That(observatory.AttackPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(observatory.DefensePowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(observatory.SpaceCost, Is.EqualTo(new ExpantaNum(440)));
        Assert.That(observatory.ProductivityConsumption, Is.EqualTo(new ExpantaNum(620)));
        Assert.That(observatory.ResearchPowerGranted, Is.EqualTo(new ExpantaNum(220)));
        Assert.That(observatory.PowerConsumptionRate, Is.EqualTo(new ExpantaNum(160)));
        Assert.That(observatory.LogisticsConsumptionRate, Is.EqualTo(new ExpantaNum(40)));
        Assert.That(HasWorkshopResearch(drones, "DeepSpaceSurvey"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(drones, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(drones, "Machinery"), Is.False);
        Assert.That(HasWorkshopResourceRequirement(drones, "Electronics"), Is.True);
        Assert.That(HasWorkshopEffectType(drones, WorkshopEffectType.ExplorationPowerMultiplier), Is.True);
    }

    [Test]
    public void 太空研究树必须拥有能源生命保障与自主采矿横向分支()
    {
        Research power = DataBase<Research>.Find("OrbitalPowerTransmission");
        Research lifeSupport = DataBase<Research>.Find("BioregenerativeLifeSupport");
        Research mining = DataBase<Research>.Find("AutonomousOrbitalMining");
        Research logistics = DataBase<Research>.Find("OrbitalLogisticsInfrastructure");
        Research doctrine = DataBase<Research>.Find("InterstellarLogisticsDoctrine");
        Research occupation = DataBase<Research>.Find("InterstellarOccupationAdministration");
        Research construction = DataBase<Research>.Find("OrbitalConstructionAutomation");

        Assert.That(power, Is.Not.Null);
        Assert.That(lifeSupport, Is.Not.Null);
        Assert.That(mining, Is.Not.Null);
        Assert.That(logistics, Is.Not.Null);
        Assert.That(doctrine, Is.Not.Null);
        Assert.That(occupation, Is.Not.Null);
        Assert.That(construction, Is.Not.Null);
        Assert.That(power.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(lifeSupport.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(mining.TechLevel, Is.EqualTo(TechLevel.Spacer));

        Assert.That(HasPrerequisite(power, "OrbitalEngineering"), Is.True);
        Assert.That(HasPrerequisite(power, "PowerGridEngineering"), Is.True);
        Assert.That(HasPrerequisite(lifeSupport, "OrbitalHabitation"), Is.True);
        Assert.That(HasPrerequisite(mining, "RareMetalResourceDevelopment"), Is.True);
        Assert.That(HasPrerequisite(mining, "OrbitalEngineering"), Is.True);
        Assert.That(HasPrerequisite(logistics, "OrbitalPowerTransmission"), Is.True);
        Assert.That(HasPrerequisite(doctrine, "AutonomousOrbitalMining"), Is.True);
        Assert.That(HasPrerequisite(occupation, "BioregenerativeLifeSupport"), Is.True);
        Assert.That(HasPrerequisite(construction, "IndustrialHabitationEngineering"), Is.True);
        Assert.That(HasPrerequisite(construction, "OrbitalPowerTransmission"), Is.True);
        Assert.That(
            HasWorkshopResearch(
                DataBase<WorkshopUpgrade>.Find("OrbitalPowerBeaming"),
                "OrbitalPowerTransmission"),
            Is.True);
        Assert.That(
            HasWorkshopResearch(
                DataBase<WorkshopUpgrade>.Find("OrbitalLifeSupportNetworks"),
                "BioregenerativeLifeSupport"),
            Is.True);

        Assert.That(HasResourceRequirement(power, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceRequirement(lifeSupport, "Biomass"), Is.True);
        Assert.That(HasResourceRequirement(mining, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceRequirement(mining, "Machinery"), Is.True);
        Assert.That(HasResourceRequirement(mining, "BauxiteOre"), Is.True);
        Assert.That(HasResourceRequirement(mining, "NickelConcentrate"), Is.True);
        Assert.That(HasResourceRequirement(mining, "TitaniumConcentrate"), Is.True);
        Assert.That(HasResourceRequirement(construction, "BauxiteOre"), Is.True);
        Assert.That(HasResourceRequirement(construction, "NickelConcentrate"), Is.True);
        Assert.That(HasResourceRequirement(construction, "TitaniumConcentrate"), Is.True);

        Assert.That(HasBuildingEffectType(
            power, "OrbitalSolarArray", ResearchEffectType.BuildingPowerProductionMultiplier), Is.True);
        Assert.That(HasResearchEffectType(lifeSupport, ResearchEffectType.PopulationGrowthMultiplier), Is.True);
        Assert.That(HasBuildingEffect(
            mining, "OrbitalResourceExtractionArray"), Is.True);
        Assert.That(
            HasResearchEffectType(
                mining,
                ResearchEffectType.OccupiedResourceProductionMultiplier),
            Is.True);
        Assert.That(
            HasWorkshopEffectType(
                DataBase<WorkshopUpgrade>.Find("AutonomousOrbitalMiningSystems"),
                WorkshopEffectType.OccupiedResourceProductionMultiplier),
            Is.True);
        Assert.That(HasResearchEffectType(construction, ResearchEffectType.GlobalConstructionMultiplier), Is.True);
        Assert.That(HasBuildingEffectType(
            construction,
            "OrbitalHabitatMegastructure",
            ResearchEffectType.BuildingConstructionMultiplier), Is.True);
        Assert.That(HasBuildingEffectType(
            construction,
            "OrbitalStation",
            ResearchEffectType.BuildingConstructionMultiplier), Is.True);
        Assert.That(HasRequiredResearch("OrbitalHabitatMegastructure", "OrbitalConstructionAutomation"), Is.True);
        Assert.That(HasWorkshopResearch(
            DataBase<WorkshopUpgrade>.Find("ModularHabitatSystems"),
            "OrbitalConstructionAutomation"), Is.True);
        Assert.That(
            DataBase<Building>.Find("OrbitalHabitatMegastructure").RequiredWorkshopUpgrades,
            Does.Contain(DataBase<WorkshopUpgrade>.Find("ModularHabitatSystems")));
    }

    [Test]
    public void 太空全局战役效果必须来自有明确职责的研究()
    {
        Research fleet = DataBase<Research>.Find("DeepSpaceFleet");
        Research combat = DataBase<Research>.Find("InterstellarCombatLogistics");
        Research doctrine = DataBase<Research>.Find("InterstellarLogisticsDoctrine");
        Research supply = DataBase<Research>.Find("InterstellarSupplyChainTheory");
        Research occupation = DataBase<Research>.Find("InterstellarOccupationAdministration");
        Research survey = DataBase<Research>.Find("DeepSpaceSurvey");

        Assert.That(fleet, Is.Not.Null);
        Assert.That(combat, Is.Not.Null);
        Assert.That(doctrine, Is.Not.Null);
        Assert.That(supply, Is.Not.Null);
        Assert.That(occupation, Is.Not.Null);
        Assert.That(survey, Is.Not.Null);
        Assert.That(HasResearchEffectType(fleet, ResearchEffectType.MilitaryMultiplier), Is.True);
        Assert.That(HasResearchEffectType(combat, ResearchEffectType.MilitaryMultiplier), Is.True);
        Assert.That(HasResearchEffectType(combat, ResearchEffectType.GlobalLogisticsMultiplier), Is.True);
        Assert.That(HasResearchEffectType(combat, ResearchEffectType.FleetRepairCostMultiplier), Is.True);
        Assert.That(HasResearchEffectType(doctrine, ResearchEffectType.GlobalLogisticsMultiplier), Is.True);
        Assert.That(HasResearchEffectType(doctrine, ResearchEffectType.CampaignProgressMultiplier), Is.True);
        Assert.That(HasResearchEffectType(supply, ResearchEffectType.CampaignSupplyCostMultiplier), Is.True);
        Assert.That(HasResearchEffectType(occupation, ResearchEffectType.TerritoryGranted), Is.True);
        Assert.That(HasResearchEffectType(occupation, ResearchEffectType.OccupiedResourceProductionMultiplier), Is.True);
        Assert.That(HasResearchEffectType(survey, ResearchEffectType.ExplorationPowerMultiplier), Is.True);
    }

    [Test]
    public void OrbitalVacuumMetallurgyMustExtendTheTitaniumAlloyChain()
    {
        Building industrial = DataBase<Building>.Find("TitaniumMetallurgicalComplex");
        Building orbital = DataBase<Building>.Find("OrbitalResourceExtractionArray");
        Research theory = DataBase<Research>.Find("OrbitalVacuumMetallurgy");
        WorkshopUpgrade furnaces = DataBase<WorkshopUpgrade>.Find("OrbitalVacuumFurnaces");

        Assert.That(industrial, Is.Not.Null);
        Assert.That(orbital, Is.Not.Null);
        Assert.That(theory, Is.Not.Null);
        Assert.That(furnaces, Is.Not.Null);
        Assert.That(industrial.UpgradeTo, Is.EqualTo(orbital));
        Assert.That(orbital.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(FindRate(orbital.ResourceGenerationRates, "TitaniumAlloy"),
            Is.GreaterThan(FindRate(industrial.ResourceGenerationRates, "TitaniumAlloy") * 8d));
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "TitaniumConcentrate"), Is.False);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "NickelConcentrate"), Is.False);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "Coke"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "Chemical"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalResourceExtractionArray", "PhaseMaterial"), Is.True);
        Assert.That(ContainsResource(orbital.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(ContainsResource(orbital.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(orbital.ResourceRequirements, "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingEffect(theory, "OrbitalResourceExtractionArray"), Is.True);
        Assert.That(furnaces.RequiredResearch, Does.Contain(theory));
        Assert.That(furnaces.Effects, Has.Some.Matches<WorkshopEffectDefinition>(effect =>
            effect != null && effect.Building == orbital &&
            effect.Type == WorkshopEffectType.BuildingProductionMultiplier));
    }

    [Test]
    public void OrbitalCryogenicPropellantArrayMustSupplyTheRocketFuelChain()
    {
        Building chemicalPlant = DataBase<Building>.Find("ChemicalPlant");
        Building orbital = DataBase<Building>.Find("OrbitalCryogenicPropellantArray");
        WorkshopUpgrade cryogenic = DataBase<WorkshopUpgrade>.Find("CryogenicFuelSystems");
        Research theory = DataBase<Research>.Find("OrbitalPropellantEngineering");

        Assert.That(chemicalPlant, Is.Not.Null);
        Assert.That(orbital, Is.Not.Null);
        Assert.That(cryogenic, Is.Not.Null);
        Assert.That(theory, Is.Not.Null);
        Assert.That(orbital.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(FindRate(orbital.ResourceGenerationRates, "RocketFuel"),
            Is.GreaterThan(FindRate(chemicalPlant.ResourceGenerationRates, "RocketFuel") * 4d));
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "RefinedFuel"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "Chemical"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "Electronics"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "Composite"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalCryogenicPropellantArray", "PhaseMaterial"), Is.True);
        Assert.That(ContainsResource(orbital.ResourceRequirements, "RefinedFuel"), Is.True);
        Assert.That(ContainsResource(orbital.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(orbital.RequiredWorkshopUpgrades, Does.Contain(cryogenic));
        Assert.That(orbital.RequiredResearch, Does.Contain(theory));
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null && effect.Type == ResearchEffectType.ResourceProductionMultiplier &&
            effect.Resource != null && effect.Resource.Id == "RocketFuel"));
        Assert.That(cryogenic.RequiredResearch, Does.Contain(theory));
    }

    private static bool HasPrerequisite(Research research, string id)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
            if (research.Prerequisites[i] != null && research.Prerequisites[i].Id == id)
                return true;
        return false;
    }

    private static bool HasBuildingEffect(Research research, string id)
    {
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Building != null && effect.Building.Id == id &&
                effect.Type == ResearchEffectType.BuildingProductionMultiplier)
                return true;
        }
        return false;
    }

    private static bool HasBuildingEffectType(
        Research research,
        string id,
        ResearchEffectType type)
    {
        if (research == null)
            return false;
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Building != null && effect.Building.Id == id && effect.Type == type)
                return true;
        }

        return false;
    }

    private static bool HasResearchEffectType(Research research, ResearchEffectType type)
    {
        if (research == null)
            return false;
        for (int i = 0; i < research.Effects.Count; i++)
            if (research.Effects[i] != null && research.Effects[i].Type == type)
                return true;
        return false;
    }

    [Test]
    public void 相位材料体系必须继续消耗工业焦炭()
    {
        Building synthesisArray = DataBase<Building>.Find("PhaseMaterialSynthesisArray");
        Research engineering = DataBase<Research>.Find("PhaseMaterialEngineering");
        WorkshopUpgrade calibration = DataBase<WorkshopUpgrade>.Find("PhaseMaterialCalibration");

        Assert.That(GetBuildingResourceRequirement("PhaseMaterialSynthesisArray", "Coke"),
            Is.GreaterThanOrEqualTo(new ExpantaNum(3000)));
        Assert.That(GetBuildingResourceConsumption("PhaseMaterialSynthesisArray", "Coke"),
            Is.GreaterThanOrEqualTo(new ExpantaNum("0.1")));
        Assert.That(HasResourceRequirement(engineering, "Coke"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(calibration, "Coke"), Is.True);
        Assert.That(synthesisArray.TechLevel, Is.EqualTo(TechLevel.Spacer));
    }

    private static bool HasResourceRequirement(Research research, string resourceId)
    {
        if (research == null)
            return false;
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
            if (research.ResourceRequirements[i].First != null &&
                research.ResourceRequirements[i].First.Id == resourceId &&
                research.ResourceRequirements[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static bool HasWorkshopResourceRequirement(
        WorkshopUpgrade workshop,
        string resourceId)
    {
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.ResourceRequirements.Count; i++)
            if (workshop.ResourceRequirements[i].First != null &&
                workshop.ResourceRequirements[i].First.Id == resourceId &&
                workshop.ResourceRequirements[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static ExpantaNum GetWorkshopResourceRequirement(
        WorkshopUpgrade workshop,
        string resourceId)
    {
        if (workshop == null)
            return ExpantaNum.Zero;
        for (int i = 0; i < workshop.ResourceRequirements.Count; i++)
            if (workshop.ResourceRequirements[i].First != null &&
                workshop.ResourceRequirements[i].First.Id == resourceId)
                return workshop.ResourceRequirements[i].Second;
        return ExpantaNum.Zero;
    }

    private static bool HasRequiredResearch(string buildingId, string researchId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return false;
        for (int i = 0; i < building.RequiredResearch.Count; i++)
            if (building.RequiredResearch[i] != null && building.RequiredResearch[i].Id == researchId)
                return true;
        return false;
    }

    private static bool HasBuildingResourceRequirement(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return false;
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
            if (building.ResourceRequirements[i].First != null &&
                building.ResourceRequirements[i].First.Id == resourceId &&
                building.ResourceRequirements[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static ExpantaNum GetBuildingResourceRequirement(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return ExpantaNum.Zero;
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
            if (building.ResourceRequirements[i].First != null &&
                building.ResourceRequirements[i].First.Id == resourceId)
                return building.ResourceRequirements[i].Second;
        return ExpantaNum.Zero;
    }

    private static bool HasBuildingResourceConsumption(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return false;
        for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
            if (building.ResourceConsumptionRates[i].First != null &&
                building.ResourceConsumptionRates[i].First.Id == resourceId &&
                building.ResourceConsumptionRates[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static ExpantaNum GetBuildingResourceConsumption(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return ExpantaNum.Zero;
        for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
            if (building.ResourceConsumptionRates[i].First != null &&
                building.ResourceConsumptionRates[i].First.Id == resourceId)
                return building.ResourceConsumptionRates[i].Second;
        return ExpantaNum.Zero;
    }

    private static int CountResourceSinkCategories(string resourceId)
    {
        int categories = 0;
        if (DataBase<Building>.All.Any(building =>
                HasResourcePair(building.ResourceRequirements, resourceId) ||
                HasResourcePair(building.ResourceConsumptionRates, resourceId)))
            categories++;
        if (DataBase<Research>.All.Any(research =>
                HasResourcePair(research.ResourceRequirements, resourceId)))
            categories++;
        if (DataBase<WorkshopUpgrade>.All.Any(workshop =>
                HasResourcePair(workshop.ResourceRequirements, resourceId)))
            categories++;
        if (DataBase<SectorDefinition>.All.Any(sector =>
                HasResourcePair(sector.ResourceRewards, resourceId) ||
                HasResourcePair(sector.CampaignResourceRatesPerSecond, resourceId)))
            categories++;
        return categories;
    }

    private static bool HasResourcePair(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        if (pairs == null)
            return false;
        for (int i = 0; i < pairs.Count; i++)
            if (pairs[i].First != null &&
                pairs[i].First.Id == resourceId &&
                pairs[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static bool HasWorkshopResearch(
        WorkshopUpgrade workshop,
        string researchId)
    {
        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
            if (workshop.RequiredResearch[i] != null && workshop.RequiredResearch[i].Id == researchId)
                return true;
        return false;
    }

    private static bool WorkshopResearchDependsOn(
        WorkshopUpgrade workshop,
        string researchId)
    {
        var visited = new System.Collections.Generic.HashSet<Research>();
        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
            if (ResearchDependsOn(workshop.RequiredResearch[i], researchId, visited))
                return true;
        return false;
    }

    private static bool ResearchDependsOn(
        Research research,
        string researchId,
        System.Collections.Generic.HashSet<Research> visited)
    {
        if (research == null || !visited.Add(research))
            return false;
        if (research.Id == researchId)
            return true;
        for (int i = 0; i < research.Prerequisites.Count; i++)
            if (ResearchDependsOn(research.Prerequisites[i], researchId, visited))
                return true;
        return false;
    }

    private static bool ContainsResource(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
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
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
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

    private static bool HasWorkshopUpgrade(
        WorkshopUpgrade workshop,
        string upgradeId)
    {
        for (int i = 0; i < workshop.RequiredUpgrades.Count; i++)
            if (workshop.RequiredUpgrades[i] != null && workshop.RequiredUpgrades[i].Id == upgradeId)
                return true;
        return false;
    }

    private static bool HasWorkshopEffect(
        WorkshopUpgrade workshop,
        string buildingId)
    {
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null && effect.Building != null && effect.Building.Id == buildingId &&
                effect.Type == WorkshopEffectType.BuildingProductionMultiplier)
                return true;
        }
        return false;
    }

    private static bool HasWorkshopBuildingEffectType(
        WorkshopUpgrade workshop,
        string buildingId,
        WorkshopEffectType type)
    {
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null && effect.Building != null &&
                effect.Building.Id == buildingId && effect.Type == type)
                return true;
        }
        return false;
    }

    private static bool HasResourceEffect(
        WorkshopUpgrade workshop,
        string resourceId)
    {
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null && effect.Type == WorkshopEffectType.ResourceProductionMultiplier &&
                effect.Resource != null && effect.Resource.Id == resourceId)
                return true;
        }
        return false;
    }

    private static bool HasWorkshopEffectType(
        WorkshopUpgrade workshop,
        WorkshopEffectType type)
    {
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
            if (workshop.Effects[i] != null && workshop.Effects[i].Type == type)
                return true;
        return false;
    }

    [Test]
    public void 太空工业设施必须形成焦炭化工与爆破的持续运营闭环()
    {
        Building chemicalPlant = DataBase<Building>.Find("ChemicalPlant");
        Building cokeOven = DataBase<Building>.Find("CokeOven");
        Building carbonization = DataBase<Building>.Find("OrbitalCarbonizationComplex");
        Building extraction = DataBase<Building>.Find("OrbitalResourceExtractionArray");

        Assert.That(chemicalPlant, Is.Not.Null);
        Assert.That(cokeOven, Is.Not.Null);
        Assert.That(carbonization, Is.Not.Null);
        Assert.That(extraction, Is.Not.Null);
        Assert.That(FindRate(chemicalPlant.ResourceGenerationRates, "Chemical"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(chemicalPlant.ResourceGenerationRates, "Explosives"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(cokeOven.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(carbonization.ResourceGenerationRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(carbonization.ResourceConsumptionRates, "WoodLog"),
            Is.GreaterThan(0d));
        Assert.That(ContainsResource(carbonization.ResourceRequirements, "WoodLog"), Is.False);
        Assert.That(ContainsResource(carbonization.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(carbonization.ResourceRequirements, "Composite"), Is.True);
        Assert.That(ContainsResource(carbonization.ResourceRequirements, "PhaseMaterial"), Is.True);
        Assert.That(FindRate(extraction.ResourceConsumptionRates, "Explosives"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(extraction.ResourceConsumptionRates, "Lubricant"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(extraction.ResourceConsumptionRates, "Coke"),
            Is.GreaterThan(0d));
        Assert.That(FindRate(extraction.ResourceConsumptionRates, "Chemical"),
            Is.GreaterThan(0d));
    }
}
