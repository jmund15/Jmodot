namespace Jmodot.Implementation.Shared;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// The [RequiredExport] properties and fields of a type, properties first, each in reflection order.
/// Resolved once per type: validation runs on per-frame paths (transition checks), where repeating
/// the attribute scan costs a measurable share of every frame per entity.
/// </summary>
internal static class RequiredExportMembers
{
    internal readonly record struct Member(string Name, Func<object, object?> GetValue);

    private static readonly ConcurrentDictionary<Type, Member[]> Cache = new();

    internal static IReadOnlyList<Member> For(Type type) => Cache.GetOrAdd(type, Scan);

    private static Member[] Scan(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var properties = type.GetProperties(flags)
            .Where(p => p.GetCustomAttribute<RequiredExportAttribute>() != null)
            .Select(p => new Member(p.Name, p.GetValue));
        var fields = type.GetFields(flags)
            .Where(f => f.GetCustomAttribute<RequiredExportAttribute>() != null)
            .Select(f => new Member(f.Name, f.GetValue));
        return properties.Concat(fields).ToArray();
    }
}
