namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Centralized error messages for GetMyPrincipal query operations.
/// Provides consistent error messaging across the handler.
/// </summary>
public static class GetMyPrincipalErrorMessages
{
    public const string PrincipalNotFound = "Principal not found. User may not have completed exchange yet.";
    public const string PrincipalDataLoadFailed = "Principal data could not be loaded";
}