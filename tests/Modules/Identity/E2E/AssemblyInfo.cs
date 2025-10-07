using NUnit.Framework;

// CRITICAL FIX: Prevent parallel test execution to avoid WebApplicationFactory/FastEndpoints conflicts
// YamlDotNet ReflectionTypeLoadException occurs when multiple fixtures try to create Factory concurrently
// Sequential execution ensures clean Factory creation and disposal between fixtures
[assembly: NonParallelizable]
