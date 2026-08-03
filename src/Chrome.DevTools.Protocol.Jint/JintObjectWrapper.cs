using Jint;
using Jint.Native;

namespace Chrome.DevTools.Protocol;

public class JintObjectWrapper
{
    static JintObjectWrapper() => CdpRemoteObjectAdapters.Register(JintRemoteObjectAdapter.Instance);

    public JsValue Value { get; }
    public Engine Engine { get; }

    public JintObjectWrapper(JsValue value, Engine engine)
    {
        Value = value;
        Engine = engine;
    }
}

internal sealed class JintRemoteObjectAdapter : ICdpRemoteObjectAdapter
{
    public static JintRemoteObjectAdapter Instance { get; } = new();

    public bool TryUnwrap(object value, out object? unwrapped)
    {
        var jsValue = value is JintObjectWrapper wrapper ? wrapper.Value : value as JsValue;
        if (jsValue is null)
        {
            unwrapped = null;
            return false;
        }

        unwrapped = jsValue;
        if (!jsValue.IsObject()) return true;
        try
        {
            var candidate = jsValue.ToObject();
            var typeName = candidate?.GetType().FullName ?? string.Empty;
            if (candidate is not null &&
                (typeName.StartsWith("Avalonia.", StringComparison.Ordinal) ||
                 typeName.Contains("CdpRuntime", StringComparison.Ordinal) ||
                 typeName.Contains("Mock", StringComparison.Ordinal)))
            {
                unwrapped = candidate;
            }
        }
        catch
        {
        }
        return true;
    }
}
