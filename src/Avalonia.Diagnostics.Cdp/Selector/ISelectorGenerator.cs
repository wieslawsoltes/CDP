using Avalonia;

namespace Avalonia.Diagnostics.Cdp;

public interface ISelectorGenerator
{
    string GenerateSelector(Visual visual, bool useLogicalTree = false);
}
