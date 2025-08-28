# System.Diagnostics.Activity Research Notes

## Research Question
Investigate System.Diagnostics.Activity framework capabilities in .NET 10 for implementing distributed tracing in the Axon Backend project, specifically focusing on W3C TraceContext support, ActivitySource patterns, automatic HTTP header propagation, CQRS integration, and OpenTelemetry compatibility.

## Primary Source Findings

### 1. Built-in .NET 10 Compatibility
**Source**: Existing memory research - dotnet10_library_research
**Citation**: System.Diagnostics.Activity documented as "Built into .NET Framework - No NuGet package needed"
**Finding**: System.Diagnostics.Activity is a core framework component included in .NET 10, requiring no additional packages.
**Confidence**: **High** - Built-in framework API confirmed

### 2. W3C TraceContext Implementation
**Source**: Microsoft Learn - Distributed tracing concepts
**Citation**: "NET 5 uses the W3C TraceContext ID format by default but earlier .NET versions default to using Hierarchical ID format"
**Finding**: 
- .NET 5+ uses W3C TraceContext format by default
- ActivityContext contains TraceId, SpanId, TraceFlags, and TraceState
- W3C compliance is automatic, no configuration required
**Confidence**: **High** - Official Microsoft documentation

### 3. ActivitySource and Activity Patterns
**Source**: Microsoft Learn - Add distributed tracing instrumentation
**Citation**: "Applications and libraries add distributed tracing instrumentation using the System.Diagnostics.ActivitySource and System.Diagnostics.Activity classes"
**Key Findings**:
- ActivitySource is equivalent to OpenTelemetry "Tracer"
- Activity is equivalent to OpenTelemetry "Span"
- ActivitySource.StartActivity() includes performance optimization - returns null if no listeners
- Static ActivitySource instances recommended for application lifecycle
**Confidence**: **High** - Primary Microsoft documentation

### 4. Automatic HTTP Header Propagation
**Source**: Microsoft Learn - Distributed tracing concepts
**Citation**: "Unlike many other language runtimes, .NET in-box libraries such as the ASP.NET web server and System.Net.Http natively understand how to decode and encode Activity IDs on HTTP messages"
**Finding**: 
- ASP.NET Core and HttpClient automatically handle trace context propagation
- Uses W3C TraceContext HTTP headers by default
- No additional coding required for basic HTTP trace propagation
**Confidence**: **High** - Confirmed built-in capability

### 5. Performance Optimization Patterns
**Source**: Microsoft Learn - Add distributed tracing instrumentation
**Citation**: "ActivitySource.StartActivity internally determines if there are any listeners recording the Activity. If there are no registered listeners or there are listeners that are not interested, StartActivity() will return null"
**Additional Citation**: "Activity.IsAllDataRequested is a hint that indicates whether any of the code listening to Activities intends to read auxiliary information such as Tags"
**Findings**:
- Smart performance optimization through null return when no listeners
- IsAllDataRequested flag for conditional expensive tag computation
- Recommended pattern prevents performance impact in production
**Confidence**: **High** - Official performance guidance

### 6. ActivityListener Implementation
**Source**: Microsoft Learn - Distributed tracing collection walkthroughs
**Citation**: "System.Diagnostics.ActivityListener is used to receive callbacks during the lifetime of an Activity"
**Findings**:
- ActivityListener provides ActivityStarted and ActivityStopped callbacks
- ShouldListenTo method for filtering by ActivitySource
- Sample method for controlling which activities are recorded
- Essential for activity collection and export
**Confidence**: **High** - Core framework documentation

### 7. Baggage Support
**Source**: Jimmy Bogard - Building End-to-End Diagnostics: User-Defined Context with Correlation Context
**Citation**: "ASP.NET Core will automatically parse correlation context header information, and places this in Activity.Baggage"
**Additional**: "ASP.NET Core 3.0+ has implementation support for the Correlation-Context header"
**Findings**:
- Activity.Baggage provides cross-service context propagation
- Automatic parsing of Correlation-Context headers
- Use Activity.AddBaggage() to add context information
- Propagates automatically across HTTP boundaries
**Confidence**: **High** - Authoritative secondary source with framework confirmation

### 8. OpenTelemetry Integration
**Source**: Multiple sources - OpenTelemetry documentation and Jimmy Bogard's series
**Citation**: "In .NET 'ActivitySource' is the implementation of Tracer and Activity is the implementation of 'Span'"
**Findings**:
- Direct compatibility with OpenTelemetry specification
- .NET's System.Diagnostics API is OpenTelemetry-compliant by design
- Can export to any OpenTelemetry collector without modification
- Recommended to use System.Diagnostics directly rather than OpenTelemetry SDK for most cases
**Confidence**: **High** - Multiple authoritative sources confirm

### 9. CQRS Integration Patterns
**Source**: Inferred from architecture patterns and observability best practices
**Citation**: Multiple sources discussing Activity usage in application architectures
**Findings**:
- ActivitySource can instrument MediatR pipeline behaviors
- Command and Query handlers can create nested activities
- Tags can differentiate between command and query operations
- Natural fit for tracking request flow through CQRS layers
**Confidence**: **Medium** - Inferred from general patterns, not specific CQRS documentation

### 10. Sampling Strategies
**Source**: Multiple OpenTelemetry and observability sources
**Citation**: Various mentions of head sampling vs tail sampling approaches
**Findings**:
- Head sampling: Decision made at activity creation
- Tail sampling: Decision made by collectors downstream
- ActivitySamplingResult enum controls sampling decisions
- Custom sampling logic can be implemented via ActivityListener
**Confidence**: **Medium** - General observability concepts, not .NET-specific

## Secondary Source Cross-References

### Jimmy Bogard's End-to-End Diagnostics Series
**Authority**: Recognized .NET expert and MediatR author
**Coverage**: Comprehensive practical implementation guide
**Key Insights**:
- Static ActivitySource pattern recommendations
- Performance optimization techniques
- Real-world ASP.NET Core integration examples
**Confidence**: **High** - Expert practitioner guidance

### OpenTelemetry .NET Documentation
**Authority**: CNCF official documentation
**Coverage**: Integration patterns and best practices
**Key Insights**:
- Semantic conventions for tag naming
- Exporter configuration patterns
- Instrumentation library recommendations
**Confidence**: **High** - Official specification documentation

## Apply vs Not-Apply for Axon Backend

### ✅ **APPLY IMMEDIATELY**

1. **System.Diagnostics.Activity Core Usage**
   - **Rationale**: Built into .NET 10, no dependencies, W3C compliant by default
   - **Evidence**: Microsoft Learn documentation confirms built-in support
   - **Implementation**: Static ActivitySource instances, basic activity creation patterns

2. **MediatR Pipeline Integration**
   - **Rationale**: Perfect fit for CQRS command/query tracing
   - **Evidence**: Pipeline behavior pattern well-documented
   - **Implementation**: TracingBehavior<TRequest, TResponse> for automatic instrumentation

3. **ASP.NET Core Automatic Propagation**
   - **Rationale**: Built-in HTTP header propagation requires no additional code
   - **Evidence**: Microsoft documentation confirms native support
   - **Implementation**: Works automatically with existing endpoints

4. **Performance-Optimized Patterns**
   - **Rationale**: IsAllDataRequested and null-return patterns prevent overhead
   - **Evidence**: Official Microsoft performance guidance
   - **Implementation**: Conditional tag creation and smart activity handling

### ⚠️ **APPLY WITH MONITORING**

1. **OpenTelemetry Export Integration**
   - **Rationale**: Useful for production observability but adds complexity
   - **Evidence**: Well-documented integration path available
   - **Consideration**: Monitor for performance impact and infrastructure requirements

2. **Baggage Usage**
   - **Rationale**: Powerful for cross-service context but can impact performance
   - **Evidence**: ASP.NET Core automatic support confirmed
   - **Consideration**: Use sparingly, monitor payload size

3. **Custom Sampling Logic**
   - **Rationale**: Important for production scale but requires careful tuning
   - **Evidence**: ActivityListener and sampling patterns documented
   - **Consideration**: Start with simple sampling, evolve based on usage patterns

### 🔄 **DEFER FOR LATER EVALUATION**

1. **Advanced Activity Events**
   - **Rationale**: Not immediately needed for basic tracing
   - **Evidence**: Feature exists but may add complexity
   - **Consideration**: Evaluate based on specific debugging requirements

2. **Complex Multi-Service Scenarios**
   - **Rationale**: Axon Backend is modular monolith, not microservices initially
   - **Evidence**: Patterns exist for microservice tracing
   - **Consideration**: Relevant when extracting services to separate deployments

## Confidence Assessment

**Overall Confidence**: **High (90%)**

**High Confidence Areas (95-100%)**:
- Built-in .NET 10 support - Framework documentation
- W3C TraceContext compliance - Microsoft Learn confirmation
- HTTP automatic propagation - ASP.NET Core built-in capability
- ActivitySource/Activity patterns - Official API documentation
- Performance optimization techniques - Microsoft guidance

**Medium Confidence Areas (80-90%)**:
- CQRS-specific patterns - Inferred from general patterns
- Complex sampling strategies - Requires tuning based on actual usage
- Production scale considerations - Environment-dependent factors

**Implementation Risk**: **Low**
- No external dependencies
- Built-in framework support
- Backward compatible patterns
- Gradual adoption possible

## Primary Source Links

1. [Microsoft Learn - Distributed tracing concepts](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts)
2. [Microsoft Learn - Add distributed tracing instrumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
3. [Microsoft Learn - Collect a distributed trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-collection-walkthroughs)
4. [ActivitySource Class Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activitysource?view=net-9.0)
5. [Activity Class Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity?view=net-9.0)

## Secondary Source Links

1. [Jimmy Bogard - Building End-to-End Diagnostics Series](https://www.jimmybogard.com/building-end-to-end-diagnostics-and-tracing-a-primer-trace-context/)
2. [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/dotnet/instrumentation/)
3. [.NET Runtime Activity User Guide](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md)

## Next Research Questions

1. **Production Scale Impact**: What are the actual performance implications at high request volumes?
2. **Storage and Export Costs**: What are the data volume implications for different sampling strategies?
3. **Integration Testing Patterns**: How to effectively test trace propagation in integration scenarios?
4. **Correlation with Application Metrics**: How to correlate Activity data with business metrics?

## Recommendation Summary

**Immediate Implementation**: Start with basic ActivitySource usage in MediatR pipeline and key business operations. Leverage built-in ASP.NET Core propagation.

**Phase 2**: Add OpenTelemetry export for production observability, implement custom sampling based on actual usage patterns.

**Phase 3**: Advanced features like custom events, complex baggage usage, and multi-service correlation as architecture evolves.

The research strongly supports immediate adoption of System.Diagnostics.Activity for the Axon Backend project with high confidence in the implementation approach and minimal risks.