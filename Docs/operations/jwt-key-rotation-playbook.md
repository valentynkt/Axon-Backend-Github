# JWT Key Rotation Playbook

## Overview
This playbook documents the procedures for rotating JWT signing keys in Azure Key Vault for the Axon Backend API.

## Key Rotation Schedule
- **Frequency**: Quarterly (every 3 months)
- **Overlap Window**: 48 hours (configurable via `AzureKeyVault:KeyRotationOverlapHours`)
- **Cache Duration**: 60 minutes (configurable via `AzureKeyVault:KeyCacheDurationMinutes`)

## Standard Rotation Procedure

### Prerequisites
1. Azure Key Vault access with Key Management permissions
2. Access to application configuration
3. Monitoring dashboard access

### Step 1: Create New Key Version
```bash
# Using Azure CLI
az keyvault key create \
  --vault-name <vault-name> \
  --name <jwt-signing-key-name> \
  --kty RSA \
  --size 2048 \
  --ops sign verify
```

### Step 2: Update Application Configuration
The application automatically detects new key versions. No manual configuration update required.

### Step 3: Monitor Key Usage
Monitor the following metrics during rotation:
- JWT generation success rate
- JWT validation success rate
- Key cache hit rate
- Authentication error rate

### Step 4: Verify Rotation
```bash
# Check current key version
az keyvault key show \
  --vault-name <vault-name> \
  --name <jwt-signing-key-name> \
  --query "key.kid"
```

### Step 5: Deactivate Old Key (After Overlap Window)
After 48 hours, disable the old key version:
```bash
az keyvault key set-attributes \
  --vault-name <vault-name> \
  --name <jwt-signing-key-name> \
  --version <old-version> \
  --enabled false
```

## Emergency Rotation Procedure

### Trigger Conditions
- Key compromise suspected
- Security breach detected
- Compliance requirement

### Emergency Steps

#### 1. Immediate Key Rotation
```bash
# Create emergency key
az keyvault key create \
  --vault-name <vault-name> \
  --name <jwt-signing-key-name>-emergency \
  --kty RSA \
  --size 2048 \
  --ops sign verify
```

#### 2. Update Application Configuration (Emergency)
```yaml
# Update appsettings.json
{
  "AzureKeyVault": {
    "JwtSigningKeyName": "<jwt-signing-key-name>-emergency"
  }
}
```

#### 3. Force Cache Clear
Restart application instances to clear cached keys:
```bash
# For Kubernetes
kubectl rollout restart deployment axon-api

# For Azure App Service
az webapp restart --name <app-name> --resource-group <rg-name>
```

#### 4. Invalidate Existing Tokens
- All tokens signed with compromised key become invalid
- Users will need to re-authenticate
- Monitor authentication failures

#### 5. Post-Incident Actions
- Document incident in security log
- Review key management procedures
- Update rotation schedule if needed

## Monitoring and Alerts

### Key Metrics
- `jwt.generation.success`: JWT generation success rate
- `jwt.generation.failure`: JWT generation failure rate
- `jwt.validation.success`: JWT validation success rate
- `jwt.validation.failure`: JWT validation failure rate
- `keyvault.key.retrieval.success`: Key retrieval success rate
- `keyvault.key.retrieval.failure`: Key retrieval failure rate

### Alert Thresholds
- JWT generation failure rate > 1%
- JWT validation failure rate > 5%
- Key retrieval failure rate > 1%

## Rollback Procedure

If issues occur after rotation:

1. **Re-enable Previous Key**
```bash
az keyvault key set-attributes \
  --vault-name <vault-name> \
  --name <jwt-signing-key-name> \
  --version <previous-version> \
  --enabled true
```

2. **Update Configuration** (if using emergency key)
```yaml
{
  "AzureKeyVault": {
    "JwtSigningKeyName": "<original-key-name>"
  }
}
```

3. **Clear Cache**
Restart application to clear key cache

## Testing Procedures

### Pre-Rotation Testing
1. Verify current key is working
2. Test JWT generation endpoint
3. Test JWT validation endpoint
4. Check monitoring dashboards

### Post-Rotation Testing
1. Generate new JWT with new key
2. Validate old JWTs (should work during overlap window)
3. Validate new JWTs
4. Monitor error rates

## Contact Information

### Primary Contacts
- Security Team: security@axon.io
- DevOps Team: devops@axon.io
- On-Call Engineer: See PagerDuty

### Escalation Path
1. On-Call Engineer
2. Security Team Lead
3. CTO

## Appendix

### Configuration Reference
```json
{
  "AzureKeyVault": {
    "VaultUri": "https://<vault-name>.vault.azure.net/",
    "JwtSigningKeyName": "axon-jwt-signing-key",
    "UseManagedIdentity": true,
    "KeyRotationOverlapHours": 48,
    "KeyCacheDurationMinutes": 60
  }
}
```

### Azure CLI Commands Reference
- List keys: `az keyvault key list --vault-name <vault-name>`
- Show key: `az keyvault key show --vault-name <vault-name> --name <key-name>`
- Create key: `az keyvault key create --vault-name <vault-name> --name <key-name>`
- Disable key: `az keyvault key set-attributes --vault-name <vault-name> --name <key-name> --version <version> --enabled false`

### Useful Links
- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [Axon Security Guidelines](internal-docs/security-guidelines.md)