# 18. Source Tree (Module)

```
src/Modules/Identity/
├── API/
│   ├── Endpoints/
│   │   ├── Auth/
│   │   │   ├── ExchangeEndpoint.cs        # POST /auth/exchange
│   │   │   └── MeEndpoint.cs              # GET /auth/me
│   ├── Contracts/                         # request/response DTOs
│   └── Swagger/Scalar/                    # OpenAPI generation & UI
├── Application/
│   ├── Commands/
│   │   └── ExchangeCredential/
│   │       ├── ExchangeCredentialCommand.cs
│   │       ├── ExchangeCredentialHandler.cs
│   │       └── ExchangeUserData.cs        # normalized claims+wallets DTO (no contact identifiers)
│   ├── Queries/
│   │   └── GetMyPrincipal/
│   │       ├── GetMyPrincipalQuery.cs
│   │       └── GetMyPrincipalHandler.cs
│   ├── Contracts/
│   │   └── Persistence/                   # interfaces + additions
│   └── Validation/                        # FluentValidators
├── Domain/
│   ├── Aggregates/
│   │   ├── AxonPrincipal/...
│   │   └── Wallet/...
│   ├── Entities/ ValueObjects/ Enums/
│   └── Abstractions/ Results/
├── Infrastructure/
│   ├── Persistence/
│   │   ├── IdentityWriteDbContext.cs
│   │   ├── IdentityReadDbContext.cs
│   │   ├── Configurations/                # EF model configs (indices, partial unique)
│   │   ├── Repositories/                  # EF implementations
│   │   └── Migrations/
│   ├── Auth/
│   │   ├── JwtValidator.cs                # JwtBearerHandler config helpers
│   │   └── JwksCache.cs                   # IMemoryCache policy
│   └── ETags/
│       └── PrincipalFingerprintReader.cs
└── Tests/
    ├── Unit/
    └── Integration/
```

---
