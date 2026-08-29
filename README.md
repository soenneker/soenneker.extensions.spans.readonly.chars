[![](https://img.shields.io/nuget/v/soenneker.extensions.spans.readonly.chars.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.extensions.spans.readonly.chars/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.spans.readonly.chars/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.spans.readonly.chars/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.extensions.spans.readonly.chars.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.extensions.spans.readonly.chars/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.spans.readonly.chars/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.spans.readonly.chars/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Spans.Readonly.Chars
A collection of helpful ReadOnlySpan (char) extension methods.

## Installation

```bash
dotnet add package Soenneker.Extensions.Spans.Readonly.Chars
```

## Quick start

```csharp
using Soenneker.Extensions.Spans.Readonly.Chars;

// Given an existing ReadOnlySpan<char> named span:
var result = span.IsWhiteSpace();
```

## Common operations

- `IsWhiteSpace()` - Determines whether all characters in the specified read-only character span are white-space characters.
- `StartsWithHttpScheme()` - Returns `true` when the span begins with `http://` or `https://`, using an allocation-free ASCII case-insensitive check.
- `SplitTrimmedNonEmpty()` - Splits the specified character span into substrings based on the given separator, trims whitespace from each substring, and returns only the non-empty results.
- `ToSha256Hex()` - Computes the SHA-256 hash of the specified text and returns its hexadecimal string representation.
- `ToSha256HexStreaming()` - Computes the SHA-256 hash of the specified text using streaming encoding and returns the result as a hexadecimal string.
- `JoinCommaSeparated()` - Creates a comma-separated string by joining the trimmed substrings of the specified ranges within the input span. Returns a string consisting of the trimmed substrings, separated by commas and spaces. Returns an empty string if no non-empty substrings are found.
- `TrimToNull()` - Trims leading and trailing white-space characters from the specified span and returns the resulting string, or null if the trimmed span is empty.
- `SplitCommaRanges()` - Splits a read-only character span into ranges separated by commas and writes the resulting ranges to the specified destination span.
- `SplitNonEmptyLineRanges()` - Splits the input span into ranges representing non-empty, trimmed lines and writes them to the specified destination span. Returns the number of non-empty, trimmed line ranges written to the destination span. This value will not exceed the length of the destination span.
- `IndexOfNewline()` - Searches for the first occurrence of a newline character ('\r' or '\n') in the specified span, starting at the given index. Returns the zero-based index of the first occurrence of a newline character in the span, or -1 if no newline character is found.
- `TrimCrlf()` - Removes any leading and trailing carriage return ('\r') and line feed ('\n') characters from the specified read-only character span. This method does not modify the original data; it returns a new span referencing the trimmed range within the original span.
- `CountChar()` - Counts the number of occurrences of a specified character within a read-only span of characters. Returns the total number of times the specified character appears in the span. This method is optimized for performance and does not allocate additional memory.

The package also includes 8 additional operations for more specialized cases.
