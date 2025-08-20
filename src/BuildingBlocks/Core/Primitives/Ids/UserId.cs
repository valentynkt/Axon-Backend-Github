using StronglyTypedIds;

namespace BuildingBlocks.Primitives.Ids;

// String-backed to play nicely with external identity providers (Auth0/Entra/etc.)
[StronglyTypedId(Template.String, "string-efcore")]
public partial struct UserId { }