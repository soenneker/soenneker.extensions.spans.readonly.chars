using AwesomeAssertions;
using Soenneker.Tests.Unit;
using System;
using System.Collections.Generic;
using Xunit;

namespace Soenneker.Extensions.Spans.Readonly.Chars.Tests;

public sealed class ReadOnlySpanCharExtensionTests : UnitTest
{
    #region IsWhiteSpace Tests

    [Fact]
    public void IsWhiteSpace_EmptySpan_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "";
        span.IsWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void IsWhiteSpace_AllWhitespace_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "   \t\r\n  ";
        span.IsWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void IsWhiteSpace_ContainsNonWhitespace_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "  a  ";
        span.IsWhiteSpace().Should().BeFalse();
    }

    [Fact]
    public void IsWhiteSpace_NoWhitespace_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "hello";
        span.IsWhiteSpace().Should().BeFalse();
    }

    #endregion

    #region SplitTrimmedNonEmpty Tests

    [Fact]
    public void SplitTrimmedNonEmpty_EmptySpan_ReturnsEmptyArray()
    {
        ReadOnlySpan<char> span = "";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().BeEmpty();
    }

    [Fact]
    public void SplitTrimmedNonEmpty_SingleValue_ReturnsSingleElement()
    {
        ReadOnlySpan<char> span = "hello";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().HaveCount(1);
        result[0].Should().Be("hello");
    }

    [Fact]
    public void SplitTrimmedNonEmpty_MultipleValues_ReturnsAllElements()
    {
        ReadOnlySpan<char> span = "a,b,c";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
        result[2].Should().Be("c");
    }

    [Fact]
    public void SplitTrimmedNonEmpty_ValuesWithWhitespace_ReturnsTrimmedElements()
    {
        ReadOnlySpan<char> span = "  a  ,  b  ,  c  ";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
        result[2].Should().Be("c");
    }

    [Fact]
    public void SplitTrimmedNonEmpty_EmptySegments_SkipsEmptySegments()
    {
        ReadOnlySpan<char> span = "a,,b,  ,c";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
        result[2].Should().Be("c");
    }

    [Fact]
    public void SplitTrimmedNonEmpty_OnlyWhitespace_ReturnsEmptyArray()
    {
        ReadOnlySpan<char> span = "   ,   ,   ";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().BeEmpty();
    }

    [Fact]
    public void SplitTrimmedNonEmpty_ManySegments_HandlesArrayPoolResize()
    {
        ReadOnlySpan<char> span = "a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t";
        string[] result = span.SplitTrimmedNonEmpty(',');
        result.Should().HaveCount(20);
    }

    #endregion

    #region ToSha256Hex Tests

    [Fact]
    public void ToSha256Hex_EmptyString_ReturnsExpectedHash()
    {
        ReadOnlySpan<char> span = "";
        string result = span.ToSha256Hex();
        result.Should().HaveLength(64);
        result.Should().Be("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855");
    }

    [Fact]
    public void ToSha256Hex_SimpleString_ReturnsExpectedHash()
    {
        ReadOnlySpan<char> span = "hello";
        string result = span.ToSha256Hex();
        result.Should().HaveLength(64);
        result.Should().Be("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824");
    }

    [Fact]
    public void ToSha256Hex_UpperCase_ReturnsUppercaseHex()
    {
        ReadOnlySpan<char> span = "test";
        string result = span.ToSha256Hex(upperCase: true);
        result.Should().Be(result.ToUpperInvariant());
    }

    [Fact]
    public void ToSha256Hex_LowerCase_ReturnsLowercaseHex()
    {
        ReadOnlySpan<char> span = "test";
        string result = span.ToSha256Hex(upperCase: false);
        result.Should().Be(result.ToLowerInvariant());
    }

    [Fact]
    public void ToSha256Hex_LargeString_ReturnsValidHash()
    {
        string largeString = new('x', 2000);
        ReadOnlySpan<char> span = largeString;
        string result = span.ToSha256Hex();
        result.Should().HaveLength(64);
    }

    [Fact]
    public void ToSha256Hex_VeryLargeString_ReturnsValidHash()
    {
        string veryLargeString = new('x', 200000);
        ReadOnlySpan<char> span = veryLargeString;
        string result = span.ToSha256Hex();
        result.Should().HaveLength(64);
    }

    #endregion

    #region TrimToNull Tests

    [Fact]
    public void TrimToNull_EmptySpan_ReturnsNull()
    {
        ReadOnlySpan<char> span = "";
        span.TrimToNull().Should().BeNull();
    }

    [Fact]
    public void TrimToNull_OnlyWhitespace_ReturnsNull()
    {
        ReadOnlySpan<char> span = "   \t\r\n  ";
        span.TrimToNull().Should().BeNull();
    }

    [Fact]
    public void TrimToNull_ValueWithWhitespace_ReturnsTrimmedValue()
    {
        ReadOnlySpan<char> span = "  hello  ";
        span.TrimToNull().Should().Be("hello");
    }

    [Fact]
    public void TrimToNull_ValueWithoutWhitespace_ReturnsValue()
    {
        ReadOnlySpan<char> span = "hello";
        span.TrimToNull().Should().Be("hello");
    }

    #endregion

    #region JoinCommaSeparated Tests

    [Fact]
    public void JoinCommaSeparated_EmptyRanges_ReturnsEmptyString()
    {
        ReadOnlySpan<char> span = "hello,world";
        Span<Range> ranges = stackalloc Range[0];
        string result = span.JoinCommaSeparated(ranges, 0, 0);
        result.Should().BeEmpty();
    }

    [Fact]
    public void JoinCommaSeparated_SingleRange_ReturnsValue()
    {
        ReadOnlySpan<char> span = "hello world test";
        Span<Range> ranges = stackalloc Range[1];
        ranges[0] = 0..5;
        string result = span.JoinCommaSeparated(ranges, 0, 1);
        result.Should().Be("hello");
    }

    [Fact]
    public void JoinCommaSeparated_MultipleRanges_ReturnsCommaSeparatedValues()
    {
        ReadOnlySpan<char> span = "hello world test";
        Span<Range> ranges = stackalloc Range[3];
        ranges[0] = 0..5;
        ranges[1] = 6..11;
        ranges[2] = 12..16;
        string result = span.JoinCommaSeparated(ranges, 0, 3);
        result.Should().Be("hello, world, test");
    }

    [Fact]
    public void JoinCommaSeparated_RangesWithWhitespace_ReturnsTrimmedValues()
    {
        ReadOnlySpan<char> span = "  hello  ,  world  ";
        Span<Range> ranges = stackalloc Range[2];
        ranges[0] = 0..9;
        ranges[1] = 10..19;
        string result = span.JoinCommaSeparated(ranges, 0, 2);
        result.Should().Be("hello, world");
    }

    [Fact]
    public void JoinCommaSeparated_InvalidStartIndex_ReturnsEmptyString()
    {
        ReadOnlySpan<char> span = "hello";
        Span<Range> ranges = stackalloc Range[1];
        ranges[0] = 0..5;
        string result = span.JoinCommaSeparated(ranges, 10, 1);
        result.Should().BeEmpty();
    }

    #endregion

    #region SplitCommaRanges Tests

    [Fact]
    public void SplitCommaRanges_EmptyInput_ReturnsZero()
    {
        ReadOnlySpan<char> span = "";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitCommaRanges(ranges);
        count.Should().Be(0);
    }

    [Fact]
    public void SplitCommaRanges_SingleValue_ReturnsSingleRange()
    {
        ReadOnlySpan<char> span = "hello";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitCommaRanges(ranges);
        count.Should().Be(1);
        span[ranges[0]].ToString().Should().Be("hello");
    }

    [Fact]
    public void SplitCommaRanges_MultipleValues_ReturnsAllRanges()
    {
        ReadOnlySpan<char> span = "a,b,c";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitCommaRanges(ranges);
        count.Should().Be(3);
        span[ranges[0]].ToString().Should().Be("a");
        span[ranges[1]].ToString().Should().Be("b");
        span[ranges[2]].ToString().Should().Be("c");
    }

    [Fact]
    public void SplitCommaRanges_EmptySegments_SkipsEmpty()
    {
        ReadOnlySpan<char> span = "a,,b";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitCommaRanges(ranges);
        count.Should().Be(2);
        span[ranges[0]].ToString().Should().Be("a");
        span[ranges[1]].ToString().Should().Be("b");
    }

    [Fact]
    public void SplitCommaRanges_LimitedRanges_ReturnsUpToLimit()
    {
        ReadOnlySpan<char> span = "a,b,c,d,e";
        Span<Range> ranges = stackalloc Range[2];
        int count = span.SplitCommaRanges(ranges);
        count.Should().Be(2);
    }

    #endregion

    #region SplitNonEmptyLineRanges Tests

    [Fact]
    public void SplitNonEmptyLineRanges_EmptyInput_ReturnsZero()
    {
        ReadOnlySpan<char> span = "";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitNonEmptyLineRanges(ranges);
        count.Should().Be(0);
    }

    [Fact]
    public void SplitNonEmptyLineRanges_SingleLine_ReturnsSingleRange()
    {
        ReadOnlySpan<char> span = "hello";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitNonEmptyLineRanges(ranges);
        count.Should().Be(1);
        span[ranges[0]].ToString().Should().Be("hello");
    }

    [Fact]
    public void SplitNonEmptyLineRanges_MultipleLines_ReturnsAllRanges()
    {
        ReadOnlySpan<char> span = "a\nb\nc";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitNonEmptyLineRanges(ranges);
        count.Should().Be(3);
        span[ranges[0]].ToString().Should().Be("a");
        span[ranges[1]].ToString().Should().Be("b");
        span[ranges[2]].ToString().Should().Be("c");
    }

    [Fact]
    public void SplitNonEmptyLineRanges_WindowsLineEndings_HandlesCorrectly()
    {
        ReadOnlySpan<char> span = "a\r\nb\r\nc";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitNonEmptyLineRanges(ranges);
        count.Should().Be(3);
        span[ranges[0]].ToString().Should().Be("a");
        span[ranges[1]].ToString().Should().Be("b");
        span[ranges[2]].ToString().Should().Be("c");
    }

    [Fact]
    public void SplitNonEmptyLineRanges_EmptyLines_SkipsEmpty()
    {
        ReadOnlySpan<char> span = "a\n\nb\n  \nc";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitNonEmptyLineRanges(ranges);
        count.Should().Be(3);
    }

    [Fact]
    public void SplitNonEmptyLineRanges_LinesWithWhitespace_ReturnsTrimmedRanges()
    {
        ReadOnlySpan<char> span = "  a  \n  b  ";
        Span<Range> ranges = stackalloc Range[5];
        int count = span.SplitNonEmptyLineRanges(ranges);
        count.Should().Be(2);
        span[ranges[0]].ToString().Should().Be("a");
        span[ranges[1]].ToString().Should().Be("b");
    }

    #endregion

    #region IndexOfNewline Tests

    [Fact]
    public void IndexOfNewline_NoNewline_ReturnsNegativeOne()
    {
        ReadOnlySpan<char> span = "hello world";
        span.IndexOfNewline(0).Should().Be(-1);
    }

    [Fact]
    public void IndexOfNewline_LineFeed_ReturnsIndex()
    {
        ReadOnlySpan<char> span = "hello\nworld";
        span.IndexOfNewline(0).Should().Be(5);
    }

    [Fact]
    public void IndexOfNewline_CarriageReturn_ReturnsIndex()
    {
        ReadOnlySpan<char> span = "hello\rworld";
        span.IndexOfNewline(0).Should().Be(5);
    }

    [Fact]
    public void IndexOfNewline_StartAfterNewline_ReturnsNegativeOne()
    {
        ReadOnlySpan<char> span = "hello\nworld";
        span.IndexOfNewline(6).Should().Be(-1);
    }

    [Fact]
    public void IndexOfNewline_StartOutOfRange_ReturnsNegativeOne()
    {
        ReadOnlySpan<char> span = "hello";
        span.IndexOfNewline(10).Should().Be(-1);
    }

    #endregion

    #region TrimCrlf Tests

    [Fact]
    public void TrimCrlf_NoNewlines_ReturnsOriginal()
    {
        ReadOnlySpan<char> span = "hello";
        span.TrimCrlf().ToString().Should().Be("hello");
    }

    [Fact]
    public void TrimCrlf_LeadingNewlines_TrimsLeading()
    {
        ReadOnlySpan<char> span = "\r\nhello";
        span.TrimCrlf().ToString().Should().Be("hello");
    }

    [Fact]
    public void TrimCrlf_TrailingNewlines_TrimsTrailing()
    {
        ReadOnlySpan<char> span = "hello\r\n";
        span.TrimCrlf().ToString().Should().Be("hello");
    }

    [Fact]
    public void TrimCrlf_BothEnds_TrimsBoth()
    {
        ReadOnlySpan<char> span = "\r\nhello\r\n";
        span.TrimCrlf().ToString().Should().Be("hello");
    }

    [Fact]
    public void TrimCrlf_OnlyNewlines_ReturnsEmpty()
    {
        ReadOnlySpan<char> span = "\r\n\r\n";
        span.TrimCrlf().ToString().Should().BeEmpty();
    }

    [Fact]
    public void TrimCrlf_PreservesInternalNewlines()
    {
        ReadOnlySpan<char> span = "\nhello\nworld\n";
        span.TrimCrlf().ToString().Should().Be("hello\nworld");
    }

    #endregion

    #region CountChar Tests

    [Fact]
    public void CountChar_EmptySpan_ReturnsZero()
    {
        ReadOnlySpan<char> span = "";
        span.CountChar('a').Should().Be(0);
    }

    [Fact]
    public void CountChar_NoMatches_ReturnsZero()
    {
        ReadOnlySpan<char> span = "hello";
        span.CountChar('z').Should().Be(0);
    }

    [Fact]
    public void CountChar_SingleMatch_ReturnsOne()
    {
        ReadOnlySpan<char> span = "hello";
        span.CountChar('h').Should().Be(1);
    }

    [Fact]
    public void CountChar_MultipleMatches_ReturnsCount()
    {
        ReadOnlySpan<char> span = "hello";
        span.CountChar('l').Should().Be(2);
    }

    #endregion

    #region LeadingWhitespaceCount Tests

    [Fact]
    public void LeadingWhitespaceCount_EmptySpan_ReturnsZero()
    {
        ReadOnlySpan<char> span = "";
        span.LeadingWhitespaceCount().Should().Be(0);
    }

    [Fact]
    public void LeadingWhitespaceCount_NoLeadingWhitespace_ReturnsZero()
    {
        ReadOnlySpan<char> span = "hello";
        span.LeadingWhitespaceCount().Should().Be(0);
    }

    [Fact]
    public void LeadingWhitespaceCount_HasLeadingWhitespace_ReturnsCount()
    {
        ReadOnlySpan<char> span = "   hello";
        span.LeadingWhitespaceCount().Should().Be(3);
    }

    [Fact]
    public void LeadingWhitespaceCount_AllWhitespace_ReturnsLength()
    {
        ReadOnlySpan<char> span = "   ";
        span.LeadingWhitespaceCount().Should().Be(3);
    }

    [Fact]
    public void LeadingWhitespaceCount_MixedWhitespace_ReturnsCorrectCount()
    {
        ReadOnlySpan<char> span = " \t\r\nhello";
        span.LeadingWhitespaceCount().Should().Be(4);
    }

    #endregion

    #region TrailingWhitespaceCount Tests

    [Fact]
    public void TrailingWhitespaceCount_EmptySpan_ReturnsZero()
    {
        ReadOnlySpan<char> span = "";
        span.TrailingWhitespaceCount().Should().Be(0);
    }

    [Fact]
    public void TrailingWhitespaceCount_NoTrailingWhitespace_ReturnsZero()
    {
        ReadOnlySpan<char> span = "hello";
        span.TrailingWhitespaceCount().Should().Be(0);
    }

    [Fact]
    public void TrailingWhitespaceCount_HasTrailingWhitespace_ReturnsCount()
    {
        ReadOnlySpan<char> span = "hello   ";
        span.TrailingWhitespaceCount().Should().Be(3);
    }

    [Fact]
    public void TrailingWhitespaceCount_AllWhitespace_ReturnsLength()
    {
        ReadOnlySpan<char> span = "   ";
        span.TrailingWhitespaceCount().Should().Be(3);
    }

    #endregion

    #region TryParseHexUInt64 Tests

    [Fact]
    public void TryParseHexUInt64_ValidUppercase_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "0123456789ABCDEF";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeTrue();
        value.Should().Be(0x0123456789ABCDEFuL);
    }

    [Fact]
    public void TryParseHexUInt64_ValidLowercase_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "0123456789abcdef";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeTrue();
        value.Should().Be(0x0123456789ABCDEFuL);
    }

    [Fact]
    public void TryParseHexUInt64_TooShort_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "0123456789ABCDE";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeFalse();
        value.Should().Be(0uL);
    }

    [Fact]
    public void TryParseHexUInt64_TooLong_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "0123456789ABCDEF0";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseHexUInt64_InvalidCharacter_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "0123456789GHIJKL";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseHexUInt64_AllZeros_ReturnsZero()
    {
        ReadOnlySpan<char> span = "0000000000000000";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeTrue();
        value.Should().Be(0uL);
    }

    [Fact]
    public void TryParseHexUInt64_MaxValue_ReturnsMaxValue()
    {
        ReadOnlySpan<char> span = "FFFFFFFFFFFFFFFF";
        bool result = span.TryParseHexUInt64(out ulong value);
        result.Should().BeTrue();
        value.Should().Be(ulong.MaxValue);
    }

    #endregion

    #region SkipWhitespace Tests

    [Fact]
    public void SkipWhitespace_EmptySpan_IndexUnchanged()
    {
        ReadOnlySpan<char> span = "";
        int idx = 0;
        span.SkipWhitespace(ref idx);
        idx.Should().Be(0);
    }

    [Fact]
    public void SkipWhitespace_NoWhitespace_IndexUnchanged()
    {
        ReadOnlySpan<char> span = "hello";
        int idx = 0;
        span.SkipWhitespace(ref idx);
        idx.Should().Be(0);
    }

    [Fact]
    public void SkipWhitespace_HasWhitespace_SkipsToNonWhitespace()
    {
        ReadOnlySpan<char> span = "   hello";
        int idx = 0;
        span.SkipWhitespace(ref idx);
        idx.Should().Be(3);
    }

    [Fact]
    public void SkipWhitespace_AllWhitespace_SkipsToEnd()
    {
        ReadOnlySpan<char> span = "   ";
        int idx = 0;
        span.SkipWhitespace(ref idx);
        idx.Should().Be(3);
    }

    [Fact]
    public void SkipWhitespace_StartInMiddle_SkipsFromStartIndex()
    {
        ReadOnlySpan<char> span = "hello   world";
        int idx = 5;
        span.SkipWhitespace(ref idx);
        idx.Should().Be(8);
    }

    #endregion

    #region AddTokens Tests

    [Fact]
    public void AddTokens_EmptySpan_AddsNothing()
    {
        ReadOnlySpan<char> span = "";
        var set = new HashSet<string>();
        span.AddTokens(set);
        set.Should().BeEmpty();
    }

    [Fact]
    public void AddTokens_SingleToken_AddsOne()
    {
        ReadOnlySpan<char> span = "hello";
        var set = new HashSet<string>();
        span.AddTokens(set);
        set.Should().HaveCount(1);
        set.Should().Contain("hello");
    }

    [Fact]
    public void AddTokens_MultipleTokens_AddsAll()
    {
        ReadOnlySpan<char> span = "hello world test";
        var set = new HashSet<string>();
        span.AddTokens(set);
        set.Should().HaveCount(3);
        set.Should().Contain("hello");
        set.Should().Contain("world");
        set.Should().Contain("test");
    }

    [Fact]
    public void AddTokens_DuplicateTokens_AddsOnlyUnique()
    {
        ReadOnlySpan<char> span = "hello hello world";
        var set = new HashSet<string>();
        span.AddTokens(set);
        set.Should().HaveCount(2);
    }

    [Fact]
    public void AddTokens_LeadingTrailingWhitespace_IgnoresWhitespace()
    {
        ReadOnlySpan<char> span = "   hello   world   ";
        var set = new HashSet<string>();
        span.AddTokens(set);
        set.Should().HaveCount(2);
        set.Should().Contain("hello");
        set.Should().Contain("world");
    }

    [Fact]
    public void AddTokens_ExistingSet_MergesTokens()
    {
        ReadOnlySpan<char> span = "world";
        var set = new HashSet<string> { "hello" };
        span.AddTokens(set);
        set.Should().HaveCount(2);
        set.Should().Contain("hello");
        set.Should().Contain("world");
    }

    #endregion

    #region EqualsAsciiIgnoreCase Tests

    [Fact]
    public void EqualsAsciiIgnoreCase_EmptySpans_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "";
        ReadOnlySpan<char> b = "";
        a.EqualsAsciiIgnoreCase(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_SameCase_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "hello";
        a.EqualsAsciiIgnoreCase(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_DifferentCase_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "HELLO";
        a.EqualsAsciiIgnoreCase(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_MixedCase_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "HeLLo";
        ReadOnlySpan<char> b = "hElLO";
        a.EqualsAsciiIgnoreCase(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_DifferentContent_ReturnsFalse()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "world";
        a.EqualsAsciiIgnoreCase(b).Should().BeFalse();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_DifferentLength_ReturnsFalse()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "hello!";
        a.EqualsAsciiIgnoreCase(b).Should().BeFalse();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_NonAscii_ReturnsFalse()
    {
        ReadOnlySpan<char> a = "héllo";
        ReadOnlySpan<char> b = "hÉllo";
        a.EqualsAsciiIgnoreCase(b).Should().BeFalse();
    }

    #endregion

    #region EqualsAsciiIgnoreCase_AssumeAscii Tests

    [Fact]
    public void EqualsAsciiIgnoreCase_AssumeAscii_EmptySpans_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "";
        ReadOnlySpan<char> b = "";
        a.EqualsAsciiIgnoreCase_AssumeAscii(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_AssumeAscii_SameCase_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "hello";
        a.EqualsAsciiIgnoreCase_AssumeAscii(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_AssumeAscii_DifferentCase_ReturnsTrue()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "HELLO";
        a.EqualsAsciiIgnoreCase_AssumeAscii(b).Should().BeTrue();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_AssumeAscii_DifferentContent_ReturnsFalse()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "world";
        a.EqualsAsciiIgnoreCase_AssumeAscii(b).Should().BeFalse();
    }

    [Fact]
    public void EqualsAsciiIgnoreCase_AssumeAscii_DifferentLength_ReturnsFalse()
    {
        ReadOnlySpan<char> a = "hello";
        ReadOnlySpan<char> b = "hello!";
        a.EqualsAsciiIgnoreCase_AssumeAscii(b).Should().BeFalse();
    }

    #endregion

    #region IsAscii Tests

    [Fact]
    public void IsAscii_EmptySpan_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "";
        span.IsAscii().Should().BeTrue();
    }

    [Fact]
    public void IsAscii_AllAscii_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "Hello, World! 123";
        span.IsAscii().Should().BeTrue();
    }

    [Fact]
    public void IsAscii_ContainsNonAscii_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "Hëllo";
        span.IsAscii().Should().BeFalse();
    }

    [Fact]
    public void IsAscii_AllControlCharacters_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "\t\r\n";
        span.IsAscii().Should().BeTrue();
    }

    [Fact]
    public void IsAscii_HighAscii_ReturnsFalse()
    {
        ReadOnlySpan<char> span = "Hello\u0080";
        span.IsAscii().Should().BeFalse();
    }

    [Fact]
    public void IsAscii_Boundary_ReturnsTrue()
    {
        ReadOnlySpan<char> span = "\u007F";
        span.IsAscii().Should().BeTrue();
    }

    #endregion
}
