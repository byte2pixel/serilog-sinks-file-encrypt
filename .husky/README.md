# Husky Git Hooks

This directory contains Git hooks managed by [Husky.NET](https://alirezanet.github.io/Husky.Net/).

## What are Git Hooks?

Git hooks are scripts that run automatically when certain Git events occur (like committing, pushing, etc.).

## Configured Hooks

### pre-commit
Runs **before** a commit is created.

**What it does:**
- Runs CSharpier on the staged files to ensure code formatting.
- Changes are included in the commit if formatting is applied.

**If you prefer to format locally first:**
```bash
dotnet tool restore
dotnet csharpier check .
dotnet csharpier format .
git add -A
git commit -m "Your message"
```

## Setup for New Contributors

When you clone the repository and run `dotnet tool restore`, Husky hooks are automatically installed.

If hooks don't work, reinstall them:
```bash
dotnet husky install
```

## Task Runner

The `task-runner.json` file defines reusable tasks:

- **format-csharpier**: Runs CSharpier formatting (invoked by pre-commit)
- **build-check**: Verifies solution builds (runs on pre-push)

## Why Use Hooks?

✅ **Consistency**: All code is formatted before it enters the repository  
✅ **Quality**: Catch issues early, before CI runs  
✅ **Speed**: Fast local checks save time in code review  
✅ **Automation**: No need to remember to format or setup formatters manually
