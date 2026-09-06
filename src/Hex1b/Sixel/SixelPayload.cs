namespace Hex1b.Sixel;

internal static class SixelPayload
{
    private const string SevenBitIntroducer = "\x1bP";
    private const string SevenBitTerminator = "\x1b\\";
    private const char EightBitIntroducer = '\x90';
    private const char EightBitTerminator = '\x9c';

    public static string NormalizeAndValidate(string imageData, string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(imageData, parameterName);

        string framed;
        if (imageData.StartsWith(SevenBitIntroducer, StringComparison.Ordinal))
        {
            if (!imageData.EndsWith(SevenBitTerminator, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A framed Sixel sequence must end with the ESC \\ string terminator.",
                    parameterName);
            }

            framed = imageData;
        }
        else if (imageData[0] == EightBitIntroducer)
        {
            if (imageData[^1] != EightBitTerminator)
            {
                throw new ArgumentException(
                    "An 8-bit framed Sixel sequence must end with the 8-bit string terminator.",
                    parameterName);
            }

            framed = imageData;
        }
        else
        {
            framed = $"{SevenBitIntroducer}q{imageData}{SevenBitTerminator}";
        }

        var parseResult = SixelParser.ParsePayload(framed);
        if (parseResult.Outcome != SixelParseOutcome.Complete)
        {
            var diagnostic = parseResult.Diagnostics.FirstOrDefault();
            var detail = string.IsNullOrEmpty(diagnostic.Message)
                ? parseResult.Outcome.ToString()
                : diagnostic.Message;
            throw new ArgumentException($"The value is not a complete valid Sixel sequence: {detail}", parameterName);
        }

        return framed;
    }
}
