// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Utilities;

internal static class CaseSensitiveEqualHelper
{
    internal static bool Equals(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
    {
        return CollectionEqualityHelper.Equals(list1, list2, StringComparer.Ordinal);
    }

    internal static bool Equals(IReadOnlyDictionary<string, string>? dictionary1, IReadOnlyDictionary<string, string>? dictionary2)
    {
        return CollectionEqualityHelper.Equals(dictionary1, dictionary2, StringComparer.Ordinal);
    }

    internal static bool Equals(IReadOnlyList<IReadOnlyDictionary<string, string>>? dictionaryList1, IReadOnlyList<IReadOnlyDictionary<string, string>>? dictionaryList2)
    {
        return CollectionEqualityHelper.Equals(dictionaryList1, dictionaryList2, StringComparer.Ordinal);
    }

    internal static int GetHashCode(IReadOnlyList<string>? values)
    {
        return CollectionEqualityHelper.GetHashCode(values, StringComparer.Ordinal);
    }

    internal static int GetHashCode(IReadOnlyDictionary<string, string>? dictionary)
    {
        return CollectionEqualityHelper.GetHashCode(dictionary, StringComparer.Ordinal);
    }

    internal static int GetHashCode(IReadOnlyList<IReadOnlyDictionary<string, string>>? dictionaryList)
    {
        return CollectionEqualityHelper.GetHashCode(dictionaryList);
    }
}
