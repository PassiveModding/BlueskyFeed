
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithRedisInsight();

var jetstreamService = builder.AddProject<Projects.BlueskyFeed_Jetstream>("jetstreamservice")
    .WithReference(redis)
    .WaitFor(redis);

var apiService = builder.AddProject<Projects.BlueskyFeed_Api>("apiservice")
    .WithReference(redis)
    .WaitFor(redis);

builder.Build().Run();