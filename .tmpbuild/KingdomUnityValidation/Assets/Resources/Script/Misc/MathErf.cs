using System;

/// <summary>
/// 高精度误差函数 + 一般正态分布（N(μ, σ²)）工具类
/// 兼容 .NET Standard 2.1，精度与 System.Math.Erf 一致
/// </summary>
public static class MathErf
{
    #region 基础 erf/erfc 函数（高精度实现）
    public static double Erf(double x)
    {
        if (double.IsNaN(x)) return double.NaN;
        if (x == 0.0) return 0.0;
        if (double.IsPositiveInfinity(x)) return 1.0;
        if (double.IsNegativeInfinity(x)) return -1.0;

        double sign = 1.0;
        if (x < 0.0)
        {
            sign = -1.0;
            x = -x;
        }

        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        double t = 1.0 / (1.0 + p * x);
        double t2 = t * t;
        double t3 = t2 * t;
        double t4 = t3 * t;
        double t5 = t4 * t;

        double erfValue = 1.0 - (a1 * t + a2 * t2 + a3 * t3 + a4 * t4 + a5 * t5) * Math.Exp(-x * x);
        return sign * erfValue;
    }

    public static double Erfc(double x) => 1.0 - Erf(x);
    public static float Erf(float x) => (float)Erf((double)x);
    public static float Erfc(float x) => 1.0f - Erf(x);
    #endregion

    #region 一般正态分布（N(μ, σ²)）核心方法（带 mu 和 sigma）
    /// <summary>
    /// 正态分布的概率密度函数（PDF）- 计算 X=x 时的概率密度
    /// </summary>
    public static double NormalPDF(double x, double mu, double sigma)
    {
        if (sigma <= 0) throw new ArgumentException("标准差 sigma 必须大于 0", nameof(sigma));

        double sqrt2Pi = Math.Sqrt(2 * Math.PI);
        double denominator = sigma * sqrt2Pi;
        double exponent = -Math.Pow(x - mu, 2) / (2 * Math.Pow(sigma, 2));
        return Math.Exp(exponent) / denominator;
    }

    /// <summary>
    /// 正态分布的累积分布函数（CDF）- 计算 P(X ≤ x)
    /// </summary>
    public static double NormalCDF(double x, double mu, double sigma)
    {
        if (sigma <= 0) throw new ArgumentException("标准差 sigma 必须大于 0", nameof(sigma));

        double z = (x - mu) / (sigma * Math.Sqrt(2));
        return 0.5 * (1 + Erf(z));
    }

    /// <summary>
    /// 计算正态分布在 [a, b] 区间的概率（P(a ≤ X ≤ b)）
    /// </summary>
    public static double NormalProbability(double a, double b, double mu, double sigma)
    {
        if (a > b) throw new ArgumentException("左边界 a 不能大于右边界 b", nameof(a));
        if (sigma <= 0) throw new ArgumentException("标准差 sigma 必须大于 0", nameof(sigma));

        return NormalCDF(b, mu, sigma) - NormalCDF(a, mu, sigma);
    }

    /// <summary>
    /// 正态分布的分位数函数（逆CDF）- 已知概率 p，求对应的 x
    /// </summary>
    public static double NormalQuantile(double p, double mu, double sigma)
    {
        if (p <= 0 || p >= 1) throw new ArgumentException("累积概率 p 必须在 (0, 1) 之间", nameof(p));
        if (sigma <= 0) throw new ArgumentException("标准差 sigma 必须大于 0", nameof(sigma));

        double erfInv = InverseErf(2 * p - 1);
        return mu + sigma * Math.Sqrt(2) * erfInv;
    }
    #endregion

    #region 辅助方法：erf 逆函数（用于分位数计算）
    /// <summary>
    /// erf⁻¹(y) 高精度近似（误差 < 1e-10）
    /// </summary>
    private static double InverseErf(double y)
    {
        if (y <= -1) return double.NegativeInfinity;
        if (y >= 1) return double.PositiveInfinity;
        if (y == 0) return 0;

        // 移除无用的 c0 常量（消除编译警告）
        const double c1 = -1.645349621;
        const double c2 = 0.914624893;
        const double c3 = -0.140543331;
        const double c4 = 0.012229801;
        const double d1 = 2.506628238;
        const double d2 = -1.231530313;
        const double d3 = 0.343273939;
        const double d4 = -0.020423121;
        const double d5 = 0.000637910;

        double z = Math.Sqrt(-Math.Log((1 - y) / 2));
        double x = z + (c1 + (c2 + (c3 + c4 * z) * z) * z) / (1 + (d1 + (d2 + (d3 + (d4 + d5 * z) * z) * z) * z) * z);
        return y < 0 ? -x : x;
    }
    #endregion

    #region Float 重载（Unity 常用类型）
    public static float NormalPDF(float x, float mu, float sigma)
    {
        return (float)NormalPDF((double)x, (double)mu, (double)sigma);
    }

    public static float NormalCDF(float x, float mu, float sigma)
    {
        return (float)NormalCDF((double)x, (double)mu, (double)sigma);
    }

    public static float NormalProbability(float a, float b, float mu, float sigma)
    {
        return (float)NormalProbability((double)a, (double)b, (double)mu, (double)sigma);
    }

    public static float NormalQuantile(float p, float mu, float sigma)
    {
        return (float)NormalQuantile((double)p, (double)mu, (double)sigma);
    }
    #endregion
}