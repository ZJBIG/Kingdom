using NUnit.Framework;
using UnityEngine;

public class IndustrialWorkshopAvailabilityTests
{
    [Test]
    public void WorkshopDefinitionsAreIndustrialOrLater()
    {
        WorkshopUpgrade[] definitions =
            Resources.LoadAll<WorkshopUpgrade>("Datas/Workshop");

        for (int i = 0; i < definitions.Length; i++)
            Assert.That(definitions[i].TechLevel, Is.GreaterThanOrEqualTo(TechLevel.Industrial), definitions[i].Id);
    }

    [Test]
    public void AddedIndustrialResearchHasRequirementsAndEffects()
    {
        string[] ids =
        {
            "PowerGridEngineering",
            "IndustrialChemistry",
            "IndustrialWorkshop",
            "ScientificInstrumentation",
            "Industrialization"
        };

        for (int i = 0; i < ids.Length; i++)
        {
            Research research = Resources.Load<Research>("Datas/Research/Industrial/" + ids[i]);
            Assert.That(research.TechLevel, Is.EqualTo(TechLevel.Industrial), ids[i]);
            Assert.That(research.Description, Is.Not.Empty, ids[i]);
            Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0), ids[i]);
            Assert.That(research.Effects.Count, Is.GreaterThan(0), ids[i]);
        }
    }

    [Test]
    public void IndustrialWorkshopClosureItemsHaveRealCostsAndEffects()
    {
        AssertWorkshop("ReinforcedBoilers", "Steel", "BasicMetalIngot", WorkshopEffectType.BuildingPowerProductionMultiplier, "SteamPlant");
        AssertWorkshop("RotaryKilns", "Steel", "Machinery", WorkshopEffectType.BuildingProductionMultiplier, "BuildingMaterialsComplex");
        WorkshopUpgrade rotaryKilns =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/RotaryKilns");
        Assert.That(rotaryKilns.ResourceRequirements, Has.Some.Matches<Pair<Resource, ExpantaNum>>(x =>
            x != null && x.First != null && x.First.Id == "Ceramic" && x.Second > ExpantaNum.Zero));
        WorkshopUpgrade integratedFurnaces =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/IntegratedFurnaces");
        Assert.That(integratedFurnaces.ResourceRequirements, Has.Some.Matches<Pair<Resource, ExpantaNum>>(x =>
            x != null && x.First != null && x.First.Id == "Ceramic" && x.Second > ExpantaNum.Zero));
        Assert.That(integratedFurnaces.Effects, Has.Some.Matches<WorkshopEffectDefinition>(x =>
            x != null && x.Type == WorkshopEffectType.BuildingProductionMultiplier &&
            x.Building != null && x.Building.Id == "IndustrialMetalSmelter" && x.NumericValue > ExpantaNum.One));
        AssertWorkshop("ElectricalInstrumentation", "CopperWire", "Glass", WorkshopEffectType.BuildingPowerProductionMultiplier, null);
        AssertWorkshop("ConveyorSystems", "Steel", "Machinery", WorkshopEffectType.GlobalLogisticsMultiplier, null);
    }

    [Test]
    public void CokeOvenOptimizationBoostsCokeOutput()
    {
        WorkshopUpgrade definition =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/CokeOvenOptimization");
        Assert.That(definition.TechLevel, Is.EqualTo(TechLevel.Industrial));
        bool requiresCoking = false;
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
            requiresCoking |= definition.RequiredResearch[i] != null && definition.RequiredResearch[i].Id == "Coking";
        Assert.That(requiresCoking, Is.True);

        bool found = false;
        for (int i = 0; i < definition.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = definition.Effects[i];
            if (effect.Type == WorkshopEffectType.ResourceProductionMultiplier &&
                effect.Resource != null && effect.Resource.Id == "Coke")
            {
                Assert.That(effect.NumericValue.ToDouble(), Is.GreaterThan(1d));
                found = true;
            }
        }

        Assert.That(found, Is.True);
    }

    [Test]
    public void DeepOilDrillingAddsARealResearchAndWorkshopProgression()
    {
        Research research = Resources.Load<Research>("Datas/Research/Industrial/DeepOilDrilling");
        Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0));
        Assert.That(research.Prerequisites, Has.Some.Matches<Research>(x =>
            x != null && x.Id == "IndustrialChemistry"));
        Assert.That(research.Effects, Has.Some.Matches<ResearchEffectDefinition>(x =>
            x != null && x.Type == ResearchEffectType.BuildingProductionMultiplier &&
            x.Building != null && x.Building.Id == "OilDerrick" &&
            x.NumericValue > ExpantaNum.One));
        WorkshopUpgrade workshop =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/RotaryDrillingHeads");
        Assert.That(workshop.ResourceRequirements.Count, Is.GreaterThan(0));
        Assert.That(workshop.RequiredResearch, Has.Some.Matches<Research>(x =>
            x != null && x.Id == "DeepOilDrilling"));
        Assert.That(workshop.RequiredUpgrades, Has.Some.Matches<WorkshopUpgrade>(x =>
            x != null && x.Id == "CokeOvenOptimization"));
        Assert.That(workshop.Effects, Has.Some.Matches<WorkshopEffectDefinition>(x =>
            x != null && x.Type == WorkshopEffectType.BuildingProductionMultiplier &&
            x.Building != null && x.Building.Id == "OilDerrick" &&
            x.NumericValue > ExpantaNum.One));

        Research explosivesResearch =
            Resources.Load<Research>("Datas/Research/Industrial/IndustrialExplosives");
        Assert.That(explosivesResearch.ResourceRequirements.Count, Is.GreaterThan(0));
        Assert.That(explosivesResearch.Effects, Has.Some.Matches<ResearchEffectDefinition>(x =>
            x != null && x.Type == ResearchEffectType.BuildingProductionMultiplier &&
            x.Building != null && x.Building.Id == "ChemicalPlant" &&
            x.NumericValue > ExpantaNum.One));

        WorkshopUpgrade blasting =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/ControlledBlasting");
        Assert.That(blasting.ResourceRequirements, Is.Not.Empty);
        Assert.That(blasting.Effects.Count, Is.GreaterThan(0));
        foreach (string buildingId in new[] { "IntegratedMetalMine", "OilDerrick" })
            Assert.That(blasting.Effects, Has.Some.Matches<WorkshopEffectDefinition>(x =>
                x != null && x.Type == WorkshopEffectType.BuildingProductionMultiplier &&
                x.Building != null && x.Building.Id == buildingId &&
            x.NumericValue > ExpantaNum.One), buildingId);
    }

    [Test]
    public void PetroleumResearchIsTheOilDerrickUnlockAndDeepDrillingIsTheUpgrade()
    {
        Research extraction = Resources.Load<Research>("Datas/Research/Industrial/PetroleumExtraction");
        Research deepDrilling = Resources.Load<Research>("Datas/Research/Industrial/DeepOilDrilling");
        Building oilDerrick = Resources.Load<Building>("Datas/Building/Industrial/OilDerrick");

        Assert.That(extraction, Is.Not.Null);
        Assert.That(deepDrilling, Is.Not.Null);
        Assert.That(oilDerrick.RequiredResearch, Has.Some.Matches<Research>(x =>
            x != null && x.Id == "PetroleumExtraction"));
        Assert.That(extraction.Label, Does.Contain("基础"));
        Assert.That(deepDrilling.Label, Does.Contain("增产"));
        Assert.That(deepDrilling.Effects, Has.Some.Matches<ResearchEffectDefinition>(x =>
            x != null && x.Building != null && x.Building.Id == "OilDerrick" &&
            x.Type == ResearchEffectType.BuildingProductionMultiplier));
    }

    [Test]
    public void LaboratoryGlasswareImprovesUniversityResearchPower()
    {
        AssertWorkshop(
            "LaboratoryGlassware",
            "Glass",
            "BasicMetalIngot",
            WorkshopEffectType.BuildingResearchPowerMultiplier,
            "University");
    }

    private static void AssertWorkshop(
        string id,
        string firstResource,
        string secondResource,
        WorkshopEffectType effectType,
        string targetBuilding)
    {
        WorkshopUpgrade definition =
            Resources.Load<WorkshopUpgrade>("Datas/Workshop/" + id);
        Assert.That(definition.ResourceRequirements[0].First.Id, Is.EqualTo(firstResource), id);
        Assert.That(definition.ResourceRequirements[1].First.Id, Is.EqualTo(secondResource), id);

        bool found = false;
        for (int i = 0; i < definition.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = definition.Effects[i];
            if (effect.Type != effectType)
                continue;
            if (targetBuilding == null ||
                (effect.Building != null && effect.Building.Id == targetBuilding))
                found = true;
        }

        Assert.That(found, Is.True, id);
    }

}
