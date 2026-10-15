namespace LogicApps.Liquid.Cli.Constants;

/// <summary>
/// Process exit codes, so pipelines can tell a failed validation apart from a wrong command.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Everything passed.</summary>
    public const int Success = 0;

    /// <summary>Validation or test failures.</summary>
    public const int Failed = 1;

    /// <summary>Invalid arguments, or a file could not be read or written.</summary>
    public const int UsageError = 2;
}
