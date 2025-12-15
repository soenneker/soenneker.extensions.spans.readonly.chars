using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Soenneker.Extensions.Char;
using Soenneker.Extensions.Spans.Readonly.Bytes;

namespace Soenneker.Extensions.Spans.Readonly.Chars;

/// <summary>
/// A collection of helpful ReadOnlySpan (char) extension methods
/// </summary>
public static class ReadOnlySpanCharExtension
{
    /// <summary>
    /// Determines whether all characters in the specified span are white-space characters.
    /// </summary>
    /// <param name="span">The span of characters to evaluate.</param>
    /// <returns>true if every character in the span is a white-space character; otherwise, false.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(this ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (!span[i]
                    .IsWhiteSpaceFast())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a span of characters into an array of non-empty, trimmed substrings that are separated by the specified
    /// character.
    /// </summary>
    /// <remarks>Empty segments and segments consisting only of whitespace are ignored. Each returned
    /// substring is trimmed of leading and trailing whitespace.</remarks>
    /// <param name="span">The span of characters to parse for separated values.</param>
    /// <param name="separator">The character used to separate substrings within the span.</param>
    /// <returns>An array of non-empty, trimmed substrings. Returns an empty array if no non-empty substrings are found.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string[] SplitTrimmedNonEmpty(this ReadOnlySpan<char> span, char separator)
    {
        // pass 1: count non-empty trimmed segments
        var count = 0;
        var start = 0;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == separator)
            {
                ReadOnlySpan<char> seg = span.Slice(start, i - start)
                                             .Trim();
                if (!seg.IsEmpty)
                    count++;

                start = i + 1;
            }
        }

        if (count == 0)
            return [];

        // pass 2: allocate exact array + fill
        var result = new string[count];
        start = 0;
        var idx = 0;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == separator)
            {
                ReadOnlySpan<char> seg = span.Slice(start, i - start)
                                             .Trim();
                if (!seg.IsEmpty)
                    result[idx++] = seg.ToString();

                start = i + 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified text and returns its hexadecimal string representation.
    /// </summary>
    /// <remarks>This method efficiently handles input of any size, using stack allocation for small inputs
    /// and pooling or streaming for larger ones. The output string contains 64 hexadecimal characters.</remarks>
    /// <param name="text">The text to compute the SHA-256 hash for.</param>
    /// <param name="encoding">The character encoding to use when converting the text to bytes. If null, UTF-8 is used.</param>
    /// <param name="upperCase">true to return the hexadecimal string in uppercase; otherwise, false for lowercase.</param>
    /// <returns>A hexadecimal string representing the SHA-256 hash of the input text.</returns>
    [Pure, MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static string ToSha256Hex(this ReadOnlySpan<char> text, Encoding? encoding = null, bool upperCase = true)
    {
        encoding ??= Encoding.UTF8;
        int byteCount = encoding.GetByteCount(text);

        // Stack fast-path
        if (byteCount <= 1024)
        {
            Span<byte> tmp = stackalloc byte[byteCount];
            encoding.GetBytes(text, tmp);
            return ((ReadOnlySpan<byte>)tmp).ToSha256Hex(upperCase);
        }

        // Pool fallback for moderately large inputs
        if (byteCount <= 128 * 1024)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                int written = encoding.GetBytes(text, rented);
                return new ReadOnlySpan<byte>(rented, 0, written).ToSha256Hex(upperCase);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // Very large inputs: stream to avoid big rents
        return ToSha256HexStreaming(text, encoding, upperCase);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the specified text using the provided encoding and returns the result as a hexadecimal
    /// string.
    /// </summary>
    /// <remarks>This method processes the input text in chunks to minimize memory usage, making it suitable for
    /// hashing large strings. The output string contains 64 hexadecimal characters.</remarks>
    /// <param name="text">The text to compute the SHA-256 hash for.</param>
    /// <param name="encoding">The character encoding to use when converting the text to bytes. Cannot be null.</param>
    /// <param name="upperCase">true to return the hexadecimal string in uppercase; otherwise, false for lowercase.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash of the input text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static string ToSha256HexStreaming(ReadOnlySpan<char> text, Encoding encoding, bool upperCase)
    {
        using var ih = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Encoder encoder = encoding.GetEncoder();

        const int charChunk = 4096;
        int maxBytesPerChar = Math.Max(1, encoding.GetMaxByteCount(1)); // conservative bound
        byte[] buffer = ArrayPool<byte>.Shared.Rent(charChunk * maxBytesPerChar);

        try
        {
            for (var i = 0; i < text.Length; i += charChunk)
            {
                ReadOnlySpan<char> slice = text.Slice(i, Math.Min(charChunk, text.Length - i));
                encoder.Convert(slice, buffer, flush: i + slice.Length >= text.Length, out int charsUsed, out int bytesUsed, out _);
                ih.AppendData(buffer, 0, bytesUsed);
            }

            Span<byte> hash = stackalloc byte[32];
            ih.TryGetHashAndReset(hash, out _);
            return upperCase ? Convert.ToHexString(hash) : Convert.ToHexStringLower(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}