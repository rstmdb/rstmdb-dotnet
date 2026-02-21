# Contributing to Rstmdb.Client

Thank you for your interest in contributing to the official .NET client for rstmdb.

## Getting Started

### Prerequisites

- [.NET 7.0 SDK](https://dotnet.microsoft.com/download) or later
- A running rstmdb server (for manual testing)

### Building

```bash
git clone https://github.com/rstmdb/rstmdb-dotnet.git
cd rstmdb-dotnet
dotnet build
```

### Running Tests

```bash
dotnet test --verbosity normal
```

All tests use an in-process TCP mock server and do not require a running rstmdb instance.

## Development Workflow

1. Fork the repository and create a feature branch from `main`.
2. Make your changes.
3. Ensure `dotnet build` completes with zero warnings and zero errors.
4. Ensure `dotnet test` passes all tests.
5. Add tests for any new functionality or bug fixes.
6. Open a pull request against `main`.

## Project Structure

| Path | Description |
|------|-------------|
| `src/Rstmdb.Client/Protocol/` | RCPX binary framing, wire message types, operation constants |
| `src/Rstmdb.Client/Transport/` | TCP connection, multiplexing, subscription streaming |
| `src/Rstmdb.Client/Models/` | Request/response model types |
| `src/Rstmdb.Client/Errors/` | Error codes and typed exception |
| `src/Rstmdb.Client/RstmdbClient.cs` | Public API surface |
| `tests/Rstmdb.Client.Tests/` | Unit and integration tests |
| `examples/` | Example applications |

## Coding Conventions

- **Naming**: PascalCase for public members, `_camelCase` for private fields.
- **Nullability**: Nullable reference types are enabled. Use `?` annotations where appropriate.
- **JSON mapping**: Use `[JsonPropertyName("snake_case")]` on all model properties.
- **Async**: Every public async method should accept `CancellationToken ct = default`.
- **Disposal**: Types owning unmanaged or connection resources implement `IAsyncDisposable`.
- **Dependencies**: Avoid adding NuGet dependencies. The client currently has zero external dependencies.

## Adding a New Operation

1. Add the operation constant to `Protocol/Operations.cs`.
2. Add request/response model types in `Models/`.
3. Add the public method to `RstmdbClient.cs`.
4. Add a handler case in `tests/MockServer.cs` and a test in `ClientIntegrationTests.cs`.

## Reporting Issues

Please open an issue on GitHub with:

- A clear description of the problem or feature request.
- Steps to reproduce (if applicable).
- Your .NET SDK version (`dotnet --version`).

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
