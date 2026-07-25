using System;

/// <summary>
/// 描述一段连续软上限规则。
/// 当数值超过 Start 后，按 Start·(value/Start)^Power 压缩。
/// Power 通常位于 0 和 1 之间；值越小，超过阈值后的增长压缩得越明显。
/// </summary>
[Serializable]
public struct ExpantaNumSoftcapStage
{
    public ExpantaNum Start;
    public ExpantaNum Power;

    public ExpantaNumSoftcapStage(ExpantaNum start, ExpantaNum power)
    {
        Start = start;
        Power = power;
    }
}

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
            return (currency / firstCost).Floor();

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

    /// <summary>
    /// 计算等差增长价格的批量购买总成本。
    /// 第 k 个物品的价格为 baseCost+increment·(owned+k)，其中 k 从 0 开始。
    /// 例如基础价格为 10、每次增加 5、已经拥有 3 个，再购买 2 个时，成本为 25+30=55。
    /// </summary>
    /// <param name="baseCost">未拥有任何物品时的基础价格。</param>
    /// <param name="increment">每购买一个后，下一个价格增加的固定数值。</param>
    /// <param name="owned">购买前已经拥有的数量，必须是非负整数。</param>
    /// <param name="count">本次连续购买的数量，必须是非负整数。</param>
    /// <returns>批量购买总成本；参数不合法时返回 NaN。</returns>
    public static ExpantaNum ArithmeticSeriesCost(
        this ExpantaNum baseCost,
        ExpantaNum increment,
        ExpantaNum owned,
        ExpantaNum count)
    {
        if (baseCost.IsNegative || increment.IsNegative ||
            owned.IsNegative || count.IsNegative ||
            !owned.IsInteger() || !count.IsInteger())
        {
            return ExpantaNum.NaN;
        }

        if (count.IsZero)
            return ExpantaNum.Zero;

        ExpantaNum firstCost = baseCost + increment * owned;
        return count * (2d * firstCost + (count - ExpantaNum.One) * increment) / 2d;
    }

    /// <summary>
    /// 根据当前货币计算最多能够购买多少个等差增长价格的物品。
    /// 内部通过求解等差数列总成本对应的二次方程估算数量，再进行边界校正。
    /// </summary>
    /// <param name="currency">当前可用货币。</param>
    /// <param name="baseCost">未拥有任何物品时的基础价格。</param>
    /// <param name="increment">每购买一个后，下一个价格增加的固定数值。</param>
    /// <param name="owned">购买前已经拥有的数量，必须是非负整数。</param>
    /// <returns>最多可购买的非负整数数量。</returns>
    public static ExpantaNum MaxAffordableArithmeticSeries(
        this ExpantaNum currency,
        ExpantaNum baseCost,
        ExpantaNum increment,
        ExpantaNum owned)
    {
        if (currency.IsNegative || baseCost <= ExpantaNum.Zero ||
            increment.IsNegative || owned.IsNegative || !owned.IsInteger())
        {
            return ExpantaNum.NaN;
        }

        ExpantaNum firstCost = baseCost + increment * owned;
        if (currency < firstCost)
            return ExpantaNum.Zero;

        ExpantaNum estimate;
        if (increment.IsZero)
        {
            estimate = (currency / firstCost).Floor();
        }
        else
        {
            ExpantaNum b = 2d * firstCost - increment;
            ExpantaNum discriminant = b * b + 8d * increment * currency;
            estimate = ((-b + discriminant.Sqrt()) / (2d * increment)).Floor();
        }

        return CorrectArithmeticAffordableCount(currency, baseCost, increment, owned, estimate);
    }

    /// <summary>
    /// 计算指定等级的指数增长价格：baseCost·growth^level。
    /// 适用于只需要某一级单价，而不需要计算连续购买总价的场景。
    /// </summary>
    /// <param name="baseCost">零级时的基础价格。</param>
    /// <param name="growth">每一级的价格倍率。</param>
    /// <param name="level">目标等级，必须为非负数。</param>
    /// <returns>目标等级对应的单价。</returns>
    public static ExpantaNum ExponentialCost(
        this ExpantaNum baseCost,
        ExpantaNum growth,
        ExpantaNum level)
    {
        if (baseCost.IsNegative || growth <= ExpantaNum.Zero || level.IsNegative)
            return ExpantaNum.NaN;

        return baseCost * growth.Pow(level);
    }

    /// <summary>
    /// 对超过阈值的数值应用连续软上限：start·(value/start)^power。
    /// 当 0&lt;power&lt;1 时，数值超过 start 后仍会增长，但增长速度会降低，并且曲线在 start 处连续。
    /// </summary>
    /// <param name="value">原始数值。</param>
    /// <param name="start">软上限开始生效的阈值，必须大于零。</param>
    /// <param name="power">软上限指数，通常位于 0 和 1 之间。</param>
    /// <returns>应用软上限后的数值。</returns>
    public static ExpantaNum Softcap(
        this ExpantaNum value,
        ExpantaNum start,
        ExpantaNum power)
    {
        if (value.IsNaN || start <= ExpantaNum.Zero || power <= ExpantaNum.Zero)
            return ExpantaNum.NaN;

        return value <= start
            ? value
            : start * (value / start).Pow(power);
    }

    /// <summary>
    /// 按数组顺序依次应用多段软上限。
    /// 后一段接收前一段已经压缩后的结果，因此阶段顺序会影响最终成长曲线。
    /// </summary>
    /// <param name="value">原始数值。</param>
    /// <param name="stages">按顺序执行的软上限阶段。</param>
    /// <returns>应用全部阶段后的数值。</returns>
    public static ExpantaNum ApplySoftcaps(
        this ExpantaNum value,
        ExpantaNumSoftcapStage[] stages)
    {
        if (stages == null || stages.Length == 0)
            return value;

        ExpantaNum result = value;
        for (int i = 0; i < stages.Length; i++)
        {
            result = result.Softcap(stages[i].Start, stages[i].Power);
            if (result.IsNaN)
                return ExpantaNum.NaN;
        }

        return result;
    }

    /// <summary>
    /// 反解连续软上限，估算应用 Softcap 前的原始数值。
    /// 它使用 start·(value/start)^(1/power)；阈值以内的数值保持不变。
    /// </summary>
    /// <param name="value">已经应用软上限后的数值。</param>
    /// <param name="start">软上限开始生效的阈值。</param>
    /// <param name="power">原软上限使用的指数。</param>
    /// <returns>应用软上限前的近似数值。</returns>
    public static ExpantaNum ReverseSoftcap(
        this ExpantaNum value,
        ExpantaNum start,
        ExpantaNum power)
    {
        if (value.IsNaN || start <= ExpantaNum.Zero || power <= ExpantaNum.Zero)
            return ExpantaNum.NaN;

        return value <= start
            ? value
            : start * (value / start).Pow(ExpantaNum.One / power);
    }

    /// <summary>
    /// 仅对超过阈值的部分应用幂次缩放：start+(value-start)^power。
    /// 它与 Softcap 不同：Softcap 按 value/start 的比例压缩，而本方法只压缩超过 start 的绝对增量。
    /// </summary>
    /// <param name="value">原始数值。</param>
    /// <param name="start">缩放开始生效的阈值。</param>
    /// <param name="power">超过阈值部分使用的指数。</param>
    /// <returns>缩放后的数值。</returns>
    public static ExpantaNum ScaleAfter(
        this ExpantaNum value,
        ExpantaNum start,
        ExpantaNum power)
    {
        if (value.IsNaN || power <= ExpantaNum.Zero)
            return ExpantaNum.NaN;

        return value <= start
            ? value
            : start + (value - start).Pow(power);
    }

    /// <summary>
    /// 计算离散复合增长 principal·(1+rate)^periods。
    /// rate=0.05 表示每个周期增长 5%，每个周期产生的增长会加入下一周期本金。
    /// </summary>
    /// <param name="principal">初始数量。</param>
    /// <param name="rate">每周期增长率。</param>
    /// <param name="periods">复合增长周期数，必须为非负数。</param>
    /// <returns>复合增长后的数量。</returns>
    public static ExpantaNum CompoundGrowth(
        this ExpantaNum principal,
        ExpantaNum rate,
        ExpantaNum periods)
    {
        if (principal.IsNegative || periods.IsNegative || ExpantaNum.One + rate <= ExpantaNum.Zero)
            return ExpantaNum.NaN;

        return principal * (ExpantaNum.One + rate).Pow(periods);
    }

    /// <summary>
    /// 根据当前资源计算重置、转生或声望收益。
    /// 公式为 floor((resource/requirement)^exponent·multiplier)，资源不足 requirement 时收益为零。
    /// </summary>
    /// <param name="resource">重置前拥有的资源。</param>
    /// <param name="requirement">开始产生收益所需的资源门槛。</param>
    /// <param name="exponent">资源比值的收益指数。</param>
    /// <param name="multiplier">最终收益倍率。</param>
    /// <returns>向下取整后的收益。</returns>
    public static ExpantaNum PrestigeGain(
        this ExpantaNum resource,
        ExpantaNum requirement,
        ExpantaNum exponent,
        ExpantaNum multiplier)
    {
        if (resource.IsNegative || requirement <= ExpantaNum.Zero ||
            exponent <= ExpantaNum.Zero || multiplier.IsNegative)
        {
            return ExpantaNum.NaN;
        }

        if (resource < requirement)
            return ExpantaNum.Zero;

        return ((resource / requirement).Pow(exponent) * multiplier).Floor();
    }

    /// <summary>
    /// 根据目标转生收益反推所需资源。
    /// 这是 PrestigeGain 忽略 Floor 离散误差后的逆公式，适合显示达到目标收益所需的大致资源量。
    /// </summary>
    /// <param name="prestige">目标转生收益。</param>
    /// <param name="requirement">基础资源门槛。</param>
    /// <param name="exponent">收益指数。</param>
    /// <param name="multiplier">收益倍率。</param>
    /// <returns>达到目标收益所需的近似资源数量。</returns>
    public static ExpantaNum PrestigeRequirement(
        this ExpantaNum prestige,
        ExpantaNum requirement,
        ExpantaNum exponent,
        ExpantaNum multiplier)
    {
        if (prestige.IsNegative || requirement <= ExpantaNum.Zero ||
            exponent <= ExpantaNum.Zero || multiplier <= ExpantaNum.Zero)
        {
            return ExpantaNum.NaN;
        }

        return requirement * (prestige / multiplier).Root(exponent);
    }

    /// <summary>
    /// 计算收益增量与成本的比值 gain/cost，用于比较不同升级或购买方案的单位成本收益。
    /// 数值越大，代表每单位成本带来的收益越高。
    /// </summary>
    /// <param name="gain">购买或升级后增加的收益。</param>
    /// <param name="cost">购买或升级消耗的成本。</param>
    /// <returns>收益成本比；成本为零时返回正无穷。</returns>
    public static ExpantaNum PurchaseEfficiency(
        this ExpantaNum gain,
        ExpantaNum cost)
    {
        if (gain.IsNaN || cost.IsNaN || gain.IsNegative || cost.IsNegative)
            return ExpantaNum.NaN;

        return cost.IsZero
            ? ExpantaNum.PositiveInfinity
            : gain / cost;
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

        return count;
    }

    private static ExpantaNum CorrectArithmeticAffordableCount(
        ExpantaNum currency,
        ExpantaNum baseCost,
        ExpantaNum increment,
        ExpantaNum owned,
        ExpantaNum estimate)
    {
        if (estimate.IsNaN || estimate.IsInfinity)
            return estimate;

        ExpantaNum count = estimate.IsNegative ? ExpantaNum.Zero : estimate.Floor();

        for (int i = 0; i < 16 && count > ExpantaNum.Zero; i++)
        {
            if (baseCost.ArithmeticSeriesCost(increment, owned, count) <= currency)
                break;
            count -= ExpantaNum.One;
        }

        for (int i = 0; i < 16; i++)
        {
            ExpantaNum next = count + ExpantaNum.One;
            if (baseCost.ArithmeticSeriesCost(increment, owned, next) > currency)
                break;
            count = next;
        }

        return count;
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

    private static ExpantaNum LogOnePlus(ExpantaNum value)
    {
        double scalar = value.ToDouble();
        if (!double.IsNaN(scalar) && !double.IsInfinity(scalar) && Math.Abs(scalar) < 0.0001d)
            return new ExpantaNum(LogOnePlus(scalar));

        return (ExpantaNum.One + value).Ln();
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
