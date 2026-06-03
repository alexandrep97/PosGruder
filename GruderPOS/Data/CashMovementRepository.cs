using Dapper;

namespace GruderPOS.Data;

public class CashMovementRepository
{
    private readonly DatabaseManager _db;

    public CashMovementRepository(DatabaseManager db) => _db = db;

    public async Task<CashMovement> CreateAsync(CashMovement movement)
    {
        using var conn = _db.GetConnection();
        movement.CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO CashMovements (CashSessionId, Type, Amount, Notes, CreatedAt)
            VALUES (@CashSessionId, @Type, @Amount, @Notes, @CreatedAt);
            SELECT last_insert_rowid();", movement);
        movement.Id = id;
        return movement;
    }

    public async Task<IEnumerable<CashMovement>> GetBySessionAsync(int sessionId)
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<CashMovement>(
            "SELECT * FROM CashMovements WHERE CashSessionId = @SessionId ORDER BY CreatedAt",
            new { SessionId = sessionId });
    }

    public async Task<(double TotalDeposits, double TotalWithdrawals)> GetTotalsAsync(int sessionId)
    {
        using var conn = _db.GetConnection();
        var deposits = await conn.ExecuteScalarAsync<double>(
            $"SELECT COALESCE(SUM(Amount), 0) FROM CashMovements WHERE CashSessionId = @Id AND Type = '{MovementType.Deposit}'",
            new { Id = sessionId });
        var withdrawals = await conn.ExecuteScalarAsync<double>(
            $"SELECT COALESCE(SUM(Amount), 0) FROM CashMovements WHERE CashSessionId = @Id AND Type = '{MovementType.Withdrawal}'",
            new { Id = sessionId });
        return (deposits, withdrawals);
    }

    public async Task<IEnumerable<CashMovement>> GetAllAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<CashMovement>(
            "SELECT * FROM CashMovements ORDER BY CreatedAt");
    }
}
