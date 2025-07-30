# Dependency Rules (ENFORCED)

## Allowed Dependencies
```
Api ───► Modules.*.Application + Shared.*
Modules.*.Application ───► Modules.*.Domain + Shared.*  
Modules.*.Infrastructure ─► Modules.*.Application
```

## Forbidden Dependencies
- ❌ `Api` → `Domain` (never)
- ❌ Cross-module references (use integration events)
- ❌ `Shared/*` → `Modules/*` (shared must be generic)
- ❌ Business code in `Shared/*`

## Per Module Example (Chat)
- `Api` can reference `Modules.Chat.Application` only
- `Modules.Chat.Application` can reference `Modules.Chat.Domain` and `Shared.*`
- `Modules.Chat.Infrastructure` implements `Application` ports
- No direct `Chat` → `Portfolio` references (use events)

## DI Composition
- `Api` is the **only** place wiring implementations to Application ports
- Infrastructure exposes `ServiceRegistration` consumed by `Api`