namespace BuildingBlocks.Persistence.Infrastructure;

public interface ISeedManager
{
    Task ExecuteSeedAsync();
    Task ExecuteTestSeedAsync();
}