using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Domain.Trading;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Api;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Apply pending migrations for all 3 contexts
        var marketDb = services.GetRequiredService<MarketDataDbContext>();
        var tradingDb = services.GetRequiredService<TradingDbContext>();
        var backtestDb = services.GetRequiredService<BacktestDbContext>();

        await marketDb.Database.MigrateAsync();
        await tradingDb.Database.MigrateAsync();
        await backtestDb.Database.MigrateAsync();

        // Seed data if not already present
        if (await marketDb.Stocks.AnyAsync())
            return;

        var stocks = new List<Stock>
        {
            new() { Symbol = "AAPL", Name = "Apple Inc.", Exchange = "NASDAQ", Sector = "Technology" },
            new() { Symbol = "MSFT", Name = "Microsoft Corporation", Exchange = "NASDAQ", Sector = "Technology" },
            new() { Symbol = "GOOGL", Name = "Alphabet Inc.", Exchange = "NASDAQ", Sector = "Technology" },
            new() { Symbol = "AMZN", Name = "Amazon.com Inc.", Exchange = "NASDAQ", Sector = "Consumer Discretionary" },
            new() { Symbol = "TSLA", Name = "Tesla Inc.", Exchange = "NASDAQ", Sector = "Automotive" },
            new() { Symbol = "JPM", Name = "JPMorgan Chase & Co.", Exchange = "NYSE", Sector = "Financials" },
            new() { Symbol = "NVDA", Name = "NVIDIA Corporation", Exchange = "NASDAQ", Sector = "Technology" },
            new() { Symbol = "META", Name = "Meta Platforms Inc.", Exchange = "NASDAQ", Sector = "Technology" },
        };

        marketDb.Stocks.AddRange(stocks);

        var random = new Random(42);
        var basePrices = new Dictionary<string, decimal>
        {
            ["AAPL"] = 175m, ["MSFT"] = 380m, ["GOOGL"] = 140m, ["AMZN"] = 180m,
            ["TSLA"] = 240m, ["JPM"] = 190m, ["NVDA"] = 800m, ["META"] = 500m,
        };

        foreach (var stock in stocks)
        {
            var basePrice = basePrices[stock.Symbol];
            for (var day = 90; day >= 0; day--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-day);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    continue;

                var change = (decimal)(random.NextDouble() * 6 - 3);
                var open = basePrice;
                var close = basePrice + (basePrice * change / 100);
                var high = Math.Max(open, close) + Math.Abs(basePrice * (decimal)random.NextDouble() * 0.01m);
                var low = Math.Min(open, close) - Math.Abs(basePrice * (decimal)random.NextDouble() * 0.01m);
                var volume = (long)random.Next(1_000_000, 50_000_000);

                marketDb.PriceCandles.Add(new PriceCandle
                {
                    StockId = stock.Id,
                    Open = Math.Round(open, 2),
                    High = Math.Round(high, 2),
                    Low = Math.Round(low, 2),
                    Close = Math.Round(close, 2),
                    Volume = volume,
                    Timestamp = date,
                    Interval = CandleInterval.Daily
                });

                basePrice = close;
            }
        }

        await marketDb.SaveChangesAsync();

        // Create a system/dev user for the default account
        var devUser = new User
        {
            Email = "dev@tradingassistant.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Dev12345"),
            Role = "Admin"
        };
        tradingDb.Users.Add(devUser);

        var account = new Account
        {
            UserId = devUser.Id,
            Name = "Default Trading Account",
            Balance = 100_000m,
            Currency = "USD",
            AccountType = AccountType.Live
        };
        tradingDb.Accounts.Add(account);

        var portfolio = new Portfolio
        {
            AccountId = account.Id,
            TotalValue = 100_000m,
            CashBalance = 100_000m,
            InvestedValue = 0m,
            TotalPnL = 0m
        };
        tradingDb.Portfolios.Add(portfolio);

        await tradingDb.SaveChangesAsync();
    }
}
