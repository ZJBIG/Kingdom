using NUnit.Framework;

public sealed class SimulationDeterminismTests
{
    [Test]
    public void FoodAndResourceIntegration_IsStableAcrossThirtyAndSixtyFpsSteps()
    {
        ExpantaNum foodAtThirtyFps = IntegrateFood(30, 1d / 30d);
        ExpantaNum foodAtSixtyFps = IntegrateFood(60, 1d / 60d);
        ExpantaNum resourceAtThirtyFps = IntegrateResource(30, 1d / 30d);
        ExpantaNum resourceAtSixtyFps = IntegrateResource(60, 1d / 60d);

        Assert.That(
            foodAtThirtyFps.ToDouble(),
            Is.EqualTo(foodAtSixtyFps.ToDouble()).Within(0.001d));
        Assert.That(
            resourceAtThirtyFps.ToDouble(),
            Is.EqualTo(resourceAtSixtyFps.ToDouble()).Within(0.001d));
    }

    [Test]
    public void OfflineElapsedTime_ClampsAndRejectsClockRollback()
    {
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(100, 370, 1000),
            Is.EqualTo(270d));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(100, 370, 120),
            Is.EqualTo(120d));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(100, 99, 1000),
            Is.EqualTo(0d));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(0, 370, 1000),
            Is.EqualTo(0d));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => SaveManager.CalculateOfflineElapsedSeconds(100, 370, -1d));
    }

    [Test]
    public void OfflineEffectiveTime_UsesFullMiddleAndLateBands()
    {
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 7200d),
            Is.EqualTo(7200d).Within(0.001d));
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(7200d, 3600d),
            Is.EqualTo(2160d).Within(0.001d));
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(28800d, 3600d),
            Is.EqualTo(900d).Within(0.001d));
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(86400d, 60d),
            Is.EqualTo(15d).Within(0.001d));
    }

    [Test]
    public void OfflineEffectiveTime_RejectsNegativeInputs()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => SimulationManager.CalculateOfflineEffectiveSeconds(-1d, 1d));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => SimulationManager.CalculateOfflineEffectiveSeconds(1d, -1d));
    }

    [Test]
    public void SimulatorScalarStockpileRuleMatchesRuntimeRule()
    {
        double expected = ResourceManager.AdvanceAmount(
            new ExpantaNum(3d), new ExpantaNum(2d),
            new ExpantaNum(5d), .5d).ToDouble();
        Assert.That(
            EconomySimulationParity.AdvanceStockpile(3d, 2d, 5d, .5d),
            Is.EqualTo(expected).Within(1e-9d));
    }

    [Test]
    public void SimulatorSatisfactionRulesMatchRuntimeRules()
    {
        double resource = ResourceManager.CalculateSatisfaction(
            new ExpantaNum(2d), new ExpantaNum(3d),
            new ExpantaNum(10d), .5d).ToDouble();
        double food = HappinessFormula.CalculateFoodAvailability(
            new ExpantaNum(2d), new ExpantaNum(3d),
            new ExpantaNum(10d), .5d).ToDouble();
        double flow = GameManager.CalculateFlowSatisfaction(
            new ExpantaNum(3d), new ExpantaNum(10d)).ToDouble();

        Assert.That(EconomySimulationParity.CalculateSatisfaction(
            2d, 3d, 10d, .5d), Is.EqualTo(resource).Within(1e-9d));
        Assert.That(EconomySimulationParity.CalculateSatisfaction(
            2d, 3d, 10d, .5d), Is.EqualTo(food).Within(1e-9d));
        Assert.That(EconomySimulationParity.CalculateFlowSatisfaction(
            3d, 10d), Is.EqualTo(flow).Within(1e-9d));
    }

    [Test]
    public void SimulatorEfficiencyAndResearchSpeedMatchRuntimeRules()
    {
        double efficiency = BuildingManager.CalculateEffectiveEfficiency(
            new ExpantaNum(.9d), new ExpantaNum(.8d),
            new ExpantaNum(.7d), new ExpantaNum(.6d),
            new ExpantaNum(.5d)).ToDouble();
        double research = ResearchManager.ResearchSpeedEffect(
            TechLevel.Animal, TechLevel.Medieval);

        Assert.That(EconomySimulationParity.CalculateEffectiveEfficiency(
            .9d, .8d, .7d, .6d, .5d),
            Is.EqualTo(efficiency).Within(1e-9d));
        Assert.That(EconomySimulationParity.ResearchSpeedEffect(0, 2),
            Is.EqualTo(research).Within(1e-9d));
    }

    [Test]
    public void SimulatorPopulationConstantsMatchRuntimeConstants()
    {
        Assert.That(PopulationState.FoodConsumptionPerPerson.ToDouble(),
            Is.EqualTo(.8d).Within(1e-9d));
        Assert.That(PopulationState.ProductivityGrantedPerPerson.ToDouble(),
            Is.EqualTo(2d).Within(1e-9d));
        Assert.That(GameState.BaseFoodProductionRate.ToDouble(),
            Is.EqualTo(5d).Within(1e-9d));
        Assert.That(TerritoryState.InitialTotal.ToDouble(),
            Is.EqualTo(500d).Within(1e-9d));
    }

    private static ExpantaNum IntegrateFood(int stepCount, double deltaSeconds)
    {
        ExpantaNum amount = new ExpantaNum(300);
        for (int i = 0; i < stepCount; i++)
            amount = GameManager.AdvanceFood(
                amount,
                new ExpantaNum(8),
                new ExpantaNum(3),
                new ExpantaNum(500),
                deltaSeconds);
        return amount;
    }

    private static ExpantaNum IntegrateResource(int stepCount, double deltaSeconds)
    {
        ExpantaNum amount = ExpantaNum.Zero;
        for (int i = 0; i < stepCount; i++)
            amount = ResourceManager.AdvanceAmount(
                amount,
                new ExpantaNum(5),
                new ExpantaNum(2),
                deltaSeconds);
        return amount;
    }
}
