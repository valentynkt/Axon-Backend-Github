namespace BuildingBlocks.Application.Abstractions.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SkipValidationAttribute : Attribute
{
}