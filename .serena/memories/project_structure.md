# Project Structure

```
src/
  Api/                           # HTTP host (Minimal API), composition root (DI)
    Endpoints/                   # Feature-based endpoints
    Contracts/                   # Request/response DTOs  
    Program.cs                   # Entry point
  Shared/                        # Technical primitives only
    Common/                      # Result, Error, ValueObject, Strong IDs
    Common.Abstractions/         # Technical interfaces (IClock, etc.)
  Modules/                       # Business modules (bounded contexts)
    Chat/                        # Example module
      Application/               # Commands/queries/ports/validators
      Domain/                    # Aggregates/entities/VOs/policies
      Infrastructure/            # Adapters implementing ports
tests/                          # Mirrors production structure
Docs/                           # Architecture decisions, contracts, features
  adr/                          # Architecture Decision Records
  contracts/                    # API contracts per module
  features/                     # Feature documentation
  Claude/                       # Claude-specific guidance
```

## Key Files
- `Directory.Build.props` - Global MSBuild properties (TFM, analyzers)
- `global.json` - .NET SDK version pinning with preview support
- `Axon.Backend.slnx` - Solution file
- `CLAUDE.md` - Project-specific Claude instructions