// /Users/valentynkit/Repos/Axon-Backend/src/BuildingBlocks/Core/Diagnostics/Exceptions/BusinessRuleException.cs
#nullable enable
using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Domain.Rules;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Domain-specific exception thrown when a business rule is violated.
/// Wraps a rich Error.BusinessRule with rule code/message/metadata and Activity correlation.
/// </summary>
[Serializable]
public sealed class BusinessRuleException : DomainException
{
    /// <summary>Rule type (useful for diagnostics).</summary>
    public string RuleType { get; }

    /// <summary>Stable rule code.</summary>
    public string RuleCode { get; }

    public BusinessRuleException(IBusinessRule rule)
        : base(CreateError(rule))
    {
        ArgumentNullException.ThrowIfNull(rule);
        RuleType = rule.GetType().FullName ?? rule.GetType().Name;
        RuleCode = rule.Code;
    }

#pragma warning disable SYSLIB0051
    private BusinessRuleException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        RuleType = info.GetString(nameof(RuleType)) ?? string.Empty;
        RuleCode = info.GetString(nameof(RuleCode)) ?? string.Empty;
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(RuleType), RuleType);
        info.AddValue(nameof(RuleCode), RuleCode);
    }
#pragma warning restore SYSLIB0051

    private static Error CreateError(IBusinessRule rule)
        => Error.BusinessRule(rule.Message, rule.Code, rule.Metadata);
}