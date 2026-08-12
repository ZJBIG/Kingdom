using NUnit.Framework;
using System.Collections.Generic;

public sealed class SpaceContentTests
{
    [Test]
    public void C702_SpaceResourcesHaveStableIdsAndSpaceCategory()
    {
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
        Resource composite = DataBase<Resource>.Find("Composite");

        Assert.That(rocketFuel.Id, Is.EqualTo("RocketFuel"));
        Assert.That(composite.Id, Is.EqualTo("Composite"));
        Assert.That(string.IsNullOrWhiteSpace(rocketFuel.Label), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(composite.Label), Is.False);
    }

    [Test]
    public void C702_SpaceBuildingsUseSpacerTechLevelAndStableIds()
    {
        Building launchCenter = DataBase<Building>.Find("LaunchCenter");
        Building orbitalStation = DataBase<Building>.Find("OrbitalStation");
        Building shipyard = DataBase<Building>.Find("Shipyard");
        Building observatory = DataBase<Building>.Find("DeepSpaceObservatory");

        Assert.That(launchCenter.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbitalStation.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(shipyard.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(observatory.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(launchCenter.Id, Is.EqualTo("LaunchCenter"));
        Assert.That(orbitalStation.Id, Is.EqualTo("OrbitalStation"));
        Assert.That(shipyard.Id, Is.EqualTo("Shipyard"));
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<Resource>.Find("Electronics")), Is.True);
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<Resource>.Find("RocketFuel")), Is.True);
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<Resource>.Find("PhantomWeave")), Is.True);
        Assert.That(HasRate(observatory.ResourceConsumptionRates, DataBase<Resource>.Find("PhaseMaterial")), Is.True);
    }

    [Test]
    public void C702_SpaceResourcesHaveSourcesAndMultipleSinks()
    {
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
        Resource composite = DataBase<Resource>.Find("Composite");
        Building chemicalPlant = DataBase<Building>.Find("ChemicalPlant");
        Building machineFactory = DataBase<Building>.Find("MachineFactory");

        Assert.That(HasRate(chemicalPlant.ResourceGenerationRates, rocketFuel), Is.True);
        Assert.That(FindRate(machineFactory.ResourceGenerationRates, composite),
            Is.EqualTo(0.45d).Within(0.000001d));
        Assert.That(CountReferences(DataBase<Building>.All, rocketFuel), Is.GreaterThanOrEqualTo(2));
        Assert.That(CountReferences(DataBase<Building>.All, composite), Is.GreaterThanOrEqualTo(2));
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
            WorkshopUpgrade workshop =
                DataBase<WorkshopUpgrade>.Find(workshopIds[i]);
            Assert.That(workshop, Is.Not.Null, workshopIds[i]);
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
            "OrbitalHabitatMegastructure",
            "OrbitalLogisticsHub",
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
            Building building = DataBase<Building>.Find(suppliedBuildingIds[i]);
            Assert.That(building, Is.Not.Null, suppliedBuildingIds[i]);
            Assert.That(building.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero), suppliedBuildingIds[i]);
        }

        Building solarArray = DataBase<Building>.Find("OrbitalSolarArray");
        Assert.That(solarArray.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(solarArray.SpaceCost, Is.GreaterThanOrEqualTo(new ExpantaNum(420d)));
        Assert.That(solarArray.ProductivityConsumption, Is.GreaterThanOrEqualTo(new ExpantaNum(500d)));
        Assert.That(solarArray.PowerProductionRate, Is.GreaterThanOrEqualTo(new ExpantaNum(360d)));
        Assert.That(solarArray.LogisticsConsumptionRate, Is.GreaterThanOrEqualTo(new ExpantaNum(8d)));
    }

    private static bool HasRate(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        Resource resource)
    {
        for (int i = 0; i < pairs.Count; i++)
            if (pairs[i].First == resource && pairs[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static double FindRate(
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates,
        Resource resource)
    {
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First == resource)
                return rates[i].Second.ToDouble();
        return 0d;
    }

    private static int CountReferences(
        System.Collections.Generic.IReadOnlyList<Building> buildings,
        Resource resource)
    {
        int count = 0;
        for (int i = 0; i < buildings.Count; i++)
        {
            IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = buildings[i].ResourceRequirements;
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
