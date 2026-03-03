using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Identity;
using TradingAssistant.Domain.Intelligence;
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

        // Apply pending migrations for all 4 contexts
        var marketDb = services.GetRequiredService<MarketDataDbContext>();
        var tradingDb = services.GetRequiredService<TradingDbContext>();
        var backtestDb = services.GetRequiredService<BacktestDbContext>();
        var intelligenceDb = services.GetRequiredService<IntelligenceDbContext>();

        await marketDb.Database.MigrateAsync();
        await tradingDb.Database.MigrateAsync();
        await backtestDb.Database.MigrateAsync();
        await intelligenceDb.Database.MigrateAsync();

        // Seed stock universes (independent of stock data)
        await SeedUniversesAsync(marketDb);

        // Seed market profiles and cost profiles
        await SeedMarketProfilesAsync(intelligenceDb);

        // Seed sample stocks + user data only on first run
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

    private static async Task SeedUniversesAsync(MarketDataDbContext db)
    {
        var existingNames = await db.StockUniverses.Select(u => u.Name).ToListAsync();
        var toAdd = new List<StockUniverse>();

        var sp500 = new StockUniverse
        {
            Name = "S&P 500",
            Description = "Top 50 S&P 500 components by market cap",
            IncludesBenchmark = true
        };
        sp500.SetSymbolList(new[]
        {
            "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "BRK.B", "JPM", "V",
            "UNH", "XOM", "JNJ", "WMT", "MA", "PG", "AVGO", "HD", "CVX", "MRK",
            "ABBV", "PEP", "KO", "COST", "ADBE", "CRM", "TMO", "CSCO", "ACN", "MCD",
            "ABT", "NKE", "DHR", "NFLX", "AMD", "LIN", "TXN", "QCOM", "PM", "INTC",
            "CMCSA", "UNP", "ORCL", "LOW", "UPS", "MS", "GS", "BLK", "CAT", "BA"
        });

        var nasdaq100 = new StockUniverse
        {
            Name = "NASDAQ 100",
            Description = "Top NASDAQ 100 technology and growth stocks",
            IncludesBenchmark = false
        };
        nasdaq100.SetSymbolList(new[]
        {
            "AAPL", "MSFT", "GOOGL", "GOOG", "AMZN", "NVDA", "META", "TSLA", "AVGO", "COST",
            "ADBE", "NFLX", "AMD", "QCOM", "INTC", "CSCO", "TXN", "AMGN", "INTU", "AMAT",
            "ISRG", "BKNG", "LRCX", "ADI", "MU", "REGN", "VRTX", "MDLZ", "ADP", "PANW",
            "KLAC", "SNPS", "CDNS", "MELI", "CRWD", "MAR", "ABNB", "ORLY", "FTNT", "MRVL",
            "DASH", "WDAY", "CTAS", "CEG", "PYPL", "ROP", "MNST", "TTD", "DXCM", "IDXX"
        });

        var dowJones = new StockUniverse
        {
            Name = "Dow Jones 30",
            Description = "All 30 Dow Jones Industrial Average components",
            IncludesBenchmark = false
        };
        dowJones.SetSymbolList(new[]
        {
            "AAPL", "MSFT", "AMZN", "NVDA", "V", "UNH", "JNJ", "WMT", "JPM", "PG",
            "HD", "CVX", "MRK", "KO", "CSCO", "MCD", "DIS", "IBM", "NKE", "BA",
            "CAT", "GS", "MMM", "AXP", "DOW", "HON", "CRM", "TRV", "AMGN", "VZ"
        });

        var ftse100 = new StockUniverse
        {
            Name = "FTSE 100",
            Description = "Top FTSE 100 UK stocks (Yahoo Finance tickers)",
            IncludesBenchmark = false
        };
        ftse100.SetSymbolList(new[]
        {
            "SHEL.L", "AZN.L", "HSBA.L", "ULVR.L", "BP.L", "GSK.L", "RIO.L", "BATS.L",
            "DGE.L", "REL.L", "LSEG.L", "NG.L", "VOD.L", "BARC.L", "LLOY.L", "GLEN.L",
            "AAL.L", "BHP.L", "BA.L", "IMB.L", "PRU.L", "RR.L", "ABF.L", "CRH.L",
            "ANTO.L", "EXPN.L", "III.L", "SGE.L", "SMIN.L", "WPP.L"
        });

        var euStoxx50 = new StockUniverse
        {
            Name = "Euro Stoxx 50",
            Description = "Top Euro Stoxx 50 European stocks (Yahoo Finance tickers)",
            IncludesBenchmark = false
        };
        euStoxx50.SetSymbolList(new[]
        {
            "ASML.AS", "MC.PA", "SAP.DE", "SIE.DE", "TTE.PA", "SAN.PA", "OR.PA", "AIR.PA",
            "ALV.DE", "BNP.PA", "DTE.DE", "SU.PA", "CS.PA", "BAS.DE", "ENEL.MI", "ISP.MI",
            "ABI.BR", "INGA.AS", "MUV2.DE", "PHIA.AS", "KER.PA", "AI.PA", "DG.PA", "EL.PA",
            "BMW.DE", "ADS.DE", "IBE.MC", "SAN.MC", "BN.PA", "STLAM.MI"
        });

        foreach (var u in new[] { sp500, nasdaq100, dowJones, ftse100, euStoxx50 })
        {
            if (!existingNames.Contains(u.Name))
                toAdd.Add(u);
        }

        if (toAdd.Count > 0)
        {
            db.StockUniverses.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedMarketProfilesAsync(IntelligenceDbContext db)
    {
        if (await db.MarketProfiles.AnyAsync())
            return;

        db.MarketProfiles.AddRange(
            new MarketProfile
            {
                MarketCode = "US_SP500",
                Exchange = "NYSE/NASDAQ",
                Currency = "USD",
                Timezone = "America/New_York",
                VixSymbol = "^VIX",
                DataSource = "yahoo",
                ConfigJson = """{"tradingHours":{"open":"09:30","close":"16:00"},"regimeThresholds":{"highVol":30,"bullBreadth":0.60,"bearBreadth":0.40}}"""
            },
            new MarketProfile
            {
                MarketCode = "IN_NIFTY50",
                Exchange = "NSE",
                Currency = "INR",
                Timezone = "Asia/Kolkata",
                VixSymbol = "^INDIAVIX",
                DataSource = "yahoo",
                ConfigJson = """{"tradingHours":{"open":"09:15","close":"15:30"},"regimeThresholds":{"highVol":25,"bullBreadth":0.55,"bearBreadth":0.35}}"""
            }
        );

        db.CostProfiles.AddRange(
            new CostProfile
            {
                MarketCode = "US_SP500",
                Name = "US Equities (Default)",
                CommissionPerShare = 0.005m,
                CommissionPercent = 0m,
                ExchangeFeePercent = 0m,
                TaxPercent = 0m,
                SpreadEstimatePercent = 0.10m
            },
            new CostProfile
            {
                MarketCode = "IN_NIFTY50",
                Name = "India Equities (Default)",
                CommissionPerShare = 0m,
                CommissionPercent = 0.03m,
                ExchangeFeePercent = 0.00345m,
                TaxPercent = 0.025m,
                SpreadEstimatePercent = 0.05m
            }
        );

        await db.SaveChangesAsync();
    }
}
