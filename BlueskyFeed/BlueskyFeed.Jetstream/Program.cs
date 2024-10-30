using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlueskyFeed.Jetstream;

public class Program
{
    public static void Main(string[] args)
    {
        var host = Host.CreateApplicationBuilder(args);

        host.AddRedisClient(connectionName: "redis");
        host.AddServiceDefaults();
        
        host.Services.AddHostedService<JetStreamListener>();
        
        var app = host.Build();
        
        app.Run();
    }
}