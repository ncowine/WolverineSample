namespace TradingAssistant.Infrastructure.Caching;

public interface IDataCache<TKey, TValue>
{
    Task<TValue> Get(TKey key);
    Task<IReadOnlyDictionary<TKey, TValue>> Get(HashSet<TKey> keys);
    Task<IReadOnlyDictionary<TKey, TValue>> GetAll();
}
