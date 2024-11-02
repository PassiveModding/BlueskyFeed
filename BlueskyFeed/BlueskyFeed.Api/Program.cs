using System.Reflection;
using System.Text.Json.Serialization;
using BlueskyFeed.Api.Generator;
using BlueskyFeed.Api.Services;
using BlueskyFeed.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static BlueskyFeed.Auth.Auth;

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
        builder.AddRedisClient("redis");
        builder.Services.AddOptions<AtProtoConfig>()
            .BindConfiguration(AtProtoConfig.SectionName)
            .ValidateDataAnnotations();
        
        RegisterInterfaces<IFeedGenerator>(builder.Services);
        RegisterInterfaces<IService>(builder.Services);
        
        builder.Services.AddSingleton<DidResolver>();

        var app = builder.Build();
        
        var feeds = app.Services.GetServices<IFeedGenerator>().ToArray();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var sessionService = app.Services.GetRequiredService<SessionService>();
        foreach (var feed in feeds)
        {
            var session = await sessionService.GetSessionAsync();
            logger.LogInformation("Registered feed {Feed}", feed.GetUri(session.Did.Handler));
        }

        app.UseExceptionHandler();
        app.MapGet("/ping", () => new { Message = "Pong", Timestamp = DateTimeOffset.UtcNow })
            .WithName("Ping")
            .WithOpenApi();

        app.MapGet("/.well-known/did.json", (IOptions<AtProtoConfig> config) => new WellKnownDidResponse(
            Context: ["https://www.w3.org/ns/did/v1"],
            Id: config.Value.ServiceDid,
            Service:
            [
                new Service(
                    Id: "#bsky_fg",
                    Type: "BskyFeedGenerator",
                    ServiceEndpoint: $"https://{config.Value.HostName}"
                )
            ]
        ));
        
        app.MapGet("/xrpc/app.bsky.feed.describeFeedGenerator", async (
            IOptions<AtProtoConfig> config, 
            SessionService sessionService) =>
        {
            var session = await sessionService.GetSessionAsync();
            var response = new
            {
                encoding = "application/json",
                body = new
                {
                    did = config.Value.ServiceDid,
                    feeds = app.Services.GetServices<IFeedGenerator>().Select(x => new
                    {
                        uri = x.GetUri(session.Did.Handler),
                    }).ToArray()
                },
            };
            return Results.Ok(response);
        });
        
        app.MapGet("/xrpc/app.bsky.feed.getFeedSkeleton", async (string feed, 
            string? cursor, 
            int limit,
            [FromHeader(Name = "Authorization")] string? authorization,
            DidResolver didResolver,
            SessionService sessionService,
            IOptions<AtProtoConfig> config,
            ILogger<Program> logger) =>
        {
            var session = await sessionService.GetSessionAsync();
            logger.LogInformation("Retrieving feed {Feed} with cursor {Cursor} and limit {Limit}", feed, cursor, limit);
            var generator = app.Services.GetServices<IFeedGenerator>().FirstOrDefault(x => x.GetUri(session.Did.Handler).ToString() == feed);
            if (generator is null)
            {
                return Results.BadRequest(new
                {
                    error = "UnsupportedAlgorithm",
                    error_description = "Unsupported Algorithm"
                });
            }

            if (generator is IAuthorizedFeedGenerator authorizedFeedGenerator)
            {
                if (authorization == null)
                {
                    return Results.BadRequest(new
                    {
                        error = "UnsupportedAlgorithm",
                        error_description = "Authorization required"
                    });
                }
                
                var token = authorization.Replace("Bearer ", "").Trim();
                var validation = await VerifyJwt(token, config.Value.ServiceDid, didResolver);
                
                var response = await authorizedFeedGenerator.RetrieveAsync(cursor, limit, validation, CancellationToken.None);
                return Results.Ok(response.ToObject());
            }

            if (generator is IUnauthorizedFeedGenerator unauthorizedFeedGenerator)
            {
                var response = await unauthorizedFeedGenerator.RetrieveAsync(cursor, limit, CancellationToken.None);
                return Results.Ok(response.ToObject());
            }

            return Results.BadRequest(new
            {
                error = "UnsupportedAlgorithm",
                error_description = "Unsupported Generator Type"
            });
        });

        app.MapDefaultEndpoints();

        await app.RunAsync();
    }
    
    private record WellKnownDidResponse(
        [property: JsonPropertyName("@context")] string[] Context,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("service")] Service[] Service);

    private record Service(
        [property: JsonPropertyName("id")] string Id, 
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("serviceEndpoint")] string ServiceEndpoint);
}