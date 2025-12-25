# Orbitrap IAPI Simulator - Justfile
# https://github.com/casey/just
#
# Usage:
#   just              # List available recipes
#   just build        # Build all projects
#   just test         # Run all tests
#   just run          # Run the sample console app

set dotenv-load := false
set shell := ["bash", "-cu"]

# Default recipe - show help
default:
    @just --list

# ============================================================================
# BUILD
# ============================================================================

# Build all .NET projects
build:
    dotnet build Orbitrap.sln --configuration Release

# Build in debug mode
build-debug:
    dotnet build Orbitrap.sln --configuration Debug

# Clean build artifacts
clean:
    dotnet clean Orbitrap.sln
    rm -rf **/bin **/obj TestResults coverage

# Restore NuGet packages
restore:
    dotnet restore Orbitrap.sln

# Full rebuild (clean + restore + build)
rebuild: clean restore build

# ============================================================================
# TEST
# ============================================================================

# Run all tests
test:
    dotnet test Orbitrap.sln --configuration Release --logger "console;verbosity=normal"

# Run tests with coverage
test-coverage:
    dotnet test Orbitrap.sln \
        --configuration Release \
        --collect:"XPlat Code Coverage" \
        --results-directory ./coverage \
        --logger "console;verbosity=normal"
    @echo "Coverage reports saved to ./coverage"

# Run specific test project
test-abstractions:
    dotnet test tests/Orbitrap.Abstractions.Tests --logger "console;verbosity=normal"

test-mock:
    dotnet test tests/Orbitrap.Mock.Tests --logger "console;verbosity=normal"

test-integration:
    dotnet test tests/Orbitrap.Integration.Tests --logger "console;verbosity=normal"

# Run tests with filter
test-filter filter:
    dotnet test Orbitrap.sln --filter "{{filter}}" --logger "console;verbosity=normal"

# Watch tests (re-run on file changes)
test-watch:
    dotnet watch test --project tests/Orbitrap.Mock.Tests

# ============================================================================
# RUN
# ============================================================================

# Run the sample console application
run:
    dotnet run --project samples/Orbitrap.Console

# Run console app in release mode
run-release:
    dotnet run --project samples/Orbitrap.Console --configuration Release

# ============================================================================
# RUST SIMULATOR
# ============================================================================

# Build the Rust simulator
rust-build:
    cd src/rust/lc-ms-simulator && cargo build --release

# Run the Rust simulator
rust-run port="31417":
    cd src/rust/lc-ms-simulator && cargo run --release -- --port {{port}}

# Run Rust tests
rust-test:
    cd src/rust/lc-ms-simulator && cargo test

# Clean Rust artifacts
rust-clean:
    cd src/rust/lc-ms-simulator && cargo clean

# Check Rust formatting
rust-fmt-check:
    cd src/rust/lc-ms-simulator && cargo fmt --check

# Format Rust code
rust-fmt:
    cd src/rust/lc-ms-simulator && cargo fmt

# Run Rust clippy lints
rust-lint:
    cd src/rust/lc-ms-simulator && cargo clippy -- -D warnings

# ============================================================================
# CODE QUALITY
# ============================================================================

# Format all C# code
format:
    dotnet format Orbitrap.sln

# Check C# formatting without changing files
format-check:
    dotnet format Orbitrap.sln --verify-no-changes

# Run all lints (C# and Rust)
lint: format-check rust-fmt-check rust-lint

# ============================================================================
# PACKAGING
# ============================================================================

# Pack NuGet packages
pack:
    dotnet pack Orbitrap.sln --configuration Release --output ./artifacts

# Publish for current runtime
publish:
    dotnet publish samples/Orbitrap.Console --configuration Release --output ./publish

# Publish self-contained for specific runtime
publish-self-contained rid="osx-arm64":
    dotnet publish samples/Orbitrap.Console \
        --configuration Release \
        --runtime {{rid}} \
        --self-contained true \
        --output ./publish/{{rid}}

# ============================================================================
# DOCKER
# ============================================================================

# Build Docker image for Rust simulator
docker-build-simulator:
    docker build -t orbitrap-simulator:latest -f docker/Dockerfile.simulator .

# Run Rust simulator in Docker
docker-run-simulator port="31417":
    docker run -p {{port}}:31417 orbitrap-simulator:latest

# ============================================================================
# DEVELOPMENT
# ============================================================================

# Setup development environment
setup:
    @echo "Checking prerequisites..."
    @command -v dotnet >/dev/null 2>&1 || { echo "❌ .NET SDK not found. Install from https://dotnet.microsoft.com/download"; exit 1; }
    @command -v cargo >/dev/null 2>&1 || { echo "⚠️  Rust not found. Install from https://rustup.rs (optional)"; }
    @echo "✅ Prerequisites checked"
    @echo "Restoring packages..."
    dotnet restore Orbitrap.sln
    @echo "✅ Setup complete"

# Show project info
info:
    @echo "=== .NET ==="
    @dotnet --version
    @echo ""
    @echo "=== Projects ==="
    @dotnet sln Orbitrap.sln list
    @echo ""
    @echo "=== Rust (if available) ==="
    @cargo --version 2>/dev/null || echo "Rust not installed"

# Open solution in default IDE
ide:
    @if command -v rider >/dev/null 2>&1; then rider Orbitrap.sln; \
    elif command -v code >/dev/null 2>&1; then code .; \
    elif [ -d "/Applications/Visual Studio.app" ]; then open -a "Visual Studio" Orbitrap.sln; \
    else echo "No supported IDE found"; fi

# ============================================================================
# CI/CD HELPERS
# ============================================================================

# Full CI pipeline (what CI runs)
ci: restore build test lint

# Release build with all checks
release-build: ci pack

# Verify the project builds on clean checkout
verify:
    #!/usr/bin/env bash
    set -euo pipefail
    echo "🔍 Verifying clean build..."
    just clean
    just restore
    just build
    just test
    echo "✅ Verification complete"
