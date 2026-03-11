using System.Text;
using FluentAssertions;
using MemSentinel.Core.Collectors;

namespace MemSentinel.UnitTests.Collectors;

public sealed class ProcFileParserTests
{
    private static ReadOnlySpan<byte> Bytes(string text) =>
        Encoding.UTF8.GetBytes(text);

    [Fact]
    public void ParseField_ReturnsVmRss_FromStatusContent()
    {
        var content = Bytes(
            "Name:\tdotnet\n" +
            "VmPeak:\t 524288 kB\n" +
            "VmSize:\t 480000 kB\n" +
            "VmRSS:\t  153600 kB\n" +
            "VmData:\t  90000 kB\n");

        var result = ProcFileParser.ParseField(content, "VmRSS:"u8);

        result.Should().Be(153600);
    }

    [Fact]
    public void ParseField_ReturnsPss_FromSmapsRollupContent()
    {
        var content = Bytes(
            "Rss:              153600 kB\n" +
            "Pss:              140288 kB\n" +
            "Shared_Clean:      12288 kB\n");

        var result = ProcFileParser.ParseField(content, "Pss:"u8);

        result.Should().Be(140288);
    }

    [Fact]
    public void ParseField_ReturnsZero_WhenFieldNotPresent()
    {
        var content = Bytes(
            "Name:\tdotnet\n" +
            "VmSize:\t 480000 kB\n");

        var result = ProcFileParser.ParseField(content, "VmRSS:"u8);

        result.Should().Be(0);
    }

    [Fact]
    public void ParseField_ReturnsZero_WhenLineIsMalformed()
    {
        var content = Bytes(
            "VmRSS:\t not-a-number kB\n");

        var result = ProcFileParser.ParseField(content, "VmRSS:"u8);

        result.Should().Be(0);
    }

    [Fact]
    public void ParseField_HandlesLargeValues_AboveFourGigabytes()
    {
        var largekB = 5L * 1024 * 1024;
        var content = Bytes($"VmSize:\t {largekB} kB\n");

        var result = ProcFileParser.ParseField(content, "VmSize:"u8);

        result.Should().Be(largekB);
    }

    [Fact]
    public void ParseField_HandlesFieldAtEndOfBuffer_WithoutTrailingNewline()
    {
        var content = Bytes("VmRSS:\t  98304 kB");

        var result = ProcFileParser.ParseField(content, "VmRSS:"u8);

        result.Should().Be(98304);
    }
}
