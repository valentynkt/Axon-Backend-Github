// StronglyTypedIdDefaults.cs - Sets default template for all IDs in the project
using StronglyTypedIds;

// Use built-in Guid template plus EF Core converter from Templates package
// This provides the core functionality plus EF Core support without unnecessary converters
// Individual IDs can override this by specifying their own template
[assembly: StronglyTypedIdDefaults(Template.Guid, "guid-efcore")]