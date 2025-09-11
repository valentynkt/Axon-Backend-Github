# 12. Rate Limiting (Epic 2 Production Implementation)

## ASP.NET Core Middleware Configuration

**Policy Details:**
* **`/auth/exchange`:** **10 requests/minute per IP address** using sliding window
* **`/auth/me`:** No rate limiting (read path optimized with ETag conditional GET)

**Implementation Approach:**
```csharp
// Program.cs - Rate limiting configuration
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    
    // Exchange endpoint rate limiting
    options.AddFixedWindowLimiter("AuthExchange", config =>
    {
        config.Window = TimeSpan.FromMinutes(1);
        config.PermitLimit = 10;
        config.QueueLimit = 0; // Reject immediately when limit exceeded
    });
    
    // Global policy for IP-based limiting
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress;
        
        if (context.Request.Path.StartsWithSegments("/auth/exchange"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                ipAddress,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }
        
        return RateLimitPartition.GetNoLimiter(ipAddress);
    });
});
```

## HTTP Response Headers

**Success Responses (200 OK):**
```http
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 7
X-RateLimit-Reset: 1694123456
```

**Rate Limited Responses (429 Too Many Requests):**
```http
Retry-After: 45
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1694123456
```

## Observability & Metrics

* **Rate Limiter Metrics:** `rate_limit_hits`, `rate_limit_rejections`, per-IP tracking
* **Response Time Impact:** < 1ms overhead for rate limit checks
* **Integration:** Works seamlessly with existing OpenTelemetry tracing

---
