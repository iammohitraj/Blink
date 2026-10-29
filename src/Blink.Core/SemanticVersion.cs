// Blink · mohit

using System.Globalization;

namespace Blink.Core;

/// <summary>
/// SemVer 2.0 version value. Build metadata is accepted but ignored for ordering,
/// which is exactly what the GitHub release check needs.
/// </summary>
public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? Prerelease = null)
    : IComparable<SemanticVersion>
{
    public static SemanticVersion Zero => new(0, 0, 0);

    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span[0] is 'v' or 'V')
        {
            span = span[1..];
        }

        int build = span.IndexOf('+');
        if (build >= 0)
        {
            span = span[..build];
        }

        string? prerelease = null;
        int dash = span.IndexOf('-');
        if (dash >= 0)
        {
            ReadOnlySpan<char> suffix = span[(dash + 1)..];
            if (suffix.IsEmpty)
            {
                return false;
            }

            prerelease = suffix.ToString();
            span = span[..dash];
        }

        if (span.IsEmpty)
        {
            return false;
        }

        Span<int> numbers = stackalloc int[4];
        int count = 0;

        while (true)
        {
            int dot = span.IndexOf('.');
            ReadOnlySpan<char> part = dot >= 0 ? span[..dot] : span;

            if (part.IsEmpty ||
                !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
                count == numbers.Length)
            {
                return false;
            }

            numbers[count++] = value;

            if (dot < 0)
            {
                break;
            }

            span = span[(dot + 1)..];
            if (span.IsEmpty)
            {
                return false;
            }
        }

        version = new SemanticVersion(
            numbers[0],
            count > 1 ? numbers[1] : 0,
            count > 2 ? numbers[2] : 0,
            prerelease);
        return true;
    }

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out SemanticVersion version)
            ? version
            : throw new FormatException($"'{text}' is not a valid semantic version.");

    public int CompareTo(SemanticVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        bool hasPrerelease = !string.IsNullOrEmpty(Prerelease);
        bool otherHasPrerelease = !string.IsNullOrEmpty(other.Prerelease);

        if (!hasPrerelease && !otherHasPrerelease)
        {
            return 0;
        }

        if (!hasPrerelease)
        {
            return 1;
        }

        if (!otherHasPrerelease)
        {
            return -1;
        }

        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    public override string ToString() =>
        Prerelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    private static int ComparePrerelease(string left, string right)
    {
        ReadOnlySpan<char> leftSpan = left.AsSpan();
        ReadOnlySpan<char> rightSpan = right.AsSpan();

        while (true)
        {
            int leftDot = leftSpan.IndexOf('.');
            int rightDot = rightSpan.IndexOf('.');
            ReadOnlySpan<char> leftPart = leftDot >= 0 ? leftSpan[..leftDot] : leftSpan;
            ReadOnlySpan<char> rightPart = rightDot >= 0 ? rightSpan[..rightDot] : rightSpan;

            bool leftIsNumber = int.TryParse(leftPart, NumberStyles.None, CultureInfo.InvariantCulture, out int leftNumber);
            bool rightIsNumber = int.TryParse(rightPart, NumberStyles.None, CultureInfo.InvariantCulture, out int rightNumber);

            int result;
            if (leftIsNumber && rightIsNumber)
            {
                result = leftNumber.CompareTo(rightNumber);
            }
            else if (leftIsNumber)
            {
                result = -1;
            }
            else if (rightIsNumber)
            {
                result = 1;
            }
            else
            {
                result = leftPart.SequenceCompareTo(rightPart);
            }

            if (result != 0)
            {
                return result;
            }

            bool leftDone = leftDot < 0;
            bool rightDone = rightDot < 0;
            if (leftDone || rightDone)
            {
                return leftDone == rightDone ? 0 : leftDone ? -1 : 1;
            }

            leftSpan = leftSpan[(leftDot + 1)..];
            rightSpan = rightSpan[(rightDot + 1)..];
        }
    }
}
