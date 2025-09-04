using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Identity database connectivity and schema validation
/// </summary>
public sealed class IdentityDatabaseHealthCheck : IHealthCheck
{
    private readonly IdentityReadDbContext _dbContext;
    private readonly ILogger<IdentityDatabaseHealthCheck> _logger;

    public IdentityDatabaseHealthCheck(
        IdentityReadDbContext dbContext,
        ILogger<IdentityDatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Test basic connectivity
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to Identity database");
            }

            // Test schema exists
            var schemaQuery = @"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.schemata 
                    WHERE schema_name = 'identity'
                )";
            
            var schemaExists = await _dbContext.Database
                .SqlQueryRaw<bool>(schemaQuery)
                .SingleOrDefaultAsync(cancellationToken);
            
            if (!schemaExists)
            {
                return HealthCheckResult.Degraded("Identity schema does not exist");
            }

            // Test critical tables exist
            var tablesQuery = @"
                SELECT COUNT(*) as table_count
                FROM information_schema.tables 
                WHERE table_schema = 'identity' 
                AND table_name IN ('axon_principals', 'identity_credentials', 'wallets', 'wallet_ownerships')";
            
            var tableCount = await _dbContext.Database
                .SqlQueryRaw<int>(tablesQuery)
                .SingleOrDefaultAsync(cancellationToken);
            
            if (tableCount < 4)
            {
                return HealthCheckResult.Degraded($"Expected 4 Identity tables, found {tableCount}");
            }

            // All checks passed
            return HealthCheckResult.Healthy("Identity database is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Identity database health check failed");
            return HealthCheckResult.Unhealthy("Identity database health check failed", ex);
        }
    }
}