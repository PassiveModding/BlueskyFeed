using BlueskyFeed.Common;
using BlueskyFeed.Common.Db;

namespace BlueskyFeed.Jetstream;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.AddServiceDefaults();

        builder.Services.AddProblemDetails();
        builder.AddRedisClient(connectionName: "redis");
        builder.Services.AddSingleton<FeedRepository>();
        builder.Services.AddHostedService<JetStreamListener>();
        var app = builder.Build();

        app.UseExceptionHandler();
        app.MapDefaultEndpoints();
        
        await app.RunAsync();
    }
}