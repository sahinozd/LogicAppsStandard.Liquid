using System.Runtime.InteropServices;
using LogicApps.Liquid.Cli.Reporting;

namespace LogicApps.Liquid.Cli.Helpers;

/// <summary>
/// Decides whether to write ANSI colors, following the NO_COLOR convention (https://no-color.org).
/// </summary>
internal static partial class ConsoleColors
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    /// <summary>
    /// Determines whether colors are written.
    /// </summary>
    /// <param name="noColorOption">The <c>--no-color</c> option.</param>
    /// <returns>True for colors.</returns>
    public static bool Enabled(bool noColorOption)
    {
        if (noColorOption || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return false;
        }

        // Pipeline logs redirect the output, but show ANSI colors.
        if (CiAnnotationWriter.IsAzureDevOps || CiAnnotationWriter.IsGitHubActions)
        {
            return true;
        }

        return !Console.IsOutputRedirected && EnableVirtualTerminal();
    }

    // Windows Terminal and VS Code understand ANSI codes; the classic console needs virtual terminal processing turned on.
    private static bool EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        var handle = GetStdHandle(StdOutputHandle);
        if (!GetConsoleMode(handle, out var mode))
        {
            return false;
        }

        return (mode & EnableVirtualTerminalProcessing) != 0 || SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetStdHandle(int handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint handle, out uint mode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint handle, uint mode);
}
