---
paths:
  - "examples/**"
---

# Examples and benchmarks rules

- `examples/` is **intentionally excluded** from the Cake build (`build.cs` builds only
  `src/**` and `tests/**`): example dependencies' vulnerability advisories or warnings must
  not fail CI. Don't add examples to the pipeline; build them manually when you touch them:
  `dotnet build examples/<Project>`.
- Examples have their own `Directory.Build.props`/`Directory.Packages.props` tree — example
  package versions are managed there, separately from `src/`.
- `Example.Console` and `Example.WebApi` are runnable smoke tests of the public API
  (`dotnet run` in the project directory). `KeyService` loads an embedded `public_key.xml`
  resource — keys are baked in, nothing to generate.

## Benchmarks

- Run from `examples/Example.Benchmarks`: `dotnet run -c Release` — interactive menu with
  four BenchmarkDotNet suites (EncryptedLogStream, SerilogFileSink, WebApiRequest,
  BackgroundWorker) plus run-all. Always Release; Debug results are meaningless.
- Run benchmarks when touching crypto hot paths: `LogWriter`, the frame/header/session
  writers, or anything in Core called per log event.
- The README's benchmark tables are refreshed from **idle-machine** runs (see commit
  `a54897c`) — don't update them from a run taken while other work was executing, and
  mention machine state when reporting numbers.
