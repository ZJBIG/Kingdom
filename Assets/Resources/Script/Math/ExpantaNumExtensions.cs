using System;

/// <summary>
/// 为 ExpantaNum 提供放置游戏常用的经济公式。
/// 该类只组合 ExpantaNum 已有的数学能力，不参与大数的内部表示、解析或格式化。
/// </summary>
public static class ExpantaNumExtensions
{
    /// <summary>
    /// 计算等比增长价格的批量购买总成本。
    /// 第 k 个物品的价格为 baseCost·ratio^(owned+k)，其中 k 从 0 开始。
    /// 例如基础价格为 10、倍率为 2、已经拥有 3 个，再购买 2 个时，成本为 80+160=240。
    /// </summary>
    /// <param name="baseCost">未拥有任何物品时的基础价格。</param>
    /// <param name="ratio">每购买一个后，下一个价格乘上的倍率。</param>
    /// <param name="owned">购买前已经拥有的数量，必须是非负整数。</param>
    /// <param name="count">本次连续购买的数量，必须是非负整数。</param>
    /// <returns>批量购买总成本；参数不合法时返回 NaN。</returns>
    public static ExpantaNum GeometricSeriesCost(
        this ExpantaNum baseCost,
        ExpantaNum ratio,
        ExpantaNum owned,
        ExpantaNum count)
    {
        if (baseCost.IsNegative || ratio <= ExpantaNum.Zero ||
            owned.IsNegative || count.IsNegative ||
            !owned.IsInteger() || !count.IsInteger())
        {
            return ExpantaNum.NaN;
        }

        if (count.IsZero)
            return ExpantaNum.Zero;

        ExpantaNum firstCost = baseCost * ratio.Pow(owned);
        if (ratio == ExpantaNum.One)
            return firstCost * count;

        return firstCost * PowMinusOne(ratio, count) / (ratio - ExpantaNum.One);
    }

    /// <summary>
    /// 根据当前货币计算最多能够购买多少个等比增长价格的物品。
    /// 该方法先通过等比数列的逆公式估算数量，再进行边界校正，保证返回数量可以买得起，返回数量加一后买不起。
    /// 当 0&lt;ratio&lt;1 且货币足以支付无限项总和时，返回正无穷。
    /// </summary>
    /// <param name="currency">当前可用货币。</param>
    /// <param name="baseCost">未拥有任何物品时的基础价格。</param>
    /// <param name="ratio">每购买一个后，下一个价格乘上的倍率。</param>
    /// <param name="owned">购买前已经拥有的数量，必须是非负整数。</param>
    /// <returns>最多可购买的非负整数数量。</returns>
    public static ExpantaNum MaxAffordableGeometricSeries(
        this ExpantaNum currency,
        ExpantaNum baseCost,
        ExpantaNum ratio,
        ExpantaNum owned)
    {
        if (currency.IsNegative || baseCost <= ExpantaNum.Zero ||
            ratio <= ExpantaNum.Zero || owned.IsNegative || !owned.IsInteger())
        {
            return ExpantaNum.NaN;
        }

        ExpantaNum firstCost = baseCost * ratio.Pow(owned);
        if (currency < firstCost)
            return ExpantaNum.Zero;

        if (ratio == ExpantaNum.One)
        {
            ExpantaNum linearCount = (currency / firstCost).Floor();
            return linearCount.IsFinite && !linearCount.IsNegative
                ? linearCount
                : ExpantaNum.Zero;
        }

        if (ratio < ExpantaNum.One)
        {
            ExpantaNum infiniteCost = firstCost / (ExpantaNum.One - ratio);
            if (currency >= infiniteCost)
                return ExpantaNum.PositiveInfinity;
        }

        ExpantaNum scaledCurrency = currency * (ratio - ExpantaNum.One) / firstCost;
        ExpantaNum logarithm = LogOnePlus(scaledCurrency);
        ExpantaNum logarithmOfRatio = ratio.Ln();

        if (logarithm.IsNaN || logarithmOfRatio.IsNaN || logarithmOfRatio.IsZero)
            return ExpantaNum.Zero;

        ExpantaNum estimate = (logarithm / logarithmOfRatio).Floor();
        return CorrectGeometricAffordableCount(currency, baseCost, ratio, owned, estimate);
    }

    private static ExpantaNum CorrectGeometricAffordableCount(
        ExpantaNum currency,
        ExpantaNum baseCost,
        ExpantaNum ratio,
        ExpantaNum owned,
        ExpantaNum estimate)
    {
        if (estimate.IsNaN || estimate.IsInfinity)
            return estimate;

        ExpantaNum count = estimate.IsNegative ? ExpantaNum.Zero : estimate.Floor();

        for (int i = 0; i < 16 && count > ExpantaNum.Zero; i++)
        {
            if (baseCost.GeometricSeriesCost(ratio, owned, count) <= currency)
                break;
            count -= ExpantaNum.One;
        }

        for (int i = 0; i < 16; i++)
        {
            ExpantaNum next = count + ExpantaNum.One;
            if (baseCost.GeometricSeriesCost(ratio, owned, next) > currency)
                break;
            count = next;
        }

        return count.IsFinite && !count.IsNegative
            ? count.Floor()
            : ExpantaNum.Zero;
    }

    private static ExpantaNum LogOnePlus(ExpantaNum value)
    {
        double scalar = value.ToDouble();
        if (!double.IsNaN(scalar) && !double.IsInfinity(scalar) && Math.Abs(scalar) < 0.0001d)
            return new ExpantaNum(LogOnePlus(scalar));

        return (ExpantaNum.One + value).Ln();
    }

    private static ExpantaNum PowMinusOne(ExpantaNum value, ExpantaNum exponent)
    {
        double baseValue = value.ToDouble();
        double exponentValue = exponent.ToDouble();

        if (!double.IsNaN(baseValue) && !double.IsInfinity(baseValue) && baseValue > 0d &&
            !double.IsNaN(exponentValue) && !double.IsInfinity(exponentValue) &&
            Math.Abs(baseValue - 1d) < 0.0001d)
        {
            double result = ExpMinusOne(
                exponentValue * LogOnePlus(baseValue - 1d));

            if (!double.IsNaN(result) && !double.IsInfinity(result))
                return new ExpantaNum(result);
        }

        return value.Pow(exponent) - ExpantaNum.One;
    }

    private static double ExpMinusOne(double value)
    {
        double magnitude = Math.Abs(value);
        if (magnitude > 0.00001d)
            return Math.Exp(value) - 1d;

        double valueSquared = value * value;
        return value +
               valueSquared * 0.5d +
               valueSquared * value / 6d +
               valueSquared * valueSquared / 24d +
               valueSquared * valueSquared * value / 120d;
    }

    private static double LogOnePlus(double value)
    {
        double magnitude = Math.Abs(value);
        if (magnitude > 0.00001d)
            return Math.Log(1d + value);

        double valueSquared = value * value;
        return value -
               valueSquared * 0.5d +
               valueSquared * value / 3d -
               valueSquared * valueSquared * 0.25d +
               valueSquared * valueSquared * value * 0.2d;
    }

}
