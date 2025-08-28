# Helius.dev API Research Notes

## Research Question
Investigation of Helius.dev API documentation to understand capabilities, integration patterns, and implementation guidance for potential integration with the Axon Backend project.

## Executive Summary
Helius.dev provides comprehensive Solana blockchain infrastructure APIs including RPC nodes, data streaming, digital asset management, and real-time event notifications. The service offers high-performance, scalable infrastructure for building decentralized applications on Solana.

## Key Findings

### 1. API Endpoints and Methods

#### Core API Categories:
- **Solana RPC APIs**: Standard and enhanced Solana RPC methods via HTTP and WebSocket
- **Digital Asset Standard (DAS) APIs**: Comprehensive NFT and token querying capabilities
- **Data Streaming APIs**: LaserStream gRPC for ultra-low latency streaming
- **Webhooks**: Real-time blockchain event notifications
- **Priority Fee API**: Smart fee estimation for transactions
- **Enhanced Transactions**: Decoded instruction data
- **ZK Compression**: Storage cost reduction features

#### Endpoint Structure:
```
Mainnet: https://mainnet.helius-rpc.com/?api-key=YOUR_API_KEY
Devnet: https://devnet.helius-rpc.com/?api-key=YOUR_API_KEY
```

### 2. Authentication Requirements

#### API Key Management:
- API keys obtained through dashboard at https://dashboard.helius.dev
- Authentication via query parameter: `?api-key=YOUR_API_KEY`
- **Security Warning**: Never expose API keys in client-side code
- Recommendation: Use server-side proxying for API requests

#### Authentication Pattern:
```http
POST https://mainnet.helius-rpc.com/?api-key=YOUR_API_KEY
Content-Type: application/json

{
  "method": "searchAssets",
  "params": { ... }
}
```

### 3. Data Models and Response Formats

#### Digital Asset Standard (DAS) Response Models:
- **Asset Objects**: Contain metadata, ownership, and collection information
- **Batch Operations**: Support for multiple asset retrieval
- **Merkle Proofs**: For compressed NFT verification
- **Search Results**: Paginated responses with filtering capabilities

#### Supported Asset Types:
- Regular NFTs
- Compressed NFTs  
- Fungible Tokens (SPL, Token-2022)
- Inscriptions

#### Key DAS Methods:
- `getAsset`: Single asset by ID
- `getAssetBatch`: Multiple assets simultaneously
- `getAssetProof`: Merkle proof for compressed assets
- `getAssetsByOwner`: Assets by wallet address
- `getAssetsByCollection`: Filter by collection
- `searchAssets`: Advanced querying with complex filters

### 4. Rate Limits and Usage Guidelines

#### Pricing Tiers:
| Plan | Monthly Cost | Credits | Requests/sec | sendTransaction/sec |
|------|-------------|---------|--------------|-------------------|
| Free | $0 | 1M | 10 | 1 |
| Developer | $49 | 10M | 50 | 5 |
| Business | $499 | 100M | 200 | 50 |
| Professional | $999 | 200M | 500 | 100 |
| Enterprise | Custom | Custom | Custom | Custom |

#### Credit System:
- Additional credits: $5-$4 per million (tier-dependent)
- LaserStream: 3 credits per 0.1 MB
- Staked connections: 10 credits
- Webhook events: 1 credit per processed event
- Webhook management: 100 credits per request

### 5. Integration Patterns for .NET Applications

#### HTTP Client Pattern:
```csharp
public class HeliusClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public HeliusClient(HttpClient httpClient, string apiKey, bool useMainnet = true)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = useMainnet 
            ? "https://mainnet.helius-rpc.com"
            : "https://devnet.helius-rpc.com";
    }

    public async Task<T> PostAsync<T>(string method, object parameters)
    {
        var request = new
        {
            method = method,
            @params = parameters
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/?api-key={_apiKey}", 
            request);
            
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
```

#### Dependency Injection Setup:
```csharp
services.AddHttpClient<HeliusClient>();
services.Configure<HeliusOptions>(configuration.GetSection("Helius"));
```

### 6. Blockchain/Solana-Specific Features

#### Core Solana Capabilities:
- **Account Monitoring**: Track specific wallet addresses
- **Transaction Parsing**: Decoded instruction data
- **Real-time Streaming**: WebSocket and gRPC connections
- **NFT Management**: Comprehensive digital asset operations
- **Program Interaction**: Support for Solana program calls

#### Advanced Features:
- **ZK Compression**: Reduced storage costs for assets
- **Staked Connections**: Enhanced performance for paid plans
- **LaserStream gRPC**: Ultra-low latency data streaming
- **Enhanced WebSockets**: Granular subscription controls

## SDK Availability

### Official SDKs:
- **Node.js SDK**: Full-featured official SDK
- **Rust SDK**: Native Rust implementation

### Community SDKs:
- Kotlin, PHP, Python, additional Rust variants
- **No official .NET/C# SDK available**

### .NET Integration Recommendation:
Implement custom HTTP client wrapper using `System.Net.Http.HttpClient` with JSON serialization via `System.Text.Json`.

## Webhook Integration

### Webhook Types:
1. **Enhanced Transaction Webhooks**: Parsed, filtered transaction data
2. **Raw Transaction Webhooks**: Unfiltered transaction data  
3. **Discord Webhooks**: Direct Discord channel integration

### Configuration:
- Dashboard-based setup (no-code)
- REST API configuration
- Support for up to 25 monitored addresses
- 100+ transaction type events

### Delivery Model:
- HTTP POST to configured endpoints
- Charged per event (1 credit)
- No explicit retry mechanism documented

## Implementation Guidance for Axon Backend

### Apply vs Not-Apply Assessment:

#### ✅ APPLY IF:
- Building Solana-based blockchain features
- Requiring NFT/token asset management
- Need real-time blockchain event processing
- Implementing DeFi or Web3 functionality
- Building Solana wallet integrations

#### ❌ NOT-APPLY IF:
- No blockchain/crypto requirements
- Traditional web application without Web3 features
- Budget constraints (minimum $49/month for meaningful usage)
- No Solana ecosystem involvement

### Integration Architecture Recommendations:

1. **Modular Approach**: Create dedicated `Modules.Blockchain` module
2. **Abstraction Layer**: Implement `IBlockchainService` interface
3. **Configuration**: Use `IOptions<HeliusOptions>` pattern
4. **Error Handling**: Implement robust retry policies with Polly
5. **Caching**: Cache frequent asset queries to reduce API calls
6. **Background Services**: Use `IHostedService` for webhook processing

### Security Considerations:
- Store API keys in Azure Key Vault or similar
- Implement request signing for webhook validation
- Use server-side API calls only
- Implement rate limiting on internal endpoints

## Primary Source References

1. **Main Documentation**: https://www.helius.dev/docs
2. **DAS API Reference**: https://www.helius.dev/docs/das-api
3. **Webhook Documentation**: https://www.helius.dev/docs/webhooks
4. **SDK Documentation**: https://www.helius.dev/docs/sdks
5. **Pricing Information**: https://www.helius.dev/pricing
6. **Developer Dashboard**: https://dashboard.helius.dev

## Confidence Assessment

**Confidence Level: HIGH**

### Reasoning:
- Comprehensive official documentation available
- Clear API patterns and authentication methods
- Well-documented pricing and rate limits
- Active developer community and support
- Production-ready infrastructure with SOC 2 compliance

### Limitations:
- No official .NET SDK requires custom implementation
- Some advanced features require higher-tier plans
- Webhook retry mechanisms not clearly documented
- Enterprise features require custom negotiation

## Next Steps for Integration

1. **Proof of Concept**: Implement basic HTTP client wrapper
2. **Authentication**: Secure API key management strategy
3. **Error Handling**: Implement comprehensive error handling
4. **Testing**: Create integration tests with devnet endpoints
5. **Documentation**: Create implementation guide based on findings
6. **Performance**: Benchmark API response times and rate limits

---

*Research conducted on 2025-07-29*  
*Sources: Official Helius.dev documentation and pricing pages*