using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingVerticalSliceTests
{
    [Test]
    public void FoodCapacity_IsLimitedToThePlannedFoodBuildings()
    {
        Assert.That(DataBase<BuildingDef>.Find("Granary").FoodCapacityGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<BuildingDef>.Find("CeramicKiln").FoodCapacityGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<BuildingDef>.Find("WoodHouse").FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(DataBase<BuildingDef>.Find("KnowledgeCircle").FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void WoodHouse_IsTheZeroProductivityCostStartingPopulationBootstrap()
    {
        BuildingDef woodHouse = DataBase<BuildingDef>.Find("WoodHouse");
        Assert.That(woodHouse.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(woodHouse.ProductivityConsumption, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(woodHouse.RequiredResearch, Is.Empty);
        Assert.That(woodHouse.RequiredWorkshopUpgrades, Is.Empty);
    }

    [Test]
    public void HousingChainScalesCapacityWithoutCreatingFoodCapacity()
    {
        BuildingDef woodHouse = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingDef stoneHouse = DataBase<BuildingDef>.Find("StoneHouse");
        BuildingDef townHouse = DataBase<BuildingDef>.Find("TownHouse");
        BuildingDef industrialHousing = DataBase<BuildingDef>.Find("IndustrialHabitationComplex");
        BuildingDef orbitalHousing = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");

        Assert.That(woodHouse, Is.Not.Null);
        Assert.That(stoneHouse, Is.Not.Null);
        Assert.That(townHouse, Is.Not.Null);
        Assert.That(industrialHousing, Is.Not.Null);
        Assert.That(orbitalHousing, Is.Not.Null);
        Assert.That(woodHouse.UpgradeTo, Is.SameAs(stoneHouse));
        Assert.That(stoneHouse.UpgradeTo, Is.SameAs(townHouse));
        Assert.That(townHouse.UpgradeTo, Is.SameAs(industrialHousing));
        Assert.That(industrialHousing.UpgradeTo, Is.SameAs(orbitalHousing));
        Assert.That(woodHouse.PopulationCapacityGranted,
            Is.LessThan(stoneHouse.PopulationCapacityGranted));
        Assert.That(stoneHouse.PopulationCapacityGranted,
            Is.LessThan(townHouse.PopulationCapacityGranted));
        Assert.That(townHouse.PopulationCapacityGranted,
            Is.LessThan(industrialHousing.PopulationCapacityGranted));
        Assert.That(industrialHousing.PopulationCapacityGranted,
            Is.LessThan(orbitalHousing.PopulationCapacityGranted));
        Assert.That(orbitalHousing.PopulationCapacityGranted,
            Is.GreaterThan(industrialHousing.PopulationCapacityGranted * 10d));
        Assert.That(woodHouse.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(stoneHouse.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(townHouse.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(industrialHousing.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbitalHousing.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(woodHouse.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(stoneHouse.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(townHouse.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(industrialHousing.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbitalHousing.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(industrialHousing.ProductivityConsumption,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(orbitalHousing.ProductivityConsumption,
            Is.GreaterThan(industrialHousing.ProductivityConsumption));
    }

    [Test]
    public void IndustrialAndSpacerHaveDedicatedHousingBuildings()
    {
        BuildingDef industrial = DataBase<BuildingDef>.Find("IndustrialHabitationComplex");
        BuildingDef orbital = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");

        Assert.That(industrial.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(industrial.PopulationCapacityGranted, Is.EqualTo(new ExpantaNum(240)));
        Assert.That(industrial.ResourceConsumptionRates, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(orbital.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbital.PopulationCapacityGranted, Is.EqualTo(new ExpantaNum(3000)));
        Assert.That(orbital.SpaceCost, Is.EqualTo(new ExpantaNum(650)));
        Assert.That(orbital.ResourceRequirements, Has.Count.GreaterThanOrEqualTo(7));
        Assert.That(industrial.UpgradeTo, Is.SameAs(orbital));
    }

    [Test]
    public void IndustrialAndSpacerLogisticsNodesProvideFoodBufferWithoutProducingFood()
    {
        BuildingDef railHub = DataBase<BuildingDef>.Find("RailHub");
        BuildingDef orbitalHub = DataBase<BuildingDef>.Find("OrbitalStation");

        Assert.That(railHub, Is.Not.Null);
        Assert.That(orbitalHub, Is.Not.Null);
        Assert.That(railHub.FoodCapacityGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(orbitalHub.FoodCapacityGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(railHub.FoodProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbitalHub.FoodProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(railHub.LogisticsProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(orbitalHub.LogisticsProductionRate, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void 工业住宅必须区分居住理论实体工坊与建筑落地()
    {
        ResearchDef theory = DataBase<ResearchDef>.Find("IndustrialHabitationEngineering");
        WorkshopUpgradeDef standards = DataBase<WorkshopUpgradeDef>.Find("IndustrialHousingStandards");
        BuildingDef housing = DataBase<BuildingDef>.Find("IndustrialHabitationComplex");

        Assert.That(theory, Is.Not.Null);
        Assert.That(standards, Is.Not.Null);
        Assert.That(housing, Is.Not.Null);
        Assert.That(HasResearchPrerequisite(theory, "UrbanHousing"), Is.True);
        Assert.That(HasResearchPrerequisite(theory, "ConcreteEngineering"), Is.True);
        Assert.That(HasResearchEffect(theory, ResearchEffectType.PopulationGrowthMultiplier), Is.True);
        Assert.That(HasWorkshopResearch(standards, "IndustrialHabitationEngineering"), Is.True);
        Assert.That(HasBuildingResearch(housing, "IndustrialHabitationEngineering"), Is.True);
    }

    [Test]
    public void 太空住宅必须同时承担人口容量与持续运营成本()
    {
        BuildingDef housing = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");

        Assert.That(housing.PopulationCapacityGranted, Is.GreaterThan(new ExpantaNum(1000)));
        Assert.That(housing.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(housing.PowerConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(housing.LogisticsConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(housing.ResourceConsumptionRates, Has.Count.GreaterThanOrEqualTo(5));
        bool consumesTitaniumAlloy = false;
        for (int i = 0; i < housing.ResourceConsumptionRates.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = housing.ResourceConsumptionRates[i];
            if (pair.First != null && pair.First.Id == "TitaniumAlloy")
            {
                consumesTitaniumAlloy = true;
                break;
            }
        }

        Assert.That(consumesTitaniumAlloy, Is.True);
    }

    [Test]
    public void 太空住宅维护剖面必须由生态补给主导且不增加普通资源容量()
    {
        BuildingDef industrial = DataBase<BuildingDef>.Find("IndustrialHabitationComplex");
        BuildingDef orbital = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");

        Assert.That(industrial, Is.Not.Null);
        Assert.That(
            orbital.PopulationCapacityGranted,
            Is.GreaterThan(industrial.PopulationCapacityGranted * 10d));
        Assert.That(orbital.SpaceCost, Is.GreaterThan(industrial.SpaceCost * 10d));
        Assert.That(
            orbital.ProductivityConsumption,
            Is.GreaterThan(industrial.ProductivityConsumption * 4d));
        Assert.That(orbital.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbital.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbital.PowerConsumptionRate, Is.GreaterThan(industrial.PowerConsumptionRate));
        Assert.That(orbital.LogisticsConsumptionRate, Is.GreaterThan(industrial.LogisticsConsumptionRate));
        Assert.That(
            GetResourceRate(orbital, "Biomass"),
            Is.GreaterThan(GetResourceRate(orbital, "TitaniumAlloy") * 20d));
    }

    [Test]
    public void 工业与太空住宅必须使用符合时代的建造材料()
    {
        BuildingDef industrial = DataBase<BuildingDef>.Find("IndustrialHabitationComplex");
        BuildingDef orbital = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");

        Assert.That(industrial, Is.Not.Null);
        Assert.That(orbital, Is.Not.Null);
        Assert.That(HasBuildingRequirement(industrial, "Steel"), Is.True);
        Assert.That(HasBuildingRequirement(industrial, "Concrete"), Is.True);
        Assert.That(HasBuildingRequirement(industrial, "Glass"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "Composite"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "Biomass"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "PhaseMaterial"), Is.True);
        Assert.That(orbital.ResourceRequirements.Count,
            Is.GreaterThan(industrial.ResourceRequirements.Count));
    }

    private static bool HasResearchPrerequisite(ResearchDef research, string id)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
            if (research.Prerequisites[i] != null && research.Prerequisites[i].Id == id)
                return true;
        return false;
    }

    private static bool HasResearchEffect(ResearchDef research, ResearchEffectType type)
    {
        for (int i = 0; i < research.Effects.Count; i++)
            if (research.Effects[i] != null && research.Effects[i].Type == type)
                return true;
        return false;
    }

    private static bool HasWorkshopResearch(WorkshopUpgradeDef workshop, string id)
    {
        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
            if (workshop.RequiredResearch[i] != null && workshop.RequiredResearch[i].Id == id)
                return true;
        return false;
    }

    private static bool HasBuildingResearch(BuildingDef building, string id)
    {
        for (int i = 0; i < building.RequiredResearch.Count; i++)
            if (building.RequiredResearch[i] != null && building.RequiredResearch[i].Id == id)
                return true;
        return false;
    }

    private static bool HasBuildingRequirement(BuildingDef building, string id)
    {
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = building.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == id && pair.Second > ExpantaNum.Zero)
                return true;
        }

        return false;
    }

    private static ExpantaNum GetResourceRate(BuildingDef building, string resourceId)
    {
        for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = building.ResourceConsumptionRates[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second;
        }

        return ExpantaNum.Zero;
    }
}
