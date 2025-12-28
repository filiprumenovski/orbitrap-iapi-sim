#!/bin/bash
# Orbitrap IAPI Simulator - Test Runner
#
# Prerequisites:
#   - .NET 10 SDK (or newer): https://dotnet.microsoft.com/download
#   - macOS: brew install dotnet-sdk
#
# Usage:
#   ./run-tests.sh          # Run all tests
#   ./run-tests.sh -v       # Verbose output
#   ./run-tests.sh --filter "ClassName"  # Filter tests

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Check for dotnet
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK not found"
    echo ""
    echo "Please install .NET 10 SDK (or newer):"
    echo "  macOS:   brew install dotnet-sdk"
    echo "  or:      https://dotnet.microsoft.com/download"
    exit 1
fi

echo "=========================================="
echo "  Orbitrap IAPI Simulator - Test Suite"
echo "=========================================="
echo ""

# Check .NET version
echo "Using .NET SDK: $(dotnet --version)"
echo ""

# Restore packages
echo "Restoring packages..."
dotnet restore Orbitrap.sln --verbosity quiet

# Build
echo "Building solution..."
dotnet build Orbitrap.sln --configuration Release --no-restore --verbosity quiet

# Run tests
echo ""
echo "Running tests..."
echo "------------------------------------------"

dotnet test Orbitrap.sln \
    --configuration Release \
    --no-build \
    --logger "console;verbosity=normal" \
    --results-directory ./TestResults \
    "$@"

echo ""
echo "=========================================="
echo "  Test run complete!"
echo "=========================================="
