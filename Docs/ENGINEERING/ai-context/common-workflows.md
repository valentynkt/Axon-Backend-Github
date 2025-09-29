# Common Development Workflows

**Step-by-step workflows for standard tasks.**

---

**STATUS**: 🚧 Scaffold - Needs Content
**PRIORITY**: Low
**LAST_UPDATED**: 2025-01-29

---


## Content to be filled:

### Workflow: Add New Command
1. Define command record (ICommand<Result<T, Error>>)
2. Create request DTO in Api/Contracts
3. Add FluentValidation validator
4. Implement command handler
5. Add domain logic to aggregate
6. Create FastEndpoints endpoint
7. Write unit tests (handler, aggregate)
8. Write integration tests (endpoint)

### Workflow: Add New Query
1. Define query record (IQuery<Result<T, Error>>)
2. Create response DTO
3. Implement query handler
4. Add repository method if needed
5. Create FastEndpoints endpoint
6. Add caching if appropriate
7. Write tests

### Workflow: Add New Module
1. Create module folder structure (Domain/Application/Infrastructure)
2. Define aggregates and value objects
3. Implement commands and queries
4. Add API endpoints
5. Create database migrations
6. Write comprehensive tests
7. Document in modules/{name}/

### Workflow: Integrate External Service
1. Create adapter in Infrastructure
2. Define interfaces in Application
3. Implement Polly resilience policies
4. Add configuration
5. Write integration tests with mocks
6. Document in integrations/{service}/

### Workflow: Fix Performance Issue
1. Identify bottleneck (profiling, logs, metrics)
2. Review query optimization guide
3. Add indexes if needed
4. Implement caching if appropriate
5. Benchmark before/after with BenchmarkDotNet
6. Update performance docs
