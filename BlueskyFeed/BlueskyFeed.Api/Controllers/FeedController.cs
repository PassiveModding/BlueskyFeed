using System.Text.Json.Serialization;
using BlueskyFeed.Api.Generator;
using BlueskyFeed.Api.Services;
using BlueskyFeed.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlueskyFeed.Api.Controllers;

[ApiController]
public class FeedController : ControllerBase
{
    private readonly IOptions<AtProtoConfig> _config;
    private readonly SessionService _sessionService;
    private readonly DidResolver _didResolver;
    private readonly ILogger<FeedController> _logger;
    private readonly IEnumerable<IFeedGenerator> _feedGenerators;

    public FeedController(IOptions<AtProtoConfig> config, 
        SessionService sessionService,
        DidResolver didResolver,
        ILogger<FeedController> logger,
        IEnumerable<IFeedGenerator> feedGenerators)

    {
        _config = config;
        _sessionService = sessionService;
        _didResolver = didResolver;
        _logger = logger;
        _feedGenerators = feedGenerators;
    }
    
    [HttpGet("/ping")]
    public Task<IActionResult>
        PingAsync()
    {
        return Task.FromResult<IActionResult>(Ok(new { Message = "Pong", Timestamp = DateTimeOffset.UtcNow }));
    }
    
    [HttpGet("/.well-known/did.json")]
    public Task<IActionResult>
        WellKnownDidAsync()
    {
        if (!_config.Value.ServiceDid.EndsWith(_config.Value.HostName))
        {
            return Task.FromResult<IActionResult>(NotFound());
        }
        
        var response = new WellKnownDidResponse(
            Context: ["https://www.w3.org/ns/did/v1"],
            Id: _config.Value.ServiceDid,
            Service:
            [
                new Service(
                    Id: "#bsky_fg",
                    Type: "BskyFeedGenerator",
                    ServiceEndpoint: $"https://{_config.Value.HostName}"
                )
            ]
        );
        
        return Task.FromResult<IActionResult>(Ok(response));
    }
    
    private record WellKnownDidResponse(
        [property: JsonPropertyName("@context")] string[] Context,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("service")] Service[] Service);

    private record Service(
        [property: JsonPropertyName("id")] string Id, 
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("serviceEndpoint")] string ServiceEndpoint);


    [HttpGet("/xrpc/app.bsky.feed.describeFeedGenerator")]
    public async Task<IActionResult>
        DescribeFeedGeneratorAsync()
    {
        var session = await _sessionService.GetSessionAsync();
        var response = new
        {
            encoding = "application/json",
            body = new
            {
                did = _config.Value.ServiceDid,
                feeds = _feedGenerators.Select(x => new
                {
                    uri = x.GetUri(session.Did.Handler),
                }).ToArray()
            },
        };
        return Ok(response);
    }
    
    [HttpGet("/xrpc/app.bsky.feed.getFeedSkeleton")]
    public async Task<IActionResult>
        GetFeedSkeletonAsync(
        string feed,
        string? cursor, 
        int limit, 
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithFeed(feed)
            .WithCursor(cursor)
            .WithLimit(limit);
        
        var session = await _sessionService.GetSessionAsync(cancellationToken);
        _logger.LogInformation("Retrieving feed {Feed} with cursor {Cursor} and limit {Limit}", feed, cursor, limit);
        var generator = _feedGenerators.FirstOrDefault(x => x.GetUri(session.Did.Handler).ToString() == feed);
        if (generator is null)
        {
            return BadRequest(new
            {
                error = "UnsupportedAlgorithm",
                error_description = "Unsupported Algorithm"
            });
        }

        if (generator is IAuthorizedFeedGenerator authorizedFeedGenerator)
        {
            if (authorization == null)
            {
                return BadRequest(new
                {
                    error = "UnsupportedAlgorithm",
                    error_description = "Authorization required"
                });
            }
                
            var token = authorization.Replace("Bearer ", "").Trim();
            var validation = await _didResolver.VerifyJwt(token, _config.Value.ServiceDid);
            activity?.WithIssuerDid(validation);
            
            var response = await authorizedFeedGenerator.RetrieveAsync(cursor, limit, validation, CancellationToken.None);
            return Ok(response.ToObject());
        }

        if (generator is IUnauthorizedFeedGenerator unauthorizedFeedGenerator)
        {
            var response = await unauthorizedFeedGenerator.RetrieveAsync(cursor, limit, CancellationToken.None);
            return Ok(response.ToObject());
        }

        return BadRequest(new
        {
            error = "UnsupportedAlgorithm",
            error_description = "Unsupported Generator Type"
        });
    }
}