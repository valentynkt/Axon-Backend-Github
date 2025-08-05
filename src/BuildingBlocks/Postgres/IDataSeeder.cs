namespace BuildingBlocks.Postgres
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