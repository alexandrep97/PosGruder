# GRUDER POS

Sistema de Ponto de Venda (POS) para o **Grupo Desportivo da Ribeira do Farrio (GRUDER)**, desenvolvido em C# .NET 8 WinForms com WebView2.

![GRUDER](GruderPOS/wwwroot/assets/logo.jpg)

## Funcionalidades

### Caixa (POS)
- Grid de produtos com cards touch-friendly, organizados por categorias (tabs)
- Carrinho lateral com quantidades, preços unitários e subtotais
- Artigo genérico com introdução manual de valor e descrição (numpad touch)
- Métodos de pagamento: Dinheiro, Cartão, MB Way
- Animação de confirmação de pagamento
- Impressão automática de talão via ESC/POS

### Sessões de Caixa
- Abertura de caixa com fundo inicial
- Fecho de caixa com resumo e impressão automática de relatório
- Indicador visual de estado da caixa (aberta/fechada)

### Histórico
- Consulta de transações por período com filtros de data
- Resumo com totais de vendas e contagem de transações
- Detalhe expandível de cada transação (artigos, quantidades, valores)
- Anulação de transações
- Histórico de sessões de caixa com estatísticas

### Configurações
- **Categorias**: Criar, editar, reordenar e remover categorias de produtos
- **Produtos**: CRUD completo, associação a categorias, flag de artigo genérico
- **Impressora**: Seleção de porta COM, baud rate, teste de impressão
- **Geral**: Nome do evento/festa

## Stack Tecnológico

| Componente | Tecnologia |
|---|---|
| Framework | .NET 8.0 Windows Forms |
| UI Engine | WebView2 (HTML/CSS/JavaScript) |
| Base de Dados | SQLite (Microsoft.Data.Sqlite + Dapper) |
| Impressão | ESC/POS via System.IO.Ports |
| Impressora | appPOS80AM3 (porta série) |

## Pré-requisitos

- Windows 10/11
- .NET 8.0 SDK
- WebView2 Runtime ([Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/))
- Visual Studio 2022 (recomendado)

## Instalação e Execução

```bash
# Restaurar packages
dotnet restore

# Compilar
dotnet build

# Executar
dotnet run --project GruderPOS
```

Ou abrir `GruderPOS.sln` no Visual Studio e executar (F5).

## Estrutura do Projeto

```
GruderPOS/
├── GruderPOS.sln
├── README.md
└── GruderPOS/
    ├── GruderPOS.csproj
    ├── Program.cs              # Entry point
    ├── MainForm.cs             # Formulário principal + WebView2
    ├── Data/
    │   ├── Models.cs           # Entidades (Category, Product, Transaction, etc.)
    │   ├── DatabaseManager.cs  # SQLite init, schema, seed data
    │   ├── CategoryRepository.cs
    │   ├── ProductRepository.cs
    │   ├── TransactionRepository.cs
    │   ├── CashSessionRepository.cs
    │   └── SettingsRepository.cs
    ├── Printing/
    │   ├── EscPosCommands.cs   # Constantes ESC/POS
    │   ├── ReceiptPrinter.cs   # Lógica de impressão de talões
    │   └── SerialPortManager.cs # Gestão da porta série
    ├── Bridge/
    │   └── WebBridge.cs        # Ponte JS ↔ C# via WebView2
    └── wwwroot/
        ├── index.html          # SPA principal
        ├── css/styles.css      # Design completo (tema amarelo/preto)
        ├── js/
        │   ├── bridge.js       # Comunicação com C#
        │   ├── app.js          # Router + controller principal
        │   ├── pos.js          # Lógica do POS (carrinho, pagamentos)
        │   ├── settings.js     # Gestão de configurações
        │   └── history.js      # Histórico de transações e sessões
        └── assets/
            └── logo.jpg        # Logótipo do GRUDER
```

## Configuração da Impressora

A impressora **appPOS80AM3** comunica por porta série com comandos ESC/POS:

- **Porta**: Configurável nas definições (default: COM3)
- **Baud Rate**: 9600 (configurável)
- **Data Bits**: 8
- **Parity**: None
- **Stop Bits**: 1

### Layout do Talão
```
=============================
    GRUPO DESPORTIVO DA
  RIBEIRA DO FARRIO - GRUDER
      Fundado em 1977
=============================
  [Nome do Evento]
-----------------------------
Data: DD/MM/YYYY HH:MM
Talão Nº: XXXX
-----------------------------
Artigo          Qtd   Total
-----------------------------
Bifana           x2   6,00€
Cerveja          x3   4,50€
-----------------------------
TOTAL:               10,50€
Pagamento: Dinheiro
=============================
   Obrigado pela preferência!
      GRUDER - 1977
=============================
```

## Base de Dados

SQLite local (`gruder_pos.db`), criada automaticamente na primeira execução com dados de exemplo:

- **Bebidas**: Cerveja, Água, Sumo, Refrigerante, Sangria, Vinho
- **Comida**: Bifana, Prego, Francesinha, Batatas Fritas, Courato
- **Doces**: Bolo, Farturas, Gelado
- **Outros**: Artigo Genérico (valor manual)

## Design

- Cores do clube: **Amarelo (#F5C518)** e **Preto (#1A1A1A)**
- Interface touch-friendly com botões grandes (mín. 48px)
- Cards de produtos com badges de quantidade
- Sidebar de navegação compacta
- Modais para operações (artigo genérico, abertura/fecho de caixa)
- Notificações toast
- Animação de sucesso no pagamento

---

Desenvolvido para o **Grupo Desportivo da Ribeira do Farrio - GRUDER** (Fundado em 1977)
