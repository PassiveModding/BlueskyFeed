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
        builder.Services.AddSingleton<LikeRepository>();
        builder.Services.AddHostedService<JetStreamListener>();
        var app = builder.Build();

        app.UseExceptionHandler();
        app.MapDefaultEndpoints();
        
        await app.RunAsync();
    }
}