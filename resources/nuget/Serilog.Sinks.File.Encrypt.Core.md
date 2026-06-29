# Serilog.Sinks.File.Encrypt.Core

[![Build Status](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml/badge.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/actions/workflows/ci.yaml)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.File.Encrypt.Core.svg)](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/byte2pixel/serilog-sinks-file-encrypt/blob/main/LICENSE.md)

Shared cryptographic primitives for [Serilog.Sinks.File.Encrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Encrypt) and [Serilog.Sinks.File.Decrypt](https://www.nuget.org/packages/Serilog.Sinks.File.Decrypt).

> [!NOTE]
> This package is not intended to be referenced directly. It is automatically included as a transitive dependency when you install either `Serilog.Sinks.File.Encrypt` or `Serilog.Sinks.File.Decrypt`.

## Versioning

All packages in this repository (`Serilog.Sinks.File.Encrypt`, `Serilog.Sinks.File.Decrypt`, `Serilog.Sinks.File.Encrypt.Cli`, `Serilog.Sinks.File.Encrypt.Core`) are released in lockstep. Every package is versioned and published together on every release, even when a change only affects one of them. Always use the same version across all packages you reference.

## What's Included

- `CryptographicUtils.GenerateRsaKeyPair` — generates RSA key pairs for use with the encrypt/decrypt packages
- `CryptographicUtils.FromString` — RSA key import extension for XML and PEM formats
- `KeyFormat` — enum for specifying XML or PEM key format
