using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Soenneker.Extensions.Char;
using Soenneker.Extensions.Spans.Readonly.Bytes;

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
        for (int i = 0; i < span.Length; i++)
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
        int count = 0;
        int start = 0;

        // pass 1: count
        for (int i = 0; i <= span.Length; i++)
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
        int idx = 0;

        // pass 2: fill
        for (int i = 0; i <= span.Length; i++)
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
    public static string ToSha256HexStreaming(ReadOnlySpan<char> text, Encoding encoding, bool upperCase)
    {
        using var ih = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Encoder encoder = encoding.GetEncoder();

        const int charChunk = 4096;

        // Correct sizing: max bytes for *charChunk* chars (includes encoder overhead properly)
        int maxBytes = encoding.GetMaxByteCount(charChunk);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxBytes);

        try
        {
            for (int i = 0; i < text.Length; i += charChunk)
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
                    int offset = 0;
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
        int start = 0;
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
}
