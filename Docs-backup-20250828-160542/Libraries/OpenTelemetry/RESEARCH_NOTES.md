# OpenTelemetry 1.12.0 Research Notes

## Research Question
How to implement OpenTelemetry version 1.12.0 for distributed tracing, metrics collection, and observability in a .NET 10 Clean Architecture + DDD + CQRS application?

## Key Findings

### .NET 10 Compatibility ✅ HIGH CONFIDENCE
- OpenTelemetry .NET 1.12.0 is fully compatible with .NET 10 preview
- Native integration with System.Diagnostics.Activity API
- W3C TraceContext compliance built-in
- No compatibility issues reported in official documentation

**Primary Sources:**
- [NuGet Gallery: OpenTelemetry.Instrumentation.AspNetCore 1.12.0](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
- [OpenTelemetry .NET Getting Started](https://opentelemetry.io/docs/languages/dotnet/getting-started/)

### Architecture Integration Patterns ✅ HIGH CONFIDENCE

#### CQRS/MediatR Integration
- **Status**: No official MediatR instrumentation package exists yet
- **Solution**: Custom pipeline behaviors using System.Diagnostics.Activity
- **Pattern**: Community consensus on using IPipelineBehavior<TRequest, TResponse> for instrumentation
- **Implementation**: ActivitySource-based tracing with proper span naming and tagging

**Primary Sources:**
- [MediatR OpenTelemetry Discussion](https://github.com/jbogard/MediatR/discussions/762)
- [Building End-to-End Diagnostics: OpenTelemetry Integration](https://www.jimmybogard.com/building-end-to-end-diagnostics-opentelemetry-integration/)

#### System.Diagnostics.Activity Integration
- **Core Concept**: .NET uses existing Activity/ActivitySource APIs for OpenTelemetry
- **Mapping**: ActivitySource = Tracer, Activity = Span in OpenTelemetry terms
- **Registration**: Must explicitly connect ActivitySource to TracerProvider via AddSource()
- **Performance**: Use HasListeners() and IsAllDataRequested for high-performance scenarios

**Primary Sources:**
- [Microsoft .NET Observability Guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [OpenTelemetry .NET Instrumentation Documentation](https://opentelemetry.io/docs/languages/dotnet/instrumentation/)

### OTLP Exporters and Observability Platforms ✅ HIGH CONFIDENCE

#### OTLP Protocol Support
- **Version**: OpenTelemetry.Exporter.OpenTelemetryProtocol 1.12.0 available
- **Protocols**: Both gRPC and HTTP/Protobuf supported
- **Cross-Signal**: Single UseOtlpExporter() extension for logs, metrics, and traces (since 1.8.0-beta.1)
- **Configuration**: Environment variables follow OpenTelemetry specification

#### Jaeger Integration ⚠️ MEDIUM CONFIDENCE (with limitations)
- **OTLP Support**: Jaeger natively supports OTLP for trace ingestion
- **Limitation**: Jaeger only accepts traces via OTLP, not metrics or logs
- **Recommendation**: Use OTLP to Jaeger rather than deprecated Jaeger-specific exporter
- **Ports**: gRPC OTLP on 4317, HTTP OTLP on 4318

**Primary Sources:**
- [OTLP Exporter for OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
- [OpenTelemetry Collector Configuration](https://opentelemetry.io/docs/collector/configuration/)

### Performance Considerations ✅ HIGH CONFIDENCE

#### Sampling and Optimization
- **Default Sampling**: TraceIdRatioBasedSampler with configurable ratios
- **Batch Processing**: Built-in BatchActivityExportProcessor with configurable parameters
- **Conditional Enrichment**: Use Activity.IsAllDataRequested to avoid expensive operations
- **Hot Path Optimization**: ActivitySource.HasListeners() check for zero-overhead scenarios

#### Resource Usage
- **HTTP Client Factory**: Shared HttpClient instances for OTLP exporters
- **Memory Management**: Configurable batch sizes and export timeouts
- **CPU Impact**: Minimal when properly configured with sampling

**Primary Sources:**
- [OpenTelemetry .NET Performance Best Practices](https://opentelemetry.io/docs/languages/dotnet/instrumentation/)
- [Microsoft Distributed Tracing Instrumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)

### ASP.NET Core Integration ✅ HIGH CONFIDENCE

#### Automatic Instrumentation
- **Package**: OpenTelemetry.Instrumentation.AspNetCore 1.12.0
- **Coverage**: HTTP requests, middleware pipeline, routing
- **Enrichment**: Built-in request/response enrichment with custom options
- **Filtering**: Request filtering capabilities for health checks, metrics endpoints

#### Configuration Patterns
- **Hosting Extensions**: OpenTelemetry.Extensions.Hosting for DI integration
- **Resource Configuration**: Service name, version, environment attributes
- **Multi-Signal**: Unified configuration for tracing, metrics, and logging

**Primary Sources:**
- [ASP.NET Core OpenTelemetry Configuration](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [OpenTelemetry ASP.NET Core Examples](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-prgrja-example)

## Apply vs Not-Apply Recommendations for Axon Backend

### ✅ RECOMMENDED TO APPLY

1. **Core OpenTelemetry Setup**
   - Implement basic tracing with ASP.NET Core instrumentation
   - Use OTLP exporters for vendor-neutral telemetry
   - Configure resource attributes for service identification

2. **CQRS Pipeline Instrumentation**
   - Custom MediatR pipeline behavior for command/query tracing
   - ActivitySource per application layer (CQRS, Domain, Repository)
   - Structured span naming following operation patterns

3. **Custom Metrics Collection**
   - Business metrics using System.Diagnostics.Metrics
   - CQRS operation counters and duration histograms
   - Domain event metrics for aggregate operations

4. **Performance Optimizations**
   - Sampling configuration for production (10% recommended start)
   - Conditional enrichment with IsAllDataRequested
   - Batch processing with appropriate buffer sizes

### ⚠️ APPLY WITH CAUTION

1. **Comprehensive Logging Integration**
   - Start with traces and metrics first
   - Add structured logging integration in later phase
   - Monitor resource usage impact

2. **Advanced Exporters**
   - Begin with Console + OTLP exporters
   - Add Prometheus/Jaeger based on infrastructure decisions
   - Evaluate vendor-specific exporters carefully

### ❌ NOT RECOMMENDED TO APPLY IMMEDIATELY

1. **Zero-Code Instrumentation**
   - Automatic instrumentation may be too broad initially
   - Prefer explicit instrumentation for better control
   - Consider for later optimization phases

2. **Complex Sampling Strategies**
   - Start with simple ratio-based sampling
   - Avoid advanced sampling until usage patterns are understood
   - Complex rules can impact performance

## Confidence Assessment

- **Overall Implementation**: HIGH CONFIDENCE
- **CQRS Integration**: HIGH CONFIDENCE (proven patterns available)
- **Production Readiness**: HIGH CONFIDENCE (mature 1.12.0 release)
- **Performance Impact**: MEDIUM CONFIDENCE (requires monitoring and tuning)
- **Jaeger Integration**: MEDIUM CONFIDENCE (limited to traces only)

## Primary Sources Used

1. **Official OpenTelemetry Documentation**
   - https://opentelemetry.io/docs/languages/dotnet/getting-started/
   - https://opentelemetry.io/docs/languages/dotnet/instrumentation/

2. **Microsoft Documentation**
   - https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel
   - https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs

3. **GitHub Repositories**
   - https://github.com/open-telemetry/opentelemetry-dotnet/
   - https://github.com/jbogard/MediatR/discussions/762

4. **Package Repositories**
   - https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore

5. **Community Resources**
   - https://www.jimmybogard.com/building-end-to-end-diagnostics-opentelemetry-integration/
   - https://www.milanjovanovic.tech/blog/introduction-to-distributed-tracing-with-opentelemetry-in-dotnet

## Next Research Areas (if needed)

1. **Vendor-Specific Integrations**: Azure Monitor, AWS X-Ray, Google Cloud Trace
2. **Advanced Sampling**: Head-based vs tail-based sampling strategies  
3. **Security Considerations**: PII scrubbing, secure transport configuration
4. **Cost Optimization**: Telemetry data volume management strategies
5. **Testing Strategies**: Contract testing for telemetry data formats

## Date Researched
July 29, 2025

## Research Methodology
- Primary sources prioritized (official documentation, package repositories)
- Community discussions for implementation patterns
- Cross-referenced multiple sources for consistency
- Focused on .NET 10 compatibility and Clean Architecture integration