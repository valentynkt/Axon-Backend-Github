namespace BuildingBlocks.Infrastructure.Messaging.MassTransit;

public class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public string HostName => Host; // Alias for backwards compatibility
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string UserName => Username; // Alias for backwards compatibility
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;
    public bool UseSsl { get; set; }
}