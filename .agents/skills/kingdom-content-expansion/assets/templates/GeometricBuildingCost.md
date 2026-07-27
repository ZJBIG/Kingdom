# Geometric building-cost integration

For each requirement pair:

```csharp
ExpantaNum total = pair.Second.GeometricSeriesCost(
    building.CostGrowth,
    state.Amount,
    amount);
```

Maximum affordable for one currency:

```csharp
ExpantaNum affordable = current.MaxAffordableGeometricSeries(
    pair.Second,
    building.CostGrowth,
    state.Amount);
```

Refund the last N buildings:

```csharp
ExpantaNum remainingOwned = state.Amount - amount;
ExpantaNum historicalCost = pair.Second.GeometricSeriesCost(
    building.CostGrowth,
    remainingOwned,
    amount);
ExpantaNum refund = historicalCost * DeconstructionReturnRate;
```

Do not loop one building at a time.
