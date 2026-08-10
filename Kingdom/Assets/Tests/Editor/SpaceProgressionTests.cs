using NUnit.Framework;
using UnityEngine;

public sealed class SpaceProgressionTests
{
    [Test]
    public void IndustrialProgressionAuditReachesEverySpacerDefinition()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.EqualTo(TechLevel.Spacer), result.FormatFailureReport());
        Assert.That(result.UnreachableResearch, Is.Empty, result.FormatFailureReport());
        Assert.That(result.UnreachableBuildings, Is.Empty, result.FormatFailureReport());
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
        Assert.That(HasPrerequisite(orbitalHabitation, "FirstContact"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("FirstContact"), "Electronics"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("FirstContact"), "CopperWire"), Is.True);
        Assert.That(HasPrerequisite(deepSpaceShipbuilding, "OrbitalHabitation"), Is.True);
        Assert.That(HasPrerequisite(deepSpaceShipbuilding, "DeepSpaceFleet"), Is.True);
        Assert.That(HasPrerequisite(DataBase<Research>.Find("DeepSpaceFleet"), "LogisticsManagement"), Is.True);
        Assert.That(HasPrerequisite(phaseFieldNavigation, "PhantomMaterials"), Is.True);
        Assert.That(HasPrerequisite(phaseFieldNavigation, "PhaseMaterialEngineering"), Is.True);
        Assert.That(DataBase<Research>.Find("DeepSpaceSurvey").Description, Does.Contain("探索"));
        Assert.That(DataBase<Research>.Find("OrbitalLogisticsInfrastructure").Description, Does.Contain("后勤"));
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Machinery"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Engine"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Chemical"), Is.True);
        Assert.That(HasResourceRequirement(deepSpaceShipbuilding, "Chemical"), Is.True);

        Assert.That(HasBuildingEffect(orbitalEngineering, "LaunchCenter"), Is.True);
        Assert.That(HasBuildingEffect(orbitalHabitation, "OrbitalStation"), Is.True);
        Assert.That(HasBuildingEffect(deepSpaceShipbuilding, "Shipyard"), Is.True);
        Assert.That(HasBuildingEffect(phaseFieldNavigation, "DeepSpaceRelay"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "Steel"), Is.True);
        Assert.That(HasBuildingResourceConsumption("DeepSpaceRelay", "PhaseMaterial"), Is.True);
        Assert.That(DataBase<Building>.Find("DeepSpaceRelay").SpaceCost, Is.EqualTo(new ExpantaNum(360)));
        Assert.That(DataBase<Building>.Find("DeepSpaceRelay").ProductivityConsumption, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(DataBase<Building>.Find("DeepSpaceRelay").PowerConsumptionRate, Is.EqualTo(new ExpantaNum(110)));
        Assert.That(DataBase<Building>.Find("DeepSpaceRelay").LogisticsConsumptionRate, Is.EqualTo(new ExpantaNum(35)));
    }

    [Test]
    public void SpacerRoutesKeepCampaignUnlockSeparateFromLaterLogisticsSupport()
    {
        Research fleet = DataBase<Research>.Find("DeepSpaceFleet");
        Research logistics = DataBase<Research>.Find("OrbitalLogisticsInfrastructure");
        Research survey = DataBase<Research>.Find("DeepSpaceSurvey");

        Assert.That(fleet, Is.Not.Null);
        Assert.That(logistics, Is.Not.Null);
        Assert.That(survey, Is.Not.Null);
        Assert.That(HasPrerequisite(fleet, "LogisticsManagement"), Is.True);
        Assert.That(HasPrerequisite(fleet, "OrbitalLogisticsInfrastructure"), Is.False);
        Assert.That(HasPrerequisite(logistics, "DeepSpaceFleet"), Is.True);
        Assert.That(HasPrerequisite(survey, "QuantumComputing"), Is.True);
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
    public void 太空工坊形成研究后的持续成长()
    {
        WorkshopUpgradeDefinition launchStages =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/ReusableLaunchStages");
        WorkshopUpgradeDefinition habitatSystems =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/ModularHabitatSystems");
        WorkshopUpgradeDefinition shipyardAssembly =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/AutomatedShipyardAssembly");
        WorkshopUpgradeDefinition cryogenicFuel =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/CryogenicFuelSystems");
        WorkshopUpgradeDefinition compositeLayup =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/AdvancedCompositeLayup");
        WorkshopUpgradeDefinition phaseFieldContainment =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/PhaseFieldContainment");
        WorkshopUpgradeDefinition orbitalThermalManagement =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/OrbitalThermalManagement");

        Assert.That(launchStages, Is.Not.Null, "可复用发射级工坊必须存在。");
        Assert.That(habitatSystems, Is.Not.Null, "模块化空间舱工坊必须存在。");
        Assert.That(shipyardAssembly, Is.Not.Null, "自动化船坞装配工坊必须存在。");
        Assert.That(cryogenicFuel, Is.Not.Null, "低温推进剂系统工坊必须存在。");
        Assert.That(compositeLayup, Is.Not.Null, "先进复合材料铺层工坊必须存在。");
        Assert.That(orbitalThermalManagement, Is.Not.Null, "轨道热管理工坊必须存在。");
        Assert.That(HasWorkshopResearch(launchStages, "OrbitalEngineering"), Is.True);
        Assert.That(HasWorkshopResearch(habitatSystems, "OrbitalHabitation"), Is.True);
        Assert.That(HasWorkshopUpgrade(habitatSystems, "ReusableLaunchStages"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(habitatSystems, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(habitatSystems, "Cloth"), Is.True);
        Assert.That(HasWorkshopResearch(shipyardAssembly, "DeepSpaceShipbuilding"), Is.True);
        Assert.That(HasWorkshopUpgrade(shipyardAssembly, "ModularHabitatSystems"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "Lubricant"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "PhantomAlloy"), Is.False);
        Assert.That(HasWorkshopResourceRequirement(shipyardAssembly, "PhantomWeave"), Is.False);
        Assert.That(HasWorkshopEffect(launchStages, "LaunchCenter"), Is.True);
        Assert.That(HasWorkshopEffect(habitatSystems, "OrbitalStation"), Is.True);
        Assert.That(HasWorkshopEffect(shipyardAssembly, "Shipyard"), Is.True);
        Assert.That(HasWorkshopResearch(orbitalThermalManagement, "OrbitalPowerBeaming"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Aluminum"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "CopperWire"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Ceramic"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "Rubber"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(orbitalThermalManagement, "TitaniumAlloy"), Is.True);
        Assert.That(
            HasWorkshopBuildingEffectType(
                orbitalThermalManagement,
                "OrbitalSolarArray",
                WorkshopEffectType.BuildingPowerProductionMultiplier),
            Is.True);
        Assert.That(HasResourceEffect(cryogenicFuel, "RocketFuel"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(cryogenicFuel, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(cryogenicFuel, "PhantomWeave"), Is.False);
        Assert.That(HasResourceEffect(compositeLayup, "Composite"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(compositeLayup, "Nickel"), Is.True);
        Assert.That(phaseFieldContainment, Is.Not.Null);
        Assert.That(HasWorkshopEffectType(phaseFieldContainment, WorkshopEffectType.MilitaryMultiplier), Is.True);
        Assert.That(HasWorkshopEffectType(
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/DeepSpaceNetworkAutomation"),
            WorkshopEffectType.GlobalLogisticsMultiplier), Is.True);
        Assert.That(HasWorkshopResourceRequirement(
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/DeepSpaceNetworkAutomation"),
            "PhaseMaterial"), Is.True);
    }

    [Test]
    public void SpacerQuantumComputingSliceUsesAdvancedMaterials()
    {
        Research quantumComputing = DataBase<Research>.Find("QuantumComputing");
        Building quantumArray = DataBase<Building>.Find("QuantumComputingArray");
        WorkshopUpgradeDefinition errorCorrection =
            DataBase<WorkshopUpgradeDefinition>.Find("QuantumErrorCorrection");

        Assert.That(quantumComputing, Is.Not.Null);
        Assert.That(quantumArray, Is.Not.Null);
        Assert.That(errorCorrection, Is.Not.Null);
        Assert.That(quantumComputing.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(quantumComputing.AdvancesTechLevel, Is.False);
        Assert.That(quantumArray.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(errorCorrection.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(HasResourceRequirement(quantumComputing, "PhantomAlloy"), Is.True);
        Assert.That(HasResourceRequirement(quantumComputing, "PhantomWeave"), Is.True);
        Assert.That(HasResourceRequirement(quantumComputing, "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingEffect(quantumComputing, "QuantumComputingArray"), Is.True);
        Assert.That(HasWorkshopEffect(errorCorrection, "QuantumComputingArray"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(errorCorrection, "Nickel"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(errorCorrection, "PhaseMaterial"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "PhantomAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "PhantomWeave"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "Nickel"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceConsumption("QuantumComputingArray", "PhaseMaterial"), Is.True);
    }

    [Test]
    public void CeramicRemainsAUsefulSpacerStructureMaterial()
    {
        Assert.That(HasBuildingResourceRequirement("QuantumComputingArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingResourceRequirement("QuantumComputingArray", "Nickel"), Is.True);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "Ceramic"), Is.True);
    }

    [Test]
    public void SpacerLogisticsHubTurnsAdvancedMaterialsIntoFleetSupport()
    {
        Building hub = DataBase<Building>.Find("OrbitalLogisticsHub");
        Research infrastructure = DataBase<Research>.Find("OrbitalLogisticsInfrastructure");
        WorkshopUpgradeDefinition automation =
            DataBase<WorkshopUpgradeDefinition>.Find("AutonomousFleetLogistics");

        Assert.That(hub, Is.Not.Null);
        Assert.That(infrastructure, Is.Not.Null);
        Assert.That(automation, Is.Not.Null);
        Assert.That(hub.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(hub.LogisticsProductionRate, Is.GreaterThan(hub.LogisticsConsumptionRate));
        Assert.That(HasRequiredResearch("OrbitalLogisticsHub", "OrbitalLogisticsInfrastructure"), Is.True);
        Assert.That(HasResourceRequirement(infrastructure, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceRequirement(infrastructure, "RocketFuel"), Is.True);
        Assert.That(HasWorkshopEffect(automation, "OrbitalLogisticsHub"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "PhantomWeave"), Is.False);
        Assert.That(HasWorkshopResourceRequirement(automation, "Lubricant"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(automation, "Rubber"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalLogisticsHub", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("OrbitalLogisticsHub", "PhantomWeave"), Is.False);
        Assert.That(HasBuildingResourceRequirement("OrbitalLogisticsHub", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceRequirement("OrbitalLogisticsHub", "PhantomWeave"), Is.False);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceConsumption("Shipyard", "PhantomAlloy"), Is.False);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "PhantomAlloy"), Is.False);
        Assert.That(HasBuildingResourceRequirement("Shipyard", "PhantomWeave"), Is.False);
    }

    [Test]
    public void SpacerDeepSpaceSurveyTurnsQuantumComputingIntoExplorationSupport()
    {
        Building observatory = DataBase<Building>.Find("DeepSpaceObservatory");
        Research survey = DataBase<Research>.Find("DeepSpaceSurvey");
        WorkshopUpgradeDefinition drones =
            DataBase<WorkshopUpgradeDefinition>.Find("AutonomousSurveyDrones");

        Assert.That(observatory, Is.Not.Null);
        Assert.That(survey, Is.Not.Null);
        Assert.That(drones, Is.Not.Null);
        Assert.That(observatory.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(survey.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(drones.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(HasRequiredResearch("DeepSpaceObservatory", "DeepSpaceSurvey"), Is.True);
        Assert.That(HasPrerequisite(survey, "QuantumComputing"), Is.True);
        Assert.That(HasResourceRequirement(survey, "PhantomAlloy"), Is.True);
        Assert.That(HasResourceRequirement(survey, "PhantomWeave"), Is.True);
        Assert.That(HasResourceRequirement(survey, "Explosives"), Is.True);
        Assert.That(observatory.AttackPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(observatory.DefensePowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(observatory.SpaceCost, Is.EqualTo(new ExpantaNum(440)));
        Assert.That(observatory.ProductivityConsumption, Is.EqualTo(new ExpantaNum(620)));
        Assert.That(observatory.ResearchPowerGranted, Is.EqualTo(new ExpantaNum(220)));
        Assert.That(observatory.PowerConsumptionRate, Is.EqualTo(new ExpantaNum(160)));
        Assert.That(observatory.LogisticsConsumptionRate, Is.EqualTo(new ExpantaNum(40)));
        Assert.That(HasWorkshopResearch(drones, "DeepSpaceSurvey"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(drones, "TitaniumAlloy"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(drones, "Explosives"), Is.True);
        Assert.That(HasWorkshopResourceRequirement(drones, "PhaseMaterial"), Is.True);
        Assert.That(HasWorkshopEffectType(drones, WorkshopEffectType.BuildingResearchPowerMultiplier), Is.True);
        Assert.That(HasWorkshopEffectType(drones, WorkshopEffectType.MilitaryMultiplier), Is.True);
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
        WorkshopUpgradeDefinition workshop,
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

    private static bool HasWorkshopResearch(
        WorkshopUpgradeDefinition workshop,
        string researchId)
    {
        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
            if (workshop.RequiredResearch[i] != null && workshop.RequiredResearch[i].Id == researchId)
                return true;
        return false;
    }

    private static bool HasWorkshopUpgrade(
        WorkshopUpgradeDefinition workshop,
        string upgradeId)
    {
        for (int i = 0; i < workshop.RequiredUpgrades.Count; i++)
            if (workshop.RequiredUpgrades[i] != null && workshop.RequiredUpgrades[i].Id == upgradeId)
                return true;
        return false;
    }

    private static bool HasWorkshopEffect(
        WorkshopUpgradeDefinition workshop,
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
        WorkshopUpgradeDefinition workshop,
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
        WorkshopUpgradeDefinition workshop,
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
        WorkshopUpgradeDefinition workshop,
        WorkshopEffectType type)
    {
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
            if (workshop.Effects[i] != null && workshop.Effects[i].Type == type)
                return true;
        return false;
    }
}
