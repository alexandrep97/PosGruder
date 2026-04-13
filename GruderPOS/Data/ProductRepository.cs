using Dapper;

namespace GruderPOS.Data;

public class ProductRepository
{
    private readonly DatabaseManager _db;

    public ProductRepository(DatabaseManager db) => _db = db;

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<Product>(@"
            SELECT p.*, c.Name as CategoryName 
            FROM Products p 
            JOIN Categories c ON p.CategoryId = c.Id 
            WHERE p.IsActive = 1 AND c.IsActive = 1
            ORDER BY p.SortOrder, p.Name");
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId)
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<Product>(@"
            SELECT p.*, c.Name as CategoryName 
            FROM Products p 
            JOIN Categories c ON p.CategoryId = c.Id 
            WHERE p.CategoryId = @CategoryId AND p.IsActive = 1
            ORDER BY p.SortOrder, p.Name",
            new { CategoryId = categoryId });
    }

    public async Task<IEnumerable<Product>> GetAllIncludingInactiveAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<Product>(@"
            SELECT p.*, c.Name as CategoryName 
            FROM Products p 
            JOIN Categories c ON p.CategoryId = c.Id 
            ORDER BY c.SortOrder, p.SortOrder, p.Name");
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        return await conn.QueryFirstOrDefaultAsync<Product>(
            "SELECT * FROM Products WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> InsertAsync(Product product)
    {
        using var conn = _db.GetConnection();
        var maxOrder = await conn.ExecuteScalarAsync<int?>(
            "SELECT MAX(SortOrder) FROM Products WHERE CategoryId = @CategoryId",
            new { product.CategoryId }) ?? 0;
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Products (CategoryId, Name, Price, IsGeneric, SortOrder, IsActive) 
            VALUES (@CategoryId, @Name, @Price, @IsGeneric, @SortOrder, @IsActive);
            SELECT last_insert_rowid();",
            new { product.CategoryId, product.Name, product.Price, product.IsGeneric, SortOrder = maxOrder + 1, product.IsActive });
    }

    public async Task UpdateAsync(Product product)
    {
        using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"
            UPDATE Products SET CategoryId = @CategoryId, Name = @Name, Price = @Price, 
            IsGeneric = @IsGeneric, SortOrder = @SortOrder, IsActive = @IsActive,
            UpdatedAt = datetime('now','localtime') WHERE Id = @Id",
            new { product.CategoryId, product.Name, product.Price, product.IsGeneric, product.SortOrder, product.IsActive, product.Id });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.GetConnection();
        await conn.ExecuteAsync("UPDATE Products SET IsActive = 0, UpdatedAt = datetime('now','localtime') WHERE Id = @Id", new { Id = id });
    }

    public async Task ReorderAsync(List<int> ids)
    {
        using var conn = _db.GetConnection();
        using var transaction = conn.BeginTransaction();
        for (int i = 0; i < ids.Count; i++)
        {
            await conn.ExecuteAsync(
                "UPDATE Products SET SortOrder = @Order, UpdatedAt = datetime('now','localtime') WHERE Id = @Id",
                new { Order = i + 1, Id = ids[i] }, transaction);
        }
        transaction.Commit();
    }
}
