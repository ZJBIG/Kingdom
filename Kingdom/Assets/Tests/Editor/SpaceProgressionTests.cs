using NUnit.Framework;
using UnityEngine;

public sealed class SpaceProgressionTests
{
    [Test]
    public void 工业到太空研究链完整()
    {
        Research orbitalEngineering = Resources.Load<Research>("Datas/Research/Spacer/OrbitalEngineering");
        Research orbitalHabitation = Resources.Load<Research>("Datas/Research/Spacer/OrbitalHabitation");
        Research deepSpaceShipbuilding = Resources.Load<Research>("Datas/Research/Spacer/DeepSpaceShipbuilding");
        Research phaseFieldNavigation = Resources.Load<Research>("Datas/Research/Spacer/PhaseFieldNavigation");

        Assert.That(orbitalEngineering, Is.Not.Null, "轨道工程研究必须存在。");
        Assert.That(orbitalHabitation, Is.Not.Null, "轨道空间站工程研究必须存在。");
        Assert.That(deepSpaceShipbuilding, Is.Not.Null, "深空舰船制造研究必须存在。");
        Assert.That(phaseFieldNavigation, Is.Not.Null, "相位航行研究必须存在。");
        Assert.That(orbitalEngineering.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbitalHabitation.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(deepSpaceShipbuilding.TechLevel, Is.EqualTo(TechLevel.Spacer));

        Assert.That(HasPrerequisite(orbitalEngineering, "PowerGridEngineering"), Is.True);
        Assert.That(HasPrerequisite(orbitalEngineering, "CombustionEngines"), Is.True);
        Assert.That(HasPrerequisite(orbitalHabitation, "OrbitalEngineering"), Is.True);
        Assert.That(HasPrerequisite(orbitalHabitation, "FirstContact"), Is.True);
        Assert.That(HasPrerequisite(deepSpaceShipbuilding, "OrbitalHabitation"), Is.True);
        Assert.That(HasPrerequisite(deepSpaceShipbuilding, "DeepSpaceFleet"), Is.True);
        Assert.That(HasPrerequisite(DataBase<Research>.Find("DeepSpaceFleet"), "LogisticsManagement"), Is.True);
        Assert.That(HasPrerequisite(phaseFieldNavigation, "PhantomMaterials"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Machinery"), Is.True);
        Assert.That(HasResourceRequirement(DataBase<Research>.Find("DeepSpaceFleet"), "Engine"), Is.True);

        Assert.That(HasBuildingEffect(orbitalEngineering, "LaunchCenter"), Is.True);
        Assert.That(HasBuildingEffect(orbitalHabitation, "OrbitalStation"), Is.True);
        Assert.That(HasBuildingEffect(deepSpaceShipbuilding, "Shipyard"), Is.True);
        Assert.That(HasBuildingEffect(phaseFieldNavigation, "DeepSpaceRelay"), Is.True);
    }

    [Test]
    public void 太空建筑必须经过对应研究()
    {
        Assert.That(HasRequiredResearch("LaunchCenter", "OrbitalEngineering"), Is.True);
        Assert.That(HasRequiredResearch("OrbitalStation", "OrbitalHabitation"), Is.True);
        Assert.That(HasRequiredResearch("Shipyard", "DeepSpaceShipbuilding"), Is.True);
        Assert.That(HasRequiredResearch("DeepSpaceRelay", "PhaseFieldNavigation"), Is.True);

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

        Assert.That(launchStages, Is.Not.Null, "可复用发射级工坊必须存在。");
        Assert.That(habitatSystems, Is.Not.Null, "模块化空间舱工坊必须存在。");
        Assert.That(shipyardAssembly, Is.Not.Null, "自动化船坞装配工坊必须存在。");
        Assert.That(cryogenicFuel, Is.Not.Null, "低温推进剂系统工坊必须存在。");
        Assert.That(compositeLayup, Is.Not.Null, "先进复合材料铺层工坊必须存在。");
        Assert.That(HasWorkshopResearch(launchStages, "OrbitalEngineering"), Is.True);
        Assert.That(HasWorkshopResearch(habitatSystems, "OrbitalHabitation"), Is.True);
        Assert.That(HasWorkshopUpgrade(habitatSystems, "ReusableLaunchStages"), Is.True);
        Assert.That(HasWorkshopResearch(shipyardAssembly, "DeepSpaceShipbuilding"), Is.True);
        Assert.That(HasWorkshopUpgrade(shipyardAssembly, "ModularHabitatSystems"), Is.True);
        Assert.That(HasWorkshopEffect(launchStages, "LaunchCenter"), Is.True);
        Assert.That(HasWorkshopEffect(habitatSystems, "OrbitalStation"), Is.True);
        Assert.That(HasWorkshopEffect(shipyardAssembly, "Shipyard"), Is.True);
        Assert.That(HasResourceEffect(cryogenicFuel, "RocketFuel"), Is.True);
        Assert.That(HasResourceEffect(compositeLayup, "Composite"), Is.True);
        Assert.That(phaseFieldContainment, Is.Not.Null);
        Assert.That(HasWorkshopEffectType(phaseFieldContainment, WorkshopEffectType.MilitaryMultiplier), Is.True);
        Assert.That(HasWorkshopEffectType(
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/DeepSpaceNetworkAutomation"),
            WorkshopEffectType.GlobalLogisticsMultiplier), Is.True);
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
