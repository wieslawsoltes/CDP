using System.Collections.Generic;
using Xaml.Compiler.TypeSystem;

namespace Xaml.Compiler.TypeSystemImpl;

public class XamlNamespace : IXamlNamespace
{
    public string Uri { get; }
    public string DefaultPrefix { get; }
    public IReadOnlyCollection<string> TargetAssemblies { get; }
    public IReadOnlyCollection<string> ClrNamespaces { get; }

    public XamlNamespace(string uri, string defaultPrefix, IReadOnlyCollection<string> targetAssemblies, IReadOnlyCollection<string> clrNamespaces)
    {
        Uri = uri;
        DefaultPrefix = defaultPrefix;
        TargetAssemblies = targetAssemblies;
        ClrNamespaces = clrNamespaces;
    }
}
