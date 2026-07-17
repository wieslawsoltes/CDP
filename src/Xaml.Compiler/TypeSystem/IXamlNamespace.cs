using System.Collections.Generic;

namespace Xaml.Compiler.TypeSystem;

public interface IXamlNamespace
{
    string Uri { get; }
    string DefaultPrefix { get; }
    IReadOnlyCollection<string> TargetAssemblies { get; }
    IReadOnlyCollection<string> ClrNamespaces { get; }
}
