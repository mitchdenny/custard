namespace Hex1b.Tokens;

/// <summary>
/// Represents an Operating System Command (OSC): ESC ] command ; params ; payload ST
/// </summary>
/// <param name="Command">The OSC command number as a string (e.g., "8" for hyperlinks).</param>
/// <param name="Params">Optional parameters between command and payload.</param>
/// <param name="Payload">The main payload (e.g., URL for OSC 8).</param>
/// <remarks>
/// <para>
/// Common OSC commands:
/// <list type="bullet">
///   <item>0 = Set window title and icon name</item>
///   <item>1 = Set icon name</item>
///   <item>2 = Set window title</item>
///   <item>8 = Hyperlink: ESC ] 8 ; params ; URI ST</item>
/// </list>
/// </para>
/// <para>
/// The string terminator (ST) can be ESC \ or BEL (\x07).
/// </para>
/// </remarks>
public sealed record OscToken(string Command, string Params, string Payload) : AnsiToken;
