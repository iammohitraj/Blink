// Blink · mohit

namespace Blink.Core.Algorithms;

/// <summary>Linear-time substring search and small text helpers.</summary>
public static class StringAlgorithms
{
    /// <summary>
    /// Knuth-Morris-Pratt. O(n + m) with an O(m) prefix table, no backtracking over the text.
    /// </summary>
    public static List<int> KmpSearch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        List<int> matches = new();

        if (pattern.IsEmpty || text.Length < pattern.Length)
        {
            return matches;
        }

        int[] prefix = BuildPrefixTable(pattern);
        int i = 0;
        int j = 0;

        while (i < text.Length)
        {
            if (text[i] == pattern[j])
            {
                i++;
                j++;

                if (j == pattern.Length)
                {
                    matches.Add(i - j);
                    j = prefix[j - 1];
                }
            }
            else if (j > 0)
            {
                j = prefix[j - 1];
            }
            else
            {
                i++;
            }
        }

        return matches;
    }

    /// <summary>
    /// Rabin-Karp. O(n + m) expected, with a rolling hash and a direct comparison on a hit.
    /// </summary>
    public static List<int> RabinKarpSearch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        List<int> matches = new();

        if (pattern.IsEmpty || text.Length < pattern.Length)
        {
            return matches;
        }

        const long Base = 256;
        const long Modulus = 1_000_000_007;

        long patternHash = 0;
        long windowHash = 0;
        long highPower = 1;

        for (int i = 0; i < pattern.Length - 1; i++)
        {
            highPower = (highPower * Base) % Modulus;
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            patternHash = ((patternHash * Base) + pattern[i]) % Modulus;
            windowHash = ((windowHash * Base) + text[i]) % Modulus;
        }

        int lastStart = text.Length - pattern.Length;

        for (int start = 0; start <= lastStart; start++)
        {
            if (windowHash == patternHash && text.Slice(start, pattern.Length).SequenceEqual(pattern))
            {
                matches.Add(start);
            }

            if (start == lastStart)
            {
                break;
            }

            windowHash = (windowHash - (text[start] * highPower)) % Modulus;
            if (windowHash < 0)
            {
                windowHash += Modulus;
            }

            windowHash = ((windowHash * Base) + text[start + pattern.Length]) % Modulus;
        }

        return matches;
    }

    public static bool IsPalindrome(ReadOnlySpan<char> text)
    {
        int left = 0;
        int right = text.Length - 1;

        while (left < right)
        {
            if (text[left++] != text[right--])
            {
                return false;
            }
        }

        return true;
    }

    private static int[] BuildPrefixTable(ReadOnlySpan<char> pattern)
    {
        int[] prefix = new int[pattern.Length];
        int length = 0;

        for (int i = 1; i < pattern.Length; i++)
        {
            while (length > 0 && pattern[i] != pattern[length])
            {
                length = prefix[length - 1];
            }

            if (pattern[i] == pattern[length])
            {
                length++;
            }

            prefix[i] = length;
        }

        return prefix;
    }
}
