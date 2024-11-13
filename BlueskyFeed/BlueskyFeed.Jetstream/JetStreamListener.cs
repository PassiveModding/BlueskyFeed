using System.Diagnostics;
using System.Net.WebSockets;
using BlueskyFeed.Common;
using BlueskyFeed.Common.Db;
using FishyFlip;
using FishyFlip.Events;
using FishyFlip.Models;

namespace BlueskyFeed.Jetstream;

public class JetStreamListener : IHostedService
{
    private static readonly string[]? WantedCollections = null;//[Constants.FeedType.Like, Constants.FeedType.Post];
    
    
    private ATJetStream? _jetStream;
    private readonly ILogger<JetStreamListener> _logger;
    private readonly LikeRepository _likeRepository;
    private long _count;
    private long _lastCount;
    private DateTime _lastCheck;
    private Timer _timer;
    private int _reconnectAttempts;
    

    public JetStreamListener(ILogger<JetStreamListener> logger,
        LikeRepository likeRepository)
    {
        _logger = logger;
        _likeRepository = likeRepository;
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
        try
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
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle record");
        }
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
            await _likeRepository.RemoveLikeAsync(args.Record.Did.Handler, args.Record.Commit.RKey);
        }
    }

    private async Task HandleRecord(Key key, JetStreamATWebSocketRecordEventArgs args)
    {
        switch (args.Record.Commit!.Record!)
        {
            case Like {Subject.Uri: not null, CreatedAt: not null} like:
            {
                await _likeRepository.AddLikeAsync(key.Handler, key.RKey, like);
                break;
            }
            // case Post {CreatedAt: not null} post:
            // {
            //     var transaction = _database.CreateTransaction();
            //     var keyString = key.ToString();
            //     var serialized = JsonSerializer.Serialize(post, Entities.JsonSerializerOptions);
            //     var setResult = transaction.StringSetAsync(keyString, serialized, TimeSpan.FromDays(1));
            //     var sortedSetResult = transaction.SortedSetAddAsync(key.Collection, keyString, GetTimestamp(DateTime.UtcNow));
            //     await transaction.ExecuteAsync();
            //     break;
            // }
        }
    }
    
    private static long GetTimestamp(DateTime dateTime) => ((DateTimeOffset) dateTime).ToUnixTimeSeconds();
}

