using System.Linq;
using NUnit.Framework;

public sealed class LateFactoryConsolidationTests
{
    [Test]
    public void OrbitalProcessingRetainsItsIndustrialFeedstocks()
    {
        BuildingDef wires = DataBase<BuildingDef>.Find("OrbitalWireWorks");
        foreach (string id in new[] { "BasicMetalIngot", "Ceramic" })
            Assert.That(HasPositiveRate(wires.ResourceConsumptionRates, DataBase<ResourceDef>.Find(id)), Is.True, id);
        BuildingDef materials = DataBase<BuildingDef>.Find("OrbitalBuildingMaterialsWorks");
        foreach (string id in new[] { "StoneBrick", "Clay", "Coke", "Chemical" })
            Assert.That(HasPositiveRate(materials.ResourceConsumptionRates, DataBase<ResourceDef>.Find(id)), Is.True, id);
    }

    [Test]
    public void UltraAssemblyRequiresIndustrialThroughputForAdvancedMaterials()
    {
        BuildingDef factory = DataBase<BuildingDef>.Find("AutonomousMatterFabricator");
        double titanium = factory.ResourceConsumptionRates.First(pair => pair.First.Id == "TitaniumAlloy").Second.ToDouble();
        double alloy = factory.ResourceGenerationRates.First(pair => pair.First.Id == "PhantomAlloy").Second.ToDouble();
        Assert.That(titanium / alloy, Is.GreaterThanOrEqualTo(10d),
            "Advanced assembly must retain substantial titanium feedstock per unit of phantom alloy.");
        foreach (string id in new[] { "Electronics", "Machinery", "AdvancedMetalIngot" })
            Assert.That(factory.ResourceConsumptionRates.First(pair => pair.First.Id == id).Second.ToDouble(),
                Is.GreaterThan(5d), id);
    }

    [Test]
    public void SpacerMaterialChainsConvergeOnOneComprehensiveFactory()
    {
        BuildingDef hub = DataBase<BuildingDef>.Find("OrbitalResourceExtractionArray");
        Assert.That(hub, Is.Not.Null);

        string[] legacyMaterialFactories =
        {
            "OrbitalBuildingMaterialsWorks",
            "OrbitalCarbonizationComplex",
            "OrbitalCryogenicPropellantArray",
            "OrbitalMachiningComplex",
            "OrbitalWireWorks",
            "PhantomMaterialsFabricator",
            "PhaseMaterialSynthesisArray"
        };

        foreach (string id in legacyMaterialFactories)
        {
            BuildingDef legacy = DataBase<BuildingDef>.Find(id);
            Assert.That(legacy.UpgradeTo, Is.SameAs(hub),
                $"后期材料链没有收敛到综合工厂：{id}");
        }

        string[] mergedOutputs =
        {
            "SpecialMetalOre", "AdvancedMetalOre", "BasicMetalOre", "Cloth", "TitaniumAlloy", "RocketFuel",
            "Glass", "Ceramic", "Concrete", "Machinery", "Engine", "Composite",
            "CopperWire", "Electronics", "PhaseMaterial"
        };

        foreach (string id in mergedOutputs)
        {
            ResourceDef resource = DataBase<ResourceDef>.Find(id);
            Assert.That(resource, Is.Not.Null, id);
            Assert.That(HasPositiveRate(hub.ResourceGenerationRates, resource), Is.True,
                $"综合工厂缺少主要材料产出：{id}");
        }

        Assert.That(hub.LogisticsContribution, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(hub.FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(hub.ProductivityConsumption >= new ExpantaNum(16000), Is.True,
            "Spacer 绁煎悎宸ュ巶蹇呴』淇濇寔宸ㄦ瀯绾х敓浜у姏娑堣€楋紒");
        Assert.That((-hub.PowerContribution) >= new ExpantaNum(5000), Is.True,
            "Spacer 绁煎悎宸ュ巶蹇呴』淇濇寔宸ㄦ瀯绾х數鍔涙秷鑰楋紒");
        Assert.That(hub.FoodConsumptionRate >= new ExpantaNum(24), Is.True,
            "Spacer 绁煎悎宸ュ巶蹇呴』淇濇寔宸ㄦ瀯绾ч鐗╂秷鑰楋紒");
        foreach (Pair<ResourceDef, ExpantaNum> generated in hub.ResourceGenerationRates)
        {
            Assert.That(hub.ResourceConsumptionRates.Any(consumed =>
                consumed.First == generated.First), Is.False,
                $"综合工厂不能同时生产并持续消耗 {generated.First.Id}");
        }
    }

    [Test]
    public void SpacerEcologyRemainsASeparateFoodAndBiomassMechanism()
    {
        BuildingDef ecology = DataBase<BuildingDef>.Find("OrbitalAgroecologyArray");
        Assert.That(ecology.UpgradeTo, Is.Null);
        Assert.That(ecology.FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(HasPositiveRate(
            ecology.ResourceGenerationRates, DataBase<ResourceDef>.Find("Biomass")), Is.True);
        Assert.That(HasPositiveRate(
            ecology.ResourceGenerationRates, DataBase<ResourceDef>.Find("WoodLog")), Is.True);
    }

    private static bool HasPositiveRate(
        System.Collections.Generic.IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates,
        ResourceDef resource)
    {
        if (rates == null || resource == null)
            return false;
        return rates.Any(item => item.First == resource && item.Second > ExpantaNum.Zero);
    }
}
