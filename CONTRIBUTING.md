# Contributing to Blink

Thanks for taking the time to improve Blink. This project is small on purpose, so a
few ground rules keep it easy to review and maintain.

## Before you start

- Search existing issues and pull requests before opening a new one.
- For anything larger than a bug fix, open an issue first so the approach can be
  agreed on before code is written.
- Never commit secrets, tokens, personal paths or machine-specific configuration.

## Development setup

You need Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/iammohitraj/Blink.git
cd Blink

dotnet restore Blink.sln
dotnet build   Blink.sln -c Release
dotnet test    Blink.sln -c Release
```

Run the app from source:

```powershell
dotnet run --project src\Blink\Blink.csproj
```

## Repository rules

- All commits in this repository must be authored and committed as
  **Mohit Raj `<321290802+iammohitraj@users.noreply.github.com>`**. The maintainer
  pushes through the `github-iammohitraj` SSH alias; contributors should fork the
  repository and open a pull request instead of pushing to it directly.
- Keep the version in `Directory.Build.props` as the only place a release number lives.
- Do not add third-party runtime dependencies without discussing it first. `Blink.Core`
  is intentionally dependency-free.

## Code style

- Follow the existing file layout and naming.
- Keep the watermark line at the top of every C# file.
- Prefer clarity over cleverness. Comments should explain *why*, not *what*.
- Use `Span<T>`, `ReadOnlySpan<T>` and `ArrayPool<T>` on hot paths, but do not
  complicate cold code for a theoretical allocation.
- Public types in `Blink.Core` need XML documentation and a complexity note when the
  cost is not obvious.
- Run `dotnet format Blink.sln` before opening a pull request.

## Tests

- Every new data structure or algorithm needs tests for the empty, single-element,
  duplicate, ordered and random cases as applicable.
- Bug fixes should come with a regression test.
- Tests must pass on a clean checkout:

```powershell
dotnet test Blink.sln -c Release
```

## Pull requests

- Keep the change focused; split unrelated work into separate pull requests.
- Describe the problem and the chosen solution, not just the diff.
- Mention how you verified the change. For UI work, include a screenshot or say
  explicitly that you could not test it.
- Update `CHANGELOG.md` for user-visible changes.
- Update the documentation when behaviour, commands or architecture change.

## Reporting bugs

Use the bug report form and include:

- Windows version and Blink version (Settings footer).
- Exact steps to reproduce.
- What you expected and what happened.
- Any relevant log output or screenshots.

Security issues must not be reported publicly. See [SECURITY.md](SECURITY.md).
