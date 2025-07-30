# Essential Development Commands

## Build & Test Commands
```bash
# Build entire solution (warnings = errors)
dotnet build

# Run API locally
dotnet run --project src/Api

# Run all tests
dotnet test

# Check .NET version
dotnet --version
```

## Development Workflow
```bash
# Restore NuGet packages
dotnet restore

# Clean build artifacts
dotnet clean

# Build in release mode
dotnet build --configuration Release

# Publish for deployment
dotnet publish --configuration Release
```

## System Commands (macOS)
```bash
# Git operations
git status
git add .
git commit -m "message"

# File operations
ls -la                    # List files with permissions
find . -name "*.cs"       # Find C# files
grep -r "pattern" src/    # Search in source code

# Directory navigation
cd src/Api               # Change directory
pwd                      # Print working directory
```

## Project-Specific Notes
- Uses `Axon.Backend.slnx` format (newer solution format)
- Build currently has errors in `Directory.Build.targets` 
- Requires .NET 10.0.100-preview.5.25277.114 or compatible