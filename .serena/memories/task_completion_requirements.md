# Task Completion Requirements

## Always Follow When Task is Complete

### 1. Build Verification
```bash
dotnet build                # Must pass with zero warnings (TreatWarningsAsErrors=true)
```

### 2. Test Execution  
```bash
dotnet test                 # All tests must pass
```

### 3. Code Quality Checks
- **Analyzers**: Latest analysis level enabled, must pass
- **Nullable**: All nullable warnings resolved
- **Documentation**: XML docs generated for libraries (not API)

### 4. Architecture Compliance
- Verify dependency rules are not violated
- Ensure no cross-module references
- Check that business logic stays out of `Shared/*`
- Confirm proper layer separation (Api → Application → Domain)

### 5. Development Workflow (Top-Down, Slice-First)
1. **Planning Phase**: Create feature docs (ONE_PAGER.md, API_CONTRACT.md, etc.)
2. **Implementation**: Skeleton → MVP slice → Tests
3. **Files Pattern**: Follow prescribed folder structure per module

### 6. Gate Workflow (If Using Claude Agents)
- **G1**: Requirements, Architecture, Task Plan artifacts
- **G2**: Implementation + Test coverage
- **G3**: Review + Policy compliance  
- **Ship**: PR body + decision log updates

### 7. Before Committing
- Code follows modern C# standards
- No `TODO` or `FIXME` comments left unaddressed
- Appropriate error handling using Result pattern
- Clean, focused commits with descriptive messages