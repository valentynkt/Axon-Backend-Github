# Show Pattern Task

<task id="library-sage/show-pattern" name="Show Library Usage Pattern from Codebase">
  <llm critical="true">
    <i>Find and show REAL usage examples of library from existing codebase</i>
    <i>CRITICAL: Extract actual code snippets with file:line, not invented examples</i>
    <i>Find 2-3 best examples showing: Basic usage, Advanced usage, Error handling</i>
  </llm>

  <flow>
    <step n="1" title="Parse Input & Locate Library Usage">
      <action>Extract library name (e.g., "MediatR", "FastEndpoints", "Dynamic Auth")</action>
      <action>Grep library-specific patterns in src/</action>
      <action>MediatR: Grep "IRequest&lt;" + "IRequestHandler&lt;"</action>
      <action>FastEndpoints: Grep "Endpoint&lt;"</action>
      <action>FluentValidation: Grep "AbstractValidator&lt;"</action>
      <action>Dynamic Auth: Grep "DynamicAuth" in services</action>
    </step>

    <step n="2" title="Find Best Examples (2-3)">
      <action>Select examples showing different usage patterns</action>
      <action>Basic: Simple, common case</action>
      <action>Advanced: Complex scenario with multiple features</action>
      <action>Error Handling: How library errors are handled</action>
      <action>Read actual code snippets (10-20 lines each)</action>
    </step>

    <step n="3" title="Extract Pattern Elements">
      <action>For each example, identify pattern elements:</action>
      <action>Setup/initialization code</action>
      <action>Core library API calls</action>
      <action>Result handling (success/failure)</action>
      <action>Common pitfalls avoided</action>
    </step>
  </flow>

  <validation>
    <i>At least 2 examples found</i>
    <i>All examples from actual codebase (file:line verified)</i>
    <i>Code snippets accurate (not invented)</i>
  </validation>

  <output format="yaml">
library_pattern:
  library: "MediatR"
  examples_found: 3

  examples:
    - type: "Basic Command"
      file: "src/Modules/Identity/Application/Commands/VerifyWalletCommandHandler.cs"
      line: 15
      snippet: |
        public class VerifyWalletCommandHandler : IRequestHandler&lt;VerifyWalletCommand, Result&lt;WalletVerificationResult>>
        {
            public async Task&lt;Result&lt;WalletVerificationResult>> Handle(VerifyWalletCommand request, CancellationToken cancellationToken)
            {
                var result = await _service.VerifySignatureAsync(...);
                if (result.IsFailure) return Result&lt;WalletVerificationResult>.Failure(result.Error);
                return Result&lt;WalletVerificationResult>.Success(verificationResult);
            }
        }
      pattern_elements:
        - "IRequestHandler interface implementation"
        - "Result&lt;T> return type"
        - "Early failure return"
        - "CancellationToken support"
  </output>

  <halt-conditions>
    <i>Zero usage found - library not yet integrated, suggest implementation</i>
    <i>Only 1 example - warn limited pattern coverage</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</i>
    <i>Example: WalletOwnership for MediatR, Conversation for EF Core</i>
  </references>
</task>