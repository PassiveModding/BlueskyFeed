using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text.Json;
using BlueskyFeed.Common;
using FishyFlip;
using FishyFlip.Events;
using FishyFlip.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BlueskyFeed.Jetstream;

public class JetStreamListener : IHostedService
{
    private static readonly Meter Meter = new("BlueskyFeed.Jetstream");
    private readonly ObservableCounter<long> _totalEventsCounter;
    private static readonly string[] WantedCollections = [Constants.FeedType.Like, Constants.FeedType.Post];
    
    private ATJetStream? _jetStream;
    private readonly ILogger<JetStreamListener> _logger;
    private readonly IDatabase _database;
    private long _count;
    private long _lastCount;
    private DateTime _lastCheck;
    private Timer _timer;
    

    public JetStreamListener(ILogger<JetStreamListener> logger, IConnectionMultiplexer connectionMultiplexer)
    {
        _logger = logger;
        _database = connectionMultiplexer.GetDatabase();
        _totalEventsCounter = Meter.CreateObservableCounter("total_events", () => _count);
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
        
        await _jetStream.ConnectAsync(wantedCollections: WantedCollections, token: cancellationToken);
    }

    private int _reconnectAttempts;
    
    private void HandleWebSocketState(WebSocketState state, bool force = false)
    {
        if (_reconnectAttempts > 5)
        {
            _logger.LogError("Failed to reconnect after {Attempts} attempts, stopping", _reconnectAttempts);
            Environment.Exit(1);
        }

        if (force)
        {
            _logger.LogWarning("Connection closed, reconnecting");
            _jetStream?.ConnectAsync(wantedCollections: WantedCollections);
            _reconnectAttempts++;
            return;
        }
        
        if (state == WebSocketState.Closed)
        {
            _logger.LogWarning("Connection closed, reconnecting");
            _jetStream?.ConnectAsync(wantedCollections: WantedCollections);
            _reconnectAttempts++;
        }
        else if (state == WebSocketState.Aborted)
        {
            _logger.LogError("Connection aborted, reconnecting");
            _jetStream?.ConnectAsync(wantedCollections: WantedCollections);
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
        await HandleReceiveAsync(args);
    }

    private async Task HandleReceiveAsync(JetStreamATWebSocketRecordEventArgs args)
    {
        if (args.Record.Did == null 
            || args.Record.Commit == null 
            || args.Record.Commit.Collection == null 
            || args.Record.Commit.RKey == null)
        {
            return;
        }
        
        var key = new Entities.Key(args.Record.Commit.Collection, args.Record.Did.Handler, args.Record.Commit.RKey);
        if (args.Record.Commit.Type is ATWebSocketCommitType.Create && args.Record.Commit.Record != null)
        {
            await HandleRecord(key.ToString(), args);
        }
        else if (args.Record.Commit.Type == ATWebSocketCommitType.Update)
        {
            // ignore for now
        }
        else if (args.Record.Commit.Type == ATWebSocketCommitType.Delete)
        {
            await _database.KeyDeleteAsync(key.ToString());
        }
    }

    private async Task HandleRecord(string key, JetStreamATWebSocketRecordEventArgs args)
    {
        var record = args.Record.Commit!.Record!;
        if (record is Like {Subject.Uri: not null} like)
        {
            var serialized = JsonSerializer.Serialize(like, Entities.JsonSerializerOptions);
            await _database.StringSetAsync(key, serialized, TimeSpan.FromDays(1));
            await _database.SortedSetAddAsync("likes", key, DateTime.UtcNow.Ticks);
        }
        else if (record is Post post)
        {
            var serialized = JsonSerializer.Serialize(post, Entities.JsonSerializerOptions);
            await _database.StringSetAsync(key, serialized, TimeSpan.FromDays(1));
            await _database.SortedSetAddAsync("posts", key, DateTime.UtcNow.Ticks);
        }
    }
}

