using System.Linq;
using NUnit.Framework;

public sealed class LateFactoryConsolidationTests
{
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

        Assert.That(hub.LogisticsProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(hub.FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(hub.ProductivityConsumption >= new ExpantaNum(16000), Is.True,
            "Spacer 绁煎悎宸ュ巶蹇呴』淇濇寔宸ㄦ瀯绾х敓浜у姏娑堣€楋紒");
        Assert.That(hub.PowerConsumptionRate >= new ExpantaNum(5000), Is.True,
            "Spacer 绁煎悎宸ュ巶蹇呴』淇濇寔宸ㄦ瀯绾х數鍔涙秷鑰楋紒");
        Assert.That(hub.LogisticsConsumptionRate >= new ExpantaNum(1200), Is.True,
            "Spacer 绁煎悎宸ュ巶蹇呴』淇濇寔宸ㄦ瀯绾х墿娴佹秷鑰楋紒");
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
