# Part 6: Migration, Verification & QA Matrix Guide

## Overview
Complete migration strategy from current state to target architecture with comprehensive QA verification matrix ensuring all components work correctly.

## Migration Strategy

### Phase 1: BuildingBlocks Foundation (Week 1)

#### 1.1 Implementation Steps
```bash
# Create new BuildingBlocks structure
mkdir -p src/BuildingBlocks/Core/Results
mkdir -p src/BuildingBlocks/Core/Domain
mkdir -p src/BuildingBlocks/Core/CQRS
mkdir -p src/BuildingBlocks/Core/Events
mkdir -p src/BuildingBlocks/Core/Specifications

# Copy new implementations
# Result.cs, Option.cs, Either.cs, Error.cs, Unit.cs
# AggregateRoot.cs, Entity.cs, ValueObject.cs
# ICommand.cs, IQuery.cs, ICommandHandler.cs, IQueryHandler.cs
```

#### 1.2 Verification Checklist
- [ ] All Result<T> methods compile
- [ ] Option<T> conversions work
- [ ] Either<TLeft, TRight> transformations work
- [ ] Error types map to HTTP status codes
- [ ] Domain base classes have proper equality
- [ ] CQRS interfaces are complete
- [ ] Event interfaces support domain events

#### 1.3 Build Verification
```bash
cd src/BuildingBlocks
dotnet build
dotnet test
```

### Phase 2: Chat Domain Layer (Week 1-2)

#### 2.1 Implementation Steps
```bash
# Create domain structure
mkdir -p src/Modules/Chat/Domain/Conversation
mkdir -p src/Modules/Chat/Domain/Conversation/ValueObjects
mkdir -p src/Modules/Chat/Domain/Conversation/Entities
mkdir -p src/Modules/Chat/Domain/Conversation/Events
mkdir -p src/Modules/Chat/Domain/Conversation/Rules
mkdir -p src/Modules/Chat/Domain/Conversation/Services
mkdir -p src/Modules/Chat/Domain/Conversation/Repositories
mkdir -p src/Modules/Chat/Domain/Conversation/Specifications
mkdir -p src/Modules/Chat/Domain/Conversation/Errors

# Implement in order:
# 1. Value Objects (IDs, Title, Content, TokenUsage)
# 2. Entities (Message, Participant)
# 3. Aggregate Root (Conversation)
# 4. Domain Events
# 5. Business Rules
# 6. Domain Services
# 7. Repository Interfaces
```

#### 2.2 Verification Checklist
- [ ] Value objects are immutable
- [ ] Factory methods return Result<T>
- [ ] Aggregate enforces all invariants
- [ ] All state changes raise events
- [ ] Business rules are explicit
- [ ] No infrastructure dependencies

### Phase 3: Chat Application Layer (Week 2)

#### 3.1 Implementation Steps
```bash
# Create application structure
mkdir -p src/Modules/Chat/Application/Commands/StartConversation
mkdir -p src/Modules/Chat/Application/Commands/SendMessage
mkdir -p src/Modules/Chat/Application/Commands/ProcessAiResponse
mkdir -p src/Modules/Chat/Application/Commands/ArchiveConversation
mkdir -p src/Modules/Chat/Application/Queries/GetConversation
mkdir -p src/Modules/Chat/Application/Queries/ListUserConversations
mkdir -p src/Modules/Chat/Application/Queries/SearchConversations
mkdir -p src/Modules/Chat/Application/DTOs
mkdir -p src/Modules/Chat/Application/Mapping
mkdir -p src/Modules/Chat/Application/Behaviors
mkdir -p src/Modules/Chat/Application/Services

# Install packages
dotnet add package MediatR
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

#### 3.2 Verification Checklist
- [ ] All handlers return Result<T>
- [ ] Validators work with pipeline
- [ ] DTOs map correctly
- [ ] Caching behavior works
- [ ] Logging behavior captures all operations
- [ ] Performance behavior tracks metrics

### Phase 4: Chat Infrastructure Layer (Week 2-3)

#### 4.1 Implementation Steps
```bash
# Create infrastructure structure
mkdir -p src/Modules/Chat/Infrastructure/Persistence
mkdir -p src/Modules/Chat/Infrastructure/Persistence/Configurations
mkdir -p src/Modules/Chat/Infrastructure/Persistence/Repositories
mkdir -p src/Modules/Chat/Infrastructure/AI
mkdir -p src/Modules/Chat/Infrastructure/Caching
mkdir -p src/Modules/Chat/Infrastructure/Search
mkdir -p src/Modules/Chat/Infrastructure/Events
mkdir -p src/Modules/Chat/Infrastructure/Observability

# Install packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Dapper
dotnet add package OpenAI
dotnet add package StackExchange.Redis
dotnet add package NEST
dotnet add package Polly
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

#### 4.2 Database Migration
```sql
-- Create schema
CREATE SCHEMA chat;

-- Create tables
CREATE TABLE chat.Conversations (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    StartedBy UNIQUEIDENTIFIER NOT NULL,
    StartedAt DATETIME2 NOT NULL,
    LastMessageAt DATETIME2 NULL,
    ArchivedAt DATETIME2 NULL,
    ArchiveReason NVARCHAR(500) NULL,
    TokenUsage_Input INT NULL,
    TokenUsage_Output INT NULL,
    TokenUsage_Total INT NULL,
    TokenUsage_EstimatedCost DECIMAL(10,4) NULL,
    Metadata NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    RowVersion ROWVERSION NOT NULL
);

CREATE TABLE chat.Messages (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ConversationId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(4000) NOT NULL,
    Role NVARCHAR(20) NOT NULL,
    SentAt DATETIME2 NOT NULL,
    EditedAt DATETIME2 NULL,
    Attachments NVARCHAR(MAX) NULL,
    Metadata NVARCHAR(MAX) NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (ConversationId) REFERENCES chat.Conversations(Id) ON DELETE CASCADE
);

CREATE TABLE chat.Participants (
    ConversationId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Role NVARCHAR(20) NOT NULL,
    JoinedAt DATETIME2 NOT NULL,
    LeftAt DATETIME2 NULL,
    PRIMARY KEY (ConversationId, UserId),
    FOREIGN KEY (ConversationId) REFERENCES chat.Conversations(Id) ON DELETE CASCADE
);

-- Create indexes
CREATE INDEX IX_Conversations_StartedAt ON chat.Conversations(StartedAt);
CREATE INDEX IX_Conversations_Status ON chat.Conversations(Status) WHERE Status = 'Active';
CREATE INDEX IX_Conversations_StartedBy ON chat.Conversations(StartedBy);
CREATE INDEX IX_Messages_ConversationId ON chat.Messages(ConversationId);
CREATE INDEX IX_Messages_UserId ON chat.Messages(UserId);
CREATE INDEX IX_Messages_SentAt ON chat.Messages(SentAt);
CREATE INDEX IX_Participants_UserId ON chat.Participants(UserId);
CREATE INDEX IX_Participants_Active ON chat.Participants(ConversationId, LeftAt) WHERE LeftAt IS NULL;
```

#### 4.3 EF Migration
```bash
cd src/Modules/Chat/Infrastructure
dotnet ef migrations add InitialChatSchema
dotnet ef database update
```

### Phase 5: API Layer (Week 3)

#### 5.1 Implementation Steps
```bash
# Create API structure
mkdir -p src/API/Endpoints/Conversations
mkdir -p src/API/Common
mkdir -p src/API/Hubs
mkdir -p src/API/EventHandlers
mkdir -p src/API/HealthChecks
mkdir -p src/API/Documentation

# Install packages
dotnet add package FastEndpoints
dotnet add package FastEndpoints.Swagger
dotnet add package FastEndpoints.Security
dotnet add package Microsoft.AspNetCore.SignalR
```

#### 5.2 Verification Checklist
- [ ] All endpoints compile
- [ ] Authentication works
- [ ] Rate limiting applied
- [ ] SignalR hub connects
- [ ] Health checks pass
- [ ] Swagger documentation generates

## Comprehensive QA Matrix

### 1. Unit Tests Matrix

| Component | Test Coverage | Critical Tests | Status |
|-----------|--------------|----------------|---------|
| **BuildingBlocks** | | | |
| Result<T> | 100% | Map, Bind, Match, Async operations | ⬜ |
| Option<T> | 100% | Some/None, Map, Bind | ⬜ |
| Either<L,R> | 100% | Left/Right, Map, Bind | ⬜ |
| Error | 100% | HTTP mapping, Metadata | ⬜ |
| **Domain Layer** | | | |
| ConversationId | 100% | Create validation | ⬜ |
| MessageContent | 100% | Length validation, harmful content | ⬜ |
| Conversation | 100% | Start, AddMessage, Archive | ⬜ |
| Business Rules | 100% | All rule validations | ⬜ |
| **Application Layer** | | | |
| Command Handlers | 90% | Success and failure paths | ⬜ |
| Query Handlers | 90% | With/without cache | ⬜ |
| Validators | 100% | All validation rules | ⬜ |
| Pipeline Behaviors | 100% | Validation, logging, caching | ⬜ |

### 2. Integration Tests Matrix

| Test Scenario | Components Tested | Expected Result | Status |
|--------------|-------------------|-----------------|---------|
| **End-to-End Conversation Flow** | | | |
| Start conversation | API → App → Domain → DB | 201 Created | ⬜ |
| Send message | API → App → Domain → DB | 200 OK | ⬜ |
| Process AI response | API → App → OpenAI → DB | 200 OK with AI content | ⬜ |
| Archive conversation | API → App → Domain → DB | 204 No Content | ⬜ |
| **Query Operations** | | | |
| Get conversation | API → App → Cache/DB | 200 with data | ⬜ |
| List conversations | API → App → DB → Paging | 200 with paged data | ⬜ |
| Search conversations | API → App → Elasticsearch | 200 with results | ⬜ |
| **Real-time Operations** | | | |
| Connect to SignalR | Client → Hub | Connected | ⬜ |
| Join conversation | Hub → Groups | Joined group | ⬜ |
| Receive message | Event → Hub → Client | Message received | ⬜ |
| **Error Scenarios** | | | |
| Invalid token | API | 401 Unauthorized | ⬜ |
| Conversation not found | API → App → DB | 404 Not Found | ⬜ |
| Rate limit exceeded | API | 429 Too Many Requests | ⬜ |
| OpenAI unavailable | App → Infrastructure | 503 Service Unavailable | ⬜ |

### 3. Performance Tests Matrix

| Test Case | Target | Measurement | Threshold | Status |
|-----------|--------|-------------|-----------|---------|
| **API Response Times** | | | | |
| Start conversation | P95 | Response time | < 200ms | ⬜ |
| Send message | P95 | Response time | < 150ms | ⬜ |
| Get conversation | P95 | Response time | < 100ms | ⬜ |
| List conversations | P95 | Response time | < 150ms | ⬜ |
| Search conversations | P95 | Response time | < 500ms | ⬜ |
| **AI Operations** | | | | |
| Process AI response | P95 | Response time | < 3000ms | ⬜ |
| Token calculation | P95 | Response time | < 50ms | ⬜ |
| **Database Operations** | | | | |
| Write conversation | P95 | Query time | < 50ms | ⬜ |
| Read conversation | P95 | Query time | < 30ms | ⬜ |
| List with paging | P95 | Query time | < 50ms | ⬜ |
| **Cache Operations** | | | | |
| Cache hit | P95 | Response time | < 10ms | ⬜ |
| Cache miss + DB | P95 | Response time | < 100ms | ⬜ |

### 4. Load Tests Matrix

| Scenario | Users | Duration | Success Rate | Throughput | Status |
|----------|-------|----------|--------------|------------|---------|
| Normal load | 100 | 10 min | > 99.9% | 1000 req/s | ⬜ |
| Peak load | 500 | 5 min | > 99% | 5000 req/s | ⬜ |
| Sustained load | 200 | 60 min | > 99.9% | 2000 req/s | ⬜ |
| Spike test | 1000 | 2 min | > 95% | 10000 req/s | ⬜ |

### 5. Security Tests Matrix

| Test Case | Type | Tool | Expected Result | Status |
|-----------|------|------|-----------------|---------|
| **Authentication** | | | | |
| JWT validation | Unit | Custom | Valid tokens accepted | ⬜ |
| Expired token | Integration | Postman | 401 Unauthorized | ⬜ |
| Invalid signature | Integration | Postman | 401 Unauthorized | ⬜ |
| **Authorization** | | | | |
| User access own data | Integration | Custom | 200 OK | ⬜ |
| User access other's data | Integration | Custom | 403 Forbidden | ⬜ |
| Admin access all data | Integration | Custom | 200 OK | ⬜ |
| **Input Validation** | | | | |
| SQL injection | Security | SQLMap | Blocked | ⬜ |
| XSS attempts | Security | OWASP ZAP | Sanitized | ⬜ |
| Large payload | Integration | Custom | 413 Payload Too Large | ⬜ |
| **Rate Limiting** | | | | |
| Within limits | Integration | K6 | 200 OK | ⬜ |
| Exceed limits | Integration | K6 | 429 Too Many Requests | ⬜ |

### 6. Observability Tests Matrix

| Component | Metric/Trace/Log | Tool | Verification | Status |
|-----------|------------------|------|--------------|---------|
| **Metrics** | | | | |
| Request count | Counter | Prometheus | Increments correctly | ⬜ |
| Request duration | Histogram | Prometheus | P95 < threshold | ⬜ |
| Error rate | Counter | Prometheus | Tracks all errors | ⬜ |
| Token usage | Counter | Prometheus | Accurate count | ⬜ |
| **Tracing** | | | | |
| API requests | Spans | Jaeger | Complete traces | ⬜ |
| Database queries | Spans | Jaeger | Query details captured | ⬜ |
| AI calls | Spans | Jaeger | Duration and tokens | ⬜ |
| Cache operations | Spans | Jaeger | Hit/miss tracked | ⬜ |
| **Logging** | | | | |
| Application logs | JSON | ELK | Structured correctly | ⬜ |
| Error logs | JSON | ELK | Stack traces included | ⬜ |
| Audit logs | JSON | ELK | User actions tracked | ⬜ |

## Verification Scripts

### 1. Build Verification
```bash
#!/bin/bash
# build-verify.sh

echo "=== Building all projects ==="
dotnet build src/BuildingBlocks/BuildingBlocks.csproj
dotnet build src/Modules/Chat/Domain/Domain.csproj
dotnet build src/Modules/Chat/Application/Application.csproj
dotnet build src/Modules/Chat/Infrastructure/Infrastructure.csproj
dotnet build src/API/API.csproj

echo "=== Running unit tests ==="
dotnet test tests/BuildingBlocks.Tests/BuildingBlocks.Tests.csproj
dotnet test tests/Domain.Tests/Domain.Tests.csproj
dotnet test tests/Application.Tests/Application.Tests.csproj

echo "=== Checking code coverage ==="
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

echo "=== Build verification complete ==="
```

### 2. Integration Test Runner
```bash
#!/bin/bash
# integration-test.sh

echo "=== Starting dependencies ==="
docker-compose up -d sqlserver redis elasticsearch

echo "=== Waiting for services ==="
sleep 30

echo "=== Running migrations ==="
dotnet ef database update --project src/Modules/Chat/Infrastructure

echo "=== Running integration tests ==="
dotnet test tests/API.IntegrationTests/API.IntegrationTests.csproj

echo "=== Cleaning up ==="
docker-compose down
```

### 3. Load Test Script
```javascript
// k6-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 100 }, // Ramp up
    { duration: '5m', target: 100 }, // Stay at 100
    { duration: '2m', target: 200 }, // Ramp up
    { duration: '5m', target: 200 }, // Stay at 200
    { duration: '2m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests under 500ms
    http_req_failed: ['rate<0.01'],   // Error rate under 1%
  },
};

const BASE_URL = 'http://localhost:5000/api/v1';

export default function () {
  // Start conversation
  let startRes = http.post(
    `${BASE_URL}/conversations`,
    JSON.stringify({
      title: 'Load Test Conversation',
      initialMessage: 'Testing performance',
    }),
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ${__ENV.JWT_TOKEN}',
      },
    }
  );

  check(startRes, {
    'start conversation status 201': (r) => r.status === 201,
    'start conversation time < 200ms': (r) => r.timings.duration < 200,
  });

  if (startRes.status === 201) {
    let conversationId = JSON.parse(startRes.body).id;

    // Send message
    let messageRes = http.post(
      `${BASE_URL}/conversations/${conversationId}/messages`,
      JSON.stringify({
        content: 'Load test message',
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer ${__ENV.JWT_TOKEN}',
        },
      }
    );

    check(messageRes, {
      'send message status 200': (r) => r.status === 200,
      'send message time < 150ms': (r) => r.timings.duration < 150,
    });
  }

  sleep(1);
}
```

### 4. Health Check Monitor
```bash
#!/bin/bash
# health-check.sh

while true; do
  echo "=== Health Check $(date) ==="
  
  # API Health
  curl -s http://localhost:5000/health | jq .
  
  # Database Health
  curl -s http://localhost:5000/health/ready | jq .
  
  # Liveness
  curl -s http://localhost:5000/health/live | jq .
  
  sleep 30
done
```

## Rollback Plan

### Phase-wise Rollback Strategy

1. **BuildingBlocks Rollback**
   - Keep old Result implementation
   - Use adapter pattern for new code
   - Gradual migration per module

2. **Domain Layer Rollback**
   - Maintain old domain models
   - Use anti-corruption layer
   - Feature flag new implementations

3. **Infrastructure Rollback**
   - Keep connection strings configurable
   - Maintain old repository implementations
   - Use strategy pattern for switching

4. **API Rollback**
   - Version endpoints (v1, v2)
   - Maintain backward compatibility
   - Use API gateway for routing

### Emergency Rollback Script
```bash
#!/bin/bash
# emergency-rollback.sh

echo "=== EMERGENCY ROLLBACK INITIATED ==="

# Stop new services
systemctl stop axon-api-v2

# Restore database
echo "Restoring database backup..."
sqlcmd -S localhost -U sa -P $DB_PASSWORD -Q "
  RESTORE DATABASE AxonChat 
  FROM DISK = '/backups/axon-chat-backup.bak' 
  WITH REPLACE
"

# Switch to old version
echo "Switching to previous version..."
ln -sfn /apps/axon-v1 /apps/axon-current

# Start old services
systemctl start axon-api-v1

# Verify
curl http://localhost:5000/health

echo "=== ROLLBACK COMPLETE ==="
```

## Success Criteria

### Go-Live Checklist

#### Technical Criteria
- [ ] All unit tests pass (>90% coverage)
- [ ] All integration tests pass
- [ ] Performance tests meet thresholds
- [ ] Security scan shows no critical issues
- [ ] Zero memory leaks detected
- [ ] Observability dashboard operational

#### Business Criteria
- [ ] Feature parity with existing system
- [ ] AI responses < 3 second P95
- [ ] 99.9% uptime SLA achievable
- [ ] Cost within budget constraints
- [ ] Documentation complete

#### Operational Criteria
- [ ] Runbooks created
- [ ] Team trained on new architecture
- [ ] Support processes defined
- [ ] Monitoring alerts configured
- [ ] Backup/restore tested
- [ ] Disaster recovery plan tested

## Post-Migration Monitoring

### Week 1 Monitoring
- Error rates every hour
- Performance metrics every 15 minutes
- User feedback daily
- Cost analysis daily

### Week 2-4 Monitoring
- Weekly performance review
- Bi-weekly cost optimization
- User satisfaction surveys
- Technical debt assessment

### Month 2+ Monitoring
- Monthly architecture review
- Quarterly performance baseline
- Continuous improvement metrics
- Feature adoption tracking

## Summary

This migration and verification guide provides:

1. **Phased Migration**: Clear weekly phases with specific tasks
2. **Comprehensive QA Matrix**: Unit, integration, performance, load, security tests
3. **Verification Scripts**: Automated testing and monitoring
4. **Rollback Strategy**: Phase-wise and emergency rollback plans
5. **Success Criteria**: Technical, business, and operational checklists
6. **Post-Migration**: Continuous monitoring and improvement

Follow this guide to ensure a smooth, verifiable migration from the current architecture to the new railway-oriented, clean architecture implementation.