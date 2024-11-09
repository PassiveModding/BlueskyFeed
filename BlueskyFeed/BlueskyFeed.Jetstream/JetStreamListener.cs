using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using BlueskyFeed.Common;
using FishyFlip;
using FishyFlip.Events;
using FishyFlip.Models;
using StackExchange.Redis;

namespace BlueskyFeed.Jetstream;

public class JetStreamListener : IHostedService
{
    private static readonly string[]? WantedCollections = null;//[Constants.FeedType.Like, Constants.FeedType.Post];
    
    
    private ATJetStream? _jetStream;
    private readonly ILogger<JetStreamListener> _logger;
    private readonly IDatabase _database;
    private long _count;
    private long _lastCount;
    private DateTime _lastCheck;
    private Timer _timer;
    private int _reconnectAttempts;
    

    public JetStreamListener(ILogger<JetStreamListener> logger, IConnectionMultiplexer connectionMultiplexer)
    {
        _logger = logger;
        _database = connectionMultiplexer.GetDatabase();
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting EventProvider");
        _jetStream = new ATJetStreamBuilder()
            .WithLogger(_logger)
            .Build();
        
        _jetStream.OnRecordReceived += HandleReceive;
        _jetStream.OnConnectionUpdated += HandleConnectionUpdated;
        
        _lastCheck = DateTime.UtcNow;
        _lastCount = 0;
        _timer = new Timer(_ =>
        {
            CleanupSets();
            if (_count == _lastCount)
            {
                // connection probably dropped, attempt to restore
                HandleWebSocketState(WebSocketState.Closed, true);
            }
            
            var elapsed = DateTime.UtcNow - _lastCheck;
            var diff = _count - _lastCount;
            _logger.LogInformation("Received {Count} records in {Elapsed}", diff, elapsed);
            _lastCheck = DateTime.UtcNow;
            _lastCount = _count;
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        await ConnectAsync(cancellationToken);
    }
    
    private async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_jetStream != null)
        {
            await _jetStream.ConnectAsync(wantedCollections: WantedCollections, token: cancellationToken);
        }
    }
    
    private async void HandleWebSocketState(WebSocketState state, bool force = false)
    {
        if (_reconnectAttempts > 5)
        {
            _logger.LogError("Failed to reconnect after {Attempts} attempts, stopping", _reconnectAttempts);
            Environment.Exit(1);
        }

        if (force)
        {
            _logger.LogWarning("Connection closed, reconnecting");
            await ConnectAsync();
            _reconnectAttempts++;
            return;
        }
        
        if (state == WebSocketState.Closed)
        {
            _logger.LogWarning("Connection closed, reconnecting");
            await ConnectAsync();
            _reconnectAttempts++;
        }
        else if (state == WebSocketState.Aborted)
        {
            _logger.LogError("Connection aborted, reconnecting");
            await ConnectAsync();
            _reconnectAttempts++;
        }
        else if (state == WebSocketState.Open)
        {
            _logger.LogInformation("Connection established after {Attempts} attempts", _reconnectAttempts);
            _reconnectAttempts = 0;
        }
        else
        {
            _logger.LogInformation("Connection state: {State}", state);
        }
    }
    
    private void HandleConnectionUpdated(object? sender, SubscriptionConnectionStatusEventArgs e)
    {
        HandleWebSocketState(e.State);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
        _logger.LogInformation("Stopping EventProvider");
        if (_jetStream != null)
        {
            _jetStream.OnRecordReceived -= HandleReceive;
            _jetStream.Dispose();
        }
        
        return Task.CompletedTask;
    }
    
    private async void HandleReceive(object? sender, JetStreamATWebSocketRecordEventArgs args)
    {
        Interlocked.Increment(ref _count);
        DiagnosticsConfig.EventsCounter.Add(1, new TagList(
               [
                    new KeyValuePair<string, object?>("kind", args.Record.Kind),
                    new KeyValuePair<string, object?>("collection", args.Record.Commit?.Collection ?? string.Empty),
                    new KeyValuePair<string, object?>("operation", args.Record.Commit?.Operation.ToString() ?? string.Empty),
               ]
            ));
        await HandleReceiveAsync(args);
    }

    private async Task HandleReceiveAsync(JetStreamATWebSocketRecordEventArgs args)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithDid(args.Record.Did?.Handler ?? string.Empty)
            .WithRKey(args.Record.Commit?.RKey ?? string.Empty)
            .WithCollection(args.Record.Commit?.Collection ?? string.Empty)
            .WithOperation(args.Record.Commit?.Operation ?? ATWebSocketCommitType.Unknown)
            .WithKind(args.Record.Kind);
        
        if (args.Record.Did == null 
            || args.Record.Commit == null 
            || args.Record.Commit.Collection == null 
            || args.Record.Commit.RKey == null)
        {
            return;
        }
        
        var key = new Key(args.Record.Commit.Collection, args.Record.Did.Handler, args.Record.Commit.RKey);
        if (args.Record.Commit.Operation is ATWebSocketCommitType.Create && args.Record.Commit.Record != null)
        {
            await HandleRecord(key, args);
        }
        else if (args.Record.Commit.Operation == ATWebSocketCommitType.Update)
        {
            // ignore for now
        }
        else if (args.Record.Commit.Operation == ATWebSocketCommitType.Delete)
        {
            await _database.KeyDeleteAsync(key.ToString());
        }
    }

    private async Task HandleRecord(Key key, JetStreamATWebSocketRecordEventArgs args)
    {
        switch (args.Record.Commit!.Record!)
        {
            case Like {Subject.Uri: not null, CreatedAt: not null} like:
            {
                var transaction = _database.CreateTransaction();
                var keyString = key.ToString();
                var serialized = JsonSerializer.Serialize(like, Entities.JsonSerializerOptions);
                var setResult = transaction.StringSetAsync(keyString, serialized, TimeSpan.FromDays(1));
                var sortedSetResult = transaction.SortedSetAddAsync(key.Collection, keyString, GetTimestamp(DateTime.UtcNow));
                await transaction.ExecuteAsync();
                break;
            }
            case Post {CreatedAt: not null} post:
            {
                var transaction = _database.CreateTransaction();
                var keyString = key.ToString();
                var serialized = JsonSerializer.Serialize(post, Entities.JsonSerializerOptions);
                var setResult = transaction.StringSetAsync(keyString, serialized, TimeSpan.FromDays(1));
                var sortedSetResult = transaction.SortedSetAddAsync(key.Collection, keyString, GetTimestamp(DateTime.UtcNow));
                await transaction.ExecuteAsync();
                break;
            }
        }
    }
    
    private void CleanupSets()
    {
        using var activity = DiagnosticsConfig.Source.StartActivity();
        foreach (var collection in WantedCollections ?? [Constants.FeedType.Like, Constants.FeedType.Post])
        {
            CleanupCollection(collection);
        }
    }
    
    private void CleanupCollection(string collection)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithCollection(collection);
    
        // We want to clean up data older than 1 day ago
        DateTime minTime = DateTime.UtcNow.AddDays(-30);
    
        _logger.LogInformation("Cleaning up {Key} from {Start}", collection, minTime);

        long totalRemoved = 0;
    
        while (minTime < DateTime.UtcNow - TimeSpan.FromDays(1))
        {
            var removed = _database.SortedSetRemoveRangeByScore(collection, double.NegativeInfinity, GetTimestamp(minTime), Exclude.Stop);
            totalRemoved += removed;
            if (removed > 0)
            {
                _logger.LogInformation("Removed {Removed} ({Total}) keys from {Collection} at {Time}", removed, totalRemoved, collection, minTime);
            }
            
            minTime = minTime.AddHours(1);
        }
        
        // We also want to cleanup and data that for some reason is in the future
        DateTime maxTime = DateTime.UtcNow.AddDays(1);
        var futureRemoved = _database.SortedSetRemoveRangeByScore(collection, GetTimestamp(maxTime), double.PositiveInfinity, Exclude.Stop);
        if (futureRemoved > 0)
        {
            _logger.LogInformation("Removed {FutureRemoved} keys from {Collection} that were in the future", futureRemoved, collection);
            totalRemoved += futureRemoved;
        }
        
        _logger.LogInformation("Removed {Total} keys from {Collection}", totalRemoved, collection);
    }
    
    private static long GetTimestamp(DateTime dateTime) => ((DateTimeOffset) dateTime).ToUnixTimeSeconds();
}

