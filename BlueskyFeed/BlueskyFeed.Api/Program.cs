using System.Reflection;
using BlueskyFeed.Api.Generator;
using BlueskyFeed.Api.Services;
using BlueskyFeed.Auth;
using BlueskyFeed.Common;
using BlueskyFeed.Common.Db;

namespace BlueskyFeed.Api;

public class Program
{
    private static void RegisterInterfaces<T>(IServiceCollection services)
    {
        var feedGeneratorTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(x => x.GetInterfaces().Contains(typeof(T)) && x is {IsAbstract: false, IsInterface: false});
        foreach (var type in feedGeneratorTypes)
        {
            services.AddSingleton(typeof(T), type);
            services.AddSingleton(type);
        }
    }
    
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.Services.AddProblemDetails();
        builder.Services.AddControllers();
        builder.AddRedisClient(connectionName: "redis");
        builder.Services.AddOptions<AtProtoConfig>()
            .BindConfiguration(AtProtoConfig.SectionName)
            .ValidateDataAnnotations();
        
        RegisterInterfaces<IFeedGenerator>(builder.Services);
        RegisterInterfaces<IService>(builder.Services);
        
        builder.Services.AddSingleton<DidResolver>();
        builder.Services.AddSingleton<FeedRepository>();

        var app = builder.Build();

        app.MapControllers();
        app.UseExceptionHandler();
        app.MapDefaultEndpoints();

        await app.RunAsync();
    }
}