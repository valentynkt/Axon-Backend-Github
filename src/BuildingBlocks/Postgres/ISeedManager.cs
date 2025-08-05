namespace BuildingBlocks.Postgres;

public interface ISeedManager
{
    Task ExecuteSeedAsync();
    Task ExecuteTestSeedAsync();
}