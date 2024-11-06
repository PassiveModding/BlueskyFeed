using System.Diagnostics;
using System.Diagnostics.Metrics;
using FishyFlip.Models;

namespace BlueskyFeed.Jetstream;

public static class DiagnosticsConfig
{
    public const string SourceName = "BlueskyFeed.Jetstream";
    public static ActivitySource Source { get; } = new(SourceName);
    public static Meter Meter { get; } = new(SourceName);
    public static Counter<long> EventsCounter = Meter.CreateCounter<long>("events");
    
    public const string CollectionKey = "Collection";
    public const string RemovedKey = "Removed";
    public const string DidKey = "Did";
    public const string RKeyKey = "RKey";
    public const string OperationKey = "Operation";
    public const string KindKey = "Kind";
    public static Activity? WithTag(this Activity? activity, string key, string value)
    {
        activity?.AddTag(key, value);
        return activity;
    }
    
    public static Activity? WithCollection(this Activity? activity, string collection)
    {
        return activity?.WithTag(CollectionKey, collection);
    }
    
    public static Activity? WithRemoved(this Activity? activity, long removed)
    {
        return activity?.WithTag(RemovedKey, removed.ToString());
    }
    
    public static Activity? WithDid(this Activity? activity, string did)
    {
        return activity?.WithTag(DidKey, did);
    }
    
    public static Activity? WithRKey(this Activity? activity, string rKey)
    {
        return activity?.WithTag(RKeyKey, rKey);
    }
    
    public static Activity? WithOperation(this Activity? activity, ATWebSocketCommitType operation)
    {
        return activity?.WithTag(OperationKey, operation.ToString());
    }
    
    public static Activity? WithKind(this Activity? activity, ATWebSocketEvent kind)
    {
        return activity?.WithTag(KindKey, kind.ToString());
    }
}