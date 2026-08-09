using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using UnityEngine;

public enum ExpantaNumFormat
{
    Suffix,
    Scientific,
    Engineering,
    HyperOperation
}


[Serializable]
internal struct ExpantaNumOperator : IEquatable<ExpantaNumOperator>
{
    [SerializeField] private double operation;
    [SerializeField] private double repetitions;

    public double Operation => operation;
    public double Repetitions => repetitions;

    public ExpantaNumOperator(double operation, double repetitions)
    {
        this.operation = operation;
        this.repetitions = repetitions;
    }

    public bool Equals(ExpantaNumOperator other) =>
        operation.Equals(other.operation) && repetitions.Equals(other.repetitions);

    public override bool Equals(object obj) =>
        obj is ExpantaNumOperator other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (operation.GetHashCode() * 397) ^ repetitions.GetHashCode();
        }
    }
}

/// <summary>
/// 表示一种面向增量游戏的非精确超大实数。
/// 普通数使用内联 double；1e1000、ee100 等数使用无数组的分层十进制表示；只有真正的幂塔、五阶超运算及更高结构才启用稀疏运算符数组。
/// 常见数值不分配堆内存；结构体本身保持紧凑，高阶数组采用不可变写时复制，复制结构体不会相互修改。
/// 公开数学入口会自动选择最快且适合当前数量级的算法，调用者不需要手动选择“整数幂版本”或“大数版本”。
/// </summary>
/// <remarks>
/// 常用入口包括四则运算符、比较、Floor、Pow、Parse 和 ToGameString。
/// Tetrate 与 Pentate 仅在玩法确实需要时使用；其余特殊函数被标记为高级 API。
/// </remarks>
[System.Diagnostics.DebuggerDisplay("{ToString(),nq}")]
[Serializable]
public struct ExpantaNum : IEquatable<ExpantaNum>, IComparable<ExpantaNum>, IComparable
{
    private const byte ZeroRepresentation = 0;
    private const byte ScalarRepresentation = 1;
    private const byte LayeredRepresentation = 2;
    private const byte HyperRepresentation = 3;

    private const double MaxSafeInteger = 9007199254740991d;
    private const int DecimalPlaces = 6;
    private const double SmallestRoundedMagnitude = 0.0000005d;
    private const int MaxGameSignificantDigits = 6;
    private const double PromotionLimit = 1e300;
    private const double PromotionLog10 = 300d;
    private const double DominanceCutoff = 7d;
    private const int MaxRepeatedEOutput = 8;
    private const double DefaultTolerance = 1e-6;
    private const int MaxHyperOperators = 16;
    private const double DoubleLog10Limit = 308.25471555991675d;

    private static readonly ExpantaNumOperator[] EmptyOperators = new ExpantaNumOperator[0];
    private static readonly double[] FactorialTable = CreateFactorialTable();
    private static readonly double[] LanczosCoefficients =
    {
        0.99999999999980993d,
        676.5203681218851d,
        -1259.1392167224028d,
        771.32342877765313d,
        -176.61502916214059d,
        12.507343278686905d,
        -0.13857109526572012d,
        9.9843695780195716e-6d,
        1.5056327351493116e-7d
    };
    private const double SqrtTwoPi = 2.5066282746310005024d;
    private const double HalfLogTwoPi = 0.91893853320467274178d;
    private static readonly string[] GameSuffixes =
    {
        string.Empty, "K", "M", "B", "T"
    };
    private static readonly double[] GameSuffixValues =
    {
        1d, 1e3d, 1e6d, 1e9d, 1e12d
    };

    [SerializeField] private bool sign;
    [SerializeField] private byte representation;
    [SerializeField] private int operatorCount;
    [SerializeField] private double scalar;
    [SerializeField] private double layer;
    [SerializeField] private ExpantaNumOperator[] operators;

    private bool Sign => IsZero || IsNaN ? false : sign;
    private double Layer => representation == LayeredRepresentation || representation == HyperRepresentation
        ? layer
        : 0d;
    public bool IsNegative => !IsZero && !IsNaN && sign;
    public bool IsPositive => !IsZero && !IsNaN && !sign;
    public bool IsZero => representation == ZeroRepresentation ||
                          (representation == ScalarRepresentation && scalar == 0d);
    public bool IsNaN => representation == ScalarRepresentation && double.IsNaN(scalar);
    public bool IsInfinity => representation == ScalarRepresentation && double.IsPositiveInfinity(scalar);
    public bool IsFinite => !IsNaN && !IsInfinity;

    public static readonly ExpantaNum Zero = default;
    public static readonly ExpantaNum One = new ExpantaNum(1d);
    public static readonly ExpantaNum Ten = new ExpantaNum(10d);
    public static readonly ExpantaNum NaN = new ExpantaNum(double.NaN);
    public static readonly ExpantaNum PositiveInfinity = new ExpantaNum(double.PositiveInfinity);
    public static readonly ExpantaNum NegativeInfinity = new ExpantaNum(double.NegativeInfinity);
    public static readonly ExpantaNum E = new ExpantaNum(Math.E);
    public static readonly ExpantaNum PI = new ExpantaNum(Math.PI);


    public ExpantaNum(double value)
    {
        sign = value < 0d || double.IsNegativeInfinity(value);
        representation = ZeroRepresentation;
        operatorCount = 0;
        scalar = 0d;
        layer = 0d;
        operators = null;

        if (double.IsNaN(value))
        {
            sign = false;
            representation = ScalarRepresentation;
            scalar = double.NaN;
            return;
        }

        double magnitude = Math.Abs(value);
        if (magnitude == 0d)
            return;

        magnitude = Quantize(magnitude);
        if (magnitude > PromotionLimit && !double.IsInfinity(magnitude))
        {
            representation = LayeredRepresentation;
            scalar = Quantize(Math.Log10(magnitude));
            layer = 1d;
            return;
        }

        representation = ScalarRepresentation;
        scalar = magnitude;
    }

    public ExpantaNum(string value)
    {
        this = Parse(value);
    }

    private ExpantaNum(bool negative, double hyperLayer, ExpantaNumOperator[] operators, bool normalize)
    {
        sign = negative;
        representation = HyperRepresentation;
        operatorCount = Math.Min(operators == null ? 0 : operators.Length, MaxHyperOperators);
        scalar = 0d;
        layer = hyperLayer;
        this.operators = operators;

        if (normalize)
            NormalizeInPlace(true);
    }

    private ExpantaNum(bool negative, double decimalLayer, double magnitude)
    {
        sign = negative;
        representation = LayeredRepresentation;
        operatorCount = 0;
        scalar = magnitude;
        layer = decimalLayer;
        operators = null;
        NormalizeInPlace();
    }

    /// <summary>
    /// 根据数值绝对值的十进制对数创建大数。
    /// 例如 logarithm 为 6 时结果为 10⁶；该方法可以避免先计算已经超出 double 范围的实际数值。
    /// </summary>
    /// <param name="logarithm">目标数值绝对值的 log10。</param>
    /// <param name="negative">是否将结果设为负数。</param>
    /// <returns>对应的大数。</returns>
    private static ExpantaNum FromLog10(double logarithm, bool negative = false)
    {
        if (double.IsNaN(logarithm))
            return NaN;

        if (double.IsPositiveInfinity(logarithm))
            return negative ? NegativeInfinity : PositiveInfinity;

        if (double.IsNegativeInfinity(logarithm) || logarithm < -324d)
            return Zero;

        if (logarithm <= PromotionLog10)
            return new ExpantaNum((negative ? -1d : 1d) * Math.Pow(10d, logarithm));

        return new ExpantaNum(negative, 1d, Quantize(logarithm));
    }

    private ExpantaNumOperator[] GetOperators()
    {
        int count = GetOperatorCount();
        if (count == 0)
            return EmptyOperators;

        ExpantaNumOperator[] result = new ExpantaNumOperator[count];
        for (int i = 0; i < count; i++)
            result[i] = new ExpantaNumOperator(GetOperation(i), GetRepetitions(i));
        return result;
    }

    private double Operator(double operation)
    {
        int index = FindOperationIndex(operation);
        return index < 0 ? 0d : GetRepetitions(index);
    }

    /// <summary>
    /// 解析普通数字、科学计数法以及本结构体支持的超运算字符串。
    /// </summary>
    /// <param name="text">需要解析的文本。</param>
    /// <returns>解析得到的大数；格式无效时抛出 FormatException。</returns>
    public static ExpantaNum Parse(string text)
    {
        ExpantaNum result;
        if (!TryParse(text, out result))
            throw new FormatException("Invalid ExpantaNum value: " + text);

        return result;
    }

    /// <summary>
    /// 尝试解析普通数字、科学计数法以及本结构体支持的超运算字符串。
    /// </summary>
    /// <param name="text">需要解析的文本。</param>
    /// <param name="result">解析成功时接收结果。</param>
    /// <returns>解析成功时返回 true，否则返回 false。</returns>
    public static bool TryParse(string text, out ExpantaNum result)
    {
        result = Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string value = text.Trim();
        return TryParseCore(value, out result);
    }

    private static bool TryParseCore(string value, out ExpantaNum result)
    {
        result = Zero;

        bool negative = false;
        int signIndex = 0;
        while (signIndex < value.Length && (value[signIndex] == '+' || value[signIndex] == '-'))
        {
            if (value[signIndex] == '-')
                negative = !negative;
            signIndex++;
        }

        value = value.Substring(signIndex).Trim();
        if (value.Length == 0)
            return false;

        if (string.Equals(value, "NaN", StringComparison.OrdinalIgnoreCase))
        {
            result = NaN;
            return true;
        }

        if (string.Equals(value, "Infinity", StringComparison.OrdinalIgnoreCase))
        {
            result = negative ? NegativeInfinity : PositiveInfinity;
            return true;
        }

        if (TryParseGameSuffix(value, negative, out result))
            return true;

        double parsedNumber;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedNumber) &&
            !double.IsInfinity(parsedNumber))
        {
            result = new ExpantaNum(negative ? -Math.Abs(parsedNumber) : Math.Abs(parsedNumber));
            return true;
        }

        double parsedLayer = 0d;
        Match layerMatch = Regex.Match(value, @"^J(?:\^(?<layer>\d+(?:\.\d+)?))?\s*(?<rest>.*)$", RegexOptions.IgnoreCase);
        if (layerMatch.Success)
        {
            parsedLayer = layerMatch.Groups["layer"].Success
                ? double.Parse(layerMatch.Groups["layer"].Value, CultureInfo.InvariantCulture)
                : 1d;
            value = layerMatch.Groups["rest"].Value.Trim();
            if (value.Length == 0)
                value = "10";
        }

        ExpantaNum parsed;
        if (TryParseHyperE(value, out parsed) ||
            TryParseOperatorNotation(value, out parsed) ||
            TryParseRepeatedExponent(value, out parsed) ||
            TryParseScientificChain(value, out parsed))
        {
            parsed.sign = negative && !parsed.IsZero;
            if (parsedLayer > 0d)
            {
                double existingHyperLayer = parsed.representation == HyperRepresentation
                    ? parsed.layer
                    : 0d;
                parsed = new ExpantaNum(
                    parsed.sign,
                    existingHyperLayer + parsedLayer,
                    parsed.GetOperators(),
                    true);
            }
            else
            {
                parsed.NormalizeInPlace();
            }
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseHyperE(string value, out ExpantaNum result)
    {
        result = Zero;
        if (value.Length < 2 || value[0] != 'E' || value.IndexOf('#') < 0)
            return false;

        string[] parts = value.Substring(1).Split('#');
        double baseValue;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out baseValue))
            return false;

        result = new ExpantaNum(baseValue);
        for (int i = 1; i < parts.Length; i++)
        {
            double count;
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out count))
                return false;

            result = result.WithAddedOperator(i, count);
        }

        return true;
    }

    private static bool TryParseOperatorNotation(string value, out ExpantaNum result)
    {
        result = Zero;
        List<ExpantaNumOperator> prefixes = new List<ExpantaNumOperator>();
        string remaining = value;

        while (true)
        {
            Match repeated = Regex.Match(
                remaining,
                @"^\(10(?<operator>\^+|\{\d+(?:\.\d+)?\})\)\^(?<count>\d+(?:\.\d+)?)\s*(?<rest>.*)$");

            if (!repeated.Success)
                break;

            double operation = ParseOperationToken(repeated.Groups["operator"].Value);
            double repetitions = double.Parse(repeated.Groups["count"].Value, CultureInfo.InvariantCulture);
            prefixes.Add(new ExpantaNumOperator(operation, repetitions));
            remaining = repeated.Groups["rest"].Value.Trim();
        }

        Match direct = Regex.Match(
            remaining,
            @"^10(?<operator>\^+|\{\d+(?:\.\d+)?\})(?<argument>.+)$");

        if (direct.Success)
        {
            double operation = ParseOperationToken(direct.Groups["operator"].Value);
            ExpantaNum argument;
            if (!TryParse(direct.Groups["argument"].Value.Trim(), out argument))
                return false;

            double argumentValue;
            if (!argument.TryToFiniteDouble(out argumentValue) || argumentValue < 0d)
                argumentValue = argument.GetBottomValue();

            result = new ExpantaNum(
                false,
                argument.representation == HyperRepresentation ? argument.layer : 0d,
                new[]
                {
                    new ExpantaNumOperator(0d, argumentValue),
                    new ExpantaNumOperator(operation, 1d)
                },
                true);
        }
        else
        {
            if (prefixes.Count == 0 || !TryParse(remaining, out result))
                return false;
        }

        for (int i = 0; i < prefixes.Count; i++)
            result = result.WithAddedOperator(prefixes[i].Operation, prefixes[i].Repetitions);

        return true;
    }

    private static bool TryParseRepeatedExponent(string value, out ExpantaNum result)
    {
        result = Zero;
        int count = 0;
        while (count < value.Length && (value[count] == 'e' || value[count] == 'E'))
            count++;

        if (count == 0 || count == value.Length)
            return false;

        ExpantaNum inner;
        if (!TryParse(value.Substring(count), out inner))
            return false;

        result = inner;
        for (int i = 0; i < count; i++)
            result = Pow10(result);

        return true;
    }

    private static bool TryParseScientificChain(string value, out ExpantaNum result)
    {
        result = Zero;
        int exponentIndex = FindScientificExponent(value);
        if (exponentIndex <= 0 || exponentIndex >= value.Length - 1)
            return false;

        double mantissa;
        if (!double.TryParse(
                value.Substring(0, exponentIndex),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out mantissa))
            return false;

        ExpantaNum exponent;
        if (!TryParse(value.Substring(exponentIndex + 1), out exponent))
            return false;

        if (mantissa == 0d)
        {
            result = Zero;
            return true;
        }

        bool negative = mantissa < 0d;
        double logMantissa = Math.Log10(Math.Abs(mantissa));
        result = Pow10(exponent + logMantissa);
        if (negative)
            result = -result;
        return true;
    }

    private static int FindScientificExponent(string value)
    {
        for (int i = 1; i < value.Length; i++)
        {
            char c = value[i];
            if (c != 'e' && c != 'E')
                continue;

            char previous = value[i - 1];
            if (char.IsDigit(previous) || previous == '.')
                return i;
        }

        return -1;
    }

    private static bool TryParseGameSuffix(string value, bool negative, out ExpantaNum result)
    {
        result = Zero;

        for (int i = GameSuffixes.Length - 1; i >= 1; i--)
        {
            string suffix = GameSuffixes[i];
            if (!value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            string coefficientText = value.Substring(0, value.Length - suffix.Length).Trim();
            double coefficient;
            if (!double.TryParse(
                    coefficientText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out coefficient) ||
                double.IsNaN(coefficient) ||
                double.IsInfinity(coefficient))
                return false;

            ExpantaNum magnitude = new ExpantaNum(Math.Abs(coefficient) * GameSuffixValues[i]);
            bool resultNegative = negative ^ coefficient < 0d;
            result = resultNegative ? -magnitude : magnitude;
            return true;
        }

        return false;
    }


    private static double ParseOperationToken(string token)
    {
        if (token[0] == '{')
            return double.Parse(token.Substring(1, token.Length - 2), CultureInfo.InvariantCulture);

        return token.Length;
    }

    public static ExpantaNum operator +(ExpantaNum left, ExpantaNum right)
    {
        if (left.IsNaN || right.IsNaN)
            return NaN;

        if (left.IsInfinity || right.IsInfinity)
        {
            if (left.IsInfinity && right.IsInfinity && left.Sign != right.Sign)
                return NaN;
            return left.IsInfinity ? left : right;
        }

        if (left.IsZero)
            return right;
        if (right.IsZero)
            return left;

        if (left.representation == ScalarRepresentation && right.representation == ScalarRepresentation)
        {
            double leftScalar = left.Sign ? -left.scalar : left.scalar;
            double rightScalar = right.Sign ? -right.scalar : right.scalar;
            double sum = leftScalar + rightScalar;
            if (!double.IsInfinity(sum))
                return new ExpantaNum(sum);
        }

        if (left.Sign == right.Sign)
        {
            ExpantaNum magnitude = AddMagnitudes(left.Abs(), right.Abs());
            magnitude.sign = left.Sign;
            return magnitude;
        }

        int comparison = CompareMagnitude(left, right);
        if (comparison == 0)
            return Zero;

        ExpantaNum larger = comparison > 0 ? left : right;
        ExpantaNum smaller = comparison > 0 ? right : left;
        ExpantaNum difference = SubtractMagnitudes(larger.Abs(), smaller.Abs());
        difference.sign = larger.Sign && !difference.IsZero;
        return difference;
    }

    public static ExpantaNum operator -(ExpantaNum left, ExpantaNum right) => left + (-right);

    public static ExpantaNum operator *(ExpantaNum left, ExpantaNum right)
    {
        if (left.IsNaN || right.IsNaN ||
            (left.IsZero && right.IsInfinity) ||
            (right.IsZero && left.IsInfinity))
            return NaN;

        if (left.IsZero || right.IsZero)
            return Zero;

        bool negative = left.Sign != right.Sign;
        if (left.IsInfinity || right.IsInfinity)
            return negative ? NegativeInfinity : PositiveInfinity;

        double leftValue;
        double rightValue;
        if (left.TryGetSignedScalar(out leftValue) && right.TryGetSignedScalar(out rightValue))
        {
            double direct = leftValue * rightValue;
            if (!double.IsInfinity(direct))
                return new ExpantaNum(direct);
        }

        if (left.representation == LayeredRepresentation && left.layer == 1d &&
            right.representation == LayeredRepresentation && right.layer == 1d)
            return FromLog10(left.scalar + right.scalar, negative);

        if (left.representation == LayeredRepresentation && left.layer == 1d &&
            right.representation == ScalarRepresentation)
            return FromLog10(left.scalar + Math.Log10(right.scalar), negative);

        if (right.representation == LayeredRepresentation && right.layer == 1d &&
            left.representation == ScalarRepresentation)
            return FromLog10(right.scalar + Math.Log10(left.scalar), negative);

        ExpantaNum logarithm = left.Abs().Log10() + right.Abs().Log10();
        ExpantaNum result = Pow10(logarithm);
        result.sign = negative && !result.IsZero;
        return result;
    }

    public static ExpantaNum operator /(ExpantaNum left, ExpantaNum right)
    {
        if (left.IsNaN || right.IsNaN ||
            (left.IsZero && right.IsZero) ||
            (left.IsInfinity && right.IsInfinity))
            return NaN;

        bool negative = left.Sign != right.Sign;

        if (right.IsZero)
            return negative ? NegativeInfinity : PositiveInfinity;
        if (left.IsZero || right.IsInfinity)
            return Zero;
        if (left.IsInfinity)
            return negative ? NegativeInfinity : PositiveInfinity;
        if (left.Abs() == right.Abs())
            return negative ? -One : One;

        double leftValue;
        double rightValue;
        if (left.TryGetSignedScalar(out leftValue) && right.TryGetSignedScalar(out rightValue))
        {
            double direct = leftValue / rightValue;
            if (!double.IsInfinity(direct) && direct != 0d)
                return new ExpantaNum(direct);
        }

        if (left.representation == LayeredRepresentation && left.layer == 1d &&
            right.representation == LayeredRepresentation && right.layer == 1d)
            return FromLog10(left.scalar - right.scalar, negative);

        if (left.representation == LayeredRepresentation && left.layer == 1d &&
            right.representation == ScalarRepresentation)
            return FromLog10(left.scalar - Math.Log10(right.scalar), negative);

        ExpantaNum logarithm = left.Abs().Log10() - right.Abs().Log10();
        ExpantaNum result = Pow10(logarithm);
        result.sign = negative && !result.IsZero;
        return result;
    }

    public static ExpantaNum operator %(ExpantaNum left, ExpantaNum right) => left.Mod(right);

    public static ExpantaNum operator +(ExpantaNum value) => value;

    public static ExpantaNum operator -(ExpantaNum value)
    {
        if (value.IsZero || value.IsNaN)
            return value;

        ExpantaNum result = value;
        result.sign = !result.sign;
        return result;
    }

    public static bool operator ==(ExpantaNum left, ExpantaNum right) => left.Equals(right);
    public static bool operator !=(ExpantaNum left, ExpantaNum right) => !left.Equals(right);
    public static bool operator >(ExpantaNum left, ExpantaNum right) => left.CompareTo(right) > 0;
    public static bool operator <(ExpantaNum left, ExpantaNum right) => left.CompareTo(right) < 0;
    public static bool operator >=(ExpantaNum left, ExpantaNum right) => left.CompareTo(right) >= 0;
    public static bool operator <=(ExpantaNum left, ExpantaNum right) => left.CompareTo(right) <= 0;

    public ExpantaNum Abs()
    {
        ExpantaNum result = this;
        result.sign = false;
        return result;
    }

    public ExpantaNum Log10()
    {
        if (IsNaN || Sign || IsZero)
            return NaN;
        if (IsInfinity)
            return PositiveInfinity;

        if (representation == ScalarRepresentation)
            return new ExpantaNum(Math.Log10(scalar));

        if (representation == LayeredRepresentation)
        {
            if (layer == 1d)
                return new ExpantaNum(scalar);
            return new ExpantaNum(false, layer - 1d, scalar);
        }

        ExpantaNum result = this;
        result.sign = false;

        int exponentIndex = result.FindOperationIndex(1d);
        if (exponentIndex >= 0)
        {
            result.SetOperator(1d, result.GetRepetitions(exponentIndex) - 1d);
            return result;
        }

        int highest = result.GetOperatorCount() - 1;
        if (highest >= 0 && result.GetOperation(highest) >= 2d)
        {
            double bottom = result.GetBottomValue();
            if (bottom > 1d)
                result.SetOperator(0d, bottom - 1d);
            return result;
        }

        if (result.layer > 0d)
            return result;

        return NaN;
    }

    public ExpantaNum Log(ExpantaNum newBase)
    {
        if (newBase <= Zero || newBase == One)
            return NaN;
        return Log10() / newBase.Log10();
    }

    private ExpantaNum PowInteger(long exponent)
    {
        if (IsNaN)
            return NaN;
        if (exponent == 0L)
            return One;
        if (IsZero)
            return exponent < 0L ? PositiveInfinity : Zero;

        bool reciprocal = exponent < 0L;
        ulong power = reciprocal
            ? (ulong)(-(exponent + 1L)) + 1UL
            : (ulong)exponent;

        ExpantaNum result = One;
        ExpantaNum factor = this;

        while (power > 0UL)
        {
            if ((power & 1UL) != 0UL)
                result *= factor;

            power >>= 1;
            if (power > 0UL)
                factor *= factor;
        }

        return reciprocal ? One / result : result;
    }

    public ExpantaNum Pow(ExpantaNum exponent)
    {
        if (IsNaN || exponent.IsNaN)
            return NaN;
        if (exponent.IsZero)
            return One;
        if (exponent == One || this == One)
            return this;
        if (IsZero)
            return exponent.Sign ? PositiveInfinity : Zero;

        double exponentValue;
        if (exponent.TryGetSignedScalar(out exponentValue))
        {
            if (exponentValue == -1d)
                return Reciprocal();
            if (exponentValue == 2d)
                return this * this;
            if (exponentValue == 3d)
                return this * this * this;
            if (exponentValue == 0.5d)
                return Sqrt();
            if (Math.Abs(exponentValue - (1d / 3d)) <= DefaultTolerance)
                return Cbrt();

            if (exponentValue == Math.Truncate(exponentValue) &&
                exponentValue >= long.MinValue &&
                exponentValue <= long.MaxValue)
                return PowInteger((long)exponentValue);

            double baseValue;
            if (TryGetSignedScalar(out baseValue))
            {
                double direct = Math.Pow(baseValue, exponentValue);
                if (!double.IsNaN(direct) && !double.IsInfinity(direct))
                    return new ExpantaNum(direct);
            }
        }

        bool negativeResult = false;
        ExpantaNum magnitude = Abs();

        if (Sign)
        {
            if (!exponent.TryToFiniteDouble(out exponentValue) || exponentValue != Math.Truncate(exponentValue))
                return NaN;
            negativeResult = Math.Abs(exponentValue % 2d) == 1d;
        }

        ExpantaNum result = Pow10(magnitude.Log10() * exponent);
        result.sign = negativeResult && !result.IsZero;
        return result;
    }

    public ExpantaNum Root(ExpantaNum degree)
    {
        if (degree.IsZero || degree.IsNaN)
            return NaN;

        if (Sign)
        {
            double degreeValue;
            if (!degree.TryToFiniteDouble(out degreeValue) ||
                degreeValue != Math.Truncate(degreeValue) ||
                Math.Abs(degreeValue % 2d) != 1d)
                return NaN;

            return -Abs().Pow(One / degree);
        }

        return Pow(One / degree);
    }

    public ExpantaNum Sqrt()
    {
        if (IsNaN || Sign)
            return NaN;
        if (IsZero || IsInfinity)
            return this;

        double value;
        if (TryGetSignedScalar(out value))
            return new ExpantaNum(Math.Sqrt(value));

        return Pow10(Log10() * 0.5d);
    }

    public ExpantaNum Cbrt()
    {
        if (IsNaN || IsZero || IsInfinity)
            return this;

        double value;
        if (TryGetSignedScalar(out value))
        {
            double magnitude = Math.Pow(Math.Abs(value), 1d / 3d);
            return new ExpantaNum(value < 0d ? -magnitude : magnitude);
        }

        ExpantaNum magnitudeResult = Pow10(Abs().Log10() / 3d);
        return Sign ? -magnitudeResult : magnitudeResult;
    }

    public ExpantaNum Exp() => E.Pow(this);

    private static ExpantaNum Pow10(ExpantaNum exponent)
    {
        if (exponent.IsNaN)
            return NaN;
        if (exponent.IsInfinity)
            return exponent.Sign ? Zero : PositiveInfinity;

        if (exponent.representation == ScalarRepresentation)
        {
            double numeric = exponent.Sign ? -exponent.scalar : exponent.scalar;
            if (numeric < -324d)
                return Zero;
            if (numeric <= PromotionLog10)
                return new ExpantaNum(Math.Pow(10d, numeric));
            return new ExpantaNum(false, 1d, numeric);
        }

        if (exponent.Sign)
            return Zero;

        if (exponent.representation == LayeredRepresentation)
            return new ExpantaNum(false, exponent.layer + 1d, exponent.scalar);

        return exponent.Abs().WithAddedOperator(1d, 1d);
    }

    public static ExpantaNum Min(ExpantaNum left, ExpantaNum right) => left <= right ? left : right;
    public static ExpantaNum Max(ExpantaNum left, ExpantaNum right) => left >= right ? left : right;

    public static ExpantaNum Clamp(ExpantaNum value, ExpantaNum minimum, ExpantaNum maximum)
    {
        if (minimum > maximum)
            throw new ArgumentException("minimum must not be greater than maximum.");
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    public static ExpantaNum Clamp01(ExpantaNum value) => Clamp(value, Zero, One);


    private ExpantaNum Mod(ExpantaNum divisor)
    {
        if (IsNaN || divisor.IsNaN || IsInfinity || divisor.IsZero)
            return NaN;
        if (divisor.IsInfinity)
            return this;
        if (IsZero)
            return Zero;
        if (Abs() < divisor.Abs())
            return this;
        if (Abs() == divisor.Abs())
            return Zero;

        double leftValue;
        double rightValue;
        if (TryToFiniteDouble(out leftValue) && divisor.TryToFiniteDouble(out rightValue))
            return new ExpantaNum(leftValue % rightValue);

        ExpantaNum quotient = (this / divisor).Truncate();
        double finiteQuotient;
        if (quotient.TryToFiniteDouble(out finiteQuotient) && Math.Abs(finiteQuotient) <= MaxSafeInteger)
            return this - quotient * divisor;

        return NaN;
    }

    public ExpantaNum Floor()
    {
        if (!IsFinite || IsZero)
            return this;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(Math.Floor(value));

        return this;
    }

    public ExpantaNum Ceiling()
    {
        if (!IsFinite || IsZero)
            return this;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(Math.Ceiling(value));

        return this;
    }

    public ExpantaNum Round()
    {
        if (!IsFinite || IsZero)
            return this;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(Math.Round(value, MidpointRounding.AwayFromZero));

        return this;
    }

    public ExpantaNum Truncate()
    {
        if (!IsFinite || IsZero)
            return this;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(Math.Truncate(value));

        return this;
    }

    public bool IsInteger()
    {
        if (!IsFinite)
            return false;

        double value;
        if (TryToFiniteDouble(out value))
            return value == Math.Truncate(value);

        return Abs() >= new ExpantaNum(MaxSafeInteger);
    }

    private ExpantaNum Reciprocal() => One / this;

    public ExpantaNum Ln()
    {
        ExpantaNum logarithm = Log10();
        return logarithm.IsNaN ? NaN : logarithm * Math.Log(10d);
    }

    private ExpantaNum Log1P()
    {
        if (IsNaN)
            return NaN;

        double value;
        if (TryToFiniteDouble(out value))
        {
            if (value < -1d)
                return NaN;
            if (value == -1d)
                return NegativeInfinity;

            return new ExpantaNum(Log1PDouble(value));
        }

        return (One + this).Ln();
    }

    private ExpantaNum ExpM1()
    {
        if (IsNaN)
            return NaN;
        if (IsInfinity)
            return Sign ? -One : PositiveInfinity;

        double value;
        if (TryToFiniteDouble(out value))
        {
            if (value > Math.Log(double.MaxValue))
                return PositiveInfinity;

            return new ExpantaNum(ExpM1Double(value));
        }

        return Exp() - One;
    }

    private ExpantaNum PowM1(ExpantaNum exponent)
    {
        if (IsNaN || exponent.IsNaN)
            return NaN;
        if (exponent.IsZero)
            return Zero;
        if (Sign || IsZero)
            return Pow(exponent) - One;

        return (Ln() * exponent).ExpM1();
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Sin() => FromFiniteUnary(Math.Sin);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Cos() => FromFiniteUnary(Math.Cos);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Tan() => FromFiniteUnary(Math.Tan);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Cot()
    {
        ExpantaNum tangent = Tan();
        return tangent.IsNaN || tangent.IsZero ? NaN : One / tangent;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Sec()
    {
        ExpantaNum cosine = Cos();
        return cosine.IsNaN || cosine.IsZero ? NaN : One / cosine;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Csc()
    {
        ExpantaNum sine = Sin();
        return sine.IsNaN || sine.IsZero ? NaN : One / sine;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Asin()
    {
        double value;
        return TryToFiniteDouble(out value) && value >= -1d && value <= 1d
            ? new ExpantaNum(Math.Asin(value))
            : NaN;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Acos()
    {
        double value;
        return TryToFiniteDouble(out value) && value >= -1d && value <= 1d
            ? new ExpantaNum(Math.Acos(value))
            : NaN;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Atan()
    {
        if (IsNaN)
            return NaN;
        if (IsInfinity)
            return new ExpantaNum(Sign ? -Math.PI / 2d : Math.PI / 2d);

        double value;
        return TryToFiniteDouble(out value) ? new ExpantaNum(Math.Atan(value)) : NaN;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Sinh()
    {
        if (IsNaN)
            return NaN;
        if (IsInfinity)
            return Sign ? NegativeInfinity : PositiveInfinity;

        double value;
        if (TryToFiniteDouble(out value) && Math.Abs(value) < 710d)
            return new ExpantaNum(Math.Sinh(value));

        ExpantaNum magnitude = Abs().Exp() / 2d;
        return Sign ? -magnitude : magnitude;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Cosh()
    {
        if (IsNaN)
            return NaN;
        if (IsInfinity)
            return PositiveInfinity;

        double value;
        if (TryToFiniteDouble(out value) && Math.Abs(value) < 710d)
            return new ExpantaNum(Math.Cosh(value));

        return Abs().Exp() / 2d;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Tanh()
    {
        if (IsNaN)
            return NaN;
        if (IsInfinity)
            return Sign ? -One : One;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(Math.Tanh(value));

        return Sign ? -One : One;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Asinh()
    {
        if (IsNaN)
            return NaN;
        if (IsInfinity)
            return Sign ? NegativeInfinity : PositiveInfinity;

        ExpantaNum magnitude = Abs();
        ExpantaNum result = (magnitude + (magnitude * magnitude + One).Sqrt()).Ln();
        return Sign ? -result : result;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Acosh()
    {
        if (this < One || IsNaN)
            return NaN;
        if (IsInfinity)
            return PositiveInfinity;

        return (this + (this - One).Sqrt() * (this + One).Sqrt()).Ln();
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Atanh()
    {
        if (IsNaN || this <= -One || this >= One)
            return NaN;

        return ((One + this) / (One - this)).Ln() / 2d;
    }

    /// <summary>
    /// 计算非负整数的阶乘 n!。
    /// 较小整数直接连乘；当数值较大时改用 Γ(n+1)，避免进行极大量的循环。
    /// </summary>
    /// <returns>阶乘结果；当前值不是非负整数时返回 NaN。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Factorial()
    {
        if (IsNaN || Sign)
            return NaN;
        if (IsInfinity)
            return PositiveInfinity;
        if (!IsInteger())
            return NaN;

        double value;
        if (TryToFiniteDouble(out value) && value <= 170d)
            return new ExpantaNum(FactorialTable[(int)value]);

        return (this + One).Gamma();
    }

    /// <summary>
    /// 计算 Gamma 函数 Γ(x)。Gamma 函数把阶乘推广到了非整数：对于正整数 n，有 Γ(n)=(n-1)!。
    /// 普通范围采用 Lanczos 近似；超大正数先计算 log Γ(x)，再恢复数量级，以避免中间结果溢出。
    /// </summary>
    /// <returns>Gamma 函数值；零和负整数位于极点，因此返回 NaN。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum Gamma()
    {
        if (IsNaN || (IsInfinity && Sign))
            return NaN;
        if (IsInfinity)
            return PositiveInfinity;

        double value;
        if (TryToFiniteDouble(out value))
        {
            if (value <= 0d && value == Math.Truncate(value))
                return NaN;

            double gamma = GammaLanczosDouble(value);
            if (!double.IsNaN(gamma) && !double.IsInfinity(gamma))
                return new ExpantaNum(gamma);

            double logAbsGamma = LogGammaLanczosDouble(value);
            if (double.IsNaN(logAbsGamma))
                return NaN;

            ExpantaNum magnitude = new ExpantaNum(logAbsGamma).Exp();
            if (value < 0d && Math.Sin(Math.PI * value) < 0d)
                magnitude = -magnitude;
            return magnitude;
        }

        if (Sign)
            return NaN;

        return LogGamma().Exp();
    }

    /// <summary>
    /// 计算 ln(Γ(x))。
    /// 当 Γ(x) 极其巨大时，直接保存其自然对数通常比先求 Γ(x) 更快、更稳定，也更适合后续乘除和概率公式。
    /// </summary>
    /// <returns>Gamma 函数的自然对数；当前实现主要支持正数定义域。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum LogGamma()
    {
        if (IsNaN || Sign || IsZero)
            return NaN;
        if (IsInfinity)
            return PositiveInfinity;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(LogGammaLanczosDouble(value));

        ExpantaNum inverse = One / this;
        return (this - 0.5d) * Ln() - this + 0.5d * Math.Log(2d * Math.PI)
               + inverse / 12d - inverse.Pow(3d) / 360d + inverse.Pow(5d) / 1260d;
    }

    /// <summary>
    /// 计算 Lambert W 函数，即求解 w·eʷ=x 中的 w。
    /// 它常用于把“未知数同时出现在指数和指数外部”的方程反解出来，例如指数增长、冷却时间和连续复利公式。
    /// 实数范围内支持两个分支：0 为主分支；-1 为下分支，后者只在 -1/e≤x<0 上存在。
    /// </summary>
    /// <param name="branch">分支编号，只能为 0 或 -1。</param>
    /// <returns>指定实数分支上的 Lambert W 值；超出定义域时返回 NaN。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum LambertW(int branch = 0)
    {
        if (branch != 0 && branch != -1)
            throw new ArgumentOutOfRangeException(nameof(branch), "仅支持 Lambert W 的 0 分支和 -1 分支。");
        if (IsNaN)
            return NaN;
        if (branch == -1 && IsZero)
            return NegativeInfinity;
        if (IsInfinity)
            return !Sign && branch == 0 ? PositiveInfinity : NaN;

        double value;
        if (TryToFiniteDouble(out value))
            return new ExpantaNum(LambertWDouble(value, branch));

        if (Sign || branch == -1)
            return NaN;

        ExpantaNum l1 = Ln();
        ExpantaNum l2 = l1.Ln();
        return l1 - l2 + l2 / l1 + l2 * (-2d + l2) / (2d * l1 * l1);
    }

    /// <summary>
    /// 计算带塔顶载荷的 tetration。其递推关系为 T(0,p)=p，T(h+1,p)=a^T(h,p)。
    /// 例如 a.Tetrate(2,p)=a^(a^p)。负高度通过重复对数近似逆运算；非整数高度在相邻整数高度间线性插值。
    /// 线性插值不是唯一或严格的连续 tetration 定义，但计算速度快，适合放置游戏。超大高度不会逐层循环，而会压缩成高阶表示。
    /// </summary>
    /// <param name="height">幂塔高度，可以是整数、非整数、负数或正无穷。</param>
    /// <param name="payload">可选塔顶载荷；省略时使用一，因此通常只需传入 height。</param>
    /// <returns>指定高度和载荷的幂塔结果。</returns>
    public ExpantaNum Tetrate(ExpantaNum height, ExpantaNum? payload = null)
    {
        ExpantaNum top = payload ?? One;
        if (IsNaN || height.IsNaN || top.IsNaN)
            return NaN;
        if (Sign)
            return NaN;
        if (height.IsInfinity)
        {
            if (height.Sign)
                return NaN;
            return InfiniteTetration();
        }

        double heightValue;
        if (!height.TryToFiniteDouble(out heightValue))
            return CompressHyperOperation(2d, height, top);

        if (heightValue < 0d)
            return top.IteratedLog(this, new ExpantaNum(-heightValue));
        if (heightValue == 0d)
            return top;

        double integerHeight = Math.Floor(heightValue);
        double fraction = heightValue - integerHeight;
        int directLimit = GetDirectTetrationLimit();
        int directSteps = (int)Math.Min(integerHeight, directLimit);
        ExpantaNum result = top;
        double completedHeight = 0d;

        for (int i = 0; i < directSteps; i++)
        {
            ExpantaNum next = Pow(result);
            completedHeight += 1d;
            if (next.ApproximatelyEquals(result))
            {
                result = next;
                completedHeight = integerHeight;
                break;
            }

            result = next;
            if (result.representation >= LayeredRepresentation && completedHeight < integerHeight)
                break;
        }

        double remaining = integerHeight - completedHeight;
        if (remaining > 0d)
            result = CompressHyperOperation(2d, new ExpantaNum(remaining), result);

        if (fraction > 0d)
        {
            ExpantaNum next = Pow(result);
            result = Lerp(result, next, new ExpantaNum(fraction));
        }

        return result;
    }

    /// <summary>
    /// 计算带初始载荷的 pentation。其递推关系可理解为 P(0,p)=p，P(h+1,p)=a.Tetrate(P(h,p))。
    /// 即每增加一层，都把前一层结果当作下一次幂塔的高度。即使很小的输入也会迅速超过普通科学计数法范围。
    /// 非整数高度使用相邻整数高度的线性近似；高度过大时压缩为高阶运算符表示，而不是逐次求值。
    /// </summary>
    /// <param name="height">pentation 高度。</param>
    /// <param name="payload">可选初始载荷；省略时使用一，因此通常只需传入 height。</param>
    /// <returns>指定高度和载荷的 pentation 结果。</returns>
    public ExpantaNum Pentate(ExpantaNum height, ExpantaNum? payload = null)
    {
        ExpantaNum top = payload ?? One;
        if (IsNaN || height.IsNaN || top.IsNaN || Sign)
            return NaN;
        if (height.IsInfinity)
            return height.Sign ? NaN : CompressHyperOperation(3d, height, top);

        double heightValue;
        if (!height.TryToFiniteDouble(out heightValue))
            return CompressHyperOperation(3d, height, top);
        if (heightValue < 0d)
            return NaN;
        if (heightValue == 0d)
            return top;

        double integerHeight = Math.Floor(heightValue);
        double fraction = heightValue - integerHeight;
        int directSteps = (int)Math.Min(integerHeight, GetDirectPentationLimit());
        ExpantaNum result = top;
        double completedHeight = 0d;

        for (int i = 0; i < directSteps; i++)
        {
            ExpantaNum next = Tetrate(result);
            completedHeight += 1d;
            if (next.ApproximatelyEquals(result))
            {
                result = next;
                completedHeight = integerHeight;
                break;
            }

            result = next;
            if (result.representation >= LayeredRepresentation && completedHeight < integerHeight)
                break;
        }

        double remaining = integerHeight - completedHeight;
        if (remaining > 0d)
            result = CompressHyperOperation(3d, new ExpantaNum(remaining), result);

        if (fraction > 0d)
        {
            ExpantaNum next = Tetrate(result);
            result = Lerp(result, next, new ExpantaNum(fraction));
        }

        return result;
    }

    /// <summary>
    /// 重复执行指定底数的对数。
    /// 它可以看作 tetration 在高度方向上的逆向移动：对 a 的幂塔取一次以 a 为底的对数，通常会降低一层高度。
    /// 非整数次数采用最后两次结果之间的线性近似。
    /// </summary>
    /// <param name="newBase">每次对数运算使用的底数，必须大于零且不等于一。</param>
    /// <param name="times">重复取对数的次数，必须非负。</param>
    /// <returns>重复取对数后的结果；无法可靠降层时返回 NaN。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum IteratedLog(ExpantaNum newBase, ExpantaNum times)
    {
        if (IsNaN || newBase <= Zero || newBase == One || times.IsNaN || times.Sign)
            return NaN;

        double count;
        if (!times.TryToFiniteDouble(out count))
            return NaN;

        int whole = (int)Math.Min(Math.Floor(count), GetDirectLogIterationLimit());
        double fraction = count - Math.Floor(count);
        ExpantaNum result = this;

        for (int i = 0; i < whole; i++)
            result = result.Log(newBase);

        if (Math.Floor(count) > whole)
        {
            double remaining = Math.Floor(count) - whole;
            if (newBase == Ten && result.Operator(1d) >= remaining)
            {
                result = result.Clone();
                result.SetOperator(1d, result.Operator(1d) - remaining);
            }
            else
            {
                return NaN;
            }
        }

        if (fraction > 0d)
            result = Lerp(result, result.Log(newBase), new ExpantaNum(fraction));

        return result;
    }

    /// <summary>
    /// 估算超对数 slogₐ(x)，也就是寻找高度 h，使 a.Tetrate(h)≈x。
    /// 普通对数回答“需要多少次乘方”，超对数回答“需要多高的幂塔”。它是 tetration 关于高度的近似反函数。
    /// 对于高层级表示可直接读取层数；普通范围则通过反复取对数估算，因此结果是近似值。
    /// </summary>
    /// <param name="newBase">定义幂塔的底数，必须大于一。</param>
    /// <returns>近似的 tetration 高度。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ExpantaNum SuperLog(ExpantaNum newBase)
    {
        if (IsNaN || Sign || IsZero || newBase <= One)
            return NaN;

        if (newBase == Ten)
        {
            double tetrationLayers = Operator(2d);
            if (tetrationLayers > 0d)
                return new ExpantaNum(tetrationLayers) + GetBottomValue();
            if (Layer > 0d)
                return new ExpantaNum(Layer + 1d);
        }

        ExpantaNum value = this;
        double height = 0d;
        while (value > One && height < GetDirectLogIterationLimit())
        {
            value = value.Log(newBase);
            height += 1d;
            if (value.IsNaN)
                return NaN;
        }

        double remainder;
        if (value.TryToFiniteDouble(out remainder))
            height += remainder - 1d;

        return new ExpantaNum(height);
    }

    private static ExpantaNum Lerp(ExpantaNum a, ExpantaNum b, ExpantaNum t) => a + (b - a) * t;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static ExpantaNum Atan2(ExpantaNum y, ExpantaNum x)
    {
        double yValue;
        double xValue;
        return y.TryToFiniteDouble(out yValue) && x.TryToFiniteDouble(out xValue)
            ? new ExpantaNum(Math.Atan2(yValue, xValue))
            : NaN;
    }

















    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public bool ApproximatelyEquals(ExpantaNum other, double tolerance = DefaultTolerance)
    {
        if (tolerance < 0d || double.IsNaN(tolerance))
            throw new ArgumentOutOfRangeException(nameof(tolerance));

        if (Equals(other))
            return true;
        if (IsNaN || other.IsNaN || IsInfinity || other.IsInfinity || Sign != other.Sign)
            return false;

        double leftScalar;
        double rightScalar;
        if (TryGetSignedScalar(out leftScalar) && other.TryGetSignedScalar(out rightScalar))
        {
            double scale = Math.Max(1d, Math.Max(Math.Abs(leftScalar), Math.Abs(rightScalar)));
            return Math.Abs(leftScalar - rightScalar) <= tolerance * scale;
        }

        double leftLog;
        double rightLog;
        if (Abs().TryGetLog10Double(out leftLog) && other.Abs().TryGetLog10Double(out rightLog))
        {
            double scale = Math.Max(1d, Math.Max(Math.Abs(leftLog), Math.Abs(rightLog)));
            return Math.Abs(leftLog - rightLog) <= tolerance * scale;
        }

        return false;
    }

    public double ToDouble()
    {
        if (IsNaN)
            return double.NaN;
        if (IsInfinity)
            return Sign ? double.NegativeInfinity : double.PositiveInfinity;

        double value;
        if (!TryToFiniteDouble(out value))
            return Sign ? double.NegativeInfinity : double.PositiveInfinity;
        return value;
    }

    private bool TryToFiniteDouble(out double value)
    {
        value = 0d;
        if (!IsFinite)
            return false;
        if (IsZero)
            return true;
        if (representation == ScalarRepresentation)
        {
            value = Sign ? -scalar : scalar;
            return true;
        }
        if (representation == LayeredRepresentation)
            return false;
        if (layer != 0d)
            return false;

        int count = GetOperatorCount();
        for (int i = 0; i < count; i++)
        {
            if (GetOperation(i) > 1d && GetRepetitions(i) != 0d)
                return false;
        }

        double magnitude = Operator(0d);
        double exponentRepetitions = Operator(1d);
        if (exponentRepetitions != Math.Truncate(exponentRepetitions) || exponentRepetitions > 64d)
            return false;

        for (int i = 0; i < (int)exponentRepetitions; i++)
        {
            if (magnitude > DoubleLog10Limit)
                return false;
            magnitude = Math.Pow(10d, magnitude);
        }

        value = Sign ? -magnitude : magnitude;
        return !double.IsInfinity(value) && !double.IsNaN(value);
    }

    private bool TryGetLog10Double(out double value)
    {
        value = 0d;
        if (!IsFinite || IsZero)
            return false;
        if (representation == ScalarRepresentation)
        {
            value = Math.Log10(scalar);
            return !double.IsInfinity(value) && !double.IsNaN(value);
        }
        if (representation == LayeredRepresentation)
        {
            if (layer != 1d)
                return false;
            value = scalar;
            return !double.IsInfinity(value) && !double.IsNaN(value);
        }
        if (layer != 0d)
            return false;

        int count = GetOperatorCount();
        for (int i = 0; i < count; i++)
        {
            if (GetOperation(i) > 1d && GetRepetitions(i) != 0d)
                return false;
        }

        double magnitude = Operator(0d);
        double repetitions = Operator(1d);
        if (repetitions != Math.Truncate(repetitions) || repetitions > 64d)
            return false;

        if (repetitions == 0d)
        {
            value = Math.Log10(magnitude);
            return !double.IsInfinity(value) && !double.IsNaN(value);
        }

        repetitions -= 1d;
        for (int i = 0; i < (int)repetitions; i++)
        {
            if (magnitude > DoubleLog10Limit)
                return false;
            magnitude = Math.Pow(10d, magnitude);
        }

        value = magnitude;
        return !double.IsInfinity(value) && !double.IsNaN(value);
    }


    public int CompareTo(ExpantaNum other)
    {
        if (IsNaN || other.IsNaN)
            throw new InvalidOperationException("NaN cannot be ordered.");

        if (Equals(other))
            return 0;

        if (IsInfinity)
            return Sign ? -1 : 1;
        if (other.IsInfinity)
            return other.Sign ? 1 : -1;

        if (IsZero)
            return other.Sign ? 1 : -1;
        if (other.IsZero)
            return Sign ? -1 : 1;

        if (Sign != other.Sign)
            return Sign ? -1 : 1;

        int magnitude = CompareMagnitude(this, other);
        return Sign ? -magnitude : magnitude;
    }

    int IComparable.CompareTo(object obj)
    {
        if (obj == null)
            return 1;
        if (!(obj is ExpantaNum))
            throw new ArgumentException("Object must be an ExpantaNum.", nameof(obj));
        return CompareTo((ExpantaNum)obj);
    }

    public bool Equals(ExpantaNum other)
    {
        if (IsNaN || other.IsNaN)
            return false;
        if (IsZero && other.IsZero)
            return true;
        if (Sign != other.Sign || representation != other.representation)
            return false;

        if (representation == ScalarRepresentation)
            return scalar.Equals(other.scalar);
        if (representation == LayeredRepresentation)
            return layer.Equals(other.layer) && scalar.Equals(other.scalar);
        if (!layer.Equals(other.layer))
            return false;

        int count = GetOperatorCount();
        if (count != other.GetOperatorCount())
            return false;
        for (int i = 0; i < count; i++)
        {
            if (GetOperation(i) != other.GetOperation(i) ||
                GetRepetitions(i) != other.GetRepetitions(i))
                return false;
        }
        return true;
    }

    public override bool Equals(object obj) => obj is ExpantaNum other && Equals(other);

    public override int GetHashCode()
    {
        if (IsZero)
            return 0;

        unchecked
        {
            int hash = Sign.GetHashCode();
            hash = (hash * 397) ^ representation.GetHashCode();
            if (representation == ScalarRepresentation)
                return (hash * 397) ^ scalar.GetHashCode();
            if (representation == LayeredRepresentation)
            {
                hash = (hash * 397) ^ layer.GetHashCode();
                return (hash * 397) ^ scalar.GetHashCode();
            }

            hash = (hash * 397) ^ layer.GetHashCode();
            int count = GetOperatorCount();
            for (int i = 0; i < count; i++)
            {
                hash = (hash * 397) ^ GetOperation(i).GetHashCode();
                hash = (hash * 397) ^ GetRepetitions(i).GetHashCode();
            }
            return hash;
        }
    }

    public override string ToString()
    {
        if (IsNaN)
            return "NaN";
        if (IsInfinity)
            return Sign ? "-Infinity" : "Infinity";
        if (IsZero)
            return "0";

        if (representation == ScalarRepresentation)
        {
            double numeric = Sign ? -scalar : scalar;
            if (Math.Abs(numeric) >= 1e-6 && Math.Abs(numeric) < 1e21)
                return numeric.ToString("0.######", CultureInfo.InvariantCulture);

            return numeric.ToString("R", CultureInfo.InvariantCulture)
                .Replace("E+", "e")
                .Replace("E", "e");
        }

        StringBuilder builder = new StringBuilder();
        if (Sign)
            builder.Append('-');

        if (representation == LayeredRepresentation)
        {
            if (layer == Math.Truncate(layer) && layer <= MaxRepeatedEOutput)
            {
                for (int i = 0; i < (int)layer; i++)
                    builder.Append('e');
            }
            else
            {
                builder.Append("(10^)^" );
                builder.Append(layer.ToString("G6", CultureInfo.InvariantCulture));
                builder.Append(' ');
            }
            builder.Append(scalar.ToString("G6", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        if (layer > 0d)
        {
            builder.Append("J^");
            builder.Append(layer.ToString("G6", CultureInfo.InvariantCulture));
            builder.Append(' ');
        }

        int count = GetOperatorCount();
        double exponentCount = Operator(1d);
        for (int i = count - 1; i >= 0; i--)
        {
            double operation = GetOperation(i);
            double repetitions = GetRepetitions(i);
            if (operation <= 1d || repetitions == 0d)
                continue;

            if (repetitions == 1d)
            {
                builder.Append("10{");
                builder.Append(operation.ToString("G6", CultureInfo.InvariantCulture));
                builder.Append('}');
            }
            else
            {
                builder.Append("(10{");
                builder.Append(operation.ToString("G6", CultureInfo.InvariantCulture));
                builder.Append("})^");
                builder.Append(repetitions.ToString("G6", CultureInfo.InvariantCulture));
                builder.Append(' ');
            }
        }

        if (exponentCount > 0d)
        {
            if (exponentCount == Math.Truncate(exponentCount) && exponentCount <= MaxRepeatedEOutput)
            {
                for (int i = 0; i < (int)exponentCount; i++)
                    builder.Append('e');
            }
            else
            {
                builder.Append("(10^)^" );
                builder.Append(exponentCount.ToString("G6", CultureInfo.InvariantCulture));
                builder.Append(' ');
            }
        }

        builder.Append(GetBottomValue().ToString("G6", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    /// <summary>
    /// 将数值格式化为适合放置游戏界面的紧凑文本。默认使用 K、M、B、T，超过 T 后自动切换为科学计数法。
    /// 等价值会先规范化，因此 1e6、1000000 和 100e4 会得到相同显示结果。
    /// </summary>
    /// <param name="significantDigits">显示的有效数字位数，限制在 1 到 6。</param>
    /// <param name="showPositiveSign">是否为正数显示“+”。</param>
    /// <param name="format">显示模式；通常保持默认值即可。</param>
    /// <returns>格式化后的游戏 UI 文本。</returns>
    public string ToGameString(
        int significantDigits = 4,
        bool showPositiveSign = false,
        ExpantaNumFormat format = ExpantaNumFormat.Suffix)
    {
        if (significantDigits < 1)
            significantDigits = 1;
        else if (significantDigits > MaxGameSignificantDigits)
            significantDigits = MaxGameSignificantDigits;

        if (IsNaN)
            return "NaN";
        if (IsInfinity)
            return Sign ? "-Infinity" : showPositiveSign ? "+Infinity" : "Infinity";
        if (IsZero)
            return "0";

        string prefix = Sign ? "-" : showPositiveSign ? "+" : string.Empty;
        if (representation == LayeredRepresentation)
        {
            if (layer == 1d && format != ExpantaNumFormat.HyperOperation)
                return prefix + "1e" + FormatDisplayNumber(scalar, significantDigits);
            return prefix + Abs().ToString();
        }
        if (representation == HyperRepresentation)
            return prefix + Abs().ToString();

        double magnitude = scalar;
        if (format == ExpantaNumFormat.HyperOperation)
            return prefix + Abs().ToString();

        if (format == ExpantaNumFormat.Scientific)
        {
            int exponent = (int)Math.Floor(Math.Log10(magnitude));
            double mantissa = magnitude / Math.Pow(10d, exponent);
            mantissa = RoundToSignificantDigits(mantissa, significantDigits);
            if (mantissa >= 10d)
            {
                mantissa /= 10d;
                exponent++;
            }

            return prefix + FormatDisplayNumber(mantissa, significantDigits) + "e" +
                   exponent.ToString(CultureInfo.InvariantCulture);
        }

        if (format == ExpantaNumFormat.Engineering)
        {
            int exponent = (int)Math.Floor(Math.Log10(magnitude) / 3d) * 3;
            double scaled = magnitude / Math.Pow(10d, exponent);
            scaled = RoundToSignificantDigits(scaled, significantDigits);
            if (scaled >= 1000d)
            {
                scaled /= 1000d;
                exponent += 3;
            }

            return prefix + FormatDisplayNumber(scaled, significantDigits) + "e" +
                   exponent.ToString(CultureInfo.InvariantCulture);
        }

        if (magnitude < 1000d)
            return prefix + FormatDisplayNumber(magnitude, significantDigits);

        // K/M/B/T 是游戏 UI 最常见的区间，直接比较阈值比每次 Log10/Pow 更快。
        int group;
        if (magnitude < 1e6d)
            group = 1;
        else if (magnitude < 1e9d)
            group = 2;
        else if (magnitude < 1e12d)
            group = 3;
        else if (magnitude < 1e15d)
            group = 4;
        else
            group = 0;

        if (group != 0)
        {
            double scaled = RoundToSignificantDigits(
                magnitude / GameSuffixValues[group],
                significantDigits);

            if (scaled >= 1000d)
            {
                if (group < 4)
                {
                    group++;
                    scaled = RoundToSignificantDigits(
                        magnitude / GameSuffixValues[group],
                        significantDigits);
                }
                else
                {
                    group = 0;
                }
            }

            if (group != 0)
                return prefix + FormatDisplayNumber(scaled, significantDigits) + GameSuffixes[group];
        }

        int exponentFallback = (int)Math.Floor(Math.Log10(magnitude));
        double mantissaFallback = magnitude / Math.Pow(10d, exponentFallback);
        mantissaFallback = RoundToSignificantDigits(mantissaFallback, significantDigits);
        if (mantissaFallback >= 10d)
        {
            mantissaFallback /= 10d;
            exponentFallback++;
        }

        return prefix + FormatDisplayNumber(mantissaFallback, significantDigits) + "e" +
               exponentFallback.ToString(CultureInfo.InvariantCulture);
    }


    /// <summary>
    /// 返回便于排查数值错误的内部表示文本。
    /// 该文本会显示符号、表示类型、标量、层级以及稀疏运算符数组，不适合直接展示给玩家。
    /// </summary>
    /// <returns>当前 ExpantaNum 的内部调试信息。</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public string ToDebugString()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Sign=");
        builder.Append(Sign);
        builder.Append(", Representation=");
        builder.Append(GetRepresentationName());
        builder.Append(", Scalar=");
        builder.Append(scalar.ToString("R", CultureInfo.InvariantCulture));
        builder.Append(", Layer=");
        builder.Append(Layer.ToString("R", CultureInfo.InvariantCulture));
        int count = GetOperatorCount();
        builder.Append(", OperatorCount=");
        builder.Append(count);
        builder.Append(", OperatorCapacity=");
        builder.Append(operators == null ? 0 : operators.Length);
        builder.Append(", Operators=[");

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append('(');
            builder.Append(GetOperation(i).ToString("R", CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(GetRepetitions(i).ToString("R", CultureInfo.InvariantCulture));
            builder.Append(')');
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string FormatDisplayNumber(double value, int significantDigits)
    {
        string result = value.ToString(
            "G" + significantDigits.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        return result.Replace("E+", "e").Replace("E", "e");
    }

    private static double RoundToSignificantDigits(double value, int digits)
    {
        if (value == 0d || double.IsNaN(value) || double.IsInfinity(value))
            return value;

        double scale = Math.Pow(10d, digits - 1 - Math.Floor(Math.Log10(Math.Abs(value))));
        return Math.Round(value * scale, 0, MidpointRounding.AwayFromZero) / scale;
    }


    private ExpantaNum FromFiniteUnary(Func<double, double> function)
    {
        double value;
        if (!TryToFiniteDouble(out value))
            return NaN;

        double result = function(value);
        return double.IsNaN(result) ? NaN : new ExpantaNum(result);
    }

    private int GetDirectTetrationLimit()
    {
        double baseValue;
        if (!TryGetSignedScalar(out baseValue))
            return 2;

        baseValue = Math.Abs(baseValue);
        if (baseValue >= 10d)
            return 3;
        if (baseValue >= 2d)
            return 4;
        if (baseValue > 1.1d)
            return 8;
        return 16;
    }

    private int GetDirectPentationLimit()
    {
        double baseValue;
        return TryGetSignedScalar(out baseValue) && Math.Abs(baseValue) < 2d ? 3 : 2;
    }

    private static int GetDirectLogIterationLimit() => 64;

    private ExpantaNum InfiniteTetration()
    {
        if (IsNaN || Sign)
            return NaN;
        if (IsZero)
            return NaN;
        if (this == One)
            return One;

        double baseValue;
        if (!TryToFiniteDouble(out baseValue))
            return PositiveInfinity;

        double lower = Math.Exp(-Math.E);
        double upper = Math.Exp(1d / Math.E);
        if (baseValue < lower || baseValue > upper)
            return NaN;

        double logarithm = Math.Log(baseValue);
        if (logarithm == 0d)
            return One;

        return new ExpantaNum(-LambertWDouble(-logarithm, 0) / logarithm);
    }

    private ExpantaNum CompressHyperOperation(double operation, ExpantaNum repetitions, ExpantaNum payload)
    {
        if (repetitions.IsNaN || repetitions.Sign)
            return NaN;
        if (repetitions.IsZero)
            return payload;

        double count;
        if (!repetitions.TryToFiniteDouble(out count))
            count = MaxSafeInteger;

        ExpantaNum result = payload.Clone();
        result.SetOperator(operation, result.Operator(operation) + Math.Min(MaxSafeInteger, count));

        double baseValue;
        if (TryToFiniteDouble(out baseValue) && baseValue > 0d && baseValue != 10d)
        {
            double adjustment = Math.Log10(baseValue);
            if (adjustment > 0d && !double.IsInfinity(adjustment))
                result.SetOperator(0d, Math.Max(result.GetBottomValue(), adjustment));
        }

        return result;
    }

    private static double GammaLanczosDouble(double value)
    {
        if (value <= 0d && value == Math.Truncate(value))
            return double.NaN;

        if (value < 0.5d)
            return Math.PI / (Math.Sin(Math.PI * value) * GammaLanczosDouble(1d - value));


        double z = value - 1d;
        double x = LanczosCoefficients[0];
        for (int i = 1; i < LanczosCoefficients.Length; i++)
            x += LanczosCoefficients[i] / (z + i);

        double t = z + 7.5d;
        return SqrtTwoPi * Math.Pow(t, z + 0.5d) * Math.Exp(-t) * x;
    }

    private static double LogGammaLanczosDouble(double value)
    {
        if (value <= 0d)
            return double.NaN;


        double z = value - 1d;
        double x = LanczosCoefficients[0];
        for (int i = 1; i < LanczosCoefficients.Length; i++)
            x += LanczosCoefficients[i] / (z + i);

        double t = z + 7.5d;
        return HalfLogTwoPi + (z + 0.5d) * Math.Log(t) - t + Math.Log(x);
    }

    private static double LambertWDouble(double value, int branch)
    {
        double minimum = -1d / Math.E;
        if (double.IsNaN(value) || value < minimum || (branch == -1 && (value >= 0d || value < minimum)))
            return double.NaN;
        if (value == 0d)
            return branch == 0 ? 0d : double.NegativeInfinity;
        if (value == minimum)
            return -1d;
        if (double.IsPositiveInfinity(value))
            return double.PositiveInfinity;

        double w;
        if (branch == -1)
        {
            double l1 = Math.Log(-value);
            double l2 = Math.Log(-l1);
            w = l1 - l2 + l2 / l1;
            if (w > -1d)
                w = -1.1d;
        }
        else if (value < 1d)
        {
            w = value;
        }
        else
        {
            double l1 = Math.Log(value);
            double l2 = Math.Log(l1);
            w = l1 - l2 + l2 / l1;
        }

        for (int i = 0; i < 8; i++)
        {
            double exponential = Math.Exp(w);
            double f = w * exponential - value;
            double denominator = exponential * (w + 1d) - (w + 2d) * f / (2d * w + 2d);
            if (denominator == 0d || double.IsInfinity(denominator) || double.IsNaN(denominator))
                break;

            double next = w - f / denominator;
            if (Math.Abs(next - w) <= 5e-7d * Math.Max(1d, Math.Abs(next)))
                return next;
            w = next;
        }

        return w;
    }

    private static ExpantaNum AddMagnitudes(ExpantaNum left, ExpantaNum right)
    {
        double leftValue;
        double rightValue;
        if (left.TryToFiniteDouble(out leftValue) && right.TryToFiniteDouble(out rightValue))
        {
            double sum = leftValue + rightValue;
            if (!double.IsInfinity(sum) && sum <= PromotionLimit)
                return new ExpantaNum(sum);
        }

        double leftLog;
        double rightLog;
        if (left.TryGetLog10Double(out leftLog) && right.TryGetLog10Double(out rightLog))
        {
            double maximum = Math.Max(leftLog, rightLog);
            double minimum = Math.Min(leftLog, rightLog);
            if (maximum - minimum > DominanceCutoff)
                return leftLog >= rightLog ? left : right;

            return FromLog10(maximum + Math.Log10(1d + Math.Pow(10d, minimum - maximum)));
        }

        return CompareMagnitude(left, right) >= 0 ? left : right;
    }

    private static ExpantaNum SubtractMagnitudes(ExpantaNum larger, ExpantaNum smaller)
    {
        double largerValue;
        double smallerValue;
        if (larger.TryToFiniteDouble(out largerValue) && smaller.TryToFiniteDouble(out smallerValue))
            return new ExpantaNum(Math.Max(0d, largerValue - smallerValue));

        double largerLog;
        double smallerLog;
        if (larger.TryGetLog10Double(out largerLog) && smaller.TryGetLog10Double(out smallerLog))
        {
            if (largerLog - smallerLog > DominanceCutoff)
                return larger;

            double factor = 1d - Math.Pow(10d, smallerLog - largerLog);
            if (factor <= 0d)
                return Zero;
            return FromLog10(largerLog + Math.Log10(factor));
        }

        return larger;
    }

    private static int CompareMagnitude(ExpantaNum left, ExpantaNum right)
    {
        if (left.representation == ScalarRepresentation && right.representation == ScalarRepresentation)
            return left.scalar.CompareTo(right.scalar);

        if (left.representation != right.representation)
        {
            if (left.representation == HyperRepresentation)
                return 1;
            if (right.representation == HyperRepresentation)
                return -1;
            if (left.representation == LayeredRepresentation)
                return 1;
            if (right.representation == LayeredRepresentation)
                return -1;
        }

        if (left.representation == LayeredRepresentation)
        {
            int layerComparison = left.layer.CompareTo(right.layer);
            return layerComparison != 0 ? layerComparison : left.scalar.CompareTo(right.scalar);
        }

        if (left.layer > right.layer)
            return 1;
        if (left.layer < right.layer)
            return -1;

        int ai = left.GetOperatorCount() - 1;
        int bi = right.GetOperatorCount() - 1;
        while (ai >= 0 || bi >= 0)
        {
            double aOperation = ai >= 0 ? left.GetOperation(ai) : -1d;
            double bOperation = bi >= 0 ? right.GetOperation(bi) : -1d;

            if (aOperation > bOperation)
                return left.GetRepetitions(ai) > 0d ? 1 : -1;
            if (aOperation < bOperation)
                return right.GetRepetitions(bi) > 0d ? -1 : 1;

            double aRepetitions = ai >= 0 ? left.GetRepetitions(ai) : 0d;
            double bRepetitions = bi >= 0 ? right.GetRepetitions(bi) : 0d;
            int repetitionComparison = aRepetitions.CompareTo(bRepetitions);
            if (repetitionComparison != 0)
                return repetitionComparison;

            ai--;
            bi--;
        }

        return 0;
    }

    private ExpantaNum Clone()
    {
        return this;
    }

    private ExpantaNum WithAddedOperator(double operation, double repetitions)
    {
        ExpantaNum result = Clone();
        result.SetOperator(operation, result.Operator(operation) + repetitions);
        return result;
    }

    private void SetOperator(double operation, double repetitions)
    {
        operation = Math.Truncate(operation);
        repetitions = Quantize(repetitions);
        if (operation < 0d || double.IsNaN(operation) || double.IsInfinity(operation))
            return;

        if (representation == ZeroRepresentation)
        {
            if (operation == 0d)
            {
                if (repetitions < 0d)
                    sign = !sign;
                scalar = Math.Abs(repetitions);
                representation = scalar == 0d ? ZeroRepresentation : ScalarRepresentation;
                return;
            }

            if (operation == 1d)
            {
                representation = LayeredRepresentation;
                scalar = 0d;
                layer = repetitions;
                NormalizeInPlace();
                return;
            }

            representation = HyperRepresentation;
            scalar = 0d;
            layer = 0d;
            operators = new ExpantaNumOperator[3];
            operators[0] = new ExpantaNumOperator(0d, 0d);
            operators[1] = new ExpantaNumOperator(operation, repetitions);
            operatorCount = 2;
            NormalizeInPlace(true);
            return;
        }

        if (representation == ScalarRepresentation)
        {
            if (operation == 0d)
            {
                if (repetitions < 0d)
                    sign = !sign;
                scalar = Math.Abs(repetitions);
                if (scalar == 0d)
                    ResetToZero();
                return;
            }

            if (operation == 1d)
            {
                representation = LayeredRepresentation;
                layer = repetitions;
                NormalizeInPlace();
                return;
            }

            operators = new ExpantaNumOperator[3];
            operators[0] = new ExpantaNumOperator(0d, scalar);
            operators[1] = new ExpantaNumOperator(operation, repetitions);
            operatorCount = 2;
            representation = HyperRepresentation;
            scalar = 0d;
            layer = 0d;
            NormalizeInPlace(true);
            return;
        }

        if (representation == LayeredRepresentation)
        {
            if (operation == 0d)
            {
                scalar = repetitions;
                NormalizeInPlace();
                return;
            }

            if (operation == 1d)
            {
                layer = repetitions;
                NormalizeInPlace();
                return;
            }

            operators = new ExpantaNumOperator[4];
            operators[0] = new ExpantaNumOperator(0d, scalar);
            operators[1] = new ExpantaNumOperator(1d, layer);
            operators[2] = new ExpantaNumOperator(operation, repetitions);
            operatorCount = 3;
            representation = HyperRepresentation;
            scalar = 0d;
            layer = 0d;
            NormalizeInPlace(true);
            return;
        }

        ExpantaNumOperator[] current = operators ?? EmptyOperators;
        int currentCount = Math.Min(Math.Max(operatorCount, 0), current.Length);
        int index = -1;
        int insert = 0;
        while (insert < currentCount && current[insert].Operation < operation)
            insert++;
        if (insert < currentCount && current[insert].Operation == operation)
            index = insert;

        if (repetitions == 0d && operation != 0d)
        {
            if (index < 0)
                return;

            int reducedCount = currentCount - 1;
            ExpantaNumOperator[] reduced = new ExpantaNumOperator[Math.Max(reducedCount + 1, 1)];
            if (index > 0)
                Array.Copy(current, 0, reduced, 0, index);
            if (index < currentCount - 1)
                Array.Copy(current, index + 1, reduced, index, currentCount - index - 1);
            operators = reduced;
            operatorCount = reducedCount;
            NormalizeInPlace(true);
            return;
        }

        if (index >= 0)
        {
            ExpantaNumOperator[] updated = CloneOperators(current, currentCount, true);
            updated[index] = new ExpantaNumOperator(operation, repetitions);
            operators = updated;
            operatorCount = currentCount;
            NormalizeInPlace(true);
            return;
        }

        if (currentCount >= MaxHyperOperators)
        {
            // 达到容量上限时保留底值和数量级最高的运算符。
            // 新运算符若比当前最小的非底层运算符还低，则忽略它；否则淘汰较低层级。
            if (currentCount <= 1 || operation <= current[1].Operation)
                return;

            ExpantaNumOperator[] capped = new ExpantaNumOperator[MaxHyperOperators];
            capped[0] = current[0];
            int target = 1;
            bool inserted = false;
            for (int i = 2; i < currentCount && target < MaxHyperOperators; i++)
            {
                if (!inserted && operation < current[i].Operation)
                {
                    capped[target++] = new ExpantaNumOperator(operation, repetitions);
                    inserted = true;
                }

                if (target < MaxHyperOperators)
                    capped[target++] = current[i];
            }

            if (!inserted && target < MaxHyperOperators)
                capped[target++] = new ExpantaNumOperator(operation, repetitions);

            operators = capped;
            operatorCount = target;
            NormalizeInPlace(true);
            return;
        }

        int expandedCount = currentCount + 1;
        int expandedCapacity = Math.Min(MaxHyperOperators, Math.Max(expandedCount + 1, expandedCount));
        ExpantaNumOperator[] expanded = new ExpantaNumOperator[expandedCapacity];
        if (insert > 0)
            Array.Copy(current, 0, expanded, 0, insert);
        expanded[insert] = new ExpantaNumOperator(operation, repetitions);
        if (insert < currentCount)
            Array.Copy(current, insert, expanded, insert + 1, currentCount - insert);
        operators = expanded;
        operatorCount = expandedCount;
        NormalizeInPlace(true);
    }

    private void NormalizeInPlace(bool operatorsOwned = false)
    {
        if (representation == ZeroRepresentation)
        {
            ResetToZero();
            return;
        }

        if (representation == ScalarRepresentation)
        {
            operatorCount = 0;
            operators = null;
            layer = 0d;

            if (double.IsNaN(scalar))
            {
                sign = false;
                return;
            }

            scalar = Quantize(Math.Abs(scalar));
            if (scalar == 0d)
            {
                ResetToZero();
                return;
            }

            if (scalar > PromotionLimit && !double.IsInfinity(scalar))
            {
                scalar = Quantize(Math.Log10(scalar));
                layer = 1d;
                representation = LayeredRepresentation;
            }
            return;
        }

        if (representation == LayeredRepresentation)
        {
            operatorCount = 0;
            operators = null;

            if (double.IsNaN(scalar) || double.IsNaN(layer))
            {
                representation = ScalarRepresentation;
                scalar = double.NaN;
                sign = false;
                layer = 0d;
                return;
            }

            if (double.IsInfinity(scalar) || double.IsPositiveInfinity(layer))
            {
                representation = ScalarRepresentation;
                scalar = double.PositiveInfinity;
                layer = 0d;
                return;
            }

            layer = Math.Max(0d, Math.Min(MaxSafeInteger, Math.Truncate(layer)));
            scalar = Quantize(scalar);
            if (layer == 0d)
            {
                double signedValue = sign ? -Math.Abs(scalar) : scalar;
                this = new ExpantaNum(signedValue);
                return;
            }

            while (layer > 1d && scalar <= DoubleLog10Limit)
            {
                scalar = Quantize(Math.Pow(10d, scalar));
                layer -= 1d;
            }

            if (layer == 1d)
            {
                if (scalar < -324d)
                {
                    ResetToZero();
                    return;
                }

                if (scalar <= PromotionLog10)
                {
                    double magnitude = Math.Pow(10d, scalar);
                    this = new ExpantaNum(sign ? -magnitude : magnitude);
                    return;
                }
            }

            return;
        }

        int sourceCount = operators == null
            ? 0
            : Math.Min(Math.Min(Math.Max(operatorCount, 0), operators.Length), MaxHyperOperators);
        if (sourceCount == 0)
        {
            ResetToZero();
            return;
        }

        if (double.IsNaN(layer) || layer < 0d)
            layer = 0d;
        else if (layer > MaxSafeInteger)
            layer = MaxSafeInteger;
        else
            layer = Math.Truncate(layer);

        // 共享数组必须复制；刚由 SetOperator 创建的数组归当前值独占，可以直接复用。
        // 高阶值最多只保留少量稀疏运算符，避免极端输入造成数组无限膨胀。
        int capacity = Math.Min(sourceCount + 1, MaxHyperOperators);
        ExpantaNumOperator[] compact;
        int copied = Math.Min(sourceCount, capacity);
        if (operatorsOwned && operators.Length >= capacity)
        {
            compact = operators;
        }
        else
        {
            compact = new ExpantaNumOperator[capacity];
            Array.Copy(operators, compact, copied);
        }
        SortOperatorsInPlace(compact, copied);

        int count = 0;
        bool special = false;
        double specialValue = 0d;

        for (int i = 0; i < copied && count < MaxHyperOperators; i++)
        {
            double operation = compact[i].Operation;
            double repetitions = compact[i].Repetitions;

            if (operation == 0d && (double.IsNaN(repetitions) || double.IsInfinity(repetitions)))
            {
                special = true;
                specialValue = double.IsNaN(repetitions) ? double.NaN : double.PositiveInfinity;
                break;
            }

            if (double.IsNaN(operation) || double.IsInfinity(operation) || operation < 0d ||
                double.IsNaN(repetitions) || repetitions < 0d)
                continue;

            operation = Math.Min(MaxSafeInteger, Math.Truncate(operation));
            repetitions = Quantize(repetitions);
            if (operation != 0d && repetitions == 0d)
                continue;

            if (count > 0 && compact[count - 1].Operation == operation)
            {
                double merged = Math.Min(
                    MaxSafeInteger,
                    Quantize(compact[count - 1].Repetitions + repetitions));
                compact[count - 1] = new ExpantaNumOperator(operation, merged);
            }
            else
            {
                compact[count++] = new ExpantaNumOperator(operation, repetitions);
            }
        }

        if (special)
        {
            representation = ScalarRepresentation;
            operatorCount = 0;
            scalar = specialValue;
            layer = 0d;
            operators = null;
            if (double.IsNaN(specialValue))
                sign = false;
            return;
        }

        if (count == 0 || compact[0].Operation != 0d)
        {
            if (count >= compact.Length)
            {
                // 容量已满时丢弃最低的非底层运算符，保留数量级最高的部分。
                Array.Copy(compact, 1, compact, 1, count - 1);
                compact[0] = new ExpantaNumOperator(0d, 0d);
            }
            else
            {
                Array.Copy(compact, 0, compact, 1, count);
                compact[0] = new ExpantaNumOperator(0d, 0d);
                count++;
            }
        }

        double bottom = compact[0].Repetitions;
        if (bottom < 0d)
        {
            bottom = Math.Abs(bottom);
            sign = !sign;
        }

        if (bottom > PromotionLimit && !double.IsInfinity(bottom))
        {
            int exponentIndex = -1;
            for (int i = 1; i < count; i++)
            {
                if (compact[i].Operation == 1d)
                {
                    exponentIndex = i;
                    break;
                }
            }

            // 只有能够同时记录新增指数层时才压缩底值，避免容量满时改变数值含义。
            if (exponentIndex >= 0 || count < compact.Length)
            {
                bottom = Quantize(Math.Log10(bottom));
                if (exponentIndex >= 0)
                {
                    compact[exponentIndex] = new ExpantaNumOperator(
                        1d,
                        Quantize(compact[exponentIndex].Repetitions + 1d));
                }
                else
                {
                    int insert = 1;
                    while (insert < count && compact[insert].Operation < 1d)
                        insert++;
                    Array.Copy(compact, insert, compact, insert + 1, count - insert);
                    compact[insert] = new ExpantaNumOperator(1d, 1d);
                    count++;
                }
            }
        }
        compact[0] = new ExpantaNumOperator(0d, Quantize(bottom));

        if (count == 1 && layer == 0d)
        {
            representation = ScalarRepresentation;
            operatorCount = 0;
            scalar = compact[0].Repetitions;
            layer = 0d;
            operators = null;
            NormalizeInPlace();
            return;
        }

        if (count == 2 &&
            compact[0].Operation == 0d &&
            compact[1].Operation == 1d &&
            compact[1].Repetitions >= 0d &&
            compact[1].Repetitions == Math.Truncate(compact[1].Repetitions) &&
            layer == 0d)
        {
            representation = LayeredRepresentation;
            operatorCount = 0;
            scalar = compact[0].Repetitions;
            layer = compact[1].Repetitions;
            operators = null;
            NormalizeInPlace();
            return;
        }

        representation = HyperRepresentation;
        scalar = 0d;
        operators = compact;
        operatorCount = count;
    }

    private int FindOperationIndex(double operation)
    {
        if (representation == ZeroRepresentation)
            return -1;
        if (representation == ScalarRepresentation)
            return operation == 0d ? 0 : -1;
        if (representation == LayeredRepresentation)
        {
            if (operation == 0d)
                return 0;
            if (operation == 1d)
                return 1;
            return -1;
        }

        ExpantaNumOperator[] current = operators ?? EmptyOperators;
        int low = 0;
        int high = Math.Min(Math.Max(operatorCount, 0), current.Length) - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            double candidate = current[middle].Operation;
            if (candidate == operation)
                return middle;
            if (candidate < operation)
                low = middle + 1;
            else
                high = middle - 1;
        }
        return -1;
    }

    private string GetRepresentationName()
    {
        switch (representation)
        {
            case ZeroRepresentation:
                return "Zero";
            case ScalarRepresentation:
                return "Scalar";
            case LayeredRepresentation:
                return "Layered";
            case HyperRepresentation:
                return "Hyper";
            default:
                return "Invalid";
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetOperatorCount()
    {
        if (representation == ZeroRepresentation)
            return 0;
        if (representation == ScalarRepresentation)
            return 1;
        if (representation == LayeredRepresentation)
            return 2;
        return operators == null ? 0 : Math.Min(Math.Max(operatorCount, 0), operators.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetOperation(int index)
    {
        if (representation == ScalarRepresentation)
            return 0d;
        if (representation == LayeredRepresentation)
            return index == 0 ? 0d : 1d;
        return operators[index].Operation;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetRepetitions(int index)
    {
        if (representation == ScalarRepresentation)
            return scalar;
        if (representation == LayeredRepresentation)
            return index == 0 ? scalar : layer;
        return operators[index].Repetitions;
    }

    private double GetBottomValue()
    {
        if (representation == ScalarRepresentation || representation == LayeredRepresentation)
            return scalar;
        return Operator(0d);
    }

    private static void SortOperatorsInPlace(ExpantaNumOperator[] values, int count)
    {
        for (int i = 1; i < count; i++)
        {
            ExpantaNumOperator current = values[i];
            int j = i - 1;
            while (j >= 0 && values[j].Operation > current.Operation)
            {
                values[j + 1] = values[j];
                j--;
            }
            values[j + 1] = current;
        }
    }

    private static ExpantaNumOperator[] CloneOperators(
        ExpantaNumOperator[] source,
        int count,
        bool reserveOne = false)
    {
        if (source == null || count <= 0)
            return EmptyOperators;

        int capacity = reserveOne && count < MaxHyperOperators ? count + 1 : count;
        ExpantaNumOperator[] clone = new ExpantaNumOperator[capacity];
        Array.Copy(source, clone, count);
        return clone;
    }

    private void ResetToZero()
    {
        sign = false;
        representation = ZeroRepresentation;
        operatorCount = 0;
        scalar = 0d;
        layer = 0d;
        operators = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Quantize(double value)
    {
        if (value == 0d || double.IsNaN(value) || double.IsInfinity(value))
            return value;

        double magnitude = Math.Abs(value);
        if (magnitude < SmallestRoundedMagnitude || magnitude >= 9000000000d)
            return value;

        double scaled = value * 1000000d;
        return scaled >= 0d
            ? Math.Floor(scaled + 0.5d) / 1000000d
            : Math.Ceiling(scaled - 0.5d) / 1000000d;
    }

    private static double[] CreateFactorialTable()
    {
        double[] table = new double[171];
        table[0] = 1d;
        for (int i = 1; i < table.Length; i++)
            table[i] = table[i - 1] * i;
        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetSignedScalar(out double value)
    {
        value = 0d;
        if (representation == ZeroRepresentation)
            return true;
        if (representation != ScalarRepresentation || !IsFinite)
            return false;

        value = sign ? -scalar : scalar;
        return true;
    }

    private static double Log1PDouble(double value)
    {
        if (Math.Abs(value) > 1e-4d)
            return Math.Log(1d + value);

        double term = value;
        double sum = 0d;
        for (int n = 1; n <= 8; n++)
        {
            sum += (n & 1) == 1 ? term / n : -term / n;
            term *= value;
        }
        return sum;
    }

    private static double ExpM1Double(double value)
    {
        if (Math.Abs(value) > 1e-4d)
            return Math.Exp(value) - 1d;

        double term = value;
        double sum = value;
        for (int n = 2; n <= 8; n++)
        {
            term *= value / n;
            sum += term;
        }
        return sum;
    }

    private static ExpantaNum CreateSpecial(double special, bool negative)
    {
        return new ExpantaNum(negative ? -special : special);
    }

    public static implicit operator ExpantaNum(string value) => Parse(value);
    public static implicit operator ExpantaNum(double value) => new ExpantaNum(value);
    public static implicit operator ExpantaNum(float value) => new ExpantaNum(value);
    public static implicit operator ExpantaNum(int value) => new ExpantaNum(value);
    public static implicit operator ExpantaNum(long value) => new ExpantaNum(value);
}
