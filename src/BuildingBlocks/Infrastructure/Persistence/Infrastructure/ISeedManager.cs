namespace BuildingBlocks.Infrastructure.Persistence.Infrastructure;

public interface ISeedManager
{
    Task ExecuteSeedAsync();
    Task ExecuteTestSeedAsync();
}