# Contributing to Serilog.Sinks.File.Encrypt

First off, thank you for considering contributing to Serilog.Sinks.File.Encrypt! It's people like you that make this project better for everyone.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Code Style](#code-style)
- [Submitting Changes](#submitting-changes)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Enhancements](#suggesting-enhancements)

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior by opening an issue.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the existing issues to avoid duplicates. When you create a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples** (code snippets, configuration files)
- **Describe the behavior you observed** and what you expected
- **Include your environment details** (.NET version, OS, package version)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description** of the suggested enhancement
- **Explain why this enhancement would be useful**
- **List any alternative solutions** you've considered

### Pull Requests

We actively welcome your pull requests:

1. Fork the repo and create your branch from `main`
2. If you've added code, add tests
3. If you've changed APIs, update the documentation
4. Ensure the test suite passes
5. Make sure your code follows the existing style
6. Submit your pull request!

## Getting Started

### Prerequisites

- **.NET 8.0 SDK or later** - [Download](https://dotnet.microsoft.com/download)
- **.NET 10.0 SDK** (optional, for multi-targeting) - [Download](https://dotnet.microsoft.com/download)
- **Git** - [Download](https://git-scm.com/downloads)
- **Your favorite IDE**:
  - Visual Studio 2022 (17.8+)
  - JetBrains Rider
  - Visual Studio Code with C# extension

### Cloning the Repository

```bash
git clone https://github.com/byte2pixel/serilog-sinks-file-encrypt.git
cd serilog-sinks-file-encrypt
```

## Development Setup

### Restore Tools

This project uses [Cake](https://cakebuild.net/) for build automation:

```bash
dotnet tool restore
```

### Restore NuGet Packages

The build system will restore packages automatically, but you can do it manually:

```bash
dotnet restore
```

## Building the Project

### Using Cake (Recommended)

```bash
# Build the entire solution
dotnet make

# Build in Debug mode
dotnet make --configuration Debug

# Clean build artifacts
dotnet make clean
```

### Using .NET CLI

```bash
# Build the solution
dotnet build

# Build a specific project
dotnet build src/Serilog.Sinks.File.Encrypt/Serilog.Sinks.File.Encrypt.csproj
```

## Running Tests

### Run All Tests

```bash
# Using Cake (runs lint and build first, collects coverage by default)
dotnet make test

# Using .NET CLI
dotnet test
```

**Note**: The Cake build collects code coverage by default. If you want to skip coverage collection for faster test runs:

```bash
dotnet make test --collect-coverage=false
```

### Run Specific Tests

```bash
# Run tests for a specific project
dotnet test test/Serilog.Sinks.File.Encrypt.Tests/Serilog.Sinks.File.Encrypt.Tests.csproj

# Run a specific test
dotnet test --filter "FullyQualifiedName~EncryptedStreamTests.SingleFlush_DoesNotThrow"
```

### Running Benchmarks

```bash
cd examples/Example.Benchmarks
dotnet run -c Release
```

## Code Style

This project uses strict code formatting rules to maintain consistency.

### Formatting Rules

- **C# Language Version**: 14 (latest)
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Enabled
- **Formatter**: CSharpier (verified during build)
- **Indentation**: Tabs/Spaces as configured in .editorconfig
- **Line Endings**: LF (Unix-style)

### Code Formatting

The build automatically verifies code formatting. To check formatting:

```bash
# Check formatting (this is done automatically during build)
dotnet make lint

# Format code (if you have CSharpier installed)
dotnet csharpier .
```

### Coding Conventions

- Follow standard .NET naming conventions
- Use meaningful variable and method names
- Add XML documentation comments to all public APIs
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
- Keep methods focused and concise
- Prefer async/await for I/O operations
- Use `ConfigureAwait(false)` in library code

### Example

```csharp
/// <summary>
/// Encrypts data written to the underlying stream using AES encryption.
/// </summary>
/// <param name="buffer">The buffer containing data to write.</param>
/// <param name="offset">The zero-based byte offset in buffer from which to begin copying bytes.</param>
/// <param name="count">The maximum number of bytes to write.</param>
/// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
/// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
public override void Write(byte[] buffer, int offset, int count)
{
    ObjectDisposedException.ThrowIf(_isDisposed, this);
    
    // Implementation...
}
```

## Submitting Changes

### Branch Naming

Use descriptive branch names:

- `feature/add-key-rotation` - New features
- `fix/handle-corrupted-headers` - Bug fixes
- `docs/improve-readme` - Documentation updates
- `test/add-cli-tests` - Test additions

### Commit Messages

Write clear commit messages:

```
Add support for 4096-bit RSA keys

- Update EncryptionUtils to accept key size parameter
- Add tests for different key sizes
- Update CLI tool to support --key-size option

Closes #123
```

**Format:**
- First line: Short summary (50 chars or less)
- Blank line
- Detailed description (wrap at 72 chars)
- Reference issues/PRs

### Pull Request Process

1. **Update documentation** if you've changed APIs or added features
2. **Add tests** for any new functionality
3. **Ensure CI passes** - all tests must pass and code must be formatted
4. **Request review** from maintainers
5. **Address feedback** promptly
6. **Linear history** - rebase/squash commits if necessary

### PR Checklist

- [ ] Code builds without errors or warnings
- [ ] All tests pass
- [ ] Code formatting is correct (`dotnet make lint` passes)
- [ ] New code has XML documentation
- [ ] Tests added for new functionality
- [ ] Breaking changes documented

## Project Structure

```
serilog-sinks-file-encrypt/
├── src/
│   ├── Serilog.Sinks.File.Encrypt/          # Main library
│   └── Serilog.Sinks.File.Encrypt.Cli/      # CLI tool
├── test/
│   └── Serilog.Sinks.File.Encrypt.Tests/    # Unit tests
├── examples/
│   ├── Example.Console/                      # Console example
│   └── Example.Benchmarks/                   # Performance benchmarks
├── resources/
│   └── nuget/                                # NuGet package documentation
├── build.cs                                  # Cake build script
└── global.json                               # .NET SDK version
```

## Testing Guidelines

### Unit Tests

- Use **xUnit** for test framework
- Use **Shouldly** for assertions
- Test one concern per test method
- Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- Arrange-Act-Assert pattern

### Integration Tests

- Place in `FileSinkIntegrationTests.cs`
- Clean up resources in `Dispose()`
- Use temporary directories for file operations
- Test realistic scenarios

### Example Test

```csharp
[Fact]
public async Task DecryptLogFileAsync_WithValidKey_ReturnsDecryptedContent()
{
    // Arrange
    const string testMessage = "Test log message";
    (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
    
    // Act
    // ... encryption and decryption logic
    
    // Assert
    result.ShouldBe(testMessage);
}
```

## Package Publishing

Package publishing is automated through GitHub Actions when a release is created:

1. Create a new release on GitHub
2. CI will build and publish to NuGet automatically
3. Version is determined by MinVer from git tags

Only maintainers can publish packages.

## Additional Resources

- [Serilog Documentation](https://github.com/serilog/serilog/wiki)
- [.NET Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Git Commit Best Practices](https://chris.beams.io/posts/git-commit/)

## Questions?

Feel free to open an issue with your question or reach out to the maintainers.

---

Thank you for contributing! 🎉

