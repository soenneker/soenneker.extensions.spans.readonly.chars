using System;
using System.Linq;
using AwesomeAssertions;

namespace Soenneker.Extensions.Spans.Readonly.Chars.Tests;

public class PerformanceRegressionTests
{
    [Test]
    public void Split_handles_pooled_range_growth_and_unicode_trimming()
    {
        string[] expected = Enumerable.Range(0, 300).Select(i => i.ToString()).ToArray();
        string text = ",," + string.Join(",\u3000,", expected.Select(s => "\u00a0" + s + "\u2000")) + ",,";
        text.AsSpan().SplitTrimmedNonEmpty(',').Should().Equal(expected);
    }

    [Test]
    public void Join_handles_pooled_ranges_and_from_end_ranges()
    {
        const string text = "  alpha  , beta ";
        Range[] ranges = Enumerable.Repeat<Range>(0..9, 200).ToArray();
        text.AsSpan().JoinCommaSeparated(ranges, 0, ranges.Length).Should().Be(string.Join(", ", Enumerable.Repeat("alpha", 200)));
        ranges = [0..9, ^5..^0];
        text.AsSpan().JoinCommaSeparated(ranges, 0, 2).Should().Be("alpha, beta");
    }

    [Test]
    public void Safe_ascii_comparison_preserves_non_ascii_exact_comparison()
    {
        string prefix = new string('a', 64);
        string upper = new string('A', 64);
        (prefix + "é").AsSpan().EqualsAsciiIgnoreCase((upper + "é").AsSpan()).Should().BeTrue();
        (prefix + "é").AsSpan().EqualsAsciiIgnoreCase((upper + "É").AsSpan()).Should().BeFalse();
    }
}
