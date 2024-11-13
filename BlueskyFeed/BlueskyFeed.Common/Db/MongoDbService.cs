using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace BlueskyFeed.Common.Db;

public class MongoDbService
{
    private readonly IMongoDatabase _database;
    
    public MongoDbService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("mongo");
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase("bluesky");
    }
    
    public IMongoDatabase GetDatabase() => _database;
}