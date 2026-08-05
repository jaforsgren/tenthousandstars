using Xunit;
using Tts.Utils;

namespace Tts.Tests;

public class YarnHeaderParserTests
{
    [Fact]
    public void Parse_ReadsMapHeader()
    {
        const string yarn = """
            title: mission_retreat
            map: tutorial
            enemies_left: 0
            ---

            Narrator: Line.

            ===
            """;

        var headers = YarnHeaderParser.Parse(yarn);

        Assert.Equal("tutorial", headers["mission_retreat"]["map"]);
    }

    [Fact]
    public void Parse_NodeWithoutMapHasNoMapKey()
    {
        const string yarn = """
            title: campaign_victory
            ---

            Narrator: Line.

            ===
            """;

        var headers = YarnHeaderParser.Parse(yarn);

        Assert.False(headers["campaign_victory"].ContainsKey("map"));
    }
}