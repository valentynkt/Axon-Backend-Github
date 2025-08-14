using System;

namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Marks a message class with version information for header stamping.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageVersionAttribute : Attribute
{
    public string Version { get; }

    public MessageVersionAttribute(string version)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }
}