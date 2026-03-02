using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

#region StrategySelector Tests

public class StrategySelectorTests
{
    [Fact]
    public void SelectBest_EmptyScores_ReturnsNull()
    {
        var result = StrategySelector.SelectBest([], RegimeType.Bull);
        Assert.Null(result);
    }

    [Fact]
    public void SelectBest_NoMatchingRegime_ReturnsNull()
    {
        var scores = new List<StrategyRegimeScore>
        {
            new() { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bear, SharpeRatio = 2.0m }
        };

        var result = StrategySelector.SelectBest(scores, RegimeType.Bull);
        Assert.Null(result);
    }

    [Fact]
    public void SelectBest_SingleCandidate_ReturnsIt()
    {
        var id = Guid.NewGuid();
        var scores = new List<StrategyRegimeScore>
        {
            new() { StrategyId = id, MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 1.5m }
        };

        var result = StrategySelector.SelectBest(scores, RegimeType.Bull);

        Assert.NotNull(result);
        Assert.Equal(id, result!.StrategyId);
    }

    [Fact]
    public void SelectBest_MultipleCandidates_ReturnsHighestSharpe()
    {
        var bestId = Guid.NewGuid();
        var scores = new List<StrategyRegimeScore>
        {
            new() { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 0.8m },
            new() { StrategyId = bestId, MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 2.1m },
            new() { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 1.5m },
            new() { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bear, SharpeRatio = 3.0m } // Wrong regime
        };

        var result = StrategySelector.SelectBest(scores, RegimeType.Bull);

        Assert.NotNull(result);
        Assert.Equal(bestId, result!.StrategyId);
        Assert.Equal(2.1m, result.SharpeRatio);
    }

    [Fact]
    public void SelectBest_TiedSharpe_PrefersHigherSampleSize()
    {
        var highSampleId = Guid.NewGuid();
        var scores = new List<StrategyRegimeScore>
        {
            new() { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 1.5m, SampleSize = 2 },
            new() { StrategyId = highSampleId, MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 1.5m, SampleSize = 10 }
        };

        var result = StrategySelector.SelectBest(scores, RegimeType.Bull);

        Assert.NotNull(result);
        Assert.Equal(highSampleId, result!.StrategyId);
    }

    [Theory]
    [InlineData(0, 50)]    // Day 0: 50%
    [InlineData(1, 60)]    // Day 1: 60%
    [InlineData(2, 70)]    // Day 2: 70%
    [InlineData(3, 80)]    // Day 3: 80%
    [InlineData(4, 90)]    // Day 4: 90%
    [InlineData(5, 100)]   // Day 5: 100%
    [InlineData(10, 100)]  // Day 10: still 100%
    public void ComputeAllocation_LinearRampOver5Days(int daysSince, decimal expected)
    {
        var start = new DateTime(2026, 1, 1);
        var current = start.AddDays(daysSince);

        var result = StrategySelector.ComputeAllocation(start, current);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeAllocation_NegativeDays_Returns50()
    {
        var start = new DateTime(2026, 1, 5);
        var current = new DateTime(2026, 1, 1); // Before start

        var result = StrategySelector.ComputeAllocation(start, current);

        Assert.Equal(50m, result);
    }

    [Fact]
    public void ShouldReplace_NullAssignment_ReturnsTrue()
    {
        Assert.True(StrategySelector.ShouldReplace(null, RegimeType.Bull));
    }

    [Fact]
    public void ShouldReplace_LockedAssignment_ReturnsFalse()
    {
        var assignment = new StrategyAssignment
        {
            MarketCode = "US",
            IsLocked = true,
            Regime = RegimeType.Bear
        };

        Assert.False(StrategySelector.ShouldReplace(assignment, RegimeType.Bull));
    }

    [Fact]
    public void ShouldReplace_SameRegime_ReturnsFalse()
    {
        var assignment = new StrategyAssignment
        {
            MarketCode = "US",
            IsLocked = false,
            Regime = RegimeType.Bull
        };

        Assert.False(StrategySelector.ShouldReplace(assignment, RegimeType.Bull));
    }

    [Fact]
    public void ShouldReplace_DifferentRegime_Unlocked_ReturnsTrue()
    {
        var assignment = new StrategyAssignment
        {
            MarketCode = "US",
            IsLocked = false,
            Regime = RegimeType.Bear
        };

        Assert.True(StrategySelector.ShouldReplace(assignment, RegimeType.Bull));
    }
}

#endregion

#region SelectStrategiesHandler Tests

public class SelectStrategiesHandlerTests
{
    private static readonly NullLogger<SelectStrategiesHandler> Logger = new();

    [Fact]
    public async Task HandleAsync_InvalidRegime_DoesNothing()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var @event = new RegimeChanged("US", "Bull", "InvalidRegime", DateTime.UtcNow, 0.8m);

        await SelectStrategiesHandler.HandleAsync(@event, db, Logger);

        Assert.Empty(db.StrategyAssignments);
    }

    [Fact]
    public async Task HandleAsync_NoScores_DoesNotAssign()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var @event = new RegimeChanged("US", "Sideways", "Bull", DateTime.UtcNow, 0.8m);

        await SelectStrategiesHandler.HandleAsync(@event, db, Logger);

        Assert.Empty(db.StrategyAssignments);
    }

    [Fact]
    public async Task HandleAsync_WithScores_AssignsBestStrategy()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var bestId = Guid.NewGuid();

        db.StrategyRegimeScores.AddRange(
            new StrategyRegimeScore { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 0.8m },
            new StrategyRegimeScore { StrategyId = bestId, MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 1.9m },
            new StrategyRegimeScore { StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bear, SharpeRatio = 3.0m }
        );
        await db.SaveChangesAsync();

        var @event = new RegimeChanged("US", "Sideways", "Bull", DateTime.UtcNow, 0.8m);
        await SelectStrategiesHandler.HandleAsync(@event, db, Logger);

        var assignment = Assert.Single(db.StrategyAssignments);
        Assert.Equal("US", assignment.MarketCode);
        Assert.Equal(bestId, assignment.StrategyId);
        Assert.Equal(RegimeType.Bull, assignment.Regime);
        Assert.Equal(50m, assignment.AllocationPercent);
        Assert.False(assignment.IsLocked);
    }

    [Fact]
    public async Task HandleAsync_LockedAssignment_SkipsSelection()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var lockedId = Guid.NewGuid();

        db.StrategyAssignments.Add(new StrategyAssignment
        {
            MarketCode = "US",
            StrategyId = lockedId,
            StrategyName = "Locked Strategy",
            Regime = RegimeType.Bear,
            IsLocked = true,
            AllocationPercent = 100m,
            SwitchoverStartDate = DateTime.UtcNow.AddDays(-10)
        });
        db.StrategyRegimeScores.Add(new StrategyRegimeScore
        {
            StrategyId = Guid.NewGuid(), MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 2.0m
        });
        await db.SaveChangesAsync();

        var @event = new RegimeChanged("US", "Bear", "Bull", DateTime.UtcNow, 0.9m);
        await SelectStrategiesHandler.HandleAsync(@event, db, Logger);

        var assignment = Assert.Single(db.StrategyAssignments);
        Assert.Equal(lockedId, assignment.StrategyId); // Unchanged
        Assert.True(assignment.IsLocked);
    }

    [Fact]
    public async Task HandleAsync_ExistingUnlocked_UpdatesAssignment()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var oldId = Guid.NewGuid();
        var newBestId = Guid.NewGuid();

        db.StrategyAssignments.Add(new StrategyAssignment
        {
            MarketCode = "US",
            StrategyId = oldId,
            StrategyName = "Old Strategy",
            Regime = RegimeType.Sideways,
            IsLocked = false,
            AllocationPercent = 100m,
            SwitchoverStartDate = DateTime.UtcNow.AddDays(-20)
        });
        db.StrategyRegimeScores.Add(new StrategyRegimeScore
        {
            StrategyId = newBestId, MarketCode = "US", Regime = RegimeType.Bull, SharpeRatio = 1.8m
        });
        await db.SaveChangesAsync();

        var @event = new RegimeChanged("US", "Sideways", "Bull", DateTime.UtcNow, 0.85m);
        await SelectStrategiesHandler.HandleAsync(@event, db, Logger);

        var assignment = Assert.Single(db.StrategyAssignments);
        Assert.Equal(newBestId, assignment.StrategyId);
        Assert.Equal(RegimeType.Bull, assignment.Regime);
        Assert.Equal(50m, assignment.AllocationPercent); // Reset to 50% for switchover
    }

    [Fact]
    public async Task HandleAsync_SameRegime_SkipsUpdate()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var existingId = Guid.NewGuid();
        var assignedAt = DateTime.UtcNow.AddDays(-5);

        db.StrategyAssignments.Add(new StrategyAssignment
        {
            MarketCode = "US",
            StrategyId = existingId,
            StrategyName = "Current",
            Regime = RegimeType.Bull,
            IsLocked = false,
            AllocationPercent = 100m,
            AssignedAt = assignedAt,
            SwitchoverStartDate = DateTime.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        // Same regime → ShouldReplace returns false
        var @event = new RegimeChanged("US", "Sideways", "Bull", DateTime.UtcNow, 0.8m);
        await SelectStrategiesHandler.HandleAsync(@event, db, Logger);

        var assignment = Assert.Single(db.StrategyAssignments);
        Assert.Equal(existingId, assignment.StrategyId); // Unchanged
    }
}

#endregion

#region Lock/Unlock Handler Tests

public class StrategyAssignmentHandlerTests
{
    [Fact]
    public async Task LockStrategy_NewAssignment_CreatesLocked()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var strategyId = Guid.NewGuid();
        var command = new LockStrategyCommand("US", strategyId);

        var result = await LockStrategyHandler.HandleAsync(command, db);

        Assert.Equal("US", result.MarketCode);
        Assert.Equal(strategyId, result.StrategyId);
        Assert.True(result.IsLocked);
        Assert.Equal(100m, result.AllocationPercent);
    }

    [Fact]
    public async Task LockStrategy_ExistingAssignment_UpdatesToLocked()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        db.StrategyAssignments.Add(new StrategyAssignment
        {
            MarketCode = "US",
            StrategyId = oldId,
            StrategyName = "Old",
            Regime = RegimeType.Bull,
            IsLocked = false,
            AllocationPercent = 70m,
            SwitchoverStartDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await LockStrategyHandler.HandleAsync(new LockStrategyCommand("US", newId), db);

        Assert.Equal(newId, result.StrategyId);
        Assert.True(result.IsLocked);
        Assert.Single(db.StrategyAssignments); // Updated, not duplicated
    }

    [Fact]
    public async Task UnlockStrategy_Exists_SetsLockedFalse()
    {
        using var db = TestIntelligenceDbContextFactory.Create();
        db.StrategyAssignments.Add(new StrategyAssignment
        {
            MarketCode = "US",
            StrategyId = Guid.NewGuid(),
            StrategyName = "Locked",
            Regime = RegimeType.Bull,
            IsLocked = true,
            AllocationPercent = 100m,
            SwitchoverStartDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await UnlockStrategyHandler.HandleAsync(new UnlockStrategyCommand("US"), db);

        Assert.NotNull(result);
        Assert.False(result!.IsLocked);
    }

    [Fact]
    public async Task UnlockStrategy_NotExists_ReturnsNull()
    {
        using var db = TestIntelligenceDbContextFactory.Create();

        var result = await UnlockStrategyHandler.HandleAsync(new UnlockStrategyCommand("UNKNOWN"), db);

        Assert.Null(result);
    }
}

#endregion
