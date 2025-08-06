using System;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Domain.ValueObjects
{
    /// <summary>
    /// Wrapper for the text of a user message, with length validation.
    /// </summary>
    public readonly record struct MessageContent(string Value)
    {
        public const int MaxLength = 100_000;

        public static Result<MessageContent> Create(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Error.Validation("Message content cannot be empty.", "MESSAGE_CONTENT_EMPTY");
            if (content.Length > MaxLength)
                return Error.Validation($"Message content cannot exceed {MaxLength} characters.", "MESSAGE_CONTENT_TOO_LONG");
            return new MessageContent(content.Trim());
        }

        public override string ToString() => Value;
        public static implicit operator string(MessageContent mc) => mc.Value;
        public static explicit operator MessageContent(string s) => Create(s).Value;
    }
}