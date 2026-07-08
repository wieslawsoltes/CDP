using System.Windows.Media;

namespace Wpf.Diagnostics.Cdp;

public interface ISelectorGenerator
{
    string GenerateSelector(Visual visual, bool useLogicalTree = false);
}
