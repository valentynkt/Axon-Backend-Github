using System.Linq.Expressions;

/// <summary>
/// MongoDB.Driver interface stubs for compilation compatibility
/// These minimal implementations allow the existing MongoDB interfaces to compile
/// without the MongoDB.Driver dependency while providing PostgreSQL implementations
/// </summary>
namespace MongoDB.Driver
{
    public interface IMongoCollection<T>
    {
        Task InsertOneAsync(T entity, InsertOneOptions? options = null, CancellationToken cancellationToken = default);
        Task InsertManyAsync(IEnumerable<T> entities, InsertManyOptions? options = null, CancellationToken cancellationToken = default);
        Task<ReplaceOneResult> ReplaceOneAsync(Expression<Func<T, bool>> filter, T replacement, ReplaceOptions? options = null, CancellationToken cancellationToken = default);
        Task<DeleteResult> DeleteOneAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter);
        IMongoQueryable<T> AsQueryable();
    }

    public interface IFindFluent<T, TProjection>
    {
        Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default);
        Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    }

    public interface IMongoQueryable<T> : IQueryable<T>
    {
        Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    }

    public interface IMongoClient : IDisposable
    {
        IMongoDatabase GetDatabase(string name);
        Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken = default);
    }

    public interface IMongoDatabase
    {
        IMongoCollection<T> GetCollection<T>(string name);
    }

    public interface IClientSessionHandle : IDisposable
    {
        bool IsInTransaction { get; }
        void StartTransaction();
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task AbortTransactionAsync(CancellationToken cancellationToken = default);
    }

    public class ReplaceOneResult
    {
        public long ModifiedCount { get; set; }
    }

    public class DeleteResult
    {
        public long DeletedCount { get; set; }
    }

    public class InsertOneOptions
    {
    }

    public class InsertManyOptions
    {
    }

    public class ReplaceOptions
    {
    }

    namespace Linq
    {
        public static class IMongoQueryableExtensions
        {
            public static Task<List<T>> ToListAsync<T>(this IQueryable<T> queryable, CancellationToken cancellationToken = default)
            {
                if (queryable is IMongoQueryable<T> mongoQueryable)
                {
                    return mongoQueryable.ToListAsync(cancellationToken);
                }
                
                // Fallback to regular LINQ
                return Task.FromResult(queryable.ToList());
            }
        }
    }
}

namespace MongoDB.Bson
{
    public enum BsonType
    {
        String,
        ObjectId,
        Boolean,
        DateTime,
        Int32,
        Int64,
        Double,
        Decimal128
    }

    public static class BsonSerializer
    {
        public static void RegisterSerializer<T>(IBsonSerializer<T> serializer)
        {
            // No-op for compilation compatibility
        }
    }

    public interface IBsonSerializer<T>
    {
    }

    public class GuidSerializer : IBsonSerializer<Guid>
    {
        public GuidSerializer(BsonType representation)
        {
            // No-op for compilation compatibility
        }
    }

    namespace Serialization
    {
        public interface IBsonClassMap
        {
        }

        public class BsonClassMap
        {
            public static void RegisterClassMap<T>(Action<BsonClassMap<T>> classMapInitializer)
            {
                // No-op for compilation compatibility
            }
        }

        public class BsonClassMap<T> : BsonClassMap
        {
            public BsonMemberMap MapMember<TMember>(Expression<Func<T, TMember>> memberLambda)
            {
                return new BsonMemberMap();
            }

            public BsonClassMap<T> SetIgnoreExtraElements(bool value)
            {
                return this;
            }

            public BsonClassMap<T> SetDiscriminator(string discriminator)
            {
                return this;
            }
        }

        public class BsonMemberMap
        {
            public BsonMemberMap SetElementName(string elementName)
            {
                return this;
            }

            public BsonMemberMap SetIgnoreIfDefault(bool value)
            {
                return this;
            }

            public BsonMemberMap SetDefaultValue(object value)
            {
                return this;
            }
        }

        namespace Conventions
        {
            public interface IConvention
            {
            }

            public class ConventionPack : List<IConvention>
            {
            }

            public static class ConventionRegistry
            {
                public static void Register(string name, ConventionPack pack, Func<Type, bool> filter)
                {
                    // No-op for compilation compatibility
                }
            }

            public class CamelCaseElementNameConvention : IConvention
            {
            }

            public class IgnoreExtraElementsConvention : IConvention
            {
                public IgnoreExtraElementsConvention(bool value)
                {
                }
            }

            public class EnumRepresentationConvention : IConvention
            {
                public EnumRepresentationConvention(BsonType representation)
                {
                }
            }

            public class IgnoreIfDefaultConvention : IConvention
            {
                public IgnoreIfDefaultConvention(bool value)
                {
                }
            }

            public abstract class ConventionBase : IConvention
            {
            }

            public interface IClassMapConvention : IConvention
            {
                void Apply(BsonClassMap classMap);
            }
        }

        namespace Serializers
        {
            public class GuidSerializer : IBsonSerializer<Guid>
            {
                public GuidSerializer(BsonType representation)
                {
                }
            }
        }
    }
}