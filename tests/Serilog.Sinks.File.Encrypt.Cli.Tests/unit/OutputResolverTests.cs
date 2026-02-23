namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

public class OutputResolverTests
{
    private readonly MockFileSystem _fileSystem = new();
    private OutputResolver GetSut() => new(_fileSystem);

    #region No output path specified (default behaviour)

    [Fact]
    public void GivenNoOutputPath_SingleFile_PlacesDecryptedNextToSource()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputPath: null);

        // Assert
        result.ShouldBe(Path.Join("logs", "app.decrypted.log"));
    }

    [Fact]
    public void GivenEmptyOutputPath_SingleFile_PlacesDecryptedNextToSource()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputPath: "   ");

        // Assert
        result.ShouldBe(Path.Join("logs", "app.decrypted.log"));
    }

    [Fact]
    public void GivenNoOutputPath_FileInRoot_ProducesDecryptedFilename()
    {
        // Arrange
        string inputFile = "app.log";
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputPath: null);

        // Assert
        result.ShouldBe("app.decrypted.log");
    }

    [Fact]
    public void GivenNoOutputPath_InsertsDotDecryptedBeforeExtension()
    {
        // Arrange
        string inputFile = Path.Join("logs", "service.2024-01-01.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputPath: null);

        // Assert
        result.ShouldBe(Path.Join("logs", "service.2024-01-01.decrypted.log"));
    }

    #endregion

    #region Single-file input – explicit file output path

    [Fact]
    public void GivenSingleFileInput_AndOutputPathIsNewFile_ReturnsOutputPathAsIs()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        string outputFile = Path.Join("out", "decrypted.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));
        // outputFile does NOT exist yet – not a directory

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputFile);

        // Assert
        result.ShouldBe(outputFile);
    }

    [Fact]
    public void GivenSingleFileInput_AndOutputPathIsExistingFile_ReturnsOutputPathAsIs()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        string outputFile = Path.Join("out", "decrypted.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));
        _fileSystem.AddFile(outputFile, new MockFileData("old content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputFile);

        // Assert
        result.ShouldBe(outputFile);
    }

    #endregion

    #region Single-file input – output path is a directory

    [Fact]
    public void GivenSingleFileInput_AndOutputPathIsExistingDirectory_AppendsComputedFilename()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        string outputDir = Path.Join("out", "decrypted");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));
        _fileSystem.AddDirectory(outputDir);

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputDir);

        // Assert
        result.ShouldBe(Path.Join(outputDir, "app.decrypted.log"));
    }

    [Fact]
    public void GivenSingleFileInput_AndOutputPathEndsWithDirectorySeparator_AppendsComputedFilename()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        string outputDir = Path.Join("out", "decrypted") + Path.DirectorySeparatorChar;
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputDir);

        // Assert
        result.ShouldBe(Path.Join(outputDir, "app.decrypted.log"));
    }

    [Fact]
    public void GivenSingleFileInput_AndOutputPathEndsWithAltDirectorySeparator_AppendsComputedFilename()
    {
        // Arrange
        string inputFile = Path.Join("logs", "app.log");
        string outputDir = "out/decrypted/"; // alt separator
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputDir);

        // Assert
        result.ShouldBe(Path.Join(outputDir, "app.decrypted.log"));
    }

    #endregion

    #region Multi-file input (directory) – output path always treated as directory

    [Fact]
    public void GivenDirectoryInput_AndOutputPathDoesNotExist_TreatsOutputAsDirectory()
    {
        // Arrange
        string inputDir = Path.Join("logs");
        string inputFile = Path.Join(inputDir, "app.log");
        string outputDir = Path.Join("out", "decrypted");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));
        _fileSystem.AddDirectory(inputDir);
        // outputDir does NOT exist – but must still be treated as a directory

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputDir, outputDir);

        // Assert
        result.ShouldBe(Path.Join(outputDir, "app.decrypted.log"));
    }

    [Fact]
    public void GivenDirectoryInput_AndOutputPathExists_TreatsOutputAsDirectory()
    {
        // Arrange
        string inputDir = Path.Join("logs");
        string inputFile = Path.Join(inputDir, "app.log");
        string outputDir = Path.Join("out", "decrypted");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));
        _fileSystem.AddDirectory(inputDir);
        _fileSystem.AddDirectory(outputDir);

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputDir, outputDir);

        // Assert
        result.ShouldBe(Path.Join(outputDir, "app.decrypted.log"));
    }

    [Fact]
    public void GivenDirectoryInput_NoOutputPath_PlacesDecryptedNextToSource()
    {
        // Arrange
        string inputDir = Path.Join("logs");
        string inputFile = Path.Join(inputDir, "app.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));
        _fileSystem.AddDirectory(inputDir);

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputDir, outputPath: null);

        // Assert
        result.ShouldBe(Path.Join(inputDir, "app.decrypted.log"));
    }

    [Fact]
    public void GivenDirectoryInput_MultipleFiles_EachGetsSeparateDecryptedName()
    {
        // Arrange
        string inputDir = Path.Join("logs");
        string inputFile1 = Path.Join(inputDir, "app1.log");
        string inputFile2 = Path.Join(inputDir, "app2.log");
        string outputDir = Path.Join("out");
        _fileSystem.AddFile(inputFile1, new MockFileData("content"));
        _fileSystem.AddFile(inputFile2, new MockFileData("content"));
        _fileSystem.AddDirectory(inputDir);

        OutputResolver sut = GetSut();

        // Act
        string result1 = sut.ResolveOutputPath(inputFile1, inputDir, outputDir);
        string result2 = sut.ResolveOutputPath(inputFile2, inputDir, outputDir);

        // Assert
        result1.ShouldBe(Path.Join(outputDir, "app1.decrypted.log"));
        result2.ShouldBe(Path.Join(outputDir, "app2.decrypted.log"));
    }

    #endregion

    #region Multi-file input (glob pattern) – output path always treated as directory

    [Fact]
    public void GivenGlobInput_AndOutputPathDoesNotExist_TreatsOutputAsDirectory()
    {
        // Arrange
        string inputDir = Path.Join("logs");
        string inputFile = Path.Join(inputDir, "app.log");
        string globPattern = Path.Join(inputDir, "*.log");
        string outputDir = Path.Join("out", "decrypted");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, globPattern, outputDir);

        // Assert
        result.ShouldBe(Path.Join(outputDir, "app.decrypted.log"));
    }

    [Fact]
    public void GivenGlobInput_NoOutputPath_PlacesDecryptedNextToSource()
    {
        // Arrange
        string inputDir = Path.Join("logs");
        string inputFile = Path.Join(inputDir, "app.log");
        string globPattern = Path.Join(inputDir, "*.log");
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, globPattern, outputPath: null);

        // Assert
        result.ShouldBe(Path.Join(inputDir, "app.decrypted.log"));
    }

    #endregion

    #region Generated filename format

    [Theory]
    [InlineData("app.log", "app.decrypted.log")]
    [InlineData("service.txt", "service.decrypted.txt")]
    [InlineData("archive.2024-01-01.log", "archive.2024-01-01.decrypted.log")]
    [InlineData("noextension", "noextension.decrypted")]
    public void GivenVariousFilenames_DefaultOutput_ProducesExpectedDecryptedName(
        string fileName,
        string expectedDecryptedName
    )
    {
        // Arrange
        string inputFile = Path.Join("logs", fileName);
        _fileSystem.AddFile(inputFile, new MockFileData("content"));

        // Act
        string result = GetSut().ResolveOutputPath(inputFile, inputFile, outputPath: null);

        // Assert
        result.ShouldBe(Path.Join("logs", expectedDecryptedName));
    }

    #endregion
}
