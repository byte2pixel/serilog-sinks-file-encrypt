using System.IO.Abstractions;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <inheritdoc />
public sealed class KeyFileWriter(IFileSystem fileSystem, IConsoleWriter writer) : IKeyFileWriter
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <inheritdoc />
    public void WritePrivateKey(string path, string contents)
    {
        if (OperatingSystem.IsWindows())
        {
            fileSystem.File.WriteAllText(path, contents);
            RestrictOnWindows(path);
            return;
        }

        // Unix: create with owner-only mode so there is no window where the file is
        // readable by others; SetUnixFileMode afterwards covers the overwrite case,
        // where UnixCreateMode does not apply.
        FileStreamOptions options = new()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            UnixCreateMode = OwnerOnly,
        };
        using (FileSystemStream stream = fileSystem.FileStream.New(path, options))
        using (StreamWriter streamWriter = new(stream))
        {
            streamWriter.Write(contents);
        }

        try
        {
            fileSystem.File.SetUnixFileMode(path, OwnerOnly);
            writer.Verbose($"  [dim]{path}: permissions restricted to owner (600)[/]");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            writer.Warning(
                $"[yellow]⚠ Could not restrict permissions on {path}: {ex.Message}. Restrict access manually.[/]"
            );
        }
    }

    /// <inheritdoc />
    public void WritePublicKey(string path, string contents)
    {
        fileSystem.File.WriteAllText(path, contents);
    }

    [SupportedOSPlatform("windows")]
    private void RestrictOnWindows(string path)
    {
        // ACLs are not exposed through System.IO.Abstractions, so this uses System.IO
        // directly — but only against the real file system; with a mock (tests) the
        // restriction is skipped.
        if (fileSystem is not FileSystem)
        {
            return;
        }

        try
        {
            RestrictAclToCurrentUser(path);
            writer.Verbose($"  [dim]{path}: ACL restricted to the current user[/]");
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or PlatformNotSupportedException
                        or InvalidOperationException
            )
        {
            writer.Warning(
                $"[yellow]⚠ Could not restrict permissions on {path}: {ex.Message}. Restrict access manually.[/]"
            );
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictAclToCurrentUser(string path)
    {
        SecurityIdentifier? user = WindowsIdentity.GetCurrent().User;
        if (user is null)
        {
            throw new InvalidOperationException("Could not determine the current Windows user.");
        }

        FileInfo fileInfo = new(path);
        FileSecurity security = fileInfo.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (
            FileSystemAccessRule rule in security
                .GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
        )
        {
            security.RemoveAccessRule(rule);
        }

        security.AddAccessRule(
            new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow)
        );
        fileInfo.SetAccessControl(security);
    }
}
