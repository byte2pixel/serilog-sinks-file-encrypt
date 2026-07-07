using System.Security.AccessControl;
using System.Security.Principal;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

public class KeyFileWriterTests : CommandTestBase
{
    private KeyFileWriter GetSut() => new(FileSystem, Writer);

    [Fact]
    public void WritePrivateKey_WritesContents()
    {
        // Arrange
        FileSystem.AddDirectory("keys");
        string path = Path.Join("keys", "private_key.pem");

        // Act
        GetSut().WritePrivateKey(path, "private key material");

        // Assert
        FileSystem.File.ReadAllText(path).ShouldBe("private key material");
    }

    [Fact]
    public void WritePublicKey_WritesContents()
    {
        // Arrange
        FileSystem.AddDirectory("keys");
        string path = Path.Join("keys", "public_key.pem");

        // Act
        GetSut().WritePublicKey(path, "public key material");

        // Assert
        FileSystem.File.ReadAllText(path).ShouldBe("public key material");
    }

    [Fact]
    public void WritePrivateKey_OverwritesExistingContents()
    {
        // Arrange — --force flows reach the writer with an existing file
        string path = Path.Join("keys", "private_key.pem");
        FileSystem.AddFile(path, new MockFileData("old key"));

        // Act
        GetSut().WritePrivateKey(path, "new key");

        // Assert
        FileSystem.File.ReadAllText(path).ShouldBe("new key");
    }

    [Fact]
    public void WritePrivateKey_OnUnix_RestrictsModeToOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        // Arrange
        FileSystem.AddDirectory("keys");
        string path = Path.Join("keys", "private_key.pem");

        // Act
        GetSut().WritePrivateKey(path, "private key material");

        // Assert — 600
        FileSystem
            .File.GetUnixFileMode(path)
            .ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void WritePrivateKey_OnWindowsRealFileSystem_RestrictsAclToCurrentUser()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows ACLs only.");
            return;
        }

        // Arrange — the ACL path only runs against the real file system
        string dir = Path.Join(Path.GetTempPath(), $"keywriter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Join(dir, "private_key.pem");
        var realFs = new FileSystem();
        KeyFileWriter sut = new(realFs, Writer);

        try
        {
            // Act
            sut.WritePrivateKey(path, "private key material");

            // Assert — inheritance off, exactly one rule: current user, full control
            FileSecurity security = new FileInfo(path).GetAccessControl();
            var rules = security
                .GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .ToList();
            rules.Count.ShouldBe(1);
            rules[0].IdentityReference.ShouldBe(WindowsIdentity.GetCurrent().User);
            rules[0].AccessControlType.ShouldBe(AccessControlType.Allow);
            rules[0].IsInherited.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
