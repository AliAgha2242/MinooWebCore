using System.Text.Json;
using StackExchange.Redis;

namespace MinooWebCore.Services.Redis;

public sealed class RedisService : IRedisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database;

    public RedisService(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _database.StringGetAsync(key);
        return value.IsNullOrEmpty
            ? default
            : JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) =>
        _database.StringSetAsync(key, JsonSerializer.Serialize(value, JsonOptions), expiry);

    public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry) =>
        _database.StringSetAsync(key, value, expiry, When.NotExists);

    public Task<TimeSpan?> GetTimeToLiveAsync(string key) =>
        _database.KeyTimeToLiveAsync(key);

    public Task<bool> RemoveAsync(string key) =>
        _database.KeyDeleteAsync(key);
}