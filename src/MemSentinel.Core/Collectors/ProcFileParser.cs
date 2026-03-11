using System.Buffers.Text;

namespace MemSentinel.Core.Collectors;

internal static class ProcFileParser
{
    internal static long ParseField(ReadOnlySpan<byte> content, ReadOnlySpan<byte> field)
    {
        while (!content.IsEmpty)
        {
            var lineEnd = content.IndexOf((byte)'\n');
            var line = lineEnd >= 0 ? content[..lineEnd] : content;

            if (line.StartsWith(field))
            {
                var value = SkipWhitespace(line[field.Length..]);
                var spaceIdx = value.IndexOf((byte)' ');
                var numberSpan = spaceIdx >= 0 ? value[..spaceIdx] : value;

                return Utf8Parser.TryParse(numberSpan, out long result, out _) ? result : 0;
            }

            if (lineEnd < 0) break;
            content = content[(lineEnd + 1)..];
        }

        return 0;
    }

    private static ReadOnlySpan<byte> SkipWhitespace(ReadOnlySpan<byte> span)
    {
        var i = 0;
        while (i < span.Length && (span[i] == ' ' || span[i] == '\t'))
            i++;
        return span[i..];
    }
}
