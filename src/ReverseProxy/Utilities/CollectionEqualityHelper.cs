// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarp.ReverseProxy.Utilities;

internal static class CollectionEqualityHelper
{
    public static bool Equals<T>(IReadOnlyList<T>? list1, IReadOnlyList<T>? list2, IEqualityComparer<T>? valueComparer = null)
    {
        if (ReferenceEquals(list1, list2))
        {
            return true;
        }

        if (list1 is null || list2 is null)
        {
            return false;
        }

        if (list1.Count != list2.Count)
        {
            return false;
        }

        valueComparer ??= EqualityComparer<T>.Default;

        for (var i = 0; i < list1.Count; i++)
        {
            if (!valueComparer.Equals(list1[i], list2[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool Equals<T>(IReadOnlyDictionary<string, T>? dictionary1, IReadOnlyDictionary<string, T>? dictionary2, IEqualityComparer<T>? valueComparer = null)
    {
        if (ReferenceEquals(dictionary1, dictionary2))
        {
            return true;
        }

        if (dictionary1 is null || dictionary2 is null)
        {
            return false;
        }

        if (dictionary1.Count != dictionary2.Count)
        {
            return false;
        }

        if (dictionary1.Count == 0)
        {
            return true;
        }

        valueComparer ??= EqualityComparer<T>.Default;

        foreach (var (key, value1) in dictionary1)
        {
            if (dictionary2.TryGetValue(key, out var value2))
            {
                if (!valueComparer.Equals(value1, value2))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    public static bool Equals<T>(IReadOnlyDictionary<Type, T>? dictionary1, IReadOnlyDictionary<Type, T>? dictionary2,
        IEqualityComparer<T>? valueComparer = null)
    {
        if (ReferenceEquals(dictionary1, dictionary2))
        {
            return true;
        }

        if (dictionary1 is null || dictionary2 is null)
        {
            return false;
        }

        if (dictionary1.Count != dictionary2.Count)
        {
            return false;
        }

        if (dictionary1.Count == 0)
        {
            return true;
        }

        valueComparer ??= EqualityComparer<T>.Default;

        foreach (var (key, value1) in dictionary1)
        {
            if (dictionary2.TryGetValue(key, out var value2))
            {
                if (!valueComparer.Equals(value1, value2))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    public static bool Equals<T>(IReadOnlyList<IReadOnlyDictionary<string, T>>? dictionaryList1, IReadOnlyList<IReadOnlyDictionary<string, T>>? dictionaryList2, IEqualityComparer<T>? valueComparer = null)
    {
        if (ReferenceEquals(dictionaryList1, dictionaryList2))
        {
            return true;
        }

        if (dictionaryList1 is null || dictionaryList2 is null)
        {
            return false;
        }

        if (dictionaryList1.Count != dictionaryList2.Count)
        {
            return false;
        }

        for (var i = 0; i < dictionaryList1.Count; i++)
        {
            if (!Equals(dictionaryList1[i], dictionaryList2[i], valueComparer))
            {
                return false;
            }
        }

        return true;
    }

    public static int GetHashCode<T>(IReadOnlyList<T>? values, IEqualityComparer<T>? valueComparer = null)
    {
        if (values is null)
        {
            return 0;
        }

        valueComparer ??= EqualityComparer<T>.Default;

        var hashCode = new HashCode();
        foreach (var value in values)
        {
            hashCode.Add(value, valueComparer);
        }
        return hashCode.ToHashCode();
    }

    public static int GetHashCode<T>(IReadOnlyDictionary<string, T>? dictionary, IEqualityComparer<T>? valueComparer = null)
    {
        if (dictionary is null)
        {
            return 0;
        }

        if (dictionary.Count == 0)
        {
            return 42;
        }

        // We don't know what comparer the dictionary was created with, so we assume it's Ordinal/OrdinalIgnoreCase
        // If a culture-sensitive comparer was used, this may result in GetHashCode returning different values for "equal" strings
        // If that comes up as a realistic scenario, we can consider ignoring keys in the future
        var keyComparer = StringComparer.OrdinalIgnoreCase;
        valueComparer ??= EqualityComparer<T>.Default;

        // Dictionaries are unordered collections and HashCode uses an order-sensitive algorithm (xxHash), so we have to sort the elements
        var keys = dictionary.Keys.ToArray();
        Array.Sort(keys, keyComparer);

        var hashCode = new HashCode();
        foreach (var key in keys)
        {
            hashCode.Add(key, keyComparer);
            hashCode.Add(dictionary[key], valueComparer);
        }
        return hashCode.ToHashCode();
    }

    public static int GetHashCode<T>(IReadOnlyList<IReadOnlyDictionary<string, T>>? dictionaryList, IEqualityComparer<T>? valueComparer = null)
    {
        if (dictionaryList is null)
        {
            return 0;
        }

        var hashCode = new HashCode();
        foreach (var dictionary in dictionaryList)
        {
            hashCode.Add(GetHashCode(dictionary, valueComparer));
        }
        return hashCode.ToHashCode();
    }
    public static int GetHashCode<T>(IReadOnlyDictionary<Type, T>? dictionary, IEqualityComparer<T>? valueComparer = null)
    {
        if (dictionary is null)
        {
            return 0;
        }

        if (dictionary.Count == 0)
        {
            return 42;
        }

        valueComparer ??= EqualityComparer<T>.Default;

        var hashCode = new HashCode();

        foreach (var pair in dictionary)
        {
            hashCode.Add(pair.Key, EqualityComparer<Type>.Default);
            hashCode.Add(pair.Value, valueComparer);
        }

        return hashCode.ToHashCode();
    }

}
