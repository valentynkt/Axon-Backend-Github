// DEPRECATED: ValidationException has moved to BuildingBlocks.Application.Exceptions
// This file provides backward compatibility redirect

global using ValidationException = BuildingBlocks.Application.Exceptions.ValidationException;

namespace BuildingBlocks.Core.Diagnostics.Exceptions
{
    // DEPRECATED: This namespace redirect is maintained for backward compatibility only.
    // ValidationException has moved to BuildingBlocks.Application.Exceptions to follow Clean Architecture principles.
    // Please update your using statements to: using BuildingBlocks.Application.Exceptions;
}