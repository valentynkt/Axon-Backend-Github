// /BuildingBlocks/Core/Abstractions/CQRS/Policies/IRetryableQuery.cs
#nullable enable
using System;
using BuildingBlocks.Application.Behaviors;
using Polly.Retry;

namespace BuildingBlocks.Core.Abstractions.CQRS.Policies;

/// <summary>
/// Programmatic opt-in for retry behavior (idempotent queries only).
/// Application behavior (Polly) reads this policy; Core stays dependency-free.
/// </summary>
public interface IRetryableQuery
{
    RetryPolicy GetRetryPolicy();
}