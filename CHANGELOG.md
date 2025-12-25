# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial project structure with .NET 8 and Rust components
- Core abstractions (`IOrbitrapScan`, `IOrbitrapInstrument`, `IAcquisitionSession`)
- `FrozenOrbitrapScan` for thread-safe immutable snapshots
- `ReadOnlyMemory<double>` for zero-copy spectrum data access
- `ScanFilter` for declarative scan filtering
- `ScanResult` for explicit error handling without exceptions
- `MockMsScan` with fluent builder pattern
- `MockOrbitrapInstrument` with Channel-based buffering
- `InstrumentFactory` with dependency injection support
- OpenTelemetry metrics and distributed tracing (`OrbitrapMetrics`, `OrbitrapTracing`)
- Strongly-typed configuration via `IOptions<InstrumentOptions>`
- Rust LC-MS simulator with gRPC server
- Shared protobuf contract (`simulator.proto`)
- Docker support (Dockerfile.simulator, Dockerfile.console, docker-compose.yml)
- GitHub Actions CI/CD (ci.yml, release.yml, pr-check.yml)
- Dependabot configuration for automated dependency updates
- Comprehensive test suite (99 tests across 3 projects)
- `justfile` and `Makefile` for build automation
- `.editorconfig` for consistent code formatting
- `Directory.Build.props` for shared MSBuild settings

### Architecture

- Thin waist pattern: all downstream code depends only on `IOrbitrapScan`
- Adapter pattern: `MockScanAdapter` and `RealScanAdapter` converge to common interface
- Factory pattern: `InstrumentFactory.Create()` abstracts mock/real selection
- Async streams: `IAsyncEnumerable<IOrbitrapScan>` with backpressure handling

## [0.1.0] - TBD

- Initial release

---

[Unreleased]: https://github.com/yourusername/orbitrap-iapi-sim/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/yourusername/orbitrap-iapi-sim/releases/tag/v0.1.0
