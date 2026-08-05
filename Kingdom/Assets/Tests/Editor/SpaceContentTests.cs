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
        Assert.That(rocketFuel.DisplayerSet, Is.EqualTo(Resource.Set.SpaceEraSet));
        Assert.That(composite.DisplayerSet, Is.EqualTo(Resource.Set.SpaceEraSet));
    }

    [Test]
    public void C702_SpaceBuildingsUseSpacerTechLevelAndStableIds()
    {
        Building launchCenter = DataBase<Building>.Find("LaunchCenter");
        Building orbitalStation = DataBase<Building>.Find("OrbitalStation");
        Building shipyard = DataBase<Building>.Find("Shipyard");

        Assert.That(launchCenter.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbitalStation.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(shipyard.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(launchCenter.Id, Is.EqualTo("LaunchCenter"));
        Assert.That(orbitalStation.Id, Is.EqualTo("OrbitalStation"));
        Assert.That(shipyard.Id, Is.EqualTo("Shipyard"));
    }

    [Test]
    public void C702_SpaceResourcesHaveSourcesAndMultipleSinks()
    {
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
        Resource composite = DataBase<Resource>.Find("Composite");
        Building chemicalPlant = DataBase<Building>.Find("ChemicalPlant");
        Building machineFactory = DataBase<Building>.Find("MachineFactory");

        Assert.That(HasRate(chemicalPlant.ResourceGenerationRates, rocketFuel), Is.False);
        Assert.That(HasRate(machineFactory.ResourceGenerationRates, composite), Is.False);
        Assert.That(CountReferences(DataBase<Building>.All, rocketFuel), Is.GreaterThanOrEqualTo(2));
        Assert.That(CountReferences(DataBase<Building>.All, composite), Is.GreaterThanOrEqualTo(2));
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
