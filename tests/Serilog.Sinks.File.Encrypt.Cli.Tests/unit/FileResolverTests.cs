namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class FileResolverTests
{
    private readonly MockFileSystem _fileSystem = new();
    private FileResolver GetSut() => new(_fileSystem);

    #region Single File Input

    [Fact]
    public void GivenSingleFile_ResolveFiles_ReturnsThatFile()
    {
        // Arrange
        string filePath = Path.Join("logs", "app.log");
        _fileSystem.AddFile(filePath, new MockFileData("content"));

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(filePath, recursive: false);

        // Assert
        result.ShouldHaveSingleItem();
        result[0].ShouldBe(filePath);
    }

    [Fact]
    public void GivenAlreadyDecryptedFile_ResolveFiles_ReturnsItAnyway()
    {
        // A .decrypted. file passed directly as a path should still be returned
        string filePath = Path.Join("logs", "app.decrypted.log");
        _fileSystem.AddFile(filePath, new MockFileData("content"));

        IReadOnlyList<string> result = GetSut().ResolveFiles(filePath, recursive: false);

        result.ShouldHaveSingleItem();
    }

    #endregion

    #region Directory Input

    [Fact]
    public void GivenDirectory_ResolveFiles_ReturnsLogFiles()
    {
        // Arrange
        string dir = Path.Join("logs");
        _fileSystem.AddFile(Path.Join(dir, "app1.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "app2.log"), new MockFileData("content"));

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(dir, recursive: false);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void GivenDirectory_ResolveFiles_ExcludesAlreadyDecryptedFiles()
    {
        // Arrange
        string dir = Path.Join("logs");
        _fileSystem.AddFile(Path.Join(dir, "app.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "app.decrypted.log"), new MockFileData("content"));

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(dir, recursive: false);

        // Assert
        result.ShouldHaveSingleItem();
        result[0].ShouldContain("app.log");
    }

    [Fact]
    public void GivenDirectory_WithRecursiveFalse_DoesNotIncludeSubdirectoryFiles()
    {
        // Arrange
        string dir = Path.Join("logs");
        _fileSystem.AddFile(Path.Join(dir, "app.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "sub", "nested.log"), new MockFileData("content"));

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(dir, recursive: false);

        // Assert
        result.ShouldHaveSingleItem();
    }

    [Fact]
    public void GivenDirectory_WithRecursiveTrue_IncludesSubdirectoryFiles()
    {
        // Arrange
        string dir = Path.Join("logs");
        _fileSystem.AddFile(Path.Join(dir, "app.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "sub", "nested.log"), new MockFileData("content"));

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(dir, recursive: true);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void GivenEmptyDirectory_ResolveFiles_ReturnsEmpty()
    {
        // Arrange
        string dir = Path.Join("logs", "empty");
        _fileSystem.AddDirectory(dir);

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(dir, recursive: false);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region Glob Pattern Input

    [Fact]
    public void GivenGlobPattern_ResolveFiles_ReturnsMatchingFiles()
    {
        // Arrange
        string dir = Path.Join("logs");
        _fileSystem.AddFile(Path.Join(dir, "app1.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "app2.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "app.txt"), new MockFileData("content"));
        string pattern = Path.Join(dir, "*.log");

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(pattern, recursive: false);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.EndsWith(".log"));
    }

    [Fact]
    public void GivenGlobPattern_ResolveFiles_ExcludesAlreadyDecryptedFiles()
    {
        // Arrange
        string dir = Path.Join("logs");
        _fileSystem.AddFile(Path.Join(dir, "app.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(dir, "app.decrypted.log"), new MockFileData("content"));
        string pattern = Path.Join(dir, "*.log");

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(pattern, recursive: false);

        // Assert
        result.ShouldHaveSingleItem();
    }

    [Fact]
    public void GivenGlobPatternWithNonExistentDirectory_ResolveFiles_ReturnsEmpty()
    {
        // Arrange
        string pattern = Path.Join("nonexistent", "*.log");

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(pattern, recursive: false);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GivenGlobPatternWithNoDirectory_WhenResolvesFiles_ReturnsMatchingFilesInCurrentDirectory()
    {
        // Arrange
        string currentDir = _fileSystem.Directory.GetCurrentDirectory();
        _fileSystem.AddFile(Path.Join(currentDir, "app1.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(currentDir, "app2.log"), new MockFileData("content"));
        _fileSystem.AddFile(Path.Join(currentDir, "app.txt"), new MockFileData("content"));
        string pattern = "*.log";

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(pattern, recursive: false);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.EndsWith(".log"));
    }

    #endregion

    #region Unresolvable Input

    [Fact]
    public void GivenNonExistentPathWithoutGlobChars_ResolveFiles_ReturnsEmpty()
    {
        // Arrange
        string path = Path.Join("logs", "doesnotexist.log");

        // Act
        IReadOnlyList<string> result = GetSut().ResolveFiles(path, recursive: false);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion
}
