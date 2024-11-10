using System.Text.Json;
using System.Text.RegularExpressions;
using FishyFlip;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using NRediSearch;

namespace BlueskyFeed.Common.Db;

public partial class FeedRepository
{
    private readonly IDatabase _database;
    private readonly ILogger<FeedRepository> _logger;
    private readonly Client _likeClient;

    public FeedRepository(ILogger<FeedRepository> logger, IConnectionMultiplexer connectionMultiplexer)
    {
        _logger = logger;
        _database = connectionMultiplexer.GetDatabase();
        _likeClient = new Client(LikeRecord.Collection, _database);

        try
        {
            LikeRecord.CreateSchema(_likeClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schema for {Collection}", LikeRecord.Collection);
        }
    }
    
    public async Task<bool> AddLikeAsync(string handle, string rkey, Like like)
    {
        var record = new LikeRecord(handle, rkey, like);
        var doc = record.ToDocument();
        var existing = await _likeClient.GetDocumentAsync(doc.Id);
        if (existing != null)
        {
            return false;
        }

        var created = await _likeClient.AddDocumentAsync(doc);
        await _database.KeyExpireAsync(doc.Id, TimeSpan.FromDays(1));
        return created;
    }
    
    public async Task<bool> RemoveLikeAsync(string handle, string rkey)
    {
        var id = LikeRecord.ConstructKey(handle, rkey);
        var deleted = await _likeClient.DeleteDocumentAsync(id);
        return deleted;
    }
    
    public async Task<LikeRecord[]> GetLikesByHandlesAsync(IEnumerable<string> handles, int limit, string? cursor = null)
    {
        var handleRegex = PlcHandleRegex();
        var handleSet = handles
            .Select(h =>
            {
                if (handleRegex.IsMatch(h))
                {
                    return h.Split(":").Last();
                }

                if (PlcRegex().IsMatch(h))
                {
                    return h;
                }

                throw new ArgumentException($"Handle must start with 'did:plc:' or be a plc handle but was {h}", nameof(handles));
            })
            .ToHashSet();
        
        _logger.LogInformation("Getting {Limit} likes for {CountHandles} handles with cursor {Cursor}", limit, handleSet.Count, cursor);
        var pCursor = Cursor.FromString(cursor);
        var handlePart = string.Join(" | ", handleSet);
        var indexedAtPart = pCursor != null ? $"@{nameof(LikeRecord.IndexedAt)}:[0 {pCursor.Timestamp}]" : "";
        var queryStr = $"@{nameof(LikeRecord.Plc)}:({handlePart}) {indexedAtPart}";
        var query = new Query(queryStr)
            .SetSortBy(nameof(LikeRecord.IndexedAt), false)
            .Limit(0, limit);

        var results = await _likeClient.SearchAsync(query);
        var output = results.Documents.Select(LikeRecord.FromDocument)
            .ToArray();
        
        _logger.LogInformation("Found {CountLikes} likes for {CountHandles} handles with cursor {Cursor}", output.Length, handleSet.Count, cursor);
        return output;
    }

    [GeneratedRegex("^did:plc:[a-z0-9]+$")]
    internal static partial Regex PlcHandleRegex();
    
    [GeneratedRegex("^[a-z0-9]+$")]
    internal static partial Regex PlcRegex();
}