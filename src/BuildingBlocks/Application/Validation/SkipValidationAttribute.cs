namespace BuildingBlocks.Application.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SkipValidationAttribute : Attribute
{
}