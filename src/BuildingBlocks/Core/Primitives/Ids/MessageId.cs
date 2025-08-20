using StronglyTypedIds;

namespace BuildingBlocks.Primitives.Ids;

[StronglyTypedId(
    backingType: StronglyTypedIdBackingType.Guid,
    converters: StronglyTypedIdConverter.SystemTextJson |
                StronglyTypedIdConverter.TypeConverter |
                StronglyTypedIdConverter.EfCoreValueConverter |
                StronglyTypedIdConverter.DapperTypeHandler,
    implementations: StronglyTypedIdImplementations.IEquatable |
                     StronglyTypedIdImplementations.IComparable)]
public partial struct MessageId;