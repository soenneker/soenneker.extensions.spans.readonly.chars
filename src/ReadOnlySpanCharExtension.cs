using Soenneker.Extensions.Char;
using Soenneker.Extensions.Spans.Readonly.Bytes;
using Soenneker.Utils.PooledStringBuilders;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Soenneker.Extensions.Spans.Readonly.Chars;

public static class ReadOnlySpanCharExtension
{
    /// <summary>
    /// Determines whether all characters in the specified read-only character span are white-space characters.
    /// </summary>
    /// <remarks>White-space characters are determined using a fast check that is equivalent to calling
    /// char.IsWhiteSpace for each character. The method returns true for empty spans.</remarks>
    /// <param name="span">The read-only span of characters to evaluate.</param>
    /// <returns>true if every character in the span is a white-space character; otherwise, false.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(this ReadOnlySpan<char> span)
    {
        int len = span.Length;
        if (len == 0)
            return true;

        ref char r0 = ref MemoryMarshal.GetReference(span);

        for (int i = 0; i < len; i++)
        {
            if (!Unsafe.Add(ref r0, i).IsWhiteSpaceFast())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Splits the specified character span into substrings based on the given separator, trims whitespace from each
    /// substring, and returns only the non-empty results.
    /// </summary>
    /// <remarks>Empty or whitespace-only substrings are omitted from the result. Leading and trailing
    /// whitespace is removed from each substring before inclusion. The order of substrings in the returned array
    /// matches their order in the original span.</remarks>
    /// <param name="span">The read-only character span to split and trim.</param>
    /// <param name="separator">The character used to separate substrings within the span.</param>
    /// <returns>An array of strings containing the trimmed, non-empty substrings. Returns an empty array if no non-empty
    /// substrings are found.</returns>
    [Pure]
    public static string[] SplitTrimmedNonEmpty(this ReadOnlySpan<char> span, char separator)
    {
        int len = span.Length;
        if (len == 0)
            return [];

        // stack starts with room for 16 segments (32 ints: start/len pairs)
        const int initialSegs = 16;
        const int stackInts = initialSegs * 2;

        int[]? rented = null;
        Span<int> pairs = stackalloc int[stackInts];
        var segCount = 0;

        ref char r0 = ref MemoryMarshal.GetReference(span);

        var start = 0;

        for (var i = 0; i <= len; i++)
        {
            if (i == len || Unsafe.Add(ref r0, i) == separator)
            {
                int segLen = i - start;
                if (segLen > 0)
                {
                    TrimBoundsFast(span, start, i, out int tStart, out int tLen);
                    if (tLen != 0)
                    {
                        // ensure capacity for 2 ints
                        if ((segCount * 2) == pairs.Length)
                        {
                            int newSize = pairs.Length * 2;
                            int[] newArr = ArrayPool<int>.Shared.Rent(newSize);
                            pairs.CopyTo(newArr);
                            if (rented is not null)
                                ArrayPool<int>.Shared.Return(rented, clearArray: false);
                            rented = newArr;
                            pairs = newArr;
                        }

                        int p = segCount * 2;
                        pairs[p] = tStart;
                        pairs[p + 1] = tLen;
                        segCount++;
                    }
                }

                start = i + 1;
            }
        }

        if (segCount == 0)
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented, clearArray: false);
            return [];
        }

        var result = new string[segCount];
        for (var i = 0; i < segCount; i++)
        {
            int p = i * 2;
            result[i] = span.Slice(pairs[p], pairs[p + 1])
                            .ToString();
        }

        if (rented is not null)
            ArrayPool<int>.Shared.Return(rented, clearArray: false);
        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TrimBoundsFast(ReadOnlySpan<char> s, int start, int end, out int trimmedStart, out int trimmedLen)
        {
            int i = start;
            while (i < end && s[i]
                       .IsWhiteSpaceFast())
                i++;

            int j = end - 1;
            while (j >= i && s[j]
                       .IsWhiteSpaceFast())
                j--;

            trimmedStart = i;
            int l = j - i + 1;
            trimmedLen = l > 0 ? l : 0;
        }
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified text and returns its hexadecimal string representation.
    /// </summary>
    /// <remarks>This method is optimized for performance and supports large input efficiently. The output
    /// string will contain 64 hexadecimal characters. The method does not include any prefix (such as '0x') in the
    /// result.</remarks>
    /// <param name="text">The input text to hash.</param>
    /// <param name="encoding">The character encoding to use when converting the text to bytes. If <see langword="null"/>, UTF-8 is used.</param>
    /// <param name="upperCase">Specifies whether the resulting hexadecimal string should use uppercase letters. Set to <see langword="true"/>
    /// for uppercase; otherwise, lowercase.</param>
    /// <returns>A hexadecimal string representing the SHA-256 hash of the input text.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static string ToSha256Hex(this ReadOnlySpan<char> text, Encoding? encoding = null, bool upperCase = true)
    {
        encoding ??= Encoding.UTF8;

        int byteCount = encoding.GetByteCount(text);

        // Stack fast-path
        if (byteCount <= 1024)
        {
            Span<byte> tmp = byteCount == 0 ? [] : stackalloc byte[byteCount];
            encoding.GetBytes(text, tmp);
            return tmp.ToSha256Hex(upperCase);
        }

        // Pool fallback
        if (byteCount <= 128 * 1024)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                int written = encoding.GetBytes(text, rented.AsSpan(0, byteCount));
                return new ReadOnlySpan<byte>(rented, 0, written).ToSha256Hex(upperCase);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: false);
            }
        }

        return ToSha256HexStreaming(text, encoding, upperCase);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified text using streaming encoding and returns the result as a hexadecimal
    /// string.
    /// </summary>
    /// <remarks>This method processes the input text in chunks, minimizing memory usage for large inputs. The
    /// hash is computed incrementally using the specified encoding, which may affect the output if the encoding is not
    /// deterministic. The caller is responsible for ensuring that the encoding is appropriate for the input
    /// text.</remarks>
    /// <param name="text">The input text to hash.</param>
    /// <param name="encoding">The character encoding to use when converting the text to bytes for hashing. Must not be null.</param>
    /// <param name="upperCase">Specifies whether the returned hexadecimal string should use uppercase letters. If <see langword="true"/>, the
    /// result is uppercase; otherwise, lowercase.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash of the input text.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the encoder fails to make progress when converting the input text to bytes.</exception>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static string ToSha256HexStreaming(this ReadOnlySpan<char> text, Encoding encoding, bool upperCase)
    {
        using var ih = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Encoder encoder = encoding.GetEncoder();

        const int charChunk = 4096;

        // Correct sizing: max bytes for *charChunk* chars (includes encoder overhead properly)
        int maxBytes = encoding.GetMaxByteCount(charChunk);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxBytes);

        try
        {
            for (var i = 0; i < text.Length; i += charChunk)
            {
                ReadOnlySpan<char> slice = text.Slice(i, Math.Min(charChunk, text.Length - i));
                bool flush = i + slice.Length >= text.Length;

                encoder.Convert(slice, buffer, flush, out int charsUsed, out int bytesUsed, out bool completed);

                // With GetMaxByteCount(charChunk), we should always complete in one shot.
                // If it ever doesn't (exotic encoding edge), fall back to a small loop.
                if (!completed)
                {
                    var offset = 0;
                    while (true)
                    {
                        ih.AppendData(buffer, 0, bytesUsed);

                        slice = slice.Slice(charsUsed);
                        if (slice.IsEmpty)
                            break;

                        encoder.Convert(slice, buffer, flush, out charsUsed, out bytesUsed, out completed);
                        if (bytesUsed == 0 && charsUsed == 0)
                            throw new InvalidOperationException("Encoder made no progress.");
                    }
                }
                else
                {
                    ih.AppendData(buffer, 0, bytesUsed);
                }
            }

            Span<byte> hash = stackalloc byte[32];
            ih.TryGetHashAndReset(hash, out _);

            return upperCase ? Convert.ToHexString(hash) : Convert.ToHexStringLower(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryTrimNonEmpty(ReadOnlySpan<char> segment, out ReadOnlySpan<char> trimmed)
    {
        var start = 0;
        int end = segment.Length - 1;

        while ((uint)start < (uint)segment.Length && segment[start]
                   .IsWhiteSpaceFast())
            start++;

        while (end >= start && segment[end]
                   .IsWhiteSpaceFast())
            end--;

        if (end < start)
        {
            trimmed = default;
            return false;
        }

        trimmed = segment.Slice(start, end - start + 1);
        return true;
    }

    /// <summary>
    /// Creates a comma-separated string by joining the trimmed substrings of the specified ranges within the input
    /// span.
    /// </summary>
    /// <remarks>Empty or whitespace-only substrings are ignored. The resulting string contains only
    /// non-empty, trimmed segments, separated by ", ".</remarks>
    /// <param name="address">The span of characters containing the source text from which substrings are extracted.</param>
    /// <param name="ranges">The span of ranges specifying the segments within <paramref name="address"/> to join. Each range defines a
    /// substring to include.</param>
    /// <param name="startIndex">The zero-based index in <paramref name="ranges"/> at which to begin joining substrings.</param>
    /// <param name="count">The number of ranges to process, starting from <paramref name="startIndex"/>.</param>
    /// <returns>A string consisting of the trimmed substrings, separated by commas and spaces. Returns an empty string if no
    /// non-empty substrings are found.</returns>
    [Pure]
    public static string JoinCommaSeparated(this ReadOnlySpan<char> address, Span<Range> ranges, int startIndex, int count)
    {
        if ((uint)startIndex > (uint)ranges.Length || count <= 0)
            return string.Empty;

        int max = ranges.Length - startIndex;
        if (count > max)
            count = max;

        const int stackThreshold = 128;

        int[]? rented = null;
        Span<int> pairs = count <= stackThreshold
            ? stackalloc int[count * 2]
            : (rented = ArrayPool<int>.Shared.Rent(count * 2));

        var segCount = 0;
        var totalChars = 0;

        try
        {
            for (var i = 0; i < count; i++)
            {
                Range r = ranges[startIndex + i];
                GetStartEnd(r, address.Length, out int segStart, out int segEnd);

                TrimBounds(address, segStart, segEnd, out int tStart, out int tLen);
                if (tLen == 0)
                    continue;

                int p = segCount * 2;
                pairs[p] = tStart;
                pairs[p + 1] = tLen;
                totalChars += tLen;
                segCount++;
            }

            if (segCount == 0)
                return string.Empty;

            int finalLen = totalChars + (segCount - 1) * 2;

            using var sb = new PooledStringBuilder(finalLen);

            for (var i = 0; i < segCount; i++)
            {
                if (i != 0)
                    sb.Append(", ");

                int p = i * 2;
                sb.Append(address.Slice(pairs[p], pairs[p + 1]));
            }

            return sb.ToString();
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented, clearArray: false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetStartEnd(in Range range, int length, out int start, out int end)
    {
        start = range.Start.IsFromEnd ? length - range.Start.Value : range.Start.Value;
        end = range.End.IsFromEnd ? length - range.End.Value : range.End.Value;

        // Clamp to keep it robust if callers pass odd ranges.
        if ((uint)start > (uint)length)
            start = length;
        if ((uint)end > (uint)length)
            end = length;
        if (end < start)
            end = start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TrimBounds(ReadOnlySpan<char> s, int start, int end, out int trimmedStart, out int trimmedLen)
    {
        int i = start;
        while (i < end && s[i]
                   .IsWhiteSpaceFast())
            i++;

        int j = end - 1;
        while (j >= i && s[j]
                   .IsWhiteSpaceFast())
            j--;

        trimmedStart = i;
        int len = j - i + 1;
        trimmedLen = len > 0 ? len : 0;
    }

    /// <summary>
    /// Trims leading and trailing white-space characters from the specified span and returns the resulting string, or
    /// null if the trimmed span is empty.
    /// </summary>
    /// <param name="span">The read-only character span to trim.</param>
    /// <returns>A string containing the trimmed characters from the span, or null if the trimmed span is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? TrimToNull(this ReadOnlySpan<char> span)
    {
        span = span.Trim();
        return span.Length == 0 ? null : span.ToString();
    }

    /// <summary>
    /// Splits a read-only character span into ranges separated by commas and writes the resulting ranges to the
    /// specified destination span.
    /// </summary>
    /// <remarks>Empty segments (consecutive commas or leading/trailing commas) are ignored. If the number of
    /// segments exceeds the length of the destination span, only the first ranges.Length segments are
    /// returned.</remarks>
    /// <param name="input">The input span of characters to split into comma-separated ranges.</param>
    /// <param name="ranges">The destination span that receives the resulting ranges. Each range represents the start and end indices of a
    /// segment between commas in the input.</param>
    /// <returns>The number of ranges written to the destination span. This value will not exceed the length of the destination
    /// span.</returns>
    public static int SplitCommaRanges(this ReadOnlySpan<char> input, Span<Range> ranges)
    {
        var count = 0;
        var start = 0;
        var i = 0;
        int len = input.Length;

        while (i < len && count < ranges.Length)
        {
            // scan until comma or end
            while (i < len && input[i] != ',')
                i++;

            if (i > start) // non-empty segment
                ranges[count++] = start..i;

            i++; // skip comma (or move past end by 1; fine)
            start = i;
        }

        return count;
    }

    /// <summary>
    /// Splits the input span into ranges representing non-empty, trimmed lines and writes them to the specified
    /// destination span.
    /// </summary>
    /// <remarks>Lines consisting only of whitespace or newline characters are ignored. If the number of
    /// non-empty lines exceeds the length of the destination span, only as many ranges as will fit are
    /// written.</remarks>
    /// <param name="input">The input character span to process. Each line is identified by standard newline sequences and leading or
    /// trailing whitespace is ignored when determining if a line is non-empty.</param>
    /// <param name="ranges">The destination span that receives the ranges of non-empty, trimmed lines within the input. Each range specifies
    /// the start and end indices of a non-empty line, excluding leading and trailing whitespace.</param>
    /// <returns>The number of non-empty, trimmed line ranges written to the destination span. This value will not exceed the
    /// length of the destination span.</returns>
    [Pure]
    public static int SplitNonEmptyLineRanges(this ReadOnlySpan<char> input, Span<Range> ranges)
    {
        var count = 0;
        var pos = 0;
        int len = input.Length;

        while (pos < len && count < ranges.Length)
        {
            // Find end of line (first \r or \n)
            int lineEnd = pos;
            while (lineEnd < len)
            {
                char c = input[lineEnd];
                if (c is '\r' or '\n')
                    break;
                lineEnd++;
            }

            // Trim whitespace within [pos, lineEnd)
            int start = pos;
            while (start < lineEnd && input[start]
                       .IsWhiteSpaceFast())
                start++;

            int end = lineEnd;
            while (end > start && input[end - 1]
                       .IsWhiteSpaceFast())
                end--;

            if (start < end)
                ranges[count++] = start..end;

            // Advance past newline sequence: \r\n or single \r or \n
            if (lineEnd < len)
            {
                char nl = input[lineEnd++];
                if (nl == '\r' && lineEnd < len && input[lineEnd] == '\n')
                    lineEnd++;
            }

            pos = lineEnd;
        }

        return count;
    }

    /// <summary>
    /// Searches for the first occurrence of a newline character ('\r' or '\n') in the specified span, starting at the
    /// given index.
    /// </summary>
    /// <param name="input">The span of characters to search for a newline character.</param>
    /// <param name="start">The zero-based index at which to begin searching within the span. Must be greater than or equal to 0 and less
    /// than or equal to the length of the span.</param>
    /// <returns>The zero-based index of the first occurrence of a newline character in the span, or -1 if no newline character
    /// is found.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfNewline(this ReadOnlySpan<char> input, int start)
    {
        if ((uint)start >= (uint)input.Length)
            return -1;

        int idx = input.Slice(start).IndexOfAny('\r', '\n');
        return idx < 0 ? -1 : start + idx;
    }

    /// <summary>
    /// Removes any leading and trailing carriage return ('\r') and line feed ('\n') characters from the specified
    /// read-only character span.
    /// </summary>
    /// <remarks>This method does not modify the original data; it returns a new span referencing the trimmed
    /// range within the original span. Only '\r' and '\n' characters at the start or end are removed; other whitespace
    /// characters are not affected.</remarks>
    /// <param name="span">The read-only character span to trim.</param>
    /// <returns>A span that contains the input characters with all leading and trailing '\r' and '\n' characters removed. If no
    /// such characters are present, the original span is returned.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> TrimCrlf(this ReadOnlySpan<char> span)
    {
        var start = 0;
        int end = span.Length;

        while (start < end)
        {
            char c = span[start];
            if (c != '\r' && c != '\n') break;
            start++;
        }

        while (end > start)
        {
            char c = span[end - 1];
            if (c != '\r' && c != '\n') break;
            end--;
        }

        return span.Slice(start, end - start);
    }

    /// <summary>
    /// Counts the number of occurrences of a specified character within a read-only span of characters.
    /// </summary>
    /// <remarks>This method is optimized for performance and does not allocate additional memory.</remarks>
    /// <param name="span">The read-only span of characters to search.</param>
    /// <param name="c">The character to count within the span.</param>
    /// <returns>The total number of times the specified character appears in the span.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountChar(this ReadOnlySpan<char> span, char c)
        => span.Count(c); // System.MemoryExtensions.Count

    /// <summary>
    /// Counts the number of consecutive whitespace characters at the start of the specified span.
    /// </summary>
    /// <param name="span">The span of characters to examine for leading whitespace.</param>
    /// <returns>The number of consecutive whitespace characters at the beginning of the span. Returns 0 if the span is empty or
    /// does not start with whitespace.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingWhitespaceCount(this ReadOnlySpan<char> span)
    {
        var i = 0;
        while (i < span.Length && span[i]
                   .IsWhiteSpaceFast())
            i++;
        return i;
    }

    /// <summary>
    /// Counts the number of consecutive whitespace characters at the end of the specified span.
    /// </summary>
    /// <param name="span">The span of characters to examine for trailing whitespace.</param>
    /// <returns>The number of consecutive whitespace characters at the end of the span. Returns 0 if there are no trailing
    /// whitespace characters.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingWhitespaceCount(this ReadOnlySpan<char> span)
    {
        int i = span.Length;
        while (i > 0 && span[i - 1]
                   .IsWhiteSpaceFast())
            i--;
        return span.Length - i;
    }

    /// <summary>
    /// Attempts to parse a 16-character hexadecimal string into a 64-bit unsigned integer.
    /// Accepts upper- and lowercase hexadecimal characters.
    /// </summary>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64(this ReadOnlySpan<char> hex, out ulong value)
    {
        if ((uint)hex.Length != 16u)
        {
            value = 0;
            return false;
        }

        ref char r0 = ref MemoryMarshal.GetReference(hex);

        ulong acc = 0;

        for (var i = 0; i < 16; i += 2)
        {
            if (!TryHexNibble(Unsafe.Add(ref r0, i), out uint hi) || !TryHexNibble(Unsafe.Add(ref r0, i + 1), out uint lo))
            {
                value = 0;
                return false;
            }

            acc = (acc << 8) | ((hi << 4) | lo);
        }

        value = acc;
        return true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryHexNibble(uint c, out uint digit)
        {
            digit = c - '0';
            if (digit <= 9u)
                return true;

            c |= 0x20u;
            digit = c - 'a';
            if (digit <= 5u)
            {
                digit += 10u;
                return true;
            }

            digit = 0;
            return false;
        }
    }

    /// <summary>
    /// Advances the specified index to the first non-whitespace character in the provided read-only character span.
    /// </summary>
    /// <remarks>The method updates the index in place. Callers should ensure that the index is within the
    /// bounds of the span before calling this method to avoid out-of-range access.</remarks>
    /// <param name="span">A read-only span of characters to examine for leading whitespace.</param>
    /// <param name="idx">A reference to the index within the span. This value is incremented to skip over any consecutive whitespace
    /// characters and will point to the first non-whitespace character, or the end of the span if only whitespace
    /// remains.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SkipWhitespace(this ReadOnlySpan<char> span, ref int idx)
    {
        while ((uint)idx < (uint)span.Length && span[idx]
                   .IsWhiteSpaceFast())
            idx++;
    }

    /// <summary>
    /// Extracts unique tokens from the specified read-only span of characters and adds them to the provided hash set.
    /// </summary>
    /// <remarks>Tokens are defined as contiguous sequences of non-whitespace characters. This method does not
    /// modify the input span or remove existing entries from the hash set; it only adds new tokens that are not already
    /// present.</remarks>
    /// <param name="value">A read-only span of characters from which tokens are extracted. Leading and trailing whitespace is ignored.</param>
    /// <param name="set">The hash set to which unique tokens will be added. If a token already exists in the set, it is not added again.</param>
    public static void AddTokens(this ReadOnlySpan<char> value, HashSet<string> set)
    {
        var k = 0;

        while (k < value.Length)
        {
            while ((uint)k < (uint)value.Length && value[k]
                       .IsWhiteSpaceFast())
                k++;

            if (k >= value.Length)
                break;

            int start = k;

            while ((uint)k < (uint)value.Length && !value[k]
                       .IsWhiteSpaceFast())
                k++;

            set.Add(value.Slice(start, k - start)
                         .ToString());
        }
    }

    /// <summary>
    /// Determines whether two ASCII character spans are equal, using a case-insensitive comparison.
    /// </summary>
    /// <remarks>This method performs a case-insensitive comparison using ASCII casing rules only. Non-ASCII
    /// characters are compared using their exact values without case folding. The spans must be of equal length for the
    /// comparison to succeed.</remarks>
    /// <param name="a">The first read-only span of characters to compare.</param>
    /// <param name="b">The second read-only span of characters to compare.</param>
    /// <returns>true if the spans are equal when compared case-insensitively using ASCII rules; otherwise, false.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAsciiIgnoreCase(this ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length != b.Length)
            return false;

        ref char ra = ref MemoryMarshal.GetReference(a);
        ref char rb = ref MemoryMarshal.GetReference(b);

        int len = a.Length;

        for (var i = 0; i < len; i++)
        {
            uint ac = Unsafe.Add(ref ra, i);
            uint bc = Unsafe.Add(ref rb, i);

            if (ac == bc)
                continue;

            if ((ac | bc) > 0x7Fu)
                return false;

            if (ac - 'A' <= 25u)
                ac += 32u;
            if (bc - 'A' <= 25u)
                bc += 32u;

            if (ac != bc)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether two ASCII character spans are equal, using a case-insensitive comparison. Assumes both spans
    /// contain only ASCII characters.
    /// </summary>
    /// <remarks>This method does not validate that the input spans contain only ASCII characters. Supplying
    /// non-ASCII input may result in incorrect comparisons. The spans must be of equal length.</remarks>
    /// <param name="a">The first span of characters to compare. Must contain only ASCII characters.</param>
    /// <param name="b">The second span of characters to compare. Must contain only ASCII characters and have the same length as
    /// <paramref name="a"/>.</param>
    /// <returns>true if the spans are equal when compared case-insensitively; otherwise, false.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAsciiIgnoreCase_AssumeAscii(this ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length != b.Length)
            return false;

        ref char ra = ref MemoryMarshal.GetReference(a);
        ref char rb = ref MemoryMarshal.GetReference(b);

        int len = a.Length;

        for (var i = 0; i < len; i++)
        {
            uint ac = Unsafe.Add(ref ra, i);
            uint bc = Unsafe.Add(ref rb, i);

            if (ac == bc)
                continue;

            if (ac - 'A' <= 25u)
                ac += 32u;
            if (bc - 'A' <= 25u)
                bc += 32u;

            if (ac != bc)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether all characters in the specified span are ASCII characters.
    /// </summary>
    /// <param name="span">The span of characters to evaluate.</param>
    /// <returns>true if every character in the span is an ASCII character; otherwise, false.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAscii(this ReadOnlySpan<char> span)
    {
        ref char r0 = ref MemoryMarshal.GetReference(span);

        for (var i = 0; i < span.Length; i++)
        {
            if (Unsafe.Add(ref r0, i) > 0x7Fu)
                return false;
        }

        return true;
    }
}