// Blink · mohit

namespace Blink.Core.Algorithms;

/// <summary>Number theory helpers used in the tests and benchmarks.</summary>
public static class MathAlgorithms
{
    public static long Gcd(long first, long second)
    {
        first = Math.Abs(first);
        second = Math.Abs(second);

        while (second != 0)
        {
            (first, second) = (second, first % second);
        }

        return first;
    }

    public static long Lcm(long first, long second)
    {
        if (first == 0 || second == 0)
        {
            return 0;
        }

        return Math.Abs((first / Gcd(first, second)) * second);
    }

    public static long ModPow(long value, long exponent, long modulus)
    {
        if (modulus <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modulus), "The modulus must be positive.");
        }

        if (exponent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "The exponent must be non-negative.");
        }

        long result = 1;
        value = ((value % modulus) + modulus) % modulus;

        while (exponent > 0)
        {
            if ((exponent & 1) == 1)
            {
                result = (result * value) % modulus;
            }

            value = (value * value) % modulus;
            exponent >>= 1;
        }

        return result;
    }

    public static bool IsPrime(int value)
    {
        if (value < 2)
        {
            return false;
        }

        if (value % 2 == 0)
        {
            return value == 2;
        }

        for (int divisor = 3; (long)divisor * divisor <= value; divisor += 2)
        {
            if (value % divisor == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sieve of Eratosthenes. Returns a flag per number from 0 to <paramref name="limit"/> inclusive.</summary>
    public static bool[] Sieve(int limit)
    {
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        bool[] isPrime = new bool[limit + 1];
        Array.Fill(isPrime, true);
        isPrime[0] = false;

        if (limit >= 1)
        {
            isPrime[1] = false;
        }

        for (int value = 2; (long)value * value <= limit; value++)
        {
            if (!isPrime[value])
            {
                continue;
            }

            for (int multiple = value * value; multiple <= limit; multiple += value)
            {
                isPrime[multiple] = false;
            }
        }

        return isPrime;
    }

    public static long Fibonacci(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        long previous = 0;
        long current = 1;

        for (int i = 0; i < index; i++)
        {
            (previous, current) = (current, previous + current);
        }

        return previous;
    }
}
