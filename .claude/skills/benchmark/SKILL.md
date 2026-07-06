---
name: benchmark
description: Run the BenchmarkDotNet suites in Example.Benchmarks and compare before/after results. Use when a change touches crypto hot paths (LogWriter, Writers/, Core) or when asked to benchmark or update README performance numbers.
---

# Run and compare benchmarks

Benchmarks live in `examples/Example.Benchmarks` (excluded from the Cake build — build/run
them directly). The entry point is an interactive menu; pipe the selection in:

```
"1" | dotnet run -c Release --project examples/Example.Benchmarks
```

Selections: `1` EncryptedLogStream (low-level, the usual one for crypto changes),
`2` SerilogFileSink comparison, `3` WebApiRequest, `4` BackgroundWorker, `5` all.
Always `-c Release`; Debug numbers are meaningless. Results and reports land in
`BenchmarkDotNet.Artifacts/` under the current directory.

## Comparing before/after

1. Run the relevant suite on the **baseline** (main): `git stash` or use a worktree
   (`git worktree add`) so the branch code is not mixed in. Save the summary table.
2. Run the same suite on the branch. Same machine, same power state.
3. Report Mean, Allocated, and Gen0/1/2 side by side, with % delta. Call out anything
   > ~5% Mean or any allocation growth on the per-log-event paths.

## Ground rules

- **Idle machine only** for numbers that will be published: no builds, browsers, or
  background work during the run. Note machine state when reporting.
- The README's benchmark tables are refreshed from idle-machine runs (see commit `a54897c`)
  — never update them from a noisy run, and only update them when asked.
- A full suite takes several minutes; run it in the background and check the output file.
- The project targets net8.0 + net10.0, so `dotnet run` needs `-f net10.0` (or `net8.0`) to
  pick a framework — state which one the numbers came from.
