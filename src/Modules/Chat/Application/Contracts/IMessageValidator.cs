using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Contracts;

/// <summary>
/// Contract for message validation - to be implemented by srp-decomposition-specialist
/// </summary>
public interface IMessageValidator
{
    /// <summary>
    /// Validate incoming message command
    /// </summary>
    /// <param name="command">Command to validate</param>
    /// <returns>Validation result</returns>
    Result ValidateCommand(ProcessMessageCommand command);
}