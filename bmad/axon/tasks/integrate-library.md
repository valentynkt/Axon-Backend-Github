# Integrate Library Task

<task id="implementation-surgeon/integrate-library" name="Integrate Library per Library Sage Guidance">
  <llm critical="true">
    <i>Integrate library following Library Sage recommendations</i>
    <i>CRITICAL: Use library implementation guide + existing usage patterns</i>
  </llm>

  <flow>
    <step n="1" title="Read Library Sage Guidance">
      <action>Read library recommendation (check-library, suggest-approach outputs)</action>
      <action>Extract: Library name, usage pattern, integration points</action>
    </step>

    <step n="2" title="Install & Configure Library">
      <action>Run: dotnet add package {library}</action>
      <action>Configure in DependencyInjection.cs or Program.cs</action>
      <action>Follow implementation guide setup instructions</action>
    </step>

    <step n="3" title="Implement Library Usage">
      <action>Follow existing patterns from codebase (show-pattern result)</action>
      <action>Infrastructure layer: Create service wrapper if needed</action>
      <action>Application layer: Call library via service</action>
      <action>Result&lt;T> wrapping: Convert library exceptions → Result.Failure</action>
    </step>
  </flow>

  <output format="csharp">
// Infrastructure: Library wrapper service
public class WalletVerificationService : IWalletVerificationService
{
    private readonly IDynamicAuthClient _dynamicAuth;

    public async Task&lt;Result&lt;bool>> VerifySignatureAsync(...)
    {
        try
        {
            var isValid = await _dynamicAuth.VerifyWalletSignatureAsync(...);
            return Result&lt;bool>.Success(isValid);
        }
        catch (DynamicAuthException ex)
        {
            return Result&lt;bool>.Failure(Error.External("Dynamic Auth verification failed", ex.Message));
        }
    }
}
  </output>

  <halt-conditions>
    <i>Library recommendation is MANUAL - don't integrate library</i>
    <i>Compatibility check failed - resolve conflicts first</i>
  </halt-conditions>

  <references>
    <i>Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md</i>
    <i>Library Sage outputs: check-library.yaml, suggest-approach.yaml</i>
  </references>
</task>
