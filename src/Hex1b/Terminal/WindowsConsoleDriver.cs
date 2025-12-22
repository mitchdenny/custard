using System.Runtime.InteropServices;
using System.Text;

namespace Hex1b.Terminal;

/// <summary>
/// Windows console driver targeting ConPTY/Windows Terminal with VT sequence support.
/// </summary>
/// <remarks>
/// This driver targets modern Windows terminals (Windows Terminal, VS Code terminal, etc.)
/// that support ConPTY and native VT sequence processing. It enables VT input mode so
/// the terminal sends VT escape sequences directly, similar to Unix terminals.
/// 
/// For resize detection, we use a hybrid approach: VT mode for input, but we also
/// use ReadConsoleInput in a separate check to catch WINDOW_BUFFER_SIZE_EVENT records.
/// 
/// Requirements: Windows 10 1809+ or Windows 11 with a VT-capable terminal.
/// </remarks>
internal sealed class WindowsConsoleDriver : IConsoleDriver
{
    // Standard handles
    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    
    // Console mode flags - Input
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;        // Ctrl+C processed by system
    private const uint ENABLE_LINE_INPUT = 0x0002;             // ReadFile waits for Enter
    private const uint ENABLE_ECHO_INPUT = 0x0004;             // Characters echoed
    private const uint ENABLE_WINDOW_INPUT = 0x0008;           // Window buffer size changes reported
    private const uint ENABLE_MOUSE_INPUT = 0x0010;            // Mouse events reported (legacy)
    private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;        // Quick edit mode (mouse for selection)
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;         // Required for ENABLE_QUICK_EDIT_MODE
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200; // VT input sequences (ConPTY)
    
    // INPUT_RECORD event types
    private const ushort KEY_EVENT = 0x0001;
    private const ushort MOUSE_EVENT = 0x0002;
    private const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
    private const ushort MENU_EVENT = 0x0008;
    private const ushort FOCUS_EVENT = 0x0010;
    
    // Console mode flags - Output
    private const uint ENABLE_PROCESSED_OUTPUT = 0x0001;              // Process special chars
    private const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;            // Wrap at line end
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;   // VT100 sequences (ConPTY)
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;          // No auto CR for LF
    
    // Wait constants
    private const uint WAIT_OBJECT_0 = 0;
    private const uint WAIT_TIMEOUT = 0x00000102;
    
    private nint _inputHandle;
    private nint _outputHandle;
    private uint _originalInputMode;
    private uint _originalOutputMode;
    private bool _inRawMode;
    private bool _disposed;
    private int _lastWidth;
    private int _lastHeight;
    
    public WindowsConsoleDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsConsoleDriver only works on Windows");
        }
        
        _inputHandle = GetStdHandle(STD_INPUT_HANDLE);
        _outputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        
        if (_inputHandle == nint.Zero || _outputHandle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to get console handles");
        }
        
        // Get initial size using Win32 API directly
        var (w, h) = GetWindowSize();
        _lastWidth = w;
        _lastHeight = h;
    }
    
    /// <summary>
    /// Gets the current window size using Win32 API directly.
    /// This is more reliable than Console.WindowWidth/Height in VT mode.
    /// </summary>
    private (int width, int height) GetWindowSize()
    {
        if (GetConsoleScreenBufferInfo(_outputHandle, out var info))
        {
            // Window size is the difference between right/left and bottom/top, plus 1
            var width = info.srWindow.Right - info.srWindow.Left + 1;
            var height = info.srWindow.Bottom - info.srWindow.Top + 1;
            return (width, height);
        }
        // Fallback to Console properties
        return (Console.WindowWidth, Console.WindowHeight);
    }
    
    public int Width => GetWindowSize().width;
    public int Height => GetWindowSize().height;
    
    public event Action<int, int>? Resized;
    
    public void EnterRawMode()
    {
        if (_inRawMode) return;
        
        // Save original modes
        if (!GetConsoleMode(_inputHandle, out _originalInputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for input: {Marshal.GetLastWin32Error()}");
        }
        
        if (!GetConsoleMode(_outputHandle, out _originalOutputMode))
        {
            throw new InvalidOperationException($"GetConsoleMode failed for output: {Marshal.GetLastWin32Error()}");
        }
        
        // Set up VT input mode for ConPTY:
        // - Disable line input (no waiting for Enter)
        // - Disable echo
        // - Disable Ctrl+C processing (we handle it via VT sequences)
        // - Disable quick edit mode (interferes with mouse)
        // - Enable VT input (terminal sends VT sequences directly)
        // - Enable window input (to receive WINDOW_BUFFER_SIZE_EVENT)
        var newInputMode = ENABLE_VIRTUAL_TERMINAL_INPUT | ENABLE_EXTENDED_FLAGS | ENABLE_WINDOW_INPUT;
        
        if (!SetConsoleMode(_inputHandle, newInputMode))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SetConsoleMode failed for input (error {error}). " +
                "This driver requires Windows 10 1809+ with a VT-capable terminal (Windows Terminal, VS Code, etc.).");
        }
        
        // Set up VT output mode for ConPTY
        var newOutputMode = ENABLE_PROCESSED_OUTPUT | 
                           ENABLE_WRAP_AT_EOL_OUTPUT | 
                           ENABLE_VIRTUAL_TERMINAL_PROCESSING |
                           DISABLE_NEWLINE_AUTO_RETURN;
        
        if (!SetConsoleMode(_outputHandle, newOutputMode))
        {
            // Restore input mode and fail
            SetConsoleMode(_inputHandle, _originalInputMode);
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SetConsoleMode failed for output (error {error}). " +
                "This driver requires Windows 10 1809+ with VT sequence support.");
        }
        
        _inRawMode = true;
        Console.TreatControlCAsInput = true;
    }
    
    public void ExitRawMode()
    {
        if (!_inRawMode) return;
        
        SetConsoleMode(_inputHandle, _originalInputMode);
        SetConsoleMode(_outputHandle, _originalOutputMode);
        
        _inRawMode = false;
        Console.TreatControlCAsInput = false;
    }
    
    public bool DataAvailable
    {
        get
        {
            if (!_inRawMode) return false;
            
            // Check if console has pending input
            if (GetNumberOfConsoleInputEvents(_inputHandle, out var count))
            {
                return count > 0;
            }
            return false;
        }
    }
    
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!_inRawMode)
        {
            throw new InvalidOperationException("Must enter raw mode before reading");
        }
        
        return await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                // Always check for resize first, before waiting for input
                CheckResize();
                
                // Wait for input with timeout to allow cancellation checks
                var waitResult = WaitForSingleObject(_inputHandle, 100); // 100ms timeout
                
                if (waitResult == WAIT_TIMEOUT)
                {
                    // Check for resize while waiting
                    CheckResize();
                    continue;
                }
                
                if (waitResult != WAIT_OBJECT_0)
                {
                    throw new InvalidOperationException($"WaitForSingleObject failed: {Marshal.GetLastWin32Error()}");
                }
                
                // With VT input mode, we can read raw bytes directly
                // The terminal sends VT sequences for special keys and mouse
                unsafe
                {
                    fixed (byte* ptr = buffer.Span)
                    {
                        if (ReadFile(_inputHandle, ptr, (uint)buffer.Length, out var bytesRead, nint.Zero))
                        {
                            if (bytesRead > 0)
                            {
                                // Check for resize after reading input too
                                CheckResize();
                                return (int)bytesRead;
                            }
                        }
                        else
                        {
                            var error = Marshal.GetLastWin32Error();
                            // ERROR_IO_PENDING (997) is expected for async, but we're sync here
                            if (error != 0)
                            {
                                throw new InvalidOperationException($"ReadFile failed: {error}");
                            }
                        }
                    }
                }
            }
            
            return 0; // Cancelled
        }, ct);
    }
    
    private void CheckResize()
    {
        // Peek at console input to check for WINDOW_BUFFER_SIZE_EVENT records
        // These are not delivered through ReadFile in VT mode, so we need to
        // explicitly check for them using PeekConsoleInput/ReadConsoleInput
        if (GetNumberOfConsoleInputEvents(_inputHandle, out var count) && count > 0)
        {
            var records = new INPUT_RECORD[count];
            if (PeekConsoleInput(_inputHandle, records, (uint)records.Length, out var peekedCount) && peekedCount > 0)
            {
                // Check if any of the peeked records are resize events
                for (int i = 0; i < peekedCount; i++)
                {
                    if (records[i].EventType == WINDOW_BUFFER_SIZE_EVENT)
                    {
                        // Found a resize event - consume all records up to and including this one
                        // using ReadConsoleInput (we need to remove the resize event from the queue)
                        var consumeRecords = new INPUT_RECORD[i + 1];
                        if (ReadConsoleInput(_inputHandle, consumeRecords, (uint)consumeRecords.Length, out _))
                        {
                            // Get the new size from the event
                            var newWidth = records[i].WindowBufferSizeEvent.dwSize.X;
                            var newHeight = records[i].WindowBufferSizeEvent.dwSize.Y;
                            
                            // Only fire if size actually changed
                            if (newWidth != _lastWidth || newHeight != _lastHeight)
                            {
                                _lastWidth = newWidth;
                                _lastHeight = newHeight;
                                Resized?.Invoke(newWidth, newHeight);
                            }
                        }
                        // Restart the check as there may be more resize events
                        CheckResize();
                        return;
                    }
                }
            }
        }
    }
    
    public void Write(ReadOnlySpan<byte> data)
    {
        // With VT processing enabled, we can write raw bytes containing VT sequences
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var remaining = (uint)data.Length;
                var offset = 0;
                
                while (remaining > 0)
                {
                    if (!WriteFile(_outputHandle, ptr + offset, remaining, out var bytesWritten, nint.Zero))
                    {
                        var error = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException($"WriteFile failed: {error}");
                    }
                    
                    offset += (int)bytesWritten;
                    remaining -= bytesWritten;
                }
            }
        }
    }
    
    public void Flush()
    {
        // Console output is typically unbuffered, but flush just in case
        FlushFileBuffers(_outputHandle);
    }
    
    public void DrainInput()
    {
        if (!_inRawMode) return;
        
        // Flush the console input buffer
        FlushConsoleInputBuffer(_inputHandle);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        ExitRawMode();
        // Handles are pseudo-handles from GetStdHandle, no need to close
    }
    
    // P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumberOfConsoleInputEvents(nint hConsoleInput, out uint lpcNumberOfEvents);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool ReadFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        nint lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool WriteFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        nint lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushConsoleInputBuffer(nint hConsoleInput);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(nint hFile);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(nint hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekConsoleInput(
        nint hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadConsoleInput(
        nint hConsoleInput,
        [Out] INPUT_RECORD[] lpBuffer,
        uint nLength,
        out uint lpNumberOfEventsRead);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }
    
    // INPUT_RECORD and related structures for reading console events
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_RECORD
    {
        [FieldOffset(0)]
        public ushort EventType;
        
        [FieldOffset(4)]
        public KEY_EVENT_RECORD KeyEvent;
        
        [FieldOffset(4)]
        public MOUSE_EVENT_RECORD MouseEvent;
        
        [FieldOffset(4)]
        public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
        
        [FieldOffset(4)]
        public MENU_EVENT_RECORD MenuEvent;
        
        [FieldOffset(4)]
        public FOCUS_EVENT_RECORD FocusEvent;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct KEY_EVENT_RECORD
    {
        public int bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSE_EVENT_RECORD
    {
        public COORD dwMousePosition;
        public uint dwButtonState;
        public uint dwControlKeyState;
        public uint dwEventFlags;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOW_BUFFER_SIZE_RECORD
    {
        public COORD dwSize;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct MENU_EVENT_RECORD
    {
        public uint dwCommandId;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct FOCUS_EVENT_RECORD
    {
        public int bSetFocus;
    }
}
