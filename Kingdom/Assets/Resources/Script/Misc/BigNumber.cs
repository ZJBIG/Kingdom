using System;
using UnityEngine;

[Serializable]
public struct BigNumber
{
    [SerializeField] public double Power;
    [SerializeField][Range(-1, 1)] public int Sign;
    private static readonly BigNumber Zero = 0;
    private static readonly BigNumber One = 1;
    private static readonly BigNumber NegativeOne = -1;
    public BigNumber(string val)
    {
        if (char.IsDigit(val[0]) || val[0] == '+' || val[0] == 'e')
            Sign = 1;
        else
            Sign = -1;
        if (val[0] == '+' || val[0] == '-')
            val = val.Substring(1);
        int pos = val.IndexOf('e');
        if (pos == -1)
        {
            double value = double.Parse(val);
            if (value == 0)
            {
                Power = double.NegativeInfinity;
                Sign = 0;
                return;
            }
            Power = Math.Log10(value);
            return;
        }
        string[] vals = val.Split('e');
        Power = Math.Log10(double.Parse(vals[0])) + double.Parse(vals[1]);
    }
    public BigNumber(double power = double.NegativeInfinity, int sign = 1)
    {
        Power = power;
        Sign = sign;
        Normalize();
    }
    public static bool operator ==(BigNumber a, BigNumber b)
    {
        if (a.Sign == 0 && b.Sign == 0) return true;
        return a.Sign == b.Sign && Math.Abs(a.Power - b.Power) < 1e-10;
    }
    public static bool operator >(BigNumber a, BigNumber b)
    {
        if (a.Sign != b.Sign)
            return a.Sign > b.Sign;
        return a.Sign * a.Power > b.Sign * b.Power;
    }
    public static bool operator !=(BigNumber a, BigNumber b) => !(a == b);
    public static bool operator >=(BigNumber a, BigNumber b) => a > b || a == b;
    public static bool operator <(BigNumber a, BigNumber b) => b > a;
    public static bool operator <=(BigNumber a, BigNumber b) => !(a > b);
    public static BigNumber operator +(BigNumber a, BigNumber b)
    {
        if (a.Sign == 0) return b;
        if (b.Sign == 0) return a;

        if (Math.Abs(a.Power - b.Power) >= 15)
            return AbsMax(a, b);

        double powerDiff = a.Power - b.Power;
        double temp = Math.Pow(10, powerDiff) * a.Sign + b.Sign;
        int resultSign = Math.Sign(temp);
        double resultPower = b.Power + Math.Log10(Math.Abs(temp));

        BigNumber result = new BigNumber(resultPower, resultSign);
        result.Normalize();
        return result;
    }
    public static BigNumber operator -(BigNumber a, BigNumber b)
    {
        BigNumber negativeB = new BigNumber(b.Power, -b.Sign);
        return a + negativeB;
    }
    public static BigNumber operator *(BigNumber a, BigNumber b)
    {
        BigNumber result = new BigNumber(a.Power + b.Power, a.Sign * b.Sign);
        result.Normalize();
        return result;
    }
    public static BigNumber operator /(BigNumber a, BigNumber b)
    {
        if (b.Sign == 0) throw new DivideByZeroException("Division by zero");
        BigNumber result = new BigNumber(a.Power - b.Power, a.Sign * b.Sign);
        result.Normalize();
        return result;
    }
    public static BigNumber Pow(BigNumber num, double p) => new BigNumber(num.Power * p, num.Sign > 0 ? 1 : (p % 2 == 0 ? 1 : -1));
    public static BigNumber Sqrt(BigNumber num)
    {
        if (num.Sign < 0) throw new ArgumentException("Square root of negative number");
        return Pow(num, 0.5);
    }
    public static BigNumber Cbrt(BigNumber num) => Pow(num, 1.0 / 3);
    public static BigNumber Log10(BigNumber num)
    {
        if (num.Sign <= 0) throw new ArgumentException("Logarithm of non-positive number");
        return new BigNumber(num.Power, 1);
    }
    public static BigNumber Log(BigNumber num)
    {
        if (num.Sign <= 0) throw new ArgumentException("Logarithm of non-positive number");
        return new BigNumber(num.Power * Math.Log(10), 1);
    }
    public static BigNumber Exp(BigNumber num) => new BigNumber(num.Power * Math.Log10(Math.E), 1);
    public static BigNumber Abs(BigNumber num) => new BigNumber(num.Power, Math.Abs(num.Sign));
    public static BigNumber Max(BigNumber a, BigNumber b) => a > b ? a : b;
    public static BigNumber Min(BigNumber a, BigNumber b) => a < b ? a : b;
    public static BigNumber AbsMax(BigNumber a, BigNumber b) => Abs(a) > Abs(b) ? a : b;
    public static BigNumber AbsMin(BigNumber a, BigNumber b) => Abs(a) < Abs(b) ? a : b;
    public void Normalize()
    {
        if (Power != 0 && Sign == 0)
            Sign = 1;
        if (Sign == 0)
            Power = double.NegativeInfinity;
        else if (double.IsInfinity(Power) || double.IsNaN(Power))
        {
            Power = double.NegativeInfinity;
            Sign = 0;
        }
    }
    public override bool Equals(object obj)
    {
        if (obj is BigNumber other)
            return this == other;
        return false;
    }
    public override int GetHashCode() => (Power.GetHashCode() * 397) ^ Sign.GetHashCode();

    public static implicit operator BigNumber(string val) => new BigNumber(val);
    public static implicit operator BigNumber(double val) => new BigNumber(val.ToString());
    public static implicit operator BigNumber(float val) => new BigNumber(val.ToString());
    public static implicit operator BigNumber(int val) => new BigNumber(val.ToString());
    public static implicit operator BigNumber(long val) => new BigNumber(val.ToString());
}
