using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Node that renders a hyperlink using OSC 8 escape sequences.
/// In terminals that support OSC 8, the text becomes clickable.
/// </summary>
public sealed class HyperlinkNode : Hex1bNode
{
    private string _text = "";
    public string Text 
    { 
        get => _text; 
        set 
        {
            if (_text != value)
            {
                _text = value;
                MarkDirty();
            }
        }
    }

    private string _uri = "";
    public string Uri 
    { 
        get => _uri; 
        set 
        {
            if (_uri != value)
            {
                _uri = value;
                MarkDirty();
            }
        }
    }

    private string _parameters = "";
    public string Parameters 
    { 
        get => _parameters; 
        set 
        {
            if (_parameters != value)
            {
                _parameters = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// Used to create typed event args.
    /// </summary>
    public HyperlinkWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The async action to execute when the hyperlink is activated.
    /// This is the wrapped handler that receives InputBindingActionContext.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ClickAction { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Only register activation bindings if there's an action to perform
        if (ClickAction != null)
        {
            // Enter triggers the link
            bindings.Key(Hex1bKey.Enter).Action(ClickAction, "Open link");
            
            // Left click activates the link
            bindings.Mouse(MouseButton.Left).Action(ClickAction, "Click link");
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Hyperlink renders as just the text (OSC 8 sequences are invisible)
        var width = DisplayWidth.GetStringWidth(Text);
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToInherited = context.GetResetToInheritedCodes();
        
        // OSC 8 format: ESC ] 8 ; params ; URI ST text ESC ] 8 ; ; ST
        // ST (String Terminator) can be ESC \ or BEL (\x07)
        // We use ESC \ for better compatibility
        var osc8Start = FormatOsc8Start(Uri, Parameters);
        var osc8End = "\x1b]8;;\x1b\\";
        
        // Apply styling based on focus/hover state
        string styledText;
        if (IsFocused)
        {
            // Focused: underline + bright color
            var fg = theme.Get(HyperlinkTheme.FocusedForegroundColor);
            styledText = $"{fg.ToForegroundAnsi()}\x1b[4m{Text}\x1b[24m{resetToInherited}";
        }
        else if (IsHovered)
        {
            // Hovered: underline
            var fg = theme.Get(HyperlinkTheme.HoveredForegroundColor);
            styledText = $"{fg.ToForegroundAnsi()}\x1b[4m{Text}\x1b[24m{resetToInherited}";
        }
        else
        {
            // Normal: link color (typically underlined by terminal for OSC 8 links)
            var fg = theme.Get(HyperlinkTheme.ForegroundColor);
            if (fg.IsDefault)
            {
                // Use blue as default link color
                styledText = $"\x1b[34m{Text}{resetToInherited}";
            }
            else
            {
                styledText = $"{fg.ToForegroundAnsi()}{Text}{resetToInherited}";
            }
        }
        
        // Wrap with OSC 8 sequences
        var output = $"{osc8Start}{styledText}{osc8End}";
        
        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    /// <summary>
    /// Formats the OSC 8 start sequence.
    /// Format: ESC ] 8 ; params ; URI ST
    /// </summary>
    private static string FormatOsc8Start(string uri, string parameters)
    {
        // ESC ] 8 ; params ; URI ESC \
        return $"\x1b]8;{parameters};{uri}\x1b\\";
    }
}
