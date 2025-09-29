namespace Axon.Modules.Identity.Application.Commands.VerifyWalletSignature;

public sealed record VerifyWalletSignatureResult(
    string AccessToken,
    string TokenType,
    long ExpiresIn,
    string AxonUserId,
    bool Created,
    int WalletsLinked,
    int Conflicts
);