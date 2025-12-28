# Orbitrap IAPI Simulator - Makefile
# For systems without `just` installed
#
# Usage:
#   make              # Show help
#   make build        # Build all projects
#   make test         # Run all tests

.PHONY: help build build-debug clean restore rebuild \
        test test-coverage test-watch \
        run run-release \
        rust-build rust-run rust-test rust-clean rust-fmt rust-lint \
        format format-check lint \
        pack publish \
		docker-build-simulator docker-run-simulator docker-build-console docker-run-console docker-compose-up docker-compose-down \
        setup info ci verify

# Default target - show help
help:
	@echo "Orbitrap IAPI Simulator - Available targets:"
	@echo ""
	@echo "BUILD:"
	@echo "  make build          Build all .NET projects (Release)"
	@echo "  make build-debug    Build in Debug mode"
	@echo "  make clean          Clean all build artifacts"
	@echo "  make restore        Restore NuGet packages"
	@echo "  make rebuild        Clean + restore + build"
	@echo ""
	@echo "TEST:"
	@echo "  make test           Run all tests"
	@echo "  make test-virtualorbitrap  Run VirtualOrbitrap tests"
	@echo "  make test-coverage  Run tests with code coverage"
	@echo "  make test-watch     Watch mode (re-run on changes)"
	@echo "  make test-watch-virtualorbitrap  Watch VirtualOrbitrap tests"
	@echo ""
	@echo "RUN:"
	@echo "  make run            Run sample console app"
	@echo "  make run-release    Run in Release mode"
	@echo ""
	@echo "RUST SIMULATOR:"
	@echo "  make rust-build     Build Rust simulator"
	@echo "  make rust-run       Run Rust simulator"
	@echo "  make rust-test      Run Rust tests"
	@echo "  make rust-lint      Run clippy lints"
	@echo ""
	@echo "CODE QUALITY:"
	@echo "  make format         Format C# code"
	@echo "  make format-check   Check C# formatting"
	@echo "  make lint           Run all linters"
	@echo ""
	@echo "PACKAGING:"
	@echo "  make pack           Create NuGet packages"
	@echo "  make publish        Publish console app"
	@echo ""
	@echo "DOCKER:"
	@echo "  make docker-build-simulator  Build Rust simulator image"
	@echo "  make docker-build-console    Build .NET console image"
	@echo "  make docker-compose-up       Start simulator+console"
	@echo "  make docker-compose-down     Stop compose stack"
	@echo ""
	@echo "CI/CD:"
	@echo "  make ci             Full CI pipeline"
	@echo "  make verify         Verify clean build"
	@echo "  make setup          Setup dev environment"

# ============================================================================
# BUILD
# ============================================================================

build:
	dotnet build Orbitrap.sln --configuration Release

build-debug:
	dotnet build Orbitrap.sln --configuration Debug

clean:
	dotnet clean Orbitrap.sln || true
	rm -rf **/bin **/obj TestResults coverage artifacts publish

restore:
	dotnet restore Orbitrap.sln

rebuild: clean restore build

# ============================================================================
# TEST
# ============================================================================

test:
	dotnet test Orbitrap.sln --configuration Release --logger "console;verbosity=normal"

test-virtualorbitrap:
	dotnet test tests/VirtualOrbitrap.Tests --configuration Release --logger "console;verbosity=normal"

test-coverage:
	dotnet test Orbitrap.sln \
		--configuration Release \
		--collect:"XPlat Code Coverage" \
		--results-directory ./coverage \
		--logger "console;verbosity=normal"
	@echo "Coverage reports saved to ./coverage"

test-watch:
	dotnet watch test --project tests/Orbitrap.Mock.Tests

test-watch-virtualorbitrap:
	dotnet watch test --project tests/VirtualOrbitrap.Tests

# ============================================================================
# RUN
# ============================================================================

run:
	dotnet run --project samples/Orbitrap.Console

run-release:
	dotnet run --project samples/Orbitrap.Console --configuration Release

# ============================================================================
# RUST SIMULATOR
# ============================================================================

rust-build:
	cd src/rust/lc-ms-simulator && cargo build --release

rust-run:
	cd src/rust/lc-ms-simulator && cargo run --release -- --port 31417

rust-test:
	cd src/rust/lc-ms-simulator && cargo test

rust-clean:
	cd src/rust/lc-ms-simulator && cargo clean

rust-fmt:
	cd src/rust/lc-ms-simulator && cargo fmt

rust-fmt-check:
	cd src/rust/lc-ms-simulator && cargo fmt --check

rust-lint:
	cd src/rust/lc-ms-simulator && cargo clippy -- -D warnings

# ============================================================================
# CODE QUALITY
# ============================================================================

format:
	dotnet format Orbitrap.sln

format-check:
	dotnet format Orbitrap.sln --verify-no-changes

lint: format-check
	@if command -v cargo >/dev/null 2>&1; then \
		cd src/rust/lc-ms-simulator && cargo fmt --check && cargo clippy -- -D warnings; \
	fi

# ============================================================================
# PACKAGING
# ============================================================================

pack:
	dotnet pack Orbitrap.sln --configuration Release --output ./artifacts

publish:
	dotnet publish samples/Orbitrap.Console --configuration Release --output ./publish

# ============================================================================
# DOCKER
# ============================================================================

docker-build-simulator:
	docker build -t orbitrap-simulator:latest -f docker/Dockerfile.simulator .

docker-build-console:
	docker build -t orbitrap-console:latest -f docker/Dockerfile.console .

docker-run-simulator:
	docker run -p 31417:31417 orbitrap-simulator:latest

docker-run-console:
	docker run --rm -it --network host orbitrap-console:latest

docker-compose-up:
	docker compose -f docker/docker-compose.yml up --build

docker-compose-down:
	docker compose -f docker/docker-compose.yml down

# ============================================================================
# DEVELOPMENT
# ============================================================================

setup:
	@echo "Checking prerequisites..."
	@command -v dotnet >/dev/null 2>&1 || { echo "❌ .NET SDK not found"; exit 1; }
	@echo "✅ .NET SDK found: $$(dotnet --version)"
	@command -v cargo >/dev/null 2>&1 && echo "✅ Rust found: $$(cargo --version)" || echo "⚠️  Rust not found (optional)"
	@echo "Restoring packages..."
	@dotnet restore Orbitrap.sln
	@echo "✅ Setup complete"

info:
	@echo "=== .NET ==="
	@dotnet --version
	@echo ""
	@echo "=== Solution ==="
	@dotnet sln Orbitrap.sln list
	@echo ""
	@echo "=== Rust ==="
	@cargo --version 2>/dev/null || echo "Not installed"

# ============================================================================
# CI/CD
# ============================================================================

ci: restore build test lint

verify: clean restore build test
	@echo "✅ Verification complete"
