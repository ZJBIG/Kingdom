using NUnit.Framework;
using System.Collections.Generic;

public sealed class SpaceContentTests
{
    [Test]
    public void C702_SpaceBuildingsUseSpacerTechLevelAndStableIds()
    {
        BuildingDef launchCenter = DataBase<BuildingDef>.Find("LaunchCenter");
        BuildingDef orbitalStation = DataBase<BuildingDef>.Find("OrbitalStation");
        BuildingDef shipyard = DataBase<BuildingDef>.Find("Shipyard");
        BuildingDef observatory = DataBase<BuildingDef>.Find("DeepSpaceObservatory");

        Assert.That(launchCenter.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbitalStation.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(shipyard.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(observatory.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(launchCenter.Id, Is.EqualTo("LaunchCenter"));
        Assert.That(orbitalStation.Id, Is.EqualTo("OrbitalStation"));
        Assert.That(shipyard.Id, Is.EqualTo("Shipyard"));
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<ResourceDef>.Find("Electronics")), Is.True);
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<ResourceDef>.Find("RocketFuel")), Is.True);
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhantomWeave")), Is.True);
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhaseMaterial")), Is.True);
    }

    [Test]
    public void C702_SpaceResourcesHaveSourcesAndMultipleSinks()
    {
        ResourceDef rocketFuel = DataBase<ResourceDef>.Find("RocketFuel");
        ResourceDef composite = DataBase<ResourceDef>.Find("Composite");
        BuildingDef chemicalPlant = DataBase<BuildingDef>.Find("ChemicalPlant");
        BuildingDef machineFactory = DataBase<BuildingDef>.Find("MachineFactory");

        Assert.That(HasRate(chemicalPlant.ResourceGenerationRates, rocketFuel), Is.True);
        Assert.That(FindRate(machineFactory.ResourceGenerationRates, composite),
            Is.GreaterThan(0d));
        Assert.That(CountReferences(DataBase<BuildingDef>.All, rocketFuel), Is.GreaterThanOrEqualTo(2));
        Assert.That(CountReferences(DataBase<BuildingDef>.All, composite), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void C703_SpaceLogisticsWorkshopsHaveReadableChineseLabelsAndDescriptions()
    {
        string[] workshopIds =
        {
            "CryogenicFuelSystems",
            "DeepSpaceNetworkAutomation",
            "ModularHabitatSystems",
            "PhaseFieldContainment",
            "AdvancedCompositeLayup",
            "AutomatedShipyardAssembly",
            "ReusableLaunchStages"
        };
        for (int i = 0; i < workshopIds.Length; i++)
        {
            WorkshopUpgradeDef workshop =
                DataBase<WorkshopUpgradeDef>.Find(workshopIds[i]);
            Assert.That(workshop.Label, Does.Not.Contain("閸"), workshopIds[i]);
            Assert.That(workshop.Description, Does.Not.Contain("閸"), workshopIds[i]);
            Assert.That(string.IsNullOrWhiteSpace(workshop.Label), Is.False, workshopIds[i]);
            Assert.That(string.IsNullOrWhiteSpace(workshop.Description), Is.False, workshopIds[i]);
        }
    }

    [Test]
    public void 太空居住与支援设施必须持续消耗食品而不是绕过补给()
    {
        string[] suppliedBuildingIds =
        {
            "OrbitalStation",
            "DeepSpaceRelay",
            "DeepSpaceObservatory",
            "LaunchCenter",
            "Shipyard",
            "PhantomMaterialsFabricator",
            "PhaseMaterialSynthesisArray",
            "QuantumComputingArray"
        };

        for (int i = 0; i < suppliedBuildingIds.Length; i++)
        {
            BuildingDef building = DataBase<BuildingDef>.Find(suppliedBuildingIds[i]);
            Assert.That(building.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero), suppliedBuildingIds[i]);
        }

        BuildingDef solarArray = DataBase<BuildingDef>.Find("OrbitalSolarArray");
        Assert.That(solarArray.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(solarArray.SpaceCost, Is.GreaterThanOrEqualTo(new ExpantaNum(420d)));
        Assert.That(solarArray.ProductivityConsumption, Is.GreaterThanOrEqualTo(new ExpantaNum(500d)));
        Assert.That(solarArray.PowerProductionRate, Is.GreaterThanOrEqualTo(new ExpantaNum(360d)));
        Assert.That(solarArray.LogisticsConsumptionRate, Is.GreaterThanOrEqualTo(new ExpantaNum(8d)));
    }

    private static bool HasRate(
        System.Collections.Generic.IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
        ResourceDef resource)
    {
        for (int i = 0; i < pairs.Count; i++)
            if (pairs[i].First == resource && pairs[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static double FindRate(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates,
        ResourceDef resource)
    {
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First == resource)
                return rates[i].Second.ToDouble();
        return 0d;
    }

    private static int CountReferences(
        System.Collections.Generic.IReadOnlyList<BuildingDef> buildings,
        ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < buildings.Count; i++)
        {
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = buildings[i].ResourceRequirements;
            for (int j = 0; j < requirements.Count; j++)
                if (requirements[j].First == resource && requirements[j].Second > ExpantaNum.Zero)
                {
                    count++;
                    break;
                }
        }
        return count;
    }
}
