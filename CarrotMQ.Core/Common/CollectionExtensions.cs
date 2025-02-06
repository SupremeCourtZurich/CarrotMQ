#if !NET
// NOTE: Put into same namespace as in higher .NET versions to always have the same using directives in consuming code.
// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130
namespace System.Collections.Generic;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for the <see cref="IDictionary{TKey,TValue}" /> type.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Attempts to add the specified key and value to the dictionary. If the key already exists, the method returns false;
    /// otherwise, the key-value pair is added to the dictionary, and the method returns true.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to which the key-value pair should be added.</param>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <see langword="true"/> if the key-value pair was added successfully; otherwise, <see langword="false"/> if the key already exists.
    /// </returns>
    public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary is null) throw new ArgumentNullException(nameof(dictionary));
        if (dictionary.ContainsKey(key)) return false;

        dictionary.Add(key, value);

        return true;
    }
}
#endif