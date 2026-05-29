using Microsoft.Data.Sqlite;
using Dapper;

namespace GruderPOS.Data;

public class DatabaseManager
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public DatabaseManager()
    {
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gruder_pos.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute("PRAGMA foreign_keys=ON;");
        return connection;
    }

    public void Initialize()
    {
        using var connection = GetConnection();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );
        ");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CategoryId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                IsGeneric INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );
        ");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS CashSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OpenedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                ClosedAt TEXT,
                OpeningBalance REAL NOT NULL DEFAULT 0,
                ClosingBalance REAL,
                TotalSales REAL DEFAULT 0,
                TotalTransactions INTEGER DEFAULT 0,
                Notes TEXT,
                Status TEXT NOT NULL DEFAULT 'Open'
            );
        ");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CashSessionId INTEGER NOT NULL,
                TransactionNumber INTEGER NOT NULL,
                TotalAmount REAL NOT NULL,
                PaymentMethod TEXT NOT NULL DEFAULT 'Cash',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                Voided INTEGER NOT NULL DEFAULT 0,
                CustomerNumber INTEGER NULL,
                FOREIGN KEY (CashSessionId) REFERENCES CashSessions(Id)
            );
        ");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS TransactionItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TransactionId INTEGER NOT NULL,
                ProductId INTEGER,
                ProductName TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL,
                TotalPrice REAL NOT NULL,
                IsGenericItem INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (TransactionId) REFERENCES Transactions(Id)
            );
        ");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL UNIQUE,
                Value TEXT NOT NULL
            );
        ");

        // Seed default settings
        var settingsCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM AppSettings;");
        if (settingsCount == 0)
        {
            connection.Execute(@"
                INSERT INTO AppSettings (Key, Value) VALUES ('EventName', 'Festa GRUDER 2026');
                INSERT INTO AppSettings (Key, Value) VALUES ('SerialPort', 'COM3');
                INSERT INTO AppSettings (Key, Value) VALUES ('BaudRate', '9600');
                INSERT INTO AppSettings (Key, Value) VALUES ('PrinterEnabled', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('PrintMode', 'Complete');
                INSERT INTO AppSettings (Key, Value) VALUES ('HeaderEnabled', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('HeaderLine1', 'GRUDER');
                INSERT INTO AppSettings (Key, Value) VALUES ('HeaderLine2', 'GRUPO DESPORTIVO DA');
                INSERT INTO AppSettings (Key, Value) VALUES ('HeaderLine3', 'RIBEIRA DO FARRIO');
                INSERT INTO AppSettings (Key, Value) VALUES ('HeaderLine4', 'Fundado em 1977');
                INSERT INTO AppSettings (Key, Value) VALUES ('FooterEnabled', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('FooterLine1', 'Obrigado pela preferencia!');
                INSERT INTO AppSettings (Key, Value) VALUES ('FooterLine2', 'GRUDER - 1977');
                INSERT INTO AppSettings (Key, Value) VALUES ('PrintCopies', '1');
                INSERT INTO AppSettings (Key, Value) VALUES ('BodyEnabled', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('BodyTitle', '');
                INSERT INTO AppSettings (Key, Value) VALUES ('BodyLine1', '');
                INSERT INTO AppSettings (Key, Value) VALUES ('BodyLine2', '');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowDate', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowSession', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowReceiptNumber', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowTicketNumber', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowGridHeader', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowPaymentMethod', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowTotals', 'true');
                INSERT INTO AppSettings (Key, Value) VALUES ('CustomerNumberEnabled', 'false');
                INSERT INTO AppSettings (Key, Value) VALUES ('ShowCustomerNumber', 'true');
            ");
        }
        else
        {
            // Migrate: add new print layout settings if missing
            var existingKeys = connection.Query<string>("SELECT Key FROM AppSettings").ToHashSet();
            var defaults = new Dictionary<string, string>
            {
                ["PrintMode"] = "Complete",
                ["HeaderEnabled"] = "true",
                ["HeaderLine1"] = "GRUDER",
                ["HeaderLine2"] = "GRUPO DESPORTIVO DA",
                ["HeaderLine3"] = "RIBEIRA DO FARRIO",
                ["HeaderLine4"] = "Fundado em 1977",
                ["FooterEnabled"] = "true",
                ["FooterLine1"] = "Obrigado pela preferencia!",
                ["FooterLine2"] = "GRUDER - 1977",
                ["PrintCopies"] = "1",
                ["BodyEnabled"] = "true",
                ["BodyTitle"] = "",
                ["BodyLine1"] = "",
                ["BodyLine2"] = "",
                ["ShowDate"] = "true",
                ["ShowSession"] = "true",
                ["ShowReceiptNumber"] = "true",
                ["ShowTicketNumber"] = "true",
                ["ShowGridHeader"] = "true",
                ["ShowPaymentMethod"] = "true",
                ["ShowTotals"] = "true",
                ["CustomerNumberEnabled"] = "false",
                ["ShowCustomerNumber"] = "true",
            };
            foreach (var kvp in defaults)
            {
                if (!existingKeys.Contains(kvp.Key))
                {
                    connection.Execute("INSERT INTO AppSettings (Key, Value) VALUES (@Key, @Value)",
                        new { Key = kvp.Key, Value = kvp.Value });
                }
            }

            // Migrate Transactions table: add CustomerNumber column if missing
            var transactionColumns = connection.Query<string>(
                "SELECT name FROM pragma_table_info('Transactions')").ToHashSet();
            if (!transactionColumns.Contains("CustomerNumber"))
            {
                connection.Execute(
                    "ALTER TABLE Transactions ADD COLUMN CustomerNumber INTEGER NULL;");
            }
        }

        // Seed default categories and products
        var catCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Categories;");
        if (catCount == 0)
        {
            SeedData(connection);
        }
    }

    private void SeedData(SqliteConnection connection)
    {
        // Categories
        connection.Execute(@"
            INSERT INTO Categories (Name, SortOrder) VALUES ('Bebidas', 1);
            INSERT INTO Categories (Name, SortOrder) VALUES ('Comida', 2);
            INSERT INTO Categories (Name, SortOrder) VALUES ('Doces', 3);
            INSERT INTO Categories (Name, SortOrder) VALUES ('Outros', 4);
        ");

        // Bebidas (CategoryId = 1)
        connection.Execute(@"
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (1, 'Cerveja', 1.50, 1);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (1, 'Água', 0.75, 2);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (1, 'Sumo', 1.00, 3);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (1, 'Refrigerante', 1.00, 4);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (1, 'Sangria', 2.00, 5);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (1, 'Vinho', 1.50, 6);
        ");

        // Comida (CategoryId = 2)
        connection.Execute(@"
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (2, 'Bifana', 3.00, 1);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (2, 'Prego', 3.50, 2);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (2, 'Francesinha', 5.00, 3);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (2, 'Batatas Fritas', 2.00, 4);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (2, 'Courato', 3.00, 5);
        ");

        // Doces (CategoryId = 3)
        connection.Execute(@"
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (3, 'Bolo', 1.50, 1);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (3, 'Farturas', 2.00, 2);
            INSERT INTO Products (CategoryId, Name, Price, SortOrder) VALUES (3, 'Gelado', 1.50, 3);
        ");

        // Outros (CategoryId = 4) - Artigo Genérico
        connection.Execute(@"
            INSERT INTO Products (CategoryId, Name, Price, IsGeneric, SortOrder) VALUES (4, 'Artigo Genérico', 0.00, 1, 1);
        ");
    }
}
