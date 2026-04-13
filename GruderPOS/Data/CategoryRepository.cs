using Dapper;

namespace GruderPOS.Data;

public class CategoryRepository
{
    private readonly DatabaseManager _db;

    public CategoryRepository(DatabaseManager db) => _db = db;

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<Category>(
            "SELECT * FROM Categories WHERE IsActive = 1 ORDER BY SortOrder, Name");
    }

    public async Task<IEnumerable<Category>> GetAllIncludingInactiveAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<Category>(
            "SELECT * FROM Categories ORDER BY SortOrder, Name");
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        return await conn.QueryFirstOrDefaultAsync<Category>(
            "SELECT * FROM Categories WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> InsertAsync(Category category)
    {
        using var conn = _db.GetConnection();
        var maxOrder = await conn.ExecuteScalarAsync<int?>("SELECT MAX(SortOrder) FROM Categories") ?? 0;
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Categories (Name, SortOrder, IsActive) 
            VALUES (@Name, @SortOrder, @IsActive);
            SELECT last_insert_rowid();",
            new { category.Name, SortOrder = maxOrder + 1, category.IsActive });
    }

    public async Task UpdateAsync(Category category)
    {
        using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"
            UPDATE Categories SET Name = @Name, SortOrder = @SortOrder, IsActive = @IsActive,
            UpdatedAt = datetime('now','localtime') WHERE Id = @Id",
            new { category.Name, category.SortOrder, category.IsActive, category.Id });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.ExecuteAsync("UPDATE Categories SET IsActive = 0, UpdatedAt = datetime('now','localtime') WHERE Id = @Id", new { Id = id });
    }

    public async Task ReorderAsync(List<int> ids)
    {
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();
        for (int i = 0; i < ids.Count; i++)
        {
            await conn.ExecuteAsync(
                "UPDATE Categories SET SortOrder = @Order, UpdatedAt = datetime('now','localtime') WHERE Id = @Id",
                new { Order = i + 1, Id = ids[i] }, transaction);
        }
        transaction.Commit();
    }
}
