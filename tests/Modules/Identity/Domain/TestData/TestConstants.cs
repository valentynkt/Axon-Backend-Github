using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.TestData;

public static class TestConstants
{
    // Valid blockchain addresses
    public const string ValidSolanaAddress = "11111111111111111111111111111111";
    public const string ValidEthAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb1";
    public const string AnotherSolanaAddress = "22222222222222222222222222222222";
    
    // Test emails
    public const string TestEmail = "test@example.com";
    public const string AnotherEmail = "another@example.com";
    public const string ServiceEmail = "service@axon.ai";
    
    // Google OAuth test data
    public const string GoogleSub = "google-oauth-123456";
    public const string AnotherGoogleSub = "google-oauth-789012";
    
    // Test IDs (generated once, reused)
    public static readonly AxonId TestPrincipalId = AxonId.New();
    public static readonly AxonId TestWalletId = AxonId.New();
    public static readonly AxonId AnotherPrincipalId = AxonId.New();
    
    // Timestamps
    public static readonly DateTimeOffset TestTimestamp = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset EarlierTimestamp = TestTimestamp.AddHours(-1);
    public static readonly DateTimeOffset LaterTimestamp = TestTimestamp.AddHours(1);
}