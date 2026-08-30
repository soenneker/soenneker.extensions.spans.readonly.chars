[![](https://img.shields.io/nuget/v/soenneker.extensions.spans.readonly.chars.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.extensions.spans.readonly.chars/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.spans.readonly.chars/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.spans.readonly.chars/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.extensions.spans.readonly.chars.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.extensions.spans.readonly.chars/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.spans.readonly.chars/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.spans.readonly.chars/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Spans.Readonly.Chars
Allocation-conscious parsing, splitting, hashing, and ASCII helpers for `ReadOnlySpan<char>`.

## Installation

```bash
dotnet add package Soenneker.Extensions.Spans.Readonly.Chars
```

## Split and normalize text

```csharp
using Soenneker.Extensions.Spans.Readonly.Chars;

ReadOnlySpan<char> value = " alpha, , beta ,gamma ";
string[] parts = value.SplitTrimmedNonEmpty(',');
// ["alpha", "beta", "gamma"]
```

`SplitTrimmedNonEmpty()` creates one string per retained segment. For parsing without substring allocations, write source-backed ranges into a caller-owned buffer:

```csharp
ReadOnlySpan<char> csv = "alpha,, beta,gamma";
Span<Range> ranges = stackalloc Range[8];

int count = csv.SplitCommaRanges(ranges);
for (int i = 0; i < count; i++)
{
    ReadOnlySpan<char> item = csv[ranges[i]];
}
```

`SplitCommaRanges()` skips empty segments but does not trim them. `SplitNonEmptyLineRanges()` does trim each line and supports `\r`, `\n`, and `\r\n`. Both stop when the destination is full; the return value is the number written, not the total number available.

## Hash text

```csharp
string hash = "payload".AsSpan().ToSha256Hex(
    encoding: Encoding.UTF8,
    upperCase: false);
```

The result is always 64 hexadecimal characters. UTF-8 is used when no encoding is supplied. Small inputs are encoded on the stack, medium inputs use a cleared pooled buffer, and large inputs are encoded incrementally. Temporary byte buffers are zeroed before release, but the source text and returned hash remain the caller's responsibility.

## HTTP and ASCII checks

```csharp
bool hasScheme = "HTTPS://example.com".AsSpan().StartsWithHttpScheme();
bool equal = "Content-Type".AsSpan().EqualsAsciiIgnoreCase("content-type");
bool ascii = "plain text".AsSpan().IsAscii();
```

`StartsWithHttpScheme()` checks only the prefix; it does not validate a URI. `EqualsAsciiIgnoreCase()` folds ASCII letters and requires non-ASCII characters to match exactly. Use `EqualsAsciiIgnoreCase_AssumeAscii()` only after validating both inputs with `IsAscii()`.

## Other helpers

- `IsWhiteSpace()` returns `true` for an empty span.
- `TrimToNull()` is the allocating counterpart to `Trim()`: it returns `null` for an empty trimmed value.
- `JoinCommaSeparated()` trims selected ranges, skips empty values, and joins the rest with `, `.
- `IndexOfNewline()` and `TrimCrlf()` operate specifically on CR/LF characters.
- `LeadingWhitespaceCount()`, `TrailingWhitespaceCount()`, and `SkipWhitespace()` support index-based parsers.
- `TryParseHexUInt64()` accepts exactly 16 hexadecimal characters.
- `AddTokens()` allocates strings for whitespace-delimited tokens and honors the supplied set's comparer.

Ranges and spans returned by these helpers reference the original storage and must not outlive it.
