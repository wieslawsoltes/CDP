using System;
using System.Collections.Generic;
using Jint;
using Jint.Runtime;

namespace Chrome.DevTools.Protocol;

public static class CdpJintEvaluator
{
    public static object? Evaluate(string expression, Dictionary<string, object?> globals)
    {
        // 1. Create the engine with camelCase property mapping
        var engine = new Engine(options =>
        {
            options.Interop.TypeResolver = new Jint.Runtime.Interop.TypeResolver
            {
                MemberNameCreator = member => 
                {
                    var name = member.Name;
                    if (string.IsNullOrEmpty(name)) return new[] { name };
                    var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
                    return name == camel ? new[] { name } : new[] { name, camel };
                }
            };
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
        });

        // 2. Set global variables
        foreach (var kvp in globals)
        {
            engine.SetValue(kvp.Key, kvp.Value);
        }

        // 3. Evaluate the expression
        try
        {
            var jsValue = engine.Evaluate(expression);
            return jsValue.ToObject();
        }
        catch (JavaScriptException ex)
        {
            throw new InvalidOperationException($"JS evaluation error: {ex.Message}", ex);
        }
    }
}
