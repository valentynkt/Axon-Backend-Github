using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Domain.Tests.TestInfrastructure.Mothers;

/// <summary>
/// Mother object providing pre-configured User-related test data.
/// Centralizes user scenarios for consistency across tests.
/// </summary>
public static class UserMother
{
    private static readonly Random Random = new(42); // Fixed seed for reproducibility
    
    /// <summary>
    /// Creates a standard test user ID.
    /// </summary>
    public static UserId DefaultUser()
    {
        return UserId.From(new Guid("550e8400-e29b-41d4-a716-446655440001"));
    }

    /// <summary>
    /// Creates a second standard test user ID.
    /// </summary>
    public static UserId AlternateUser()
    {
        return UserId.From(new Guid("550e8400-e29b-41d4-a716-446655440002"));
    }

    /// <summary>
    /// Creates an admin user ID.
    /// </summary>
    public static UserId AdminUser()
    {
        return UserId.From(new Guid("550e8400-e29b-41d4-a716-446655440999"));
    }

    /// <summary>
    /// Creates a system user ID.
    /// </summary>
    public static UserId SystemUser()
    {
        return UserId.From(new Guid("00000000-0000-0000-0000-000000000001"));
    }

    /// <summary>
    /// Creates a new random user ID.
    /// </summary>
    public static UserId RandomUser()
    {
        return UserId.New();
    }

    /// <summary>
    /// Creates a collection of unique user IDs.
    /// </summary>
    public static List<UserId> MultipleUsers(int count = 5)
    {
        var users = new List<UserId>();
        for (int i = 0; i < count; i++)
        {
            users.Add(UserId.New());
        }
        return users;
    }

    /// <summary>
    /// Named user scenarios for testing.
    /// </summary>
    public static class Named
    {
        public static UserId Alice()
        {
            return UserId.From(new Guid("a11ce000-0000-0000-0000-000000000001"));
        }

        public static UserId Bob()
        {
            return UserId.From(new Guid("b0b00000-0000-0000-0000-000000000001"));
        }

        public static UserId Charlie()
        {
            return UserId.From(new Guid("c4a411e0-0000-0000-0000-000000000001"));
        }

        public static UserId Diana()
        {
            return UserId.From(new Guid("d1a9a000-0000-0000-0000-000000000001"));
        }

        public static UserId Eve()
        {
            return UserId.From(new Guid("e5e00000-0000-0000-0000-000000000001"));
        }
    }

    /// <summary>
    /// User groups for testing multi-user scenarios.
    /// </summary>
    public static class Groups
    {
        public static List<UserId> TeamAlpha()
        {
            return new List<UserId>
            {
                Named.Alice(),
                Named.Bob(),
                Named.Charlie()
            };
        }

        public static List<UserId> TeamBeta()
        {
            return new List<UserId>
            {
                Named.Diana(),
                Named.Eve(),
                RandomUser()
            };
        }

        public static List<UserId> AllNamedUsers()
        {
            return new List<UserId>
            {
                Named.Alice(),
                Named.Bob(),
                Named.Charlie(),
                Named.Diana(),
                Named.Eve()
            };
        }
    }

    /// <summary>
    /// Special user IDs for edge case testing.
    /// </summary>
    public static class EdgeCases
    {
        /// <summary>
        /// Maximum GUID value user ID.
        /// </summary>
        public static UserId MaxGuid()
        {
            return UserId.From(new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"));
        }

        /// <summary>
        /// Minimum non-empty GUID value user ID.
        /// </summary>
        public static UserId MinGuid()
        {
            return UserId.From(new Guid("00000000-0000-0000-0000-000000000001"));
        }

        /// <summary>
        /// Sequential user IDs for ordering tests.
        /// </summary>
        public static List<UserId> Sequential(int count = 5)
        {
            var users = new List<UserId>();
            for (int i = 1; i <= count; i++)
            {
                var guidString = $"00000000-0000-0000-0000-{i:D12}";
                users.Add(UserId.From(new Guid(guidString)));
            }
            return users;
        }
    }

    /// <summary>
    /// User IDs from string representations.
    /// </summary>
    public static class FromString
    {
        public static Result<UserId> Valid()
        {
            return UserId.FromString("550e8400-e29b-41d4-a716-446655440001");
        }

        public static Result<UserId> InvalidFormat()
        {
            return UserId.FromString("not-a-guid");
        }

        public static Result<UserId> Empty()
        {
            return UserId.FromString(string.Empty);
        }

        public static Result<UserId> EmptyGuid()
        {
            return UserId.FromString("00000000-0000-0000-0000-000000000000");
        }
    }

    /// <summary>
    /// Creates a mapping of users to roles for testing.
    /// </summary>
    public static Dictionary<UserId, string> UserRoleMapping()
    {
        return new Dictionary<UserId, string>
        {
            { AdminUser(), "Admin" },
            { DefaultUser(), "User" },
            { AlternateUser(), "User" },
            { Named.Alice(), "Moderator" },
            { Named.Bob(), "User" }
        };
    }

    /// <summary>
    /// Generates a batch of users with specific patterns.
    /// </summary>
    public static class Patterns
    {
        /// <summary>
        /// Creates users that will sort in a specific order.
        /// </summary>
        public static List<(UserId Id, string Name)> OrderedUsers()
        {
            return new List<(UserId, string)>
            {
                (UserId.From(new Guid("10000000-0000-0000-0000-000000000000")), "First"),
                (UserId.From(new Guid("20000000-0000-0000-0000-000000000000")), "Second"),
                (UserId.From(new Guid("30000000-0000-0000-0000-000000000000")), "Third"),
                (UserId.From(new Guid("40000000-0000-0000-0000-000000000000")), "Fourth"),
                (UserId.From(new Guid("50000000-0000-0000-0000-000000000000")), "Fifth")
            };
        }

        /// <summary>
        /// Creates users with similar GUIDs for collision testing.
        /// </summary>
        public static List<UserId> SimilarUsers()
        {
            return new List<UserId>
            {
                UserId.From(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                UserId.From(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab")),
                UserId.From(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaac")),
                UserId.From(new Guid("aaaaaaaa-aaaa-aaaa-aaab-aaaaaaaaaaaa")),
                UserId.From(new Guid("aaaaaaaa-aaaa-aaab-aaaa-aaaaaaaaaaaa"))
            };
        }
    }

    /// <summary>
    /// Selects a random user from a predefined set.
    /// </summary>
    public static UserId RandomFromSet()
    {
        var users = Groups.AllNamedUsers();
        return users[Random.Next(users.Count)];
    }

    /// <summary>
    /// Creates a user ID for a specific test scenario.
    /// </summary>
    public static UserId ForScenario(string scenario)
    {
        return scenario switch
        {
            "new-user" => RandomUser(),
            "existing-user" => DefaultUser(),
            "admin" => AdminUser(),
            "system" => SystemUser(),
            "banned" => UserId.From(new Guid("DEADBEEF-DEAD-BEEF-DEAD-BEEFDEADBEEF")),
            _ => RandomUser()
        };
    }
}