using System.Text.Json;
using System.Text.Json.Serialization;
using GruderPOS.Data;
using GruderPOS.Printing;

namespace GruderPOS.Bridge;

public class WebBridge
{
    private readonly CategoryRepository _categories;
    private readonly ProductRepository _products;
    private readonly TransactionRepository _transactions;
    private readonly CashSessionRepository _cashSessions;
    private readonly SettingsRepository _settings;
    private readonly ReceiptPrinter _printer;
    private readonly SerialPortManager _serialPort;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public WebBridge(DatabaseManager db, SerialPortManager serialPort)
    {
        _categories = new CategoryRepository(db);
        _products = new ProductRepository(db);
        _transactions = new TransactionRepository(db);
        _cashSessions = new CashSessionRepository(db);
        _settings = new SettingsRepository(db);
        _serialPort = serialPort;
        _printer = new ReceiptPrinter(serialPort);
    }

    public async Task<string> HandleMessage(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;
            var action = root.GetProperty("action").GetString() ?? "";
            var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() ?? "" : "";

            object? result = action switch
            {
                "getCategories" => await HandleGetCategories(),
                "getAllCategories" => await HandleGetAllCategories(),
                "getProducts" => await HandleGetProducts(root),
                "getAllProducts" => await HandleGetAllProducts(),
                "saveCategory" => await HandleSaveCategory(root),
                "deleteCategory" => await HandleDeleteCategory(root),
                "reorderCategories" => await HandleReorderCategories(root),
                "saveProduct" => await HandleSaveProduct(root),
                "deleteProduct" => await HandleDeleteProduct(root),
                "reorderProducts" => await HandleReorderProducts(root),
                "openCashSession" => await HandleOpenCashSession(root),
                "closeCashSession" => await HandleCloseCashSession(root),
                "getCurrentSession" => await HandleGetCurrentSession(),
                "processTransaction" => await HandleProcessTransaction(root),
                "getTransactions" => await HandleGetTransactions(root),
                "getSessionTransactions" => await HandleGetSessionTransactions(root),
                "getCashSessions" => await HandleGetCashSessions(),
                "voidTransaction"    => await HandleVoidTransaction(root),
                "reprintTransaction" => await HandleReprintTransaction(root),
                "reprintSession"     => await HandleReprintSession(root),
                "testPrint" => HandleTestPrint(),
                "getSerialPorts" => HandleGetSerialPorts(),
                "getSettings" => await HandleGetSettings(),
                "saveSettings" => await HandleSaveSettings(root),
                _ => throw new Exception($"Unknown action: {action}")
            };

            var response = new { success = true, data = result };
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            return $"window.bridgeCallback('{requestId}', {json});";
        }
        catch (Exception ex)
        {
            var errorResponse = new { success = false, error = ex.Message };
            var json = JsonSerializer.Serialize(errorResponse, _jsonOptions);
            // Try to extract requestId for error response
            try
            {
                using var doc = JsonDocument.Parse(messageJson);
                var rid = doc.RootElement.TryGetProperty("requestId", out var r) ? r.GetString() ?? "" : "";
                return $"window.bridgeCallback('{rid}', {json});";
            }
            catch
            {
                return $"window.bridgeCallback('', {json});";
            }
        }
    }

    // Categories
    private async Task<object> HandleGetCategories() =>
        await _categories.GetAllAsync();

    private async Task<object> HandleGetAllCategories() =>
        await _categories.GetAllIncludingInactiveAsync();

    private async Task<object> HandleSaveCategory(JsonElement root)
    {
        var data = root.GetProperty("data");
        var id = data.TryGetProperty("id", out var idProp) && idProp.ValueKind != JsonValueKind.Null ? idProp.GetInt32() : 0;
        var category = new Category
        {
            Id = id,
            Name = data.GetProperty("name").GetString() ?? "",
            SortOrder = data.TryGetProperty("sortOrder", out var so) ? so.GetInt32() : 0,
            IsActive = data.TryGetProperty("isActive", out var ia) ? ia.GetBoolean() : true
        };

        if (category.Id == 0)
            category.Id = await _categories.InsertAsync(category);
        else
            await _categories.UpdateAsync(category);

        return category;
    }

    private async Task<object> HandleDeleteCategory(JsonElement root)
    {
        var id = root.GetProperty("id").GetInt32();
        await _categories.DeleteAsync(id);
        return new { deleted = true };
    }

    private async Task<object> HandleReorderCategories(JsonElement root)
    {
        var ids = root.GetProperty("ids").EnumerateArray().Select(e => e.GetInt32()).ToList();
        await _categories.ReorderAsync(ids);
        return new { reordered = true };
    }

    // Products
    private async Task<object> HandleGetProducts(JsonElement root)
    {
        var categoryId = root.GetProperty("categoryId").GetInt32();
        return await _products.GetByCategoryAsync(categoryId);
    }

    private async Task<object> HandleGetAllProducts() =>
        await _products.GetAllAsync();

    private async Task<object> HandleSaveProduct(JsonElement root)
    {
        var data = root.GetProperty("data");
        var id = data.TryGetProperty("id", out var idProp) && idProp.ValueKind != JsonValueKind.Null ? idProp.GetInt32() : 0;
        var product = new Product
        {
            Id = id,
            CategoryId = data.GetProperty("categoryId").GetInt32(),
            Name = data.GetProperty("name").GetString() ?? "",
            Price = data.GetProperty("price").GetDouble(),
            IsGeneric = data.TryGetProperty("isGeneric", out var ig) && ig.GetBoolean(),
            SortOrder = data.TryGetProperty("sortOrder", out var so) ? so.GetInt32() : 0,
            IsActive = data.TryGetProperty("isActive", out var ia) ? ia.GetBoolean() : true
        };

        if (product.Id == 0)
            product.Id = await _products.InsertAsync(product);
        else
            await _products.UpdateAsync(product);

        return product;
    }

    private async Task<object> HandleDeleteProduct(JsonElement root)
    {
        var id = root.GetProperty("id").GetInt32();
        await _products.DeleteAsync(id);
        return new { deleted = true };
    }

    private async Task<object> HandleReorderProducts(JsonElement root)
    {
        var ids = root.GetProperty("ids").EnumerateArray().Select(e => e.GetInt32()).ToList();
        await _products.ReorderAsync(ids);
        return new { reordered = true };
    }

    // Cash Sessions
    private async Task<object> HandleOpenCashSession(JsonElement root)
    {
        var balance = root.TryGetProperty("openingBalance", out var ob) ? ob.GetDouble() : 0;
        return await _cashSessions.OpenAsync(balance);
    }

    private async Task<object> HandleCloseCashSession(JsonElement root)
    {
        var notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;
        var session = await _cashSessions.CloseAsync(notes);

        if (session != null)
        {
            // Print session report with layout config
            var transactions = await _transactions.GetBySessionAsync(session.Id);
            try
            {
                var allSettings = await _settings.GetAllAsync();
                var printConfig = PrintLayoutConfig.FromSettings(allSettings);
                _printer.PrintCashSessionReport(session, transactions, printConfig);
            }
            catch { }
        }

        return session ?? (object)new { error = "No open session" };
    }

    private async Task<object> HandleGetCurrentSession() =>
        await _cashSessions.GetCurrentAsync() ?? (object)new { };

    private async Task<object> HandleGetCashSessions() =>
        await _cashSessions.GetAllWithBreakdownAsync();

    // Transactions
    private async Task<object> HandleProcessTransaction(JsonElement root)
    {
        var data = root.GetProperty("data");
        var transaction = new Transaction
        {
            CashSessionId = data.GetProperty("cashSessionId").GetInt32(),
            TotalAmount = data.GetProperty("totalAmount").GetDouble(),
            PaymentMethod = data.TryGetProperty("paymentMethod", out var pm) ? pm.GetString() ?? "Cash" : "Cash",
            CustomerNumber = data.TryGetProperty("customerNumber", out var cn) && cn.ValueKind != JsonValueKind.Null
                ? cn.GetInt32()
                : null
        };

        var items = new List<TransactionItem>();
        foreach (var itemEl in data.GetProperty("items").EnumerateArray())
        {
            items.Add(new TransactionItem
            {
                ProductId = itemEl.TryGetProperty("productId", out var pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetInt32() : null,
                ProductName = itemEl.GetProperty("productName").GetString() ?? "",
                Quantity = itemEl.GetProperty("quantity").GetInt32(),
                UnitPrice = itemEl.GetProperty("unitPrice").GetDouble(),
                TotalPrice = itemEl.GetProperty("totalPrice").GetDouble(),
                IsGenericItem = itemEl.TryGetProperty("isGenericItem", out var igi) && igi.GetBoolean()
            });
        }

        var result = await _transactions.CreateAsync(transaction, items);

        // Print receipt in background so the response returns to JS immediately
        var resultSnapshot = result;
        _ = Task.Run(async () =>
        {
            try
            {
                var allSettings = await _settings.GetAllAsync();
                var printConfig = PrintLayoutConfig.FromSettings(allSettings);
                _printer.PrintReceipt(resultSnapshot, printConfig);
            }
            catch { }
        });

        return result;
    }

    private async Task<object> HandleGetTransactions(JsonElement root)
    {
        var dateFrom = root.TryGetProperty("dateFrom", out var df) ? df.GetString() ?? DateTime.Today.ToString("yyyy-MM-dd") : DateTime.Today.ToString("yyyy-MM-dd");
        var dateTo = root.TryGetProperty("dateTo", out var dt) ? dt.GetString() ?? DateTime.Today.ToString("yyyy-MM-dd") : DateTime.Today.ToString("yyyy-MM-dd");
        return await _transactions.GetByDateRangeAsync(dateFrom, dateTo);
    }

    private async Task<object> HandleGetSessionTransactions(JsonElement root)
    {
        var sessionId = root.GetProperty("sessionId").GetInt32();
        return await _transactions.GetBySessionAsync(sessionId);
    }

    private async Task<object> HandleVoidTransaction(JsonElement root)
    {
        var id = root.GetProperty("id").GetInt32();
        await _transactions.VoidAsync(id);
        return new { voided = true };
    }

    private async Task<object> HandleReprintTransaction(JsonElement root)
    {
        var id = root.GetProperty("id").GetInt32();
        var transaction = await _transactions.GetByIdAsync(id)
            ?? throw new Exception("Transação não encontrada");

        if (transaction.Voided)
            throw new Exception("Não é possível reimprimir uma transação anulada");

        _ = Task.Run(async () =>
        {
            try
            {
                var allSettings = await _settings.GetAllAsync();
                var printConfig = PrintLayoutConfig.FromSettings(allSettings);
                _printer.PrintReceipt(transaction, printConfig);
            }
            catch { }
        });

        return new { reprinted = true };
    }

    private async Task<object> HandleReprintSession(JsonElement root)
    {
        var id = root.GetProperty("id").GetInt32();
        var session = await _cashSessions.GetByIdAsync(id)
            ?? throw new Exception("Sessão não encontrada");
        var transactions = await _transactions.GetBySessionAsync(id);

        _ = Task.Run(async () =>
        {
            try
            {
                var allSettings = await _settings.GetAllAsync();
                var printConfig = PrintLayoutConfig.FromSettings(allSettings);
                _printer.PrintCashSessionReport(session, transactions, printConfig);
            }
            catch { }
        });

        return new { reprinted = true };
    }

    // Printer
    private object HandleTestPrint()
    {
        var success = _printer.PrintTest();
        return new { success, message = success ? "Teste impresso com sucesso!" : "Erro ao imprimir. Verifique a ligação." };
    }

    private object HandleGetSerialPorts()
    {
        var ports = SerialPortManager.GetAvailablePorts();
        return new { ports, currentPort = _serialPort.PortName, baudRate = _serialPort.BaudRate };
    }

    // Settings
    private async Task<object> HandleGetSettings() =>
        await _settings.GetAllAsync();

    private async Task<object> HandleSaveSettings(JsonElement root)
    {
        var data = root.GetProperty("data");
        var settings = new Dictionary<string, string>();

        foreach (var prop in data.EnumerateObject())
        {
            settings[prop.Name] = prop.Value.ToString();
        }

        await _settings.SetMultipleAsync(settings);

        // Apply serial port settings if changed
        if (settings.TryGetValue("SerialPort", out var port))
        {
            var baudRate = settings.TryGetValue("BaudRate", out var br) ? int.Parse(br) : 9600;
            _serialPort.Configure(port, baudRate);
        }

        return new { saved = true };
    }
}
