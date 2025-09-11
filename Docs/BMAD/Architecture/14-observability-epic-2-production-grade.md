# 14. Observability (Epic 2 Production-Grade)

## OpenTelemetry Implementation

**Comprehensive Tracing:**
* **Request Lifecycle:** Complete trace from API → Application → Domain → Infrastructure
* **Correlation IDs:** Propagated across all layers, included in logs and responses
* **Span Structure:** 
  ```
  /auth/exchange [HTTP]
  ├── ExchangeCredentialCommand [App]
  │   ├── DynamicAuthService.ValidateToken [Infra]
  │   │   └── JwksService.GetKeys [Infra]
  │   ├── FindByCredentialAsync [Database]
  │   ├── EnsureManyByChainAndAddressAsync [Database]
  │   └── SaveChangesAsync [Database]
  └── Rate Limiter Check [Middleware]
  ```

**Epic 2 Metrics Catalog:**

**Core Business Metrics:**
- `identity_exchange_success_total` (counter) - Successful exchanges by provider
- `identity_exchange_failure_total` (counter) - Failed exchanges by error type
- `identity_wallet_conflicts_total` (counter) - Wallet ownership conflicts (409 responses)
- `identity_wallets_linked_total` (counter) - Total wallets linked during exchanges
- `identity_principals_created_total` (counter) - New principals created

**Performance Metrics:**
- `identity_etag_hits_total` (counter) - ETag cache hits (304 responses)
- `identity_etag_misses_total` (counter) - ETag cache misses (200 responses)
- `identity_request_duration_seconds` (histogram) - Request latency by endpoint
- `identity_database_query_duration_seconds` (histogram) - DB query performance

**Infrastructure Metrics:**
- `identity_rate_limit_hits_total` (counter) - Rate limit violations by IP
- `identity_jwks_cache_hits_total` (counter) - JWKS cache effectiveness
- `identity_jwt_validation_duration_seconds` (histogram) - JWT validation performance

## Structured Logging Strategy

**Log Levels & Content:**
```json
{
  "timestamp": "2025-09-11T15:30:45.123Z",
  "level": "Information",
  "message": "Exchange completed successfully",
  "correlationId": "01JA2B3C4D5E6F7G8H9J0K1L2M",
  "traceId": "abc123def456",
  "principalId": "01JA2B3C4D5E6F7G8H9J0K1L2M",
  "provider": "dynamic",
  "walletsProcessed": 2,
  "walletsLinked": 1,
  "created": false,
  "requestDuration": 156.7
}
```

**Privacy Compliance:**
- **NEVER log**: Email addresses, phone numbers, contact identifiers
- **Safe to log**: Principal IDs, wallet IDs, chain IDs, addresses (public blockchain data)
- **Always include**: Correlation IDs for request tracing

## Dashboards & Alerting

**Production Dashboards:**
1. **Identity Health Overview:** Success rates, error distributions, latency percentiles
2. **ETag Effectiveness:** Cache hit ratios, conditional GET performance
3. **Rate Limiting Stats:** Per-IP request patterns, abuse detection
4. **JWT Validation Performance:** JWKS cache health, validation latency

**Critical Alerts:**
- Exchange success rate < 95% (5min window)
- Rate limit violations > 100/hour from single IP
- ETag cache hit ratio < 60%
- JWT validation errors > 10% (indicates JWKS issues)

---
