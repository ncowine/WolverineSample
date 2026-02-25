namespace TradingAssistant.Infrastructure.Caching;

public enum ChangeType { Updated, Deleted }

public readonly record struct CacheChangeNotification<TKey>(
    IReadOnlyCollection<TKey> Keys,
    ChangeType ChangeType
);
