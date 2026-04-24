namespace Tabu.Domain.Updates;

/// <summary>
/// Raised when a downloaded installer's SHA-256 digest does not match the
/// value advertised by the release manifest. Distinguished from generic
/// download failures because the user must be warned that the file may
/// have been tampered with — not just transiently unreachable.
/// </summary>
public sealed class InstallerIntegrityException : Exception
{
    public string ExpectedDigest { get; }
    public string ActualDigest { get; }

    public InstallerIntegrityException(string expectedDigest, string actualDigest)
        : base($"Installer integrity check failed. Expected SHA-256 {expectedDigest}, computed {actualDigest}.")
    {
        ExpectedDigest = expectedDigest;
        ActualDigest = actualDigest;
    }
}
