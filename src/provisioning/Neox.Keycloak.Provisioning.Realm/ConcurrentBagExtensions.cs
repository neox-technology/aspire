using System.Collections.Concurrent;

namespace Neox.Keycloak.Provisioning.Realm;

/// <summary>Thread-safe helpers for <see cref="ConcurrentBag{T}"/> collections on realm POCOs.</summary>
public static class ConcurrentBagExtensions
{
    /// <summary>
    /// Returns the first item matching <paramref name="predicate"/>, or adds and returns
    /// <paramref name="value"/> when no match exists.
    /// </summary>
    public static T GetOrAdd<T>(
        this ConcurrentBag<T> bag,
        Func<T, bool> predicate,
        T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(value);

        foreach (var item in bag)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        bag.Add(value);
        return value;
    }
}
