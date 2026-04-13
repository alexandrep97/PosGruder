using Dapper;

namespace GruderPOS.Data;

public class TransactionRepository
{
    private readonly DatabaseManager _db;

    public TransactionRepository(DatabaseManager db) => _db = db;

    public async Task<Transaction> CreateAsync(Transaction transaction, List<TransactionItem> items)
    {
        using var conn = _db.GetConnection();
        using var dbTransaction = conn.BeginTransaction();

        // Get next transaction number for this session
        var maxNum = await conn.ExecuteScalarAsync<int?>(
            "SELECT MAX(TransactionNumber) FROM Transactions WHERE CashSessionId = @SessionId",
            new { SessionId = transaction.CashSessionId }, dbTransaction) ?? 0;

        transaction.TransactionNumber = maxNum + 1;

        var transactionId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Transactions (CashSessionId, TransactionNumber, TotalAmount, PaymentMethod)
            VALUES (@CashSessionId, @TransactionNumber, @TotalAmount, @PaymentMethod);
            SELECT last_insert_rowid();",
            new { transaction.CashSessionId, transaction.TransactionNumber, transaction.TotalAmount, transaction.PaymentMethod },
            dbTransaction);

        transaction.Id = transactionId;

        foreach (var item in items)
        {
            item.TransactionId = transactionId;
            await conn.ExecuteAsync(@"
                INSERT INTO TransactionItems (TransactionId, ProductId, ProductName, Quantity, UnitPrice, TotalPrice, IsGenericItem)
                VALUES (@TransactionId, @ProductId, @ProductName, @Quantity, @UnitPrice, @TotalPrice, @IsGenericItem)",
                item, dbTransaction);
        }

        // Update session totals
        await conn.ExecuteAsync(@"
            UPDATE CashSessions SET 
                TotalSales = TotalSales + @Amount,
                TotalTransactions = TotalTransactions + 1
            WHERE Id = @SessionId",
            new { Amount = transaction.TotalAmount, SessionId = transaction.CashSessionId }, dbTransaction);

        dbTransaction.Commit();
        transaction.Items = items;
        return transaction;
    }

    public async Task<IEnumerable<Transaction>> GetBySessionAsync(int sessionId)
    {
        using var conn = _db.GetConnection();
        var transactions = (await conn.QueryAsync<Transaction>(
            "SELECT * FROM Transactions WHERE CashSessionId = @SessionId ORDER BY CreatedAt DESC",
            new { SessionId = sessionId })).ToList();

        foreach (var t in transactions)
        {
            t.Items = (await conn.QueryAsync<TransactionItem>(
                "SELECT * FROM TransactionItems WHERE TransactionId = @TransactionId",
                new { TransactionId = t.Id })).ToList();
        }

        return transactions;
    }

    public async Task<IEnumerable<Transaction>> GetByDateRangeAsync(string dateFrom, string dateTo)
    {
        using var conn = _db.GetConnection();
        var transactions = (await conn.QueryAsync<Transaction>(@"
            SELECT * FROM Transactions 
            WHERE date(CreatedAt) >= @DateFrom AND date(CreatedAt) <= @DateTo
            ORDER BY CreatedAt DESC",
            new { DateFrom = dateFrom, DateTo = dateTo })).ToList();

        foreach (var t in transactions)
        {
            t.Items = (await conn.QueryAsync<TransactionItem>(
                "SELECT * FROM TransactionItems WHERE TransactionId = @TransactionId",
                new { TransactionId = t.Id })).ToList();
        }

        return transactions;
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        using var conn = _db.GetConnection();
        var transaction = await conn.QueryFirstOrDefaultAsync<Transaction>(
            "SELECT * FROM Transactions WHERE Id = @Id", new { Id = id });
        if (transaction != null)
        {
            transaction.Items = (await conn.QueryAsync<TransactionItem>(
                "SELECT * FROM TransactionItems WHERE TransactionId = @TransactionId",
                new { TransactionId = transaction.Id })).ToList();
        }
        return transaction;
    }

    public async Task VoidAsync(int id)
    {
        using var conn = _db.GetConnection();
        using var dbTransaction = conn.BeginTransaction();

        var transaction = await conn.QueryFirstOrDefaultAsync<Transaction>(
            "SELECT * FROM Transactions WHERE Id = @Id", new { Id = id }, dbTransaction);

        if (transaction != null && !transaction.Voided)
        {
            await conn.ExecuteAsync(
                "UPDATE Transactions SET Voided = 1 WHERE Id = @Id",
                new { Id = id }, dbTransaction);

            await conn.ExecuteAsync(@"
                UPDATE CashSessions SET 
                    TotalSales = TotalSales - @Amount,
                    TotalTransactions = TotalTransactions - 1
                WHERE Id = @SessionId",
                new { Amount = transaction.TotalAmount, SessionId = transaction.CashSessionId }, dbTransaction);
        }

        dbTransaction.Commit();
    }
}
