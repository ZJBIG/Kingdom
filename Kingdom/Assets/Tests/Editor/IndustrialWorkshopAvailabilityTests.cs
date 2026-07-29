using NUnit.Framework;
using UnityEngine;

public class IndustrialWorkshopAvailabilityTests
{
    [Test]
    public void WorkshopDefinitionsAreIndustrialOrLater()
    {
        WorkshopUpgradeDefinition[] definitions =
            Resources.LoadAll<WorkshopUpgradeDefinition>("Datas/Workshop");

        for (int i = 0; i < definitions.Length; i++)
            Assert.That(definitions[i].TechLevel, Is.GreaterThanOrEqualTo(TechLevel.Industrial), definitions[i].Id);
    }

    [Test]
    public void DeferredStoneToolsAreNotWorkshopDefinitions()
    {
        Assert.That(Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/StoneAxe"), Is.Null);
        Assert.That(Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/StonePickaxe"), Is.Null);
    }

    [Test]
    public void AddedIndustrialResearchHasRequirementsEffectsAndNoCoordinates()
    {
        string[] ids =
        {
            "PowerGridEngineering",
            "AdvancedIndustrialMaterials",
            "AutomatedAssembly",
            "IndustrialLogistics",
            "ProcessControl",
            "ScientificInstrumentation"
        };

        for (int i = 0; i < ids.Length; i++)
        {
            Research research = Resources.Load<Research>("Datas/Research/Industrial/" + ids[i]);
            Assert.That(research, Is.Not.Null, ids[i]);
            Assert.That(research.TechLevel, Is.EqualTo(TechLevel.Industrial), ids[i]);
            Assert.That(research.Description, Is.Not.Empty, ids[i]);
            Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0), ids[i]);
            Assert.That(research.Effects.Count, Is.GreaterThan(0), ids[i]);
            Assert.That(research.x, Is.EqualTo(0f), ids[i]);
            Assert.That(research.y, Is.EqualTo(0f), ids[i]);
        }
    }

    [Test]
    public void WorkshopItemsHaveThePlannedResearchOrWorkshopPrerequisites()
    {
        AssertUpgradeRequires("AcademicJournals", "LaboratoryGlassware");
        AssertUpgradeRequires("AgriculturalMachinery", "DraftingTables");
        AssertUpgradeRequires("ChemicalCatalysts", "RotaryKilns");
        AssertUpgradeRequires("ConveyorSystems", "InterchangeableParts");
        AssertUpgradeRequires("ElectricalInstrumentation", "LaboratoryGlassware");
        AssertUpgradeRequires("InterchangeableParts", "PrecisionTooling");
        AssertUpgradeRequires("MechanicalLooms", "ConveyorSystems");
        AssertUpgradeRequires("PoweredMining", "InterchangeableParts");
        AssertUpgradeRequires("PrecisionTooling", "DraftingTables");
        AssertUpgradeRequires("ReinforcedConcrete", "RotaryKilns");
        AssertUpgradeRequires("RotaryKilns", "LaboratoryGlassware");
        AssertUpgradeRequires("ShiftRegisters", "ElectricalInstrumentation");
        AssertUpgradeRequires("StandardGauge", "ReinforcedBoilers");
        AssertUpgradeRequires("TelegraphDispatch", "ElectricalInstrumentation");

        WorkshopUpgradeDefinition[] definitions =
            Resources.LoadAll<WorkshopUpgradeDefinition>("Datas/Workshop");
        for (int i = 0; i < definitions.Length; i++)
            Assert.That(
                definitions[i].RequiredResearch.Count + definitions[i].RequiredUpgrades.Count,
                Is.GreaterThan(0),
                definitions[i].Id);
    }

    [Test]
    public void IndustrialWorkshopClosureItemsHaveRealCostsAndEffects()
    {
        AssertWorkshop("ReinforcedBoilers", "Coal", "Iron", WorkshopEffectType.BuildingPowerProductionMultiplier, "SteamPlant");
        AssertWorkshop("InterchangeableParts", "Steel", "Copper", WorkshopEffectType.BuildingProductionMultiplier, "MachineFactory");
        AssertWorkshop("RotaryKilns", "Coal", "Clay", WorkshopEffectType.BuildingProductionMultiplier, "Glassworks");
        AssertWorkshop("ElectricalInstrumentation", "CopperWire", "Glass", WorkshopEffectType.PowerMultiplier, null);
        AssertWorkshop("ConveyorSystems", "Machinery", "Steel", WorkshopEffectType.GlobalBuildingProductionMultiplier, null);
        AssertWorkshop("StandardGauge", "Steel", "Coke", WorkshopEffectType.BuildingLogisticsProductionMultiplier, "RailHub");
    }

    private static void AssertWorkshop(
        string id,
        string firstResource,
        string secondResource,
        WorkshopEffectType effectType,
        string targetBuilding)
    {
        WorkshopUpgradeDefinition definition =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/" + id);
        Assert.That(definition, Is.Not.Null, id);
        Assert.That(definition.ResourceRequirements.Count, Is.EqualTo(2), id);
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

    private static void AssertUpgradeRequires(string definitionId, string prerequisiteId)
    {
        WorkshopUpgradeDefinition definition =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/" + definitionId);
        WorkshopUpgradeDefinition prerequisite =
            Resources.Load<WorkshopUpgradeDefinition>("Datas/Workshop/" + prerequisiteId);
        Assert.That(definition, Is.Not.Null, definitionId);
        Assert.That(prerequisite, Is.Not.Null, prerequisiteId);

        bool found = false;
        for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
            found |= definition.RequiredUpgrades[i] == prerequisite;
        Assert.That(found, Is.True, $"{definitionId} -> {prerequisiteId}");
    }
}
