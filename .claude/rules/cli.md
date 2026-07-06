---
paths:
  - "src/Serilog.Sinks.File.Encrypt.Cli/**"
  - "tests/Serilog.Sinks.File.Encrypt.Cli.Tests/**"
---

# CLI project rules

The CLI ships as the `serilog-encrypt` tool, built on **Spectre.Console.Cli** with
Microsoft.Extensions.DependencyInjection.

## Structure

- `Program.cs` is intentionally minimal: build registrar → `CommandApp` → run. All setup
  lives in `Infrastructure/CommandAppConfiguration.cs` (command registration) and
  `Infrastructure/ServiceCollectionExtensions.AddCliServices()` (DI registrations).
- `Infrastructure/TypeRegistrar.cs` / `TypeResolver.cs` bridge Spectre.Console.Cli and
  MS.DI — commands and their dependencies are constructor-injected.
- Commands live in `Commands/` (`GenerateCommand`, `DecryptCommand`); supporting logic goes
  in `Services/` behind an interface (`IInputResolver`/`IOutputResolver` pattern).

## Conventions

- **All file access goes through `System.IO.Abstractions.IFileSystem`** (injected; tests pass
  a mock via `CommandAppConfiguration.CreateRegistrar(fileSystem)`). Never use `System.IO`
  types directly in CLI code.
- Command registration uses `.WithDescription()` and `.WithExample()`, and
  `ValidateExamples()` is enabled — every example must remain a parseable invocation, and
  new options should come with an example.
- Adding a command: create it in `Commands/`, register services in `AddCliServices`, add it
  in `CommandAppConfiguration.GetConfiguration()` with description + examples, and update
  `resources/nuget/Serilog.Sinks.File.Encrypt.Cli.md`.
- Console output uses Spectre.Console markup; command return values are process exit codes
  (0 = success, non-zero = failure).
