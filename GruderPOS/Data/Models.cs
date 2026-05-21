namespace GruderPOS.Data;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsGeneric { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? CategoryName { get; set; }
}

public class CashSession
{
    public int Id { get; set; }
    public string OpenedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? ClosedAt { get; set; }
    public double OpeningBalance { get; set; }
    public double? ClosingBalance { get; set; }
    public double TotalSales { get; set; }
    public int TotalTransactions { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Open";
}

public class PaymentBreakdown
{
    public string Method { get; set; } = string.Empty;
    public double Total { get; set; }
    public int Count { get; set; }
}

public class CashSessionDetail
{
    public CashSession Session { get; set; } = new();
    public List<PaymentBreakdown> PaymentBreakdown { get; set; } = new();
}

public class Transaction
{
    public int Id { get; set; }
    public int CashSessionId { get; set; }
    public int TransactionNumber { get; set; }
    public double TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public bool Voided { get; set; }
    public List<TransactionItem>? Items { get; set; }
}

public class TransactionItem
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double TotalPrice { get; set; }
    public bool IsGenericItem { get; set; }
}

public class AppSettings
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
