using StronglyTypedIds;

namespace BuildingBlocks.Primitives.Ids;

// AI providers often return non-Guid opaque strings; keep string-backed.
[StronglyTypedId(
    backingType: StronglyTypedIdBackingType.String,
    converters: StronglyTypedIdConverter.SystemTextJson |
                StronglyTypedIdConverter.TypeConverter |
                StronglyTypedIdConverter.EfCoreValueConverter |
                StronglyTypedIdConverter.DapperTypeHandler,
    implementations: StronglyTypedIdImplementations.IEquatable |
                     StronglyTypedIdImplementations.IComparable)]
public partial struct AiResponseId;
