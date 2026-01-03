using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Hex1b.Terminal;

/// <summary>
/// Unix (Linux/macOS) PTY implementation using POSIX APIs.
/// Uses Process with fd redirection approach.
/// </summary>
internal sealed partial class UnixPtyHandle : IPtyHandle
{
    private int _masterFd = -1;
    private int _childPid = -1;
    private bool _disposed;
    private readonly byte[] _readBuffer = new byte[4096];
    private Process? _helperProcess;
    private string? _tempScriptPath;
    
    public int ProcessId => _childPid;
    
    public async Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        // Create a pseudo-terminal using posix_openpt
        _masterFd = PosixOpenPt(O_RDWR | O_NOCTTY);
        if (_masterFd < 0)
        {
            throw new InvalidOperationException($"posix_openpt failed with error: {Marshal.GetLastWin32Error()}");
        }
        
        // Grant access to the slave
        if (Grantpt(_masterFd) < 0)
        {
            Close(_masterFd);
            throw new InvalidOperationException($"grantpt failed with error: {Marshal.GetLastWin32Error()}");
        }
        
        // Unlock the slave
        if (Unlockpt(_masterFd) < 0)
        {
            Close(_masterFd);
            throw new InvalidOperationException($"unlockpt failed with error: {Marshal.GetLastWin32Error()}");
        }
        
        // Get slave device name
        var slaveNamePtr = Ptsname(_masterFd);
        if (slaveNamePtr == IntPtr.Zero)
        {
            Close(_masterFd);
            throw new InvalidOperationException($"ptsname failed with error: {Marshal.GetLastWin32Error()}");
        }
        var slaveName = Marshal.PtrToStringAnsi(slaveNamePtr)!;
        
        // Set terminal size
        Resize(width, height);
        
        // Make master non-blocking
        SetNonBlocking(_masterFd);
        
        // Build argument list for the process
        var allArgs = new List<string> { fileName };
        allArgs.AddRange(arguments);
        
        // Create a temporary script file to launch the process with PTY attached
        // This avoids complex shell quoting issues
        _tempScriptPath = Path.Combine(Path.GetTempPath(), $"hex1b_pty_{Environment.ProcessId}_{Guid.NewGuid():N}.sh");
        
        // Build argument array variable assignments for proper escaping
        var argAssignments = new StringBuilder();
        for (int i = 0; i < allArgs.Count; i++)
        {
            argAssignments.AppendLine($"args[{i}]={EscapeShellArg(allArgs[i])}");
        }
        
        var scriptContent = $@"#!/bin/bash
# Redirect stdio to the slave PTY
exec < ""{slaveName}"" > ""{slaveName}"" 2>&1

# Build args array
declare -a args
{argAssignments}

# Execute the command
exec ""${{args[@]}}""
";
        await File.WriteAllTextAsync(_tempScriptPath, scriptContent, ct);
        
        // Make the script executable
        Chmod(_tempScriptPath, 0x1ED); // 0755

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = _tempScriptPath,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };
        
        // Set environment variables
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }
        
        _helperProcess = Process.Start(startInfo);
        if (_helperProcess == null)
        {
            Close(_masterFd);
            throw new InvalidOperationException("Failed to start helper process");
        }
        
        _childPid = _helperProcess.Id;
        
        // Small delay to let the child set up
        await Task.Delay(50, ct);
    }
    
    private static string EscapeShellArg(string arg)
    {
        // Escape single quotes by replacing ' with '\''
        return "'" + arg.Replace("'", "'\\''") + "'";
    }
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
    {
        if (_masterFd < 0 || _disposed)
            return ReadOnlyMemory<byte>.Empty;
        
        // Use poll to wait for data with cancellation support
        while (!ct.IsCancellationRequested)
        {
            var pollResult = await PollReadableAsync(_masterFd, 100, ct);
            
            if (pollResult < 0)
            {
                // Error or hangup - but try one more read to drain any remaining data
                var finalBytes = Read(_masterFd, _readBuffer, _readBuffer.Length);
                if (finalBytes > 0)
                {
                    var finalResult = new byte[finalBytes];
                    Array.Copy(_readBuffer, finalResult, finalBytes);
                    return finalResult;
                }
                return ReadOnlyMemory<byte>.Empty;
            }
            
            if (pollResult > 0)
            {
                // Data available
                var bytesRead = Read(_masterFd, _readBuffer, _readBuffer.Length);
                
                if (bytesRead <= 0)
                {
                    // EOF or error
                    return ReadOnlyMemory<byte>.Empty;
                }
                
                var result = new byte[bytesRead];
                Array.Copy(_readBuffer, result, bytesRead);
                return result;
            }
            
            // Timeout - check if child is still alive
            if (_helperProcess?.HasExited == true)
            {
                // Child exited - try to read any remaining buffered data
                var remainingBytes = Read(_masterFd, _readBuffer, _readBuffer.Length);
                if (remainingBytes > 0)
                {
                    var remainingResult = new byte[remainingBytes];
                    Array.Copy(_readBuffer, remainingResult, remainingBytes);
                    return remainingResult;
                }
                return ReadOnlyMemory<byte>.Empty;
            }
        }
        
        return ReadOnlyMemory<byte>.Empty;
    }
    
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_masterFd < 0 || _disposed || data.IsEmpty)
            return ValueTask.CompletedTask;
        
        var span = data.Span;
        var buffer = new byte[span.Length];
        span.CopyTo(buffer);
        
        var written = Write(_masterFd, buffer, buffer.Length);
        
        if (written < 0)
        {
            // Write error - process may have exited
        }
        
        return ValueTask.CompletedTask;
    }
    
    public void Resize(int width, int height)
    {
        if (_masterFd < 0 || _disposed)
            return;
        
        var winsize = new WinSize
        {
            ws_row = (ushort)height,
            ws_col = (ushort)width,
            ws_xpixel = 0,
            ws_ypixel = 0
        };
        
        _ = Ioctl(_masterFd, TIOCSWINSZ, ref winsize);
    }
    
    public void Kill(int signal)
    {
        if (_helperProcess is { HasExited: false })
        {
            // For SIGTERM/SIGKILL, use .NET's Kill method
            if (signal == SIGTERM || signal == SIGKILL)
            {
                try
                {
                    _helperProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore - process may have already exited
                }
            }
            else if (_childPid > 0)
            {
                // For other signals, use kill()
                _ = KillProcess(_childPid, signal);
            }
        }
    }
    
    public async Task<int> WaitForExitAsync(CancellationToken ct)
    {
        if (_helperProcess == null)
            return -1;
        
        try
        {
            await _helperProcess.WaitForExitAsync(ct);
            return _helperProcess.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
    }
    
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        
        _disposed = true;
        
        if (_masterFd >= 0)
        {
            Close(_masterFd);
            _masterFd = -1;
        }
        
        // Kill child if still running
        if (_helperProcess is { HasExited: false })
        {
            try
            {
                _helperProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore - process may have already exited
            }
        }
        
        _helperProcess?.Dispose();
        
        // Clean up temp script file
        if (_tempScriptPath != null)
        {
            try { File.Delete(_tempScriptPath); } catch { }
        }
        
        return ValueTask.CompletedTask;
    }
    
    // === P/Invoke declarations ===
    
    private const int SIGTERM = 15;
    private const int SIGKILL = 9;
    
    // TIOCSWINSZ value differs between Linux and macOS
    private static readonly nuint TIOCSWINSZ = OperatingSystem.IsMacOS() 
        ? 0x80087467  // macOS
        : 0x5414;     // Linux
    
    // O_RDWR and O_NOCTTY values
    private const int O_RDWR = 2;
    private static readonly int O_NOCTTY = OperatingSystem.IsMacOS() ? 0x20000 : 0x100;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }
    
    // PTY functions
    [LibraryImport("libc", EntryPoint = "posix_openpt", SetLastError = true)]
    private static partial int PosixOpenPt(int flags);
    
    [LibraryImport("libc", EntryPoint = "grantpt", SetLastError = true)]
    private static partial int Grantpt(int fd);
    
    [LibraryImport("libc", EntryPoint = "unlockpt", SetLastError = true)]
    private static partial int Unlockpt(int fd);
    
    [LibraryImport("libc", EntryPoint = "ptsname", SetLastError = true)]
    private static partial IntPtr Ptsname(int fd);
    
    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    private static partial nint Read(int fd, byte[] buf, nint count);
    
    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
    private static partial nint Write(int fd, byte[] buf, nint count);
    
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fd);
    
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static partial int Ioctl(int fd, nuint request, ref WinSize winsize);
    
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int KillProcess(int pid, int sig);
    
    [LibraryImport("libc", EntryPoint = "chmod", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Chmod(string path, int mode);
    
    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static partial int Fcntl(int fd, int cmd, int arg);
    
    [LibraryImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static partial int Poll(ref PollFd fds, nuint nfds, int timeout);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }
    
    private const short POLLIN = 0x0001;
    private const short POLLHUP = 0x0010;
    private const short POLLERR = 0x0008;
    
    private const int F_GETFL = 3;
    private const int F_SETFL = 4;
    private static readonly int O_NONBLOCK = OperatingSystem.IsMacOS() ? 0x0004 : 0x800;
    
    private static void SetNonBlocking(int fd)
    {
        var flags = Fcntl(fd, F_GETFL, 0);
        if (flags >= 0)
        {
            _ = Fcntl(fd, F_SETFL, flags | O_NONBLOCK);
        }
    }
    
    private static Task<int> PollReadableAsync(int fd, int timeoutMs, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var pollFd = new PollFd
            {
                fd = fd,
                events = POLLIN,
                revents = 0
            };
            
            var result = Poll(ref pollFd, 1, timeoutMs);
            
            if (result < 0)
                return -1;
            
            if ((pollFd.revents & POLLHUP) != 0 || (pollFd.revents & POLLERR) != 0)
                return -1;
            
            if ((pollFd.revents & POLLIN) != 0)
                return 1;
            
            return 0;  // Timeout
        }, ct);
    }
}
