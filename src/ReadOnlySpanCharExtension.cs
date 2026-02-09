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
    /// Determines whether all characters in the specified span are Unicode white-space characters.
    /// </summary>
    /// <remarks>This method returns true for empty spans, as there are no non-white-space characters present.
    /// The definition of white-space is based on Unicode standards and includes characters such as space, tab, and line
    /// breaks.</remarks>
    /// <param name="span">The read-only span of characters to evaluate for white-space.</param>
    /// <returns>true if every character in the span is a Unicode white-space character; otherwise, false.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(this ReadOnlySpan<char> span)
    {
        ref char r0 = ref MemoryMarshal.GetReference(span);
        for (var i = 0; i < span.Length; i++)
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
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string[] SplitTrimmedNonEmpty(this ReadOnlySpan<char> span, char separator)
    {
        var count = 0;
        var start = 0;

        // pass 1: count
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == separator)
            {
                if (TryTrimNonEmpty(span.Slice(start, i - start), out _))
                    count++;

                start = i + 1;
            }
        }

        if (count == 0)
            return [];

        var result = new string[count];
        start = 0;
        var idx = 0;

        // pass 2: fill
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == separator)
            {
                if (TryTrimNonEmpty(span.Slice(start, i - start), out ReadOnlySpan<char> trimmed))
                    result[idx++] = trimmed.ToString();

                start = i + 1;
            }
        }

        return result;
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
            return ((ReadOnlySpan<byte>)tmp).ToSha256Hex(upperCase);
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
                bool flush = (i + slice.Length) >= text.Length;

                encoder.Convert(
                    slice,
                    buffer,
                    flush,
                    out int charsUsed,
                    out int bytesUsed,
                    out bool completed);

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

        while ((uint)start < (uint)segment.Length && segment[start].IsWhiteSpaceFast())
            start++;

        while (end >= start && segment[end].IsWhiteSpaceFast())
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
        // Rough capacity guess to reduce growth; we still only allocate 1 final string.
        var estimated = 0;
        for (var i = 0; i < count; i++)
        {
            ReadOnlySpan<char> s = address[ranges[startIndex + i]].Trim();
            if (s.Length == 0)
                continue;

            if (estimated != 0)
                estimated += 2; // ", "
            estimated += s.Length;
        }

        if (estimated == 0)
            return string.Empty;

        using var sb = new PooledStringBuilder(estimated);

        var first = true;
        for (var i = 0; i < count; i++)
        {
            ReadOnlySpan<char> s = address[ranges[startIndex + i]].Trim();
            if (s.Length == 0)
                continue;

            if (!first)
                sb.Append(", ");
            first = false;

            sb.Append(s);
        }

        return sb.ToString();
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
    /// Splits the specified character span into ranges separated by commas and writes the resulting ranges to the
    /// provided span.
    /// </summary>
    /// <remarks>If the number of segments in <paramref name="input"/> exceeds the length of <paramref
    /// name="ranges"/>, only as many ranges as will fit are written. Empty segments (i.e., consecutive commas or
    /// leading/trailing commas) are ignored and not included in the output.</remarks>
    /// <param name="input">The input span of characters to split. Commas (',') are treated as delimiters.</param>
    /// <param name="ranges">The span to which the resulting ranges are written. Each range represents the start and end indices of a segment
    /// between commas in the input.</param>
    /// <returns>The number of ranges written to the <paramref name="ranges"/> span.</returns>
    public static int SplitCommaRanges(this ReadOnlySpan<char> input, Span<Range> ranges)
    {
        var count = 0;
        var start = 0;

        for (var i = 0; i <= input.Length; i++)
        {
            if (i == input.Length || input[i] == ',')
            {
                if (count == ranges.Length)
                    break;

                int len = i - start;
                if (len > 0)
                    ranges[count++] = start..i;

                start = i + 1;
            }
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
    public static int SplitNonEmptyLineRanges(this ReadOnlySpan<char> input, Span<Range> ranges)
    {
        var count = 0;
        var pos = 0;

        while (pos < input.Length && count < ranges.Length)
        {
            int lineEnd = IndexOfNewline(input, pos);
            if (lineEnd < 0)
                lineEnd = input.Length;

            ReadOnlySpan<char> line = input.Slice(pos, lineEnd - pos);
            line = TrimCrlf(line).Trim();

            if (line.Length != 0)
            {
                // Recompute trimmed range bounds relative to original input.
                // (We trimmed from a slice, so find bounds within that slice.)
                int leading = LeadingWhitespaceCount(input.Slice(pos, lineEnd - pos));
                int trailing = TrailingWhitespaceCount(input.Slice(pos, lineEnd - pos));
                int start = pos + leading;
                int end = lineEnd - trailing;

                if (start < end)
                    ranges[count++] = start..end;
            }

            // Advance past newline sequence
            pos = lineEnd;
            while (pos < input.Length)
            {
                char c = input[pos];
                if (c == '\r' || c == '\n')
                    pos++;
                else
                    break;
            }
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
        for (int i = start; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\r' || c == '\n')
                return i;
        }

        return -1;
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
        // Defensive: if caller slices include CR/LF at ends
        while (span.Length != 0 && (span[0] == '\r' || span[0] == '\n'))
            span = span.Slice(1);
        while (span.Length != 0 && (span[^1] == '\r' || span[^1] == '\n'))
            span = span.Slice(0, span.Length - 1);

        return span;
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
    {
        var count = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == c)
                count++;
        }

        return count;
    }

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
        while (i < span.Length && span[i].IsWhiteSpaceFast())
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
        while (i > 0 && span[i - 1].IsWhiteSpaceFast())
            i--;
        return span.Length - i;
    }

    /// <summary>
    /// Attempts to parse a 16-character hexadecimal string into a 64-bit unsigned integer.
    /// Accepts upper- and lowercase hexadecimal characters.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64(this ReadOnlySpan<char> hex, out ulong value)
    {
        if ((uint)hex.Length != 16u)
        {
            value = default;
            return false;
        }

        ulong acc = 0;

        // 16 fixed iterations; JIT typically keeps this very tight.
        for (var i = 0; i < 16; i++)
        {
            uint c = hex[i];

            // Fast path: '0'..'9'
            uint digit = c - '0';
            if (digit > 9u)
            {
                // Normalize ASCII letters to lowercase: 'A'..'F' -> 'a'..'f'
                c |= 0x20u;

                digit = c - 'a';
                if (digit > 5u)
                {
                    value = default;
                    return false;
                }

                digit += 10u;
            }

            acc = (acc << 4) | digit;
        }

        value = acc;
        return true;
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
        while ((uint)idx < (uint)span.Length && char.IsWhiteSpace(span[idx]))
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
            // skip leading ws
            while ((uint)k < (uint)value.Length && char.IsWhiteSpace(value[k]))
                k++;

            if (k >= value.Length)
                break;

            int start = k;

            // scan token
            while ((uint)k < (uint)value.Length && !char.IsWhiteSpace(value[k]))
                k++;

            ReadOnlySpan<char> token = value.Slice(start, k - start);
            if (!token.IsEmpty)
                set.Add(token.ToString());
        }
    }
}
