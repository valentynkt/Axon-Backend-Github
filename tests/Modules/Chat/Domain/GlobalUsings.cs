// Global using directives for Chat Domain Tests
#nullable enable

// Testing Framework - NUnit
global using NUnit.Framework;
global using NUnit.Framework.Legacy;

// Assertions - Shouldly for fluent, readable assertions
global using Shouldly;

// Mocking - NSubstitute 
global using NSubstitute;
global using NSubstitute.ExceptionExtensions;
global using NSubstitute.Extensions;

// Test Data Generation - Bogus
global using Bogus;
global using AutoBogus;

// Microsoft Testing Extensions
global using Microsoft.Extensions.Time.Testing;

// System imports commonly used in tests
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;

// Domain Under Test - Chat Domain
global using Axon.Modules.Chat.Domain.Aggregates.Conversation;
global using Axon.Modules.Chat.Domain.Entities;
global using Axon.Modules.Chat.Domain.ValueObjects;
global using Axon.Modules.Chat.Domain.Events;
global using Axon.Modules.Chat.Domain.Rules;
global using Axon.Modules.Chat.Domain.Errors;
global using Axon.Modules.Chat.Domain.Specifications;

// BuildingBlocks - Core Domain and Functional Types
global using BuildingBlocks.Core.Domain.Entities.Base;
global using BuildingBlocks.Core.Domain.Rules;
global using BuildingBlocks.Core.Diagnostics.Errors;
global using BuildingBlocks.Core.Diagnostics.Exceptions;
global using BuildingBlocks.Primitives.Ids;