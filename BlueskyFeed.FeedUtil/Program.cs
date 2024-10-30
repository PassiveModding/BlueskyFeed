using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FishyFlip;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;

var configJson = File.ReadAllText("config.json");
var config = JsonSerializer.Deserialize<Config>(configJson, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

if (config == null)
{
    throw new Exception("Failed to deserialize config");
}

var logger = LoggerFactory.Create(builder =>
    {
        builder.AddSimpleConsole(c => c.SingleLine = true);
    })
    .CreateLogger<Program>();

foreach (var feed in config.Feeds)
{
    logger.LogInformation("Put Feed: {Blob}", JsonSerializer.Serialize(feed));
}

logger.LogInformation("Generators to delete: {GeneratorsToDelete}", string.Join(", ", config.FeedsToDelete));

// confirm actions
logger.LogInformation("Confirm actions: [Y/N]");
if (Console.ReadLine()?.ToLower().Trim() != "y")
{
    logger.LogInformation("Aborted");
    return;
}

var proto = new ATProtocolBuilder()
    .WithLogger(logger)
    .Build();


var session = await proto.AuthenticateWithPasswordAsync(config.AtProto.LoginIdentifier, config.AtProto.LoginToken);
if (session == null)
{
    throw new Exception("Failed to authenticate");
}

logger.LogInformation("Did: {Did}, Doc: {Doc}", session.Did, session.DidDoc);
var allFeeds = new List<GeneratorView>();
string? cursor = null;
do
{
    var result = await proto.Feed.GetActorFeedsAsync(session.Did, cursor: cursor);
    var generatorFeed = result.AsT0 ?? throw new Exception("Failed to get actor feeds");
    allFeeds.AddRange(generatorFeed.Feeds);
    cursor = generatorFeed.Cursor;
} while (cursor != null);

await DeleteGenerators();
await UpdateGenerators();
return;

async Task UpdateGenerators()
{
    foreach (var generator in config.Feeds)
    {
        logger.LogInformation("Updating feed generator record {RKey}", generator.RKey);
        var createFeedRecord = new CreateFeedGeneratorRecord(session.Did, generator.RKey,
            new GeneratorRecord
            (
                did: generator.ServiceDid,
                displayName: generator.DisplayName,
                avatar: generator.Avatar,
                description: generator.Description,
                createdAt: generator.CreatedAt
            ));

        var result = await PutFeedGenerator(proto.Repo, createFeedRecord, proto.Options.JsonSerializerOptions);
        if (result.IsT1)
        {
            throw new Exception($"[{result.AsT1.StatusCode}] {result.AsT1.Detail}");
        }
    }
}

async Task DeleteGenerators()
{
    foreach (var rKey in config.FeedsToDelete)
    {
        if (allFeeds.All(x => x.Uri.Rkey != rKey))
        {
            logger.LogInformation("Feed generator record {RKey} does not exist", rKey);
            continue;
        }

        logger.LogInformation("Deleting feed generator record {RKey}", rKey);
        var result = await DeleteFeedGenerator(proto.Repo, rKey);
        if (result.IsT1)
        {
            throw new Exception($"[{result.AsT1.StatusCode}] {result.AsT1.Detail}");
        }
    }
}

// static Task<Result<RecordRef>> CreateFeedGenerator(ATProtoRepo repo, CreateFeedGeneratorRecord generatorRecord,
//     JsonSerializerOptions options,
//     CancellationToken cancellationToken = default)
// {
//     var recordRefTypeInfo = (JsonTypeInfo<RecordRef>) options.GetTypeInfo(typeof(RecordRef));
//     var createFeedRecordTypeInfo =
//         (JsonTypeInfo<CreateFeedGeneratorRecord>) FeedSourceGen.Default.Options.GetTypeInfo(
//             typeof(CreateFeedGeneratorRecord));
//
//     return repo.CreateRecord(generatorRecord, createFeedRecordTypeInfo, recordRefTypeInfo, cancellationToken);
// }

static Task<Result<RecordRef>> PutFeedGenerator(ATProtoRepo repo, CreateFeedGeneratorRecord generatorRecord,
    JsonSerializerOptions options,
    CancellationToken cancellationToken = default)
{
    var recordRefTypeInfo = (JsonTypeInfo<RecordRef>) options.GetTypeInfo(typeof(RecordRef));
    var createFeedRecordTypeInfo =
        (JsonTypeInfo<CreateFeedGeneratorRecord>) FeedSourceGen.Default.Options.GetTypeInfo(
            typeof(CreateFeedGeneratorRecord));

    return repo.PutRecord(generatorRecord, createFeedRecordTypeInfo, recordRefTypeInfo, cancellationToken);
}

static Task<Result<Success>> DeleteFeedGenerator(ATProtoRepo repo, string rKey,
    CancellationToken cancellationToken = default)
{
    return repo.DeleteRecordAsync(Constants.FeedType.Generator, rKey, cancellationToken: cancellationToken);
}

public class Config
{
    public class AtProtoConfig
    {
        public string LoginIdentifier { get; set; } = null!;
        public string LoginToken { get; set; } = null!;
    }
    
    public AtProtoConfig AtProto { get; init; } = null!;
    public GeneratorRecordRequest[] Feeds { get; init; } = null!;
    public string[] FeedsToDelete { get; init; } = null!;
}

public record GeneratorRecordRequest(string ServiceDid, string RKey, string DisplayName, string? Avatar, string Description, DateTime? CreatedAt = null);

internal class CreateFeedGeneratorRecord(ATDid repo, string rKey, GeneratorRecord record)
{
    [JsonPropertyName("collection")] public string Collection { get; init; } = Constants.FeedType.Generator;

    [JsonPropertyName("repo")] public string Repo { get; } = repo.Handler;

    [JsonPropertyName("rkey")] public string RKey { get; } = rKey;

    [JsonPropertyName("record")] public GeneratorRecord Record { get; } = record;
}

internal class GeneratorRecord(string did, string displayName, string? avatar, string description, DateTime? createdAt)
    : ATRecord
{
    [JsonPropertyName("did")] public string Did { get; } = did;

    [JsonPropertyName("displayName")] public string DisplayName { get; } = displayName;

    [JsonPropertyName("avatar")] public string? Avatar { get; } = avatar;

    [JsonPropertyName("description")] public string? Description { get; } = description;

    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; } = createdAt ?? DateTime.UtcNow;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(CreateFeedGeneratorRecord))]
[JsonSerializable(typeof(RecordRef))]
internal partial class FeedSourceGen : JsonSerializerContext
{
}