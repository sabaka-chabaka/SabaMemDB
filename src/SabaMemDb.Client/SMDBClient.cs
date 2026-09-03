namespace SabaMemDb.Client;

public class SMDBClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _host;
    private readonly bool _disposeClient;

    public SMDBClient(string host, string? password = null)
    {
        _client = new HttpClient();
        _disposeClient = true;
        _host = host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? host.TrimEnd('/')
            : $"http://{host.TrimEnd('/')}";

        if (!string.IsNullOrEmpty(password))
        {
            _client.DefaultRequestHeaders.Add("X-Auth-Password", password);
        }
    }

    public SMDBClient(HttpClient httpClient, string host, string? password = null)
    {
        _client = httpClient;
        _disposeClient = false;
        _host = host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? host.TrimEnd('/')
            : $"http://{host.TrimEnd('/')}";

        if (!string.IsNullOrEmpty(password) && !_client.DefaultRequestHeaders.Contains("X-Auth-Password"))
        {
            _client.DefaultRequestHeaders.Add("X-Auth-Password", password);
        }
    }

    public async Task<bool> Set(string key, string value)
    {
        using var content = new StringContent(value);
        var response = await _client.PostAsync($"{_host}/api/db/set/{key}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Set(string key, byte[] value)
    {
        using var content = new ByteArrayContent(value);
        var response = await _client.PostAsync($"{_host}/api/db/set/{key}", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetNotExists(string key, string value)
    {
        using var content = new StringContent(value);
        var response = await _client.PostAsync($"{_host}/api/db/setnx/{key}", content);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> SetNx(string key, string value) => SetNotExists(key, value);

    public async Task<bool> SetNotExists(string key, byte[] value)
    {
        using var content = new ByteArrayContent(value);
        var response = await _client.PostAsync($"{_host}/api/db/setnx/{key}", content);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> SetNx(string key, byte[] value) => SetNotExists(key, value);

    public async Task<string?> Get(string key)
    {
        var response = await _client.GetAsync($"{_host}/api/db/get/{key}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<byte[]?> GetBytes(string key)
    {
        var response = await _client.GetAsync($"{_host}/api/db/get/{key}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<bool> Delete(string key)
    {
        var response = await _client.DeleteAsync($"{_host}/api/db/delete/{key}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Exists(string key)
    {
        var response = await _client.GetAsync($"{_host}/api/db/exists/{key}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Rename(string oldKey, string newKey)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/rename/{oldKey}/{newKey}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RenameNotExists(string oldKey, string newKey)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/renamenx/{oldKey}/{newKey}", null);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> RenameNx(string oldKey, string newKey) => RenameNotExists(oldKey, newKey);

    public async Task<bool> Expire(string key, int seconds)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/expire/{key}/{seconds}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PExpire(string key, int milliseconds)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/pexpire/{key}/{milliseconds}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ExpireAt(string key, long timestamp)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/expireat/{key}/{timestamp}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<int> Ttl(string key)
    {
        var response = await _client.GetAsync($"{_host}/api/db/ttl/{key}");
        if (!response.IsSuccessStatusCode)
        {
            return -2;
        }

        var content = await response.Content.ReadAsStringAsync();
        return int.TryParse(content, out var ttl) ? ttl : -2;
    }

    public async Task<int> Pttl(string key)
    {
        var response = await _client.GetAsync($"{_host}/api/db/pttl/{key}");
        if (!response.IsSuccessStatusCode)
        {
            return -2;
        }

        var content = await response.Content.ReadAsStringAsync();
        return int.TryParse(content, out var pttl) ? pttl : -2;
    }

    public async Task<bool> Persist(string key)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/persist/{key}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Incr(string key)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/incr/{key}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> Decr(string key)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/decr/{key}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> IncrBy(string key, long value)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/incrby/{key}/{value}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DecrBy(string key, long value)
    {
        var response = await _client.PatchAsync($"{_host}/api/db/decrby/{key}/{value}", null);
        return response.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _client.Dispose();
        }
    }
}