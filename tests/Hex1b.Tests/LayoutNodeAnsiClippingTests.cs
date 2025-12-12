using Hex1b.Layout;
using Hex1b.Nodes;
using Xunit;

namespace Hex1b.Tests;

public class LayoutNodeAnsiClippingTests
{
    [Fact]
    public void ClipString_RightClipsPrintableText_PreservesTrailingResetSuffix()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var text = "\x1b[31mABCDE\x1b[0m";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(1, adjustedX);
        Assert.Equal("\x1b[31mBCD\x1b[0m", clipped);
        AssertValidAnsiCsiSequences(clipped);
    }

    [Fact]
    public void ClipString_ClipsPlainText_DoesNotIntroduceAnsiCodes()
    {
        var node = new LayoutNode();
        node.Arrange(new Rect(1, 0, 3, 1));

        var text = "ABCDE";

        var (adjustedX, clipped) = node.ClipString(0, 0, text);

        Assert.Equal(1, adjustedX);
        Assert.Equal("BCD", clipped);
        Assert.DoesNotContain("\x1b[", clipped);
    }

    private static void AssertValidAnsiCsiSequences(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\x1b')
                continue;

            Assert.True(i + 1 < text.Length, "Dangling ESC at end");

            if (text[i + 1] != '[')
                continue;

            var foundFinal = false;
            for (var j = i + 2; j < text.Length; j++)
            {
                var c = text[j];
                if (c >= '@' && c <= '~')
                {
                    foundFinal = true;
                    i = j; // skip to end of sequence
                    break;
                }
            }

            Assert.True(foundFinal, "Incomplete CSI sequence");
        }
    }
}
