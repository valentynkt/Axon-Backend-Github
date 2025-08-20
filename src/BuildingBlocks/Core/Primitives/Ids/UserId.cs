using StronglyTypedIds;

namespace BuildingBlocks.Primitives.Ids;

// String-backed to play nicely with external identity providers (Auth0/Entra/etc.)
[StronglyTypedId(
    backingType: StronglyTypedIdBackingType.Guid,
    converters: StronglyTypedIdConverter.SystemTextJson |
                StronglyTypedIdConverter.TypeConverter |
                StronglyTypedIdConverter.EfCoreValueConverter |
                StronglyTypedIdConverter.DapperTypeHandler,
    implementations: StronglyTypedIdImplementations.IEquatable |
                     StronglyTypedIdImplementations.IComparable)]
public partial struct UserId;