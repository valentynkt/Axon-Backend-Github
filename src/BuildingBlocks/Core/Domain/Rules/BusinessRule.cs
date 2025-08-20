// /Users/valentynkit/Repos/Axon-Backend/src/BuildingBlocks/Core/Domain/Rules/BusinessRule.cs
#nullable enable
using System.Collections.Generic;

namespace BuildingBlocks.Core.Domain.Rules;

/// <summary>
/// Convenience base for rules: override Message and IsBroken(); Code defaults to type name.
/// </summary>
public abstract class BusinessRule : IBusinessRule
{
    public virtual string Code { get; }
    public virtual string Message { get; }
    public virtual IReadOnlyDictionary<string, object>? Metadata { get; }

    protected BusinessRule()
    {
        Code = GetType().Name;
        Message = string.Empty;
        Metadata = null;
    }

    protected BusinessRule(string message) 
    {
        Code = GetType().Name;
        Message = message;
        Metadata = null;
    }

    protected BusinessRule(string code, string message)
    {
        Code = code;
        Message = message;
        Metadata = null;
    }

    protected BusinessRule(string message, string code, IReadOnlyDictionary<string, object>? metadata)
    {
        Code = code;
        Message = message;
        Metadata = metadata;
    }

    public abstract bool IsBroken();

    public virtual ValueTask<bool> IsBrokenAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(IsBroken());
}