using Dapper;

namespace GruderPOS.Data;

public class CashSessionRepository
{
    private readonly DatabaseManager _db;

    public CashSessionRepository(DatabaseManager db) => _db = db;

    public async Task<CashSession?> GetCurrentAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryFirstOrDefaultAsync<CashSession>(
            "SELECT * FROM CashSessions WHERE Status = 'Open' ORDER BY OpenedAt DESC LIMIT 1");
    }

    public async Task<CashSession> OpenAsync(double openingBalance)
    {
        using var conn = _db.GetConnection();

        // Close any open sessions first
        await conn.ExecuteAsync(@"
            UPDATE CashSessions SET Status = 'Closed', ClosedAt = datetime('now','localtime') 
            WHERE Status = 'Open'");

        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO CashSessions (OpeningBalance, Status) VALUES (@Balance, 'Open');
            SELECT last_insert_rowid();",
            new { Balance = openingBalance });

        return (await conn.QueryFirstAsync<CashSession>(
            "SELECT * FROM CashSessions WHERE Id = @Id", new { Id = id }));
    }

    public async Task<CashSession?> CloseAsync(string? notes)
    {
        using var conn = _db.GetConnection();
        var session = await conn.QueryFirstOrDefaultAsync<CashSession>(
            "SELECT * FROM CashSessions WHERE Status = 'Open' ORDER BY OpenedAt DESC LIMIT 1");

        if (session == null) return null;

        var closingBalance = session.OpeningBalance + session.TotalSales;

        await conn.ExecuteAsync(@"
            UPDATE CashSessions SET 
                Status = 'Closed', 
                ClosedAt = datetime('now','localtime'),
                ClosingBalance = @ClosingBalance,
                Notes = @Notes
            WHERE Id = @Id",
            new { ClosingBalance = closingBalance, Notes = notes, Id = session.Id });

        return await conn.QueryFirstAsync<CashSession>(
            "SELECT * FROM CashSessions WHERE Id = @Id", new { Id = session.Id });
    }

    public async Task<IEnumerable<CashSession>> GetAllAsync()
    {
        using var conn = _db.GetConnection();
        return await conn.QueryAsync<CashSession>(
            "SELECT * FROM CashSessions ORDER BY OpenedAt DESC");
    }

    public async Task<CashSession?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        return await conn.QueryFirstOrDefaultAsync<CashSession>(
            "SELECT * FROM CashSessions WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<CashSessionDetail>> GetAllWithBreakdownAsync()
    {
        using var conn = _db.GetConnection();

        var sessions = (await conn.QueryAsync<CashSession>(
            "SELECT * FROM CashSessions ORDER BY OpenedAt DESC")).ToList();

        var rawBreakdowns = (await conn.QueryAsync<PaymentBreakdownRow>(@"
            SELECT CashSessionId, PaymentMethod,
                   SUM(TotalAmount) AS Total,
                   COUNT(*) AS Count
            FROM Transactions
            WHERE Voided = 0
            GROUP BY CashSessionId, PaymentMethod")).ToList();

        var breakdownLookup = rawBreakdowns.ToLookup(r => r.CashSessionId);

        return sessions.Select(s => new CashSessionDetail
        {
            Session = s,
            PaymentBreakdown = breakdownLookup[s.Id]
                .Select(r => new PaymentBreakdown
                {
                    Method = r.PaymentMethod,
                    Total = r.Total,
                    Count = r.Count
                })
                .ToList()
        });
    }

    private class PaymentBreakdownRow
    {
        public int CashSessionId { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public double Total { get; set; }
        public int Count { get; set; }
    }
}
