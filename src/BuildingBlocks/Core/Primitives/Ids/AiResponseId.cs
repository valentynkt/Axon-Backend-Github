using StronglyTypedIds;

namespace BuildingBlocks.Primitives.Ids;

// AI providers often return non-Guid opaque strings, but we use Guid for consistency
[StronglyTypedId("string-full")] // Uses default Guid template from assembly attribute
public partial struct AiResponseId { }
