namespace BuildingBlocks.Infrastructure.Persistence.Infrastructure
{
    public interface IDataSeeder
    {
        Task SeedAllAsync();
    }

    public interface ITestDataSeeder
    {
        Task SeedAllAsync();
    }
}