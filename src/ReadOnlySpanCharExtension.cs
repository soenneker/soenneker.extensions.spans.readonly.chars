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
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWhiteSpace(this ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (!span[i].IsWhiteSpaceFast())
                return false;
        }

        return true;
    }

    /// <summary>Text → SHA-256 hex (UTF-8 by default)</summary>
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

    // Streaming path for huge inputs (avoids renting a giant buffer)
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