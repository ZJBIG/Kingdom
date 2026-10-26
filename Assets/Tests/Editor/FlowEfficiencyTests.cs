using NUnit.Framework;

public sealed class FlowEfficiencyTests
{
    [TestCase(-9d)]
    [TestCase(0d)]
    [TestCase(9d)]
    public void BuildingFlowContributionRetainsItsSignAndRefreshesCachedValues(double value)
    {
        var building = UnityEngine.ScriptableObject.CreateInstance<BuildingDef>();
        try
        {
            building.SetPowerContributionForEditor(1);
            building.SetLogisticsContributionForEditor(1);
            Assert.That(building.PowerContribution, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(building.LogisticsContribution, Is.GreaterThan(ExpantaNum.Zero));
            building.SetPowerContributionForEditor(value);
            building.SetLogisticsContributionForEditor(-value);
            Assert.That(building.PowerContribution.ToDouble(), Is.EqualTo(value).Within(1e-8));
            Assert.That(building.LogisticsContribution.ToDouble(), Is.EqualTo(-value).Within(1e-8));
        }
        finally { UnityEngine.Object.DestroyImmediate(building); }
    }

    [TestCase("DeepSpaceRelay", false, -23d)]
    [TestCase("EarthMoonLogisticsHub", false, 240d)]
    [TestCase("OrbitalResourceExtractionArray", false, 1200d)]
    [TestCase("OrbitalStation", false, 180d)]
    [TestCase("ThunderGatePropellantWorks", false, -38.666666666667d)]
    [TestCase("AutonomousMatterFabricator", false, 3000d)]
    [TestCase("PhaseEnergyArray", true, 1410d)]
    public void MigratedBuildingUsesOneAuthoredSignedContribution(string id, bool power, double expected)
    {
        BuildingDef building = DataBase<BuildingDef>.Find(id);
        ExpantaNum actual = power ? building.PowerContribution : building.LogisticsContribution;
        // Building values use ExpantaNum's six-decimal quantization contract.
        double quantizedExpected = new ExpantaNum(expected).ToDouble();
        Assert.That(actual.ToDouble(), Is.EqualTo(quantizedExpected).Within(System.Math.Abs(quantizedExpected) * 1e-9));
    }

    [Test]
    public void FlowSatisfaction_UsesProductionAgainstDemandWithoutInventory()
    {
        ExpantaNum shortage = GameManager.CalculateFlowSatisfaction(50, 100);
        Assert.That(shortage, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
        Assert.That(GameManager.CalculateFlowSatisfaction(100, 100), Is.GreaterThan(shortage));
        Assert.That(GameManager.CalculateFlowSatisfaction(-5, 100), Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(GameManager.CalculateFlowSatisfaction(0, 0), Is.GreaterThanOrEqualTo(ExpantaNum.One));
    }

    [Test]
    public void HappinessCurveIsMonotonicAndBounded()
    {
        ExpantaNum low = HappinessFormula.CalculateMultiplier(
            ExpantaNum.Zero,
            new ExpantaNum(10));
        ExpantaNum medium = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(90),
            new ExpantaNum(10));
        ExpantaNum high = HappinessFormula.CalculateMultiplier(
            new ExpantaNum("1e100000"),
            new ExpantaNum(10));

        Assert.That(low, Is.GreaterThanOrEqualTo(ExpantaNum.One));
        Assert.That(medium, Is.GreaterThan(low));
        Assert.That(high, Is.GreaterThan(medium));
        Assert.That(high, Is.LessThan(
            ExpantaNum.One + HappinessFormula.MaximumBonus));
    }

    [Test]
    public void StoredFoodPreventsPrematureHappinessPenalty()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(-100),
            new ExpantaNum(10));

        Assert.That(result, Is.GreaterThanOrEqualTo(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateMultiplier(-200, 10), Is.GreaterThanOrEqualTo(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateMultiplier(-100, 10, 0.5d), Is.LessThan(result));
    }

    [Test]
    public void HappinessOwnsFoodShortagePenalty()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(-10),
            new ExpantaNum(10),
            new ExpantaNum(0.5d));

        Assert.That(result, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateConstraintMultiplier(result),
            Is.LessThan(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateRewardMultiplier(result),
            Is.LessThanOrEqualTo(ExpantaNum.One));
    }

    [Test]
    public void MedicalHappinessBonusOnlyAppliesWhenFoodIsAvailable()
    {
        ExpantaNum healthy = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(90),
            new ExpantaNum(10),
            ExpantaNum.One,
            new ExpantaNum(0.08d));
        ExpantaNum shortage = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(90),
            new ExpantaNum(10),
            new ExpantaNum(0.5d),
            new ExpantaNum(0.08d));

        Assert.That(healthy, Is.GreaterThan(
            HappinessFormula.CalculateMultiplier(new ExpantaNum(90), new ExpantaNum(10))));
        Assert.That(shortage, Is.LessThan(ExpantaNum.One).And.LessThanOrEqualTo(
            HappinessFormula.CalculateMultiplier(90, 10, 0.5d)));
    }

    [Test]
    public void MedicalHappinessBonusSharesTheExistingUpperBound()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum("1e100000"),
            new ExpantaNum(10),
            ExpantaNum.One,
            new ExpantaNum(0.5d));

        Assert.That(result, Is.LessThanOrEqualTo(
            ExpantaNum.One + HappinessFormula.MaximumBonus));
    }

    [Test]
    public void HappinessWithZeroPopulationIsSafe()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(100),
            ExpantaNum.Zero);

        Assert.That(result.IsNaN, Is.False);
        Assert.That(result, Is.GreaterThanOrEqualTo(ExpantaNum.One));
    }
}
