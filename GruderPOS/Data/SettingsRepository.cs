using Dapper;

namespace GruderPOS.Data;

public class SettingsRepository
{
    private readonly DatabaseManager _db;

    public SettingsRepository(DatabaseManager db) => _db = db;

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        using var conn = _db.GetConnection();
        var settings = await conn.QueryAsync<AppSettings>("SELECT * FROM AppSettings");
        return settings.ToDictionary(s => s.Key, s => s.Value);
    }

    public async Task<string?> GetAsync(string key)
    {
        using var conn = _db.GetConnection();
        return await conn.ExecuteScalarAsync<string?>(
            "SELECT Value FROM AppSettings WHERE Key = @Key", new { Key = key });
    }

    public async Task SetAsync(string key, string value)
    {
        using var conn = _db.GetConnection();
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM AppSettings WHERE Key = @Key", new { Key = key });

        if (exists > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE AppSettings SET Value = @Value WHERE Key = @Key",
                new { Key = key, Value = value });
        }
        else
        {
            await conn.ExecuteAsync(
                "INSERT INTO AppSettings (Key, Value) VALUES (@Key, @Value)",
                new { Key = key, Value = value });
        }
    }

    public async Task SetMultipleAsync(Dictionary<string, string> settings)
    {
        foreach (var kvp in settings)
        {
            await SetAsync(kvp.Key, kvp.Value);
        }
    }
}
