using Avalonia.Media;
using Avalonia.Controls.DataGridHierarchical;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Models;

public class ConsoleItemModel
{
    public string Expression { get; }
    public string Result { get; }
    public bool IsError { get; }
    public IBrush ResultBrush => IsError ? Brushes.Red : Brushes.LightGreen;

    public bool IsObject { get; }
    public string? ObjectId { get; }
    public HierarchicalModel<ConsoleObjectNode>? HierarchicalResult { get; }
    public ConsoleObjectNode? RootNode { get; }

    public ConsoleItemModel(string expr, string res, bool isError = false, string? objectId = null, string? type = null, ICdpService? cdpService = null)
    {
        Expression = expr;
        Result = res;
        IsError = isError;

        if (objectId != null && cdpService != null)
        {
            IsObject = true;
            ObjectId = objectId;

            var rootNode = new ConsoleObjectNode(cdpService)
            {
                Name = "Result",
                Value = res,
                Type = type ?? "object",
                ObjectId = objectId,
                IsExpandable = true
            };

            RootNode = rootNode;

            var options = new HierarchicalOptions<ConsoleObjectNode>
            {
                ChildrenSelector = node => node.GetChildren(),
                IsLeafSelector = node => !node.IsExpandable,
                AutoExpandRoot = false
            };

            HierarchicalResult = new HierarchicalModel<ConsoleObjectNode>(options);
            HierarchicalResult.SetRoots(new[] { rootNode });
        }
    }
}
