/*
 * pty_spawn.c - Native PTY child process spawner for Unix systems
 * 
 * This library properly spawns a child process attached to a pseudo-terminal
 * with correct session and controlling terminal setup. This is required for
 * programs like tmux, screen, and other terminal multiplexers to work correctly.
 * 
 * Key operations performed:
 * 1. fork() - Create child process
 * 2. setsid() - Create new session (detach from parent's controlling terminal)
 * 3. open() slave PTY - Open the slave end of the pseudo-terminal
 * 4. ioctl(TIOCSCTTY) - Make the slave PTY the controlling terminal
 * 5. dup2() - Redirect stdin/stdout/stderr to the slave PTY
 * 6. execve() - Execute the target program
 */

#define _GNU_SOURCE
#include <sys/types.h>
#include <sys/wait.h>
#include <sys/ioctl.h>
#include <unistd.h>
#include <fcntl.h>
#include <signal.h>
#include <errno.h>
#include <string.h>
#include <stdlib.h>
#include <termios.h>

#ifdef __APPLE__
#include <util.h>
#else
#include <pty.h>
#endif

/* External environment variable */
extern char **environ;

/* TIOCSCTTY value differs between platforms */
#ifdef __APPLE__
#define TIOCSCTTY_VALUE 0x20007461
#else
#define TIOCSCTTY_VALUE 0x540E
#endif

/**
 * Spawns a child process attached to the given PTY slave.
 * 
 * @param path          Path to the executable
 * @param argv          NULL-terminated argument array (argv[0] should be program name)
 * @param envp          NULL-terminated environment array (NULL to inherit)
 * @param slave_name    Path to the PTY slave device (e.g., "/dev/pts/0")
 * @param working_dir   Working directory for the child (NULL for current)
 * @param out_pid       Output: PID of the spawned child process
 * 
 * @return 0 on success, -1 on error (errno is set)
 */
int pty_spawn(
    const char* path,
    char* const argv[],
    char* const envp[],
    const char* slave_name,
    const char* working_dir,
    int* out_pid)
{
    if (path == NULL || argv == NULL || slave_name == NULL || out_pid == NULL) {
        errno = EINVAL;
        return -1;
    }

    /* Block signals during fork to prevent race conditions */
    sigset_t all_signals, old_signals;
    sigfillset(&all_signals);
    pthread_sigmask(SIG_SETMASK, &all_signals, &old_signals);

    pid_t pid = fork();

    if (pid == -1) {
        /* Fork failed */
        int saved_errno = errno;
        pthread_sigmask(SIG_SETMASK, &old_signals, NULL);
        errno = saved_errno;
        return -1;
    }

    if (pid == 0) {
        /* ========== CHILD PROCESS ========== */
        
        /* Restore signal mask */
        pthread_sigmask(SIG_SETMASK, &old_signals, NULL);
        
        /* Reset signal handlers to default */
        struct sigaction sa_default;
        memset(&sa_default, 0, sizeof(sa_default));
        sa_default.sa_handler = SIG_DFL;
        
        for (int sig = 1; sig < NSIG; sig++) {
            if (sig == SIGKILL || sig == SIGSTOP) continue;
            sigaction(sig, &sa_default, NULL);
        }

        /* 1. Create a new session - this detaches from the parent's 
         *    controlling terminal and makes us the session leader */
        if (setsid() < 0) {
            _exit(127);
        }

        /* 2. Open the slave PTY - since we're session leader without a 
         *    controlling terminal, opening a terminal device will make 
         *    it our controlling terminal on most systems */
        int slave_fd = open(slave_name, O_RDWR);
        if (slave_fd < 0) {
            _exit(127);
        }

        /* 3. Explicitly set as controlling terminal (required on some systems) */
#ifdef TIOCSCTTY
        /* The second argument: 0 = fail if already has ctty, 1 = steal if needed */
        if (ioctl(slave_fd, TIOCSCTTY, 0) < 0) {
            /* Try with force flag on some systems */
            ioctl(slave_fd, TIOCSCTTY, 1);
        }
#endif

        /* 4. Redirect standard file descriptors to the slave PTY */
        if (dup2(slave_fd, STDIN_FILENO) < 0) {
            _exit(127);
        }
        if (dup2(slave_fd, STDOUT_FILENO) < 0) {
            _exit(127);
        }
        if (dup2(slave_fd, STDERR_FILENO) < 0) {
            _exit(127);
        }

        /* Close the original slave_fd if it's not one of the standard fds */
        if (slave_fd > STDERR_FILENO) {
            close(slave_fd);
        }

        /* 5. Change working directory if specified */
        if (working_dir != NULL && working_dir[0] != '\0') {
            if (chdir(working_dir) < 0) {
                /* Non-fatal - continue with current directory */
            }
        }

        /* 6. Execute the target program */
        char* const* env = (envp != NULL) ? envp : environ;
        execve(path, argv, env);

        /* If execve returns, it failed */
        _exit(127);
    }

    /* ========== PARENT PROCESS ========== */
    
    /* Restore signal mask */
    pthread_sigmask(SIG_SETMASK, &old_signals, NULL);

    *out_pid = pid;
    return 0;
}

/**
 * Opens a new pseudo-terminal master/slave pair.
 * 
 * @param out_master_fd  Output: Master file descriptor
 * @param out_slave_name Output: Buffer to receive slave device path (must be at least 256 bytes)
 * @param slave_name_len Size of the slave_name buffer
 * @param width          Initial terminal width in columns
 * @param height         Initial terminal height in rows
 * 
 * @return 0 on success, -1 on error (errno is set)
 */
int pty_open(
    int* out_master_fd,
    char* out_slave_name,
    int slave_name_len,
    int width,
    int height)
{
    if (out_master_fd == NULL || out_slave_name == NULL || slave_name_len < 256) {
        errno = EINVAL;
        return -1;
    }

    /* Open master side of PTY */
    int master_fd = posix_openpt(O_RDWR | O_NOCTTY);
    if (master_fd < 0) {
        return -1;
    }

    /* Grant access to slave */
    if (grantpt(master_fd) < 0) {
        int saved_errno = errno;
        close(master_fd);
        errno = saved_errno;
        return -1;
    }

    /* Unlock slave */
    if (unlockpt(master_fd) < 0) {
        int saved_errno = errno;
        close(master_fd);
        errno = saved_errno;
        return -1;
    }

    /* Get slave device name */
    char* slave_name = ptsname(master_fd);
    if (slave_name == NULL) {
        int saved_errno = errno;
        close(master_fd);
        errno = saved_errno;
        return -1;
    }

    /* Copy slave name to output buffer */
    strncpy(out_slave_name, slave_name, slave_name_len - 1);
    out_slave_name[slave_name_len - 1] = '\0';

    /* Set terminal size */
    if (width > 0 && height > 0) {
        struct winsize ws;
        ws.ws_row = height;
        ws.ws_col = width;
        ws.ws_xpixel = 0;
        ws.ws_ypixel = 0;
        ioctl(master_fd, TIOCSWINSZ, &ws);
    }

    *out_master_fd = master_fd;
    return 0;
}

/**
 * Resizes the terminal associated with the given master PTY.
 * 
 * @param master_fd  Master file descriptor
 * @param width      New terminal width in columns
 * @param height     New terminal height in rows
 * 
 * @return 0 on success, -1 on error
 */
int pty_resize(int master_fd, int width, int height)
{
    struct winsize ws;
    ws.ws_row = height;
    ws.ws_col = width;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;
    return ioctl(master_fd, TIOCSWINSZ, &ws);
}

/**
 * Waits for a child process to exit with timeout.
 * 
 * @param pid        PID of the child process
 * @param timeout_ms Timeout in milliseconds (-1 for infinite)
 * @param out_status Output: Exit status
 * 
 * @return 0 on success (child exited), 1 on timeout, -1 on error
 */
int pty_wait(int pid, int timeout_ms, int* out_status)
{
    if (timeout_ms < 0) {
        /* Infinite wait */
        int status;
        if (waitpid(pid, &status, 0) < 0) {
            return -1;
        }
        if (out_status != NULL) {
            if (WIFEXITED(status)) {
                *out_status = WEXITSTATUS(status);
            } else if (WIFSIGNALED(status)) {
                *out_status = 128 + WTERMSIG(status);
            } else {
                *out_status = -1;
            }
        }
        return 0;
    }

    /* Poll with timeout */
    int elapsed = 0;
    while (elapsed < timeout_ms) {
        int status;
        int result = waitpid(pid, &status, WNOHANG);
        
        if (result < 0) {
            return -1;
        }
        
        if (result > 0) {
            /* Child exited */
            if (out_status != NULL) {
                if (WIFEXITED(status)) {
                    *out_status = WEXITSTATUS(status);
                } else if (WIFSIGNALED(status)) {
                    *out_status = 128 + WTERMSIG(status);
                } else {
                    *out_status = -1;
                }
            }
            return 0;
        }

        /* Sleep 10ms and try again */
        usleep(10000);
        elapsed += 10;
    }

    /* Timeout */
    return 1;
}
