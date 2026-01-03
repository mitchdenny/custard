using System.Runtime.InteropServices;

namespace Hex1b.Terminal;

/// <summary>
/// Unix (Linux/macOS) PTY implementation using native library.
/// Uses proper setsid/TIOCSCTTY for controlling terminal setup,
/// which is required for programs like tmux and screen to work correctly.
/// </summary>
internal sealed partial class UnixPtyHandle : IPtyHandle
{
    private int _masterFd = -1;
    private int _childPid = -1;
    private bool _disposed;
    private readonly byte[] _readBuffer = new byte[4096];
    private string _slaveName = string.Empty;
    private bool _useNativeSpawn = true;
    
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
        // Check if native library is available
        _useNativeSpawn = IsNativeLibraryAvailable();
        
        if (_useNativeSpawn)
        {
            await StartWithNativeSpawnAsync(fileName, arguments, workingDirectory, environment, width, height, ct);
        }
        else
        {
            await StartWithFallbackAsync(fileName, arguments, workingDirectory, environment, width, height, ct);
        }
    }
    
    /// <summary>
    /// Uses native library for proper PTY spawning with setsid/TIOCSCTTY.
    /// This is required for tmux, screen, and other programs that need a proper controlling terminal.
    /// </summary>
    private async Task StartWithNativeSpawnAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        // Allocate buffer for slave name
        var slaveNameBuffer = new byte[256];
        
        // Open PTY using native library (handles posix_openpt, grantpt, unlockpt, ptsname)
        var result = pty_open(out _masterFd, slaveNameBuffer, slaveNameBuffer.Length, width, height);
        if (result < 0)
        {
            throw new InvalidOperationException($"pty_open failed with error: {Marshal.GetLastWin32Error()}");
        }
        
        // Extract slave name from buffer
        int nullIndex = Array.IndexOf(slaveNameBuffer, (byte)0);
        _slaveName = System.Text.Encoding.UTF8.GetString(slaveNameBuffer, 0, nullIndex >= 0 ? nullIndex : slaveNameBuffer.Length);
        
        // Make master non-blocking for async reads
        SetNonBlocking(_masterFd);
        
        // Resolve executable path
        string resolvedPath = ResolveExecutablePath(fileName);
        
        // Build argv array: [program, arg1, arg2, ..., NULL]
        var argv = new string[arguments.Length + 2];
        argv[0] = resolvedPath;
        for (int i = 0; i < arguments.Length; i++)
        {
            argv[i + 1] = arguments[i];
        }
        // Last element is implicitly null for P/Invoke
        
        // Build envp array if environment was customized
        string[]? envp = null;
        if (environment.Count > 0)
        {
            // Get current environment and merge with custom
            var envDict = new Dictionary<string, string>();
            foreach (var entry in System.Environment.GetEnvironmentVariables())
            {
                if (entry is System.Collections.DictionaryEntry de)
                {
                    envDict[de.Key?.ToString() ?? ""] = de.Value?.ToString() ?? "";
                }
            }
            foreach (var (key, value) in environment)
            {
                envDict[key] = value;
            }
            
            envp = new string[envDict.Count + 1];
            int i = 0;
            foreach (var (key, value) in envDict)
            {
                envp[i++] = $"{key}={value}";
            }
            // Last element is implicitly null
        }
        
        // Spawn the child process with proper PTY setup
        result = pty_spawn(
            resolvedPath,
            argv,
            envp,
            _slaveName,
            workingDirectory ?? System.Environment.CurrentDirectory,
            out _childPid);
        
        if (result < 0)
        {
            Close(_masterFd);
            _masterFd = -1;
            throw new InvalidOperationException($"pty_spawn failed with error: {Marshal.GetLastWin32Error()}");
        }
        
        // Small delay to let child process initialize
        await Task.Delay(50, ct);
    }
    
    /// <summary>
    /// Fallback to managed PTY setup when native library is not available.
    /// Note: This won't work correctly with tmux/screen.
    /// </summary>
    private async Task StartWithFallbackAsync(
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
        _slaveName = Marshal.PtrToStringAnsi(slaveNamePtr)!;
        
        // Set terminal size
        Resize(width, height);
        
        // Make master non-blocking
        SetNonBlocking(_masterFd);
        
        // Build argument list for the process
        var allArgs = new List<string> { fileName };
        allArgs.AddRange(arguments);
        
        // Create a temporary script file to launch the process with PTY attached
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"hex1b_pty_{System.Environment.ProcessId}_{Guid.NewGuid():N}.sh");
        
        // Build argument array variable assignments for proper escaping
        var argAssignments = new System.Text.StringBuilder();
        for (int i = 0; i < allArgs.Count; i++)
        {
            argAssignments.AppendLine($"args[{i}]={EscapeShellArg(allArgs[i])}");
        }
        
        // Note: This fallback doesn't use setsid/TIOCSCTTY, so tmux won't work properly
        var scriptContent = $@"#!/bin/bash
# Redirect stdio to the slave PTY
exec < ""{_slaveName}"" > ""{_slaveName}"" 2>&1

# Build args array
declare -a args
{argAssignments}

# Execute the command
exec ""${{args[@]}}""
";
        await File.WriteAllTextAsync(tempScriptPath, scriptContent, ct);
        
        // Make the script executable
        Chmod(tempScriptPath, 0x1ED); // 0755

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = tempScriptPath,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? System.Environment.CurrentDirectory
        };
        
        // Set environment variables
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }
        
        var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            Close(_masterFd);
            throw new InvalidOperationException("Failed to start process");
        }
        
        _childPid = process.Id;
        
        // Clean up temp script after a delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            try { File.Delete(tempScriptPath); } catch { }
        }, CancellationToken.None);
        
        // Small delay to let the child set up
        await Task.Delay(50, ct);
    }
    
    private static string EscapeShellArg(string arg)
    {
        // Escape single quotes by replacing ' with '\''
        return "'" + arg.Replace("'", "'\\''") + "'";
    }
    
    private static string ResolveExecutablePath(string fileName)
    {
        // If it's an absolute or relative path, return as-is
        if (fileName.Contains('/'))
        {
            return Path.GetFullPath(fileName);
        }
        
        // Search PATH
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(':'))
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        
        // Not found in PATH, return as-is and let execve fail with proper error
        return fileName;
    }
    
    private static bool IsNativeLibraryAvailable()
    {
        try
        {
            // Try to call a simple function to verify library is loaded
            var buffer = new byte[256];
            // Just check if the function exists - don't actually call it
            return NativeLibrary.TryLoad("ptyspawn", typeof(UnixPtyHandle).Assembly, null, out _);
        }
        catch
        {
            return false;
        }
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
            if (_childPid > 0 && !IsChildRunning(_childPid))
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
        if (_childPid > 0 && IsChildRunning(_childPid))
        {
            // Use kill() syscall to send signal to child process
            _ = KillProcess(_childPid, signal);
        }
    }
    
    private static bool IsChildRunning(int pid)
    {
        // kill(pid, 0) checks if process exists without sending a signal
        return KillProcess(pid, 0) == 0;
    }
    
    public async Task<int> WaitForExitAsync(CancellationToken ct)
    {
        if (_childPid <= 0)
            return -1;
        
        // Poll for child exit
        while (!ct.IsCancellationRequested)
        {
            if (_useNativeSpawn)
            {
                // Use native pty_wait with timeout
                int status;
                int result = pty_wait(_childPid, 100, out status);
                if (result == 0)
                {
                    // Child exited
                    return status;
                }
                else if (result < 0)
                {
                    // Error
                    return -1;
                }
                // result == 1 means timeout, continue polling
            }
            else
            {
                // Fallback: poll with kill(0)
                if (!IsChildRunning(_childPid))
                {
                    // Child exited - we don't have exit code in this case
                    return 0;
                }
                await Task.Delay(100, ct);
            }
        }
        
        return -1;
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
        
        // Kill child if still running and wait for it to die
        if (_childPid > 0 && IsChildRunning(_childPid))
        {
            // Send SIGKILL (can't be ignored)
            _ = KillProcess(_childPid, SIGKILL);
            
            // Wait briefly for process to terminate (up to 100ms)
            for (int i = 0; i < 10 && IsChildRunning(_childPid); i++)
            {
                Thread.Sleep(10);
            }
            
            // Reap the zombie process if using native spawn
            if (_useNativeSpawn)
            {
                _ = pty_wait(_childPid, 100, out _);
            }
        }
        
        return ValueTask.CompletedTask;
    }
    
    // === P/Invoke declarations ===
    
    // Native ptyspawn library functions
    [LibraryImport("ptyspawn", EntryPoint = "pty_open", SetLastError = true)]
    private static partial int pty_open(out int masterFd, byte[] slaveName, int slaveNameLen, int width, int height);
    
    [LibraryImport("ptyspawn", EntryPoint = "pty_spawn", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int pty_spawn(
        string path,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] argv,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[]? envp,
        string slaveName,
        string workingDir,
        out int pid);
    
    [LibraryImport("ptyspawn", EntryPoint = "pty_wait", SetLastError = true)]
    private static partial int pty_wait(int pid, int timeoutMs, out int status);
    
    [LibraryImport("ptyspawn", EntryPoint = "pty_resize", SetLastError = true)]
    private static partial int pty_resize(int masterFd, int width, int height);
    
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
