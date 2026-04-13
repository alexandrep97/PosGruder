using GruderPOS.Data;

namespace GruderPOS.Printing;

/// <summary>
/// Configuração de layout de impressão, lida das AppSettings
/// </summary>
public class PrintLayoutConfig
{
    public string PrintMode { get; set; } = "Complete";  // "Complete" ou "Individual"
    public int PrintCopies { get; set; } = 1;

    // Cabeçalho
    public bool HeaderEnabled { get; set; } = true;
    public string HeaderLine1 { get; set; } = "GRUDER";
    public string HeaderLine2 { get; set; } = "GRUPO DESPORTIVO DA";
    public string HeaderLine3 { get; set; } = "RIBEIRA DO FARRIO";
    public string HeaderLine4 { get; set; } = "Fundado em 1977";

    // Corpo
    public bool BodyEnabled { get; set; } = true;
    public string BodyTitle { get; set; } = "";
    public string BodyLine1 { get; set; } = "";
    public string BodyLine2 { get; set; } = "";
    public bool ShowDate { get; set; } = true;
    public bool ShowSession { get; set; } = true;
    public bool ShowReceiptNumber { get; set; } = true;
    public bool ShowTicketNumber { get; set; } = true;     // Apenas em Individual
    public bool ShowGridHeader { get; set; } = true;       // Apenas em Complete
    public bool ShowPaymentMethod { get; set; } = true;
    public bool ShowTotals { get; set; } = true;

    // Rodapé
    public bool FooterEnabled { get; set; } = true;
    public string FooterLine1 { get; set; } = "Obrigado pela preferencia!";
    public string FooterLine2 { get; set; } = "GRUDER - 1977";

    public string EventName { get; set; } = "Festa GRUDER";

    public static PrintLayoutConfig FromSettings(Dictionary<string, string> settings)
    {
        return new PrintLayoutConfig
        {
            PrintMode = settings.GetValueOrDefault("PrintMode", "Complete"),
            PrintCopies = int.TryParse(settings.GetValueOrDefault("PrintCopies", "1"), out var copies) ? Math.Max(1, copies) : 1,
            HeaderEnabled = settings.GetValueOrDefault("HeaderEnabled", "true") == "true",
            HeaderLine1 = settings.GetValueOrDefault("HeaderLine1", "GRUDER"),
            HeaderLine2 = settings.GetValueOrDefault("HeaderLine2", "GRUPO DESPORTIVO DA"),
            HeaderLine3 = settings.GetValueOrDefault("HeaderLine3", "RIBEIRA DO FARRIO"),
            HeaderLine4 = settings.GetValueOrDefault("HeaderLine4", "Fundado em 1977"),
            BodyEnabled = settings.GetValueOrDefault("BodyEnabled", "true") == "true",
            BodyTitle = settings.GetValueOrDefault("BodyTitle", ""),
            BodyLine1 = settings.GetValueOrDefault("BodyLine1", ""),
            BodyLine2 = settings.GetValueOrDefault("BodyLine2", ""),
            ShowDate = settings.GetValueOrDefault("ShowDate", "true") == "true",
            ShowSession = settings.GetValueOrDefault("ShowSession", "true") == "true",
            ShowReceiptNumber = settings.GetValueOrDefault("ShowReceiptNumber", "true") == "true",
            ShowTicketNumber = settings.GetValueOrDefault("ShowTicketNumber", "true") == "true",
            ShowGridHeader = settings.GetValueOrDefault("ShowGridHeader", "true") == "true",
            ShowPaymentMethod = settings.GetValueOrDefault("ShowPaymentMethod", "true") == "true",
            ShowTotals = settings.GetValueOrDefault("ShowTotals", "true") == "true",
            FooterEnabled = settings.GetValueOrDefault("FooterEnabled", "true") == "true",
            FooterLine1 = settings.GetValueOrDefault("FooterLine1", "Obrigado pela preferencia!"),
            FooterLine2 = settings.GetValueOrDefault("FooterLine2", "GRUDER - 1977"),
            EventName = settings.GetValueOrDefault("EventName", "Festa GRUDER")
        };
    }
}

public class ReceiptPrinter
{
    private readonly SerialPortManager _serial;
    private const int LINE_WIDTH = 42; // 80mm thermal paper, normal font

    public ReceiptPrinter(SerialPortManager serial)
    {
        _serial = serial;
    }

    /// <summary>
    /// Imprime o talão da transação conforme o modo configurado:
    /// - Complete: um único talão com todos os artigos, totais e cabeçalho/rodapé
    /// - Individual: uma senha por cada unidade de cada artigo (ex: 4 cervejas = 4 senhas separadas)
    /// </summary>
    public bool PrintReceipt(Transaction transaction, PrintLayoutConfig config)
    {
        try
        {
            if (!_serial.Connect()) return false;

            int copies = Math.Max(1, config.PrintCopies);
            for (int copy = 0; copy < copies; copy++)
            {
                if (config.PrintMode == "Individual")
                {
                    PrintIndividualTickets(transaction, config);
                }
                else
                {
                    PrintCompleteReceipt(transaction, config);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Modo COMPLETO: talão único com cabeçalho, todos os artigos, totais e rodapé
    /// </summary>
    private bool PrintCompleteReceipt(Transaction transaction, PrintLayoutConfig config)
    {
        InitPrinter();

        // === CABEÇALHO ===
        if (config.HeaderEnabled)
        {
            PrintHeader(config);
        }

        // === CORPO ===
        // Título personalizado do corpo
        if (config.BodyEnabled && !string.IsNullOrWhiteSpace(config.BodyTitle))
        {
            _serial.Write(EscPosCommands.AlignCenter);
            _serial.Write(EscPosCommands.BoldOn);
            _serial.WriteText($"{config.BodyTitle}\n");
            _serial.Write(EscPosCommands.BoldOff);
        }
        else if (!string.IsNullOrWhiteSpace(config.EventName))
        {
            _serial.Write(EscPosCommands.AlignCenter);
            _serial.Write(EscPosCommands.BoldOn);
            _serial.WriteText($"{config.EventName}\n");
            _serial.Write(EscPosCommands.BoldOff);
        }

        // Linhas extra do corpo
        if (config.BodyEnabled)
        {
            _serial.Write(EscPosCommands.AlignCenter);
            if (!string.IsNullOrWhiteSpace(config.BodyLine1))
                _serial.WriteText($"{config.BodyLine1}\n");
            if (!string.IsNullOrWhiteSpace(config.BodyLine2))
                _serial.WriteText($"{config.BodyLine2}\n");
        }

        PrintLine('-');

        // Info da transação
        _serial.Write(EscPosCommands.AlignLeft);
        if (config.ShowDate)
            _serial.WriteText($"Data: {DateTime.Now:dd/MM/yyyy HH:mm}\n");
        if (config.ShowReceiptNumber)
            _serial.WriteText($"Talao No: {transaction.TransactionNumber}\n");
        if (config.ShowSession)
            _serial.WriteText($"Sessao:   #{transaction.CashSessionId}\n");

        PrintLine('=');

        // Cabeçalho das colunas
        if (config.ShowGridHeader)
        {
            _serial.Write(EscPosCommands.BoldOn);
            _serial.WriteText(FormatColumns("Artigo", "Qtd", "Total"));
            _serial.Write(EscPosCommands.BoldOff);
            PrintLine('-');
        }

        // Artigos
        if (transaction.Items != null)
        {
            foreach (var item in transaction.Items)
            {
                var name = item.ProductName;
                if (name.Length > 20) name = name[..20];
                var qty = $"x{item.Quantity}";
                var total = $"{item.TotalPrice:F2}";
                _serial.WriteText(FormatColumns(name, qty, total));
            }
        }

        PrintLine('-');

        // Total
        if (config.ShowTotals)
        {
            _serial.Write(EscPosCommands.BoldOn);
            _serial.Write(EscPosCommands.SizeDoubleHeight);
            _serial.WriteText(FormatTotalLine("TOTAL:", $"{transaction.TotalAmount:F2} EUR"));
            _serial.Write(EscPosCommands.SizeNormal);
            _serial.Write(EscPosCommands.BoldOff);
        }

        // Método de pagamento
        if (config.ShowPaymentMethod)
            _serial.WriteText($"Pagamento: {GetPaymentLabel(transaction.PaymentMethod)}\n");

        PrintLine('=');

        // === RODAPÉ ===
        if (config.FooterEnabled)
        {
            PrintFooter(config);
        }

        // Feed e corte
        _serial.Write(EscPosCommands.FeedLines(4));
        _serial.Write(EscPosCommands.PartialCut);

        return true;
    }

    /// <summary>
    /// Modo INDIVIDUAL: uma senha separada por cada unidade de artigo.
    /// Ex: 4 cervejas + 2 bifanas = 6 senhas individuais, cada uma com corte.
    /// Cada senha tem: cabeçalho (opcional), nome do artigo com destaque, rodapé (opcional).
    /// </summary>
    private bool PrintIndividualTickets(Transaction transaction, PrintLayoutConfig config)
    {
        if (transaction.Items == null || transaction.Items.Count == 0) return false;

        // Calcular total de senhas para numerar (ex: 1/6, 2/6...)
        int totalTickets = transaction.Items.Sum(i => i.Quantity);
        int ticketNum = 0;

        foreach (var item in transaction.Items)
        {
            for (int u = 0; u < item.Quantity; u++)
            {
                ticketNum++;
                InitPrinter();

                // === CABEÇALHO ===
                if (config.HeaderEnabled)
                {
                    PrintHeader(config);
                }

                // Título do corpo ou nome do evento
                if (config.BodyEnabled && !string.IsNullOrWhiteSpace(config.BodyTitle))
                {
                    _serial.Write(EscPosCommands.AlignCenter);
                    _serial.Write(EscPosCommands.BoldOn);
                    _serial.WriteText($"{config.BodyTitle}\n");
                    _serial.Write(EscPosCommands.BoldOff);
                }
                else if (!string.IsNullOrWhiteSpace(config.EventName))
                {
                    _serial.Write(EscPosCommands.AlignCenter);
                    _serial.Write(EscPosCommands.BoldOn);
                    _serial.WriteText($"{config.EventName}\n");
                    _serial.Write(EscPosCommands.BoldOff);
                }

                // Linhas extra do corpo
                if (config.BodyEnabled)
                {
                    _serial.Write(EscPosCommands.AlignCenter);
                    if (!string.IsNullOrWhiteSpace(config.BodyLine1))
                        _serial.WriteText($"{config.BodyLine1}\n");
                    if (!string.IsNullOrWhiteSpace(config.BodyLine2))
                        _serial.WriteText($"{config.BodyLine2}\n");
                }

                PrintLine('-');

                // Info da transação
                _serial.Write(EscPosCommands.AlignLeft);
                if (config.ShowDate)
                    _serial.WriteText($"Data: {DateTime.Now:dd/MM/yyyy HH:mm}\n");
                if (config.ShowReceiptNumber || config.ShowTicketNumber)
                {
                    var parts = new List<string>();
                    if (config.ShowReceiptNumber) parts.Add($"Talao: {transaction.TransactionNumber}");
                    if (config.ShowTicketNumber) parts.Add($"Senha: {ticketNum}/{totalTickets}");
                    _serial.WriteText(string.Join("  ", parts) + "\n");
                }

                PrintLine('=');

                // === ARTIGO EM DESTAQUE ===
                _serial.Write(EscPosCommands.AlignCenter);
                _serial.Write(EscPosCommands.BoldOn);
                _serial.Write(EscPosCommands.SizeDouble);
                _serial.WriteText($"{item.ProductName}\n");
                _serial.Write(EscPosCommands.SizeNormal);

                // Preço unitário
                if (config.ShowTotals)
                {
                    _serial.Write(EscPosCommands.SizeDoubleHeight);
                    _serial.WriteText($"{item.UnitPrice:F2} EUR\n");
                    _serial.Write(EscPosCommands.SizeNormal);
                }
                _serial.Write(EscPosCommands.BoldOff);

                // Método de pagamento
                if (config.ShowPaymentMethod)
                    _serial.WriteText($"\n{GetPaymentLabel(transaction.PaymentMethod)}\n");

                PrintLine('=');

                // === RODAPÉ ===
                if (config.FooterEnabled)
                {
                    PrintFooter(config);
                }

                // Feed e corte
                _serial.Write(EscPosCommands.FeedLines(4));
                _serial.Write(EscPosCommands.PartialCut);
            }
        }

        return true;
    }

    /// <summary>
    /// Imprime o cabeçalho configurável
    /// </summary>
    private void PrintHeader(PrintLayoutConfig config)
    {
        _serial.Write(EscPosCommands.AlignCenter);

        // Linha 1 - título principal (tamanho grande, bold)
        if (!string.IsNullOrWhiteSpace(config.HeaderLine1))
        {
            _serial.Write(EscPosCommands.BoldOn);
            _serial.Write(EscPosCommands.SizeDouble);
            _serial.WriteText($"{config.HeaderLine1}\n");
            _serial.Write(EscPosCommands.SizeNormal);
        }

        // Linha 2 - subtítulo (bold)
        if (!string.IsNullOrWhiteSpace(config.HeaderLine2))
        {
            _serial.Write(EscPosCommands.BoldOn);
            _serial.WriteText($"{config.HeaderLine2}\n");
        }

        // Linha 3 - subtítulo (bold)
        if (!string.IsNullOrWhiteSpace(config.HeaderLine3))
        {
            _serial.WriteText($"{config.HeaderLine3}\n");
            _serial.Write(EscPosCommands.BoldOff);
        }

        // Linha 4 - info adicional (normal)
        if (!string.IsNullOrWhiteSpace(config.HeaderLine4))
        {
            _serial.Write(EscPosCommands.BoldOff);
            _serial.WriteText($"{config.HeaderLine4}\n");
        }

        PrintLine('=');
    }

    /// <summary>
    /// Imprime o rodapé configurável
    /// </summary>
    private void PrintFooter(PrintLayoutConfig config)
    {
        _serial.Write(EscPosCommands.AlignCenter);

        if (!string.IsNullOrWhiteSpace(config.FooterLine1))
        {
            _serial.WriteText($"\n{config.FooterLine1}\n");
        }

        if (!string.IsNullOrWhiteSpace(config.FooterLine2))
        {
            _serial.Write(EscPosCommands.BoldOn);
            _serial.WriteText($"{config.FooterLine2}\n");
            _serial.Write(EscPosCommands.BoldOff);
        }
    }

    public bool PrintCashSessionReport(CashSession session, IEnumerable<Transaction> transactions, PrintLayoutConfig config)
    {
        try
        {
            if (!_serial.Connect()) return false;

            InitPrinter();

            // Header
            _serial.Write(EscPosCommands.AlignCenter);
            _serial.Write(EscPosCommands.BoldOn);
            _serial.Write(EscPosCommands.SizeDouble);

            if (config.HeaderEnabled && !string.IsNullOrWhiteSpace(config.HeaderLine1))
                _serial.WriteText($"{config.HeaderLine1}\n");
            else
                _serial.WriteText("GRUDER\n");

            _serial.Write(EscPosCommands.SizeNormal);
            _serial.WriteText("FECHO DE CAIXA\n");
            _serial.Write(EscPosCommands.BoldOff);
            PrintLine('=');

            // Session info
            _serial.Write(EscPosCommands.AlignLeft);
            _serial.WriteText($"Sessao:    #{session.Id}\n");
            _serial.WriteText($"Abertura:  {session.OpenedAt}\n");
            _serial.WriteText($"Fecho:     {session.ClosedAt ?? "Em aberto"}\n");
            PrintLine('-');

            // Totals
            _serial.Write(EscPosCommands.BoldOn);
            _serial.WriteText(FormatTotalLine("Fundo Caixa:", $"{session.OpeningBalance:F2}"));
            _serial.WriteText(FormatTotalLine("Total Vendas:", $"{session.TotalSales:F2}"));
            _serial.WriteText(FormatTotalLine("Num. Trans.:", $"{session.TotalTransactions}"));

            var closing = session.ClosingBalance ?? (session.OpeningBalance + session.TotalSales);
            _serial.Write(EscPosCommands.SizeDoubleHeight);
            _serial.WriteText(FormatTotalLine("TOTAL:", $"{closing:F2} EUR"));
            _serial.Write(EscPosCommands.SizeNormal);
            _serial.Write(EscPosCommands.BoldOff);

            // Payment method breakdown
            PrintLine('-');
            _serial.WriteText("Resumo por pagamento:\n");
            var byPayment = transactions
                .Where(t => !t.Voided)
                .GroupBy(t => t.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(t => t.TotalAmount), Count = g.Count() });

            foreach (var pm in byPayment)
            {
                _serial.WriteText(FormatTotalLine($"  {GetPaymentLabel(pm.Method)} ({pm.Count}):", $"{pm.Total:F2}"));
            }

            // Voided count
            var voidedCount = transactions.Count(t => t.Voided);
            if (voidedCount > 0)
            {
                PrintLine('-');
                _serial.WriteText($"Transacoes anuladas: {voidedCount}\n");
            }

            // Notes
            if (!string.IsNullOrWhiteSpace(session.Notes))
            {
                PrintLine('-');
                _serial.WriteText($"Notas: {session.Notes}\n");
            }

            PrintLine('=');

            if (config.FooterEnabled)
            {
                PrintFooter(config);
            }

            _serial.Write(EscPosCommands.FeedLines(4));
            _serial.Write(EscPosCommands.PartialCut);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Print session report error: {ex.Message}");
            return false;
        }
    }

    public bool PrintTest()
    {
        try
        {
            if (!_serial.Connect()) return false;

            InitPrinter();
            _serial.Write(EscPosCommands.AlignCenter);
            _serial.Write(EscPosCommands.BoldOn);
            _serial.Write(EscPosCommands.SizeDouble);
            _serial.WriteText("POS GRUDER\n");
            _serial.Write(EscPosCommands.SizeNormal);
            _serial.Write(EscPosCommands.BoldOff);
            _serial.WriteText("Teste de Impressao\n");
            PrintLine('-');
            _serial.Write(EscPosCommands.AlignLeft);
            _serial.WriteText($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n");
            _serial.WriteText("Impressora OK!\n");
            PrintLine('=');
            _serial.Write(EscPosCommands.FeedLines(3));
            _serial.Write(EscPosCommands.PartialCut);

            return true;
        }
        catch
        {
            return false;
        }
    }

    // === Helpers ===

    private void InitPrinter()
    {
        _serial.Write(EscPosCommands.Initialize);
        _serial.Write(EscPosCommands.SetCodePage860);
    }

    private void PrintLine(char c)
    {
        _serial.WriteText(new string(c, LINE_WIDTH) + "\n");
    }

    private string FormatColumns(string col1, string col2, string col3)
    {
        var c1Width = LINE_WIDTH - 12;
        var c2Width = 5;
        var c3Width = 7;

        col1 = col1.Length > c1Width ? col1[..c1Width] : col1.PadRight(c1Width);
        col2 = col2.PadLeft(c2Width);
        col3 = col3.PadLeft(c3Width);

        return $"{col1}{col2}{col3}\n";
    }

    private string FormatTotalLine(string label, string value)
    {
        var valueWidth = value.Length;
        var labelWidth = LINE_WIDTH - valueWidth;
        label = label.Length > labelWidth ? label[..labelWidth] : label.PadRight(labelWidth);
        return $"{label}{value}\n";
    }

    private static string GetPaymentLabel(string method) => method switch
    {
        "Cash" => "Dinheiro",
        "Card" => "Cartao",
        "MBWay" => "MB Way",
        _ => method
    };
}
