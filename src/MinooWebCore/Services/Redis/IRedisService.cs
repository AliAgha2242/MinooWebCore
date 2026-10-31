namespace MinooWebCore.Services.Redis;

public interface IRedisService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry);
    Task<TimeSpan?> GetTimeToLiveAsync(string key);
    Task<bool> RemoveAsync(string key);
}