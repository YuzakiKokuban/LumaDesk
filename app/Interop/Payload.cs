using System.Collections;
using System.Reflection;

namespace JiYaoChu.Interop;

/// <summary>
/// Repairs the holes the backend is allowed to leave in a payload.
/// </summary>
/// <remarks>
/// The Rust side serialises an absent collection as <c>null</c> rather than
/// <c>[]</c>: a real read of this laptop answers <c>get_power_settings</c> with
/// <c>"schemes":null</c>, and <c>System.Text.Json</c> then overwrites the
/// record's initialiser with that null. A missing list would throw a
/// <c>NullReferenceException</c> deep inside a render pass, which Reactor shows
/// as a "Render error" page instead of the screen the user asked for. Rather
/// than sprinkle <c>?? []</c> at every use — easy to forget in exactly the page
/// nobody has written yet — each payload is normalised once, on the way in.
/// </remarks>
internal static class Payload
{
    /// <summary>Fills in the nulls on a freshly deserialised payload.</summary>
    public static T Repair<T>(T value)
    {
        if (value is not null)
        {
            Fill(value, depth: 0);
        }

        return value;
    }

    private static void Fill(object instance, int depth)
    {
        // The model is a shallow tree of records; the guard is only here so a
        // self-referential payload could never spin.
        if (depth > 4)
        {
            return;
        }

        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.SetMethod is null)
            {
                continue;
            }

            var current = property.GetValue(instance);

            if (current is null)
            {
                if (Blank(property) is { } replacement)
                {
                    property.SetValue(instance, replacement);
                }

                continue;
            }

            if (Nested(property.PropertyType))
            {
                Fill(current, depth + 1);
            }
        }
    }

    /// <summary>What a null value of this property should become, if anything.</summary>
    private static object? Blank(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsInterface && type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>))
            {
                return Array.CreateInstance(arguments[0], 0);
            }

            if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>))
            {
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments));
            }

            if (definition == typeof(IEnumerable<>))
            {
                return Array.CreateInstance(arguments[0], 0);
            }

            return null;
        }

        if (type.IsClass && !type.IsAbstract && Writable(type))
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                // No parameterless constructor: leave it null and let the page
                // decide what "this section is missing" means.
                return null;
            }
        }

        return null;
    }

    /// <summary>True for a record of ours that may itself carry nulls.</summary>
    private static bool Nested(Type type)
        => type.IsClass
            && !type.IsAbstract
            && type != typeof(string)
            && !typeof(IEnumerable).IsAssignableFrom(type)
            && type.Namespace?.StartsWith("JiYaoChu.", StringComparison.Ordinal) == true;

    /// <summary>True for one of our own records, which always has a default constructor.</summary>
    private static bool Writable(Type type)
        => type.Namespace?.StartsWith("JiYaoChu.", StringComparison.Ordinal) == true;
}
