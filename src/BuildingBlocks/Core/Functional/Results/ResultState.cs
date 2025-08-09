namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Result state enumeration for pattern matching
/// </summary>
internal enum ResultState : byte
{
    Success = 1,
    Failure = 2
}