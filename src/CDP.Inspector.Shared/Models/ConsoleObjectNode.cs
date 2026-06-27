using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using Chrome.DevTools.Protocol;

namespace CdpInspectorApp.Models;

public class ConsoleObjectNode
{
    private readonly ICdpService _cdpService;
    private bool _loaded;

    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ObjectId { get; set; }
    public bool IsExpandable { get; set; }
    public ObservableCollection<ConsoleObjectNode> Children { get; } = new();

    public ConsoleObjectNode(ICdpService cdpService)
    {
        _cdpService = cdpService;
    }

    public IEnumerable<ConsoleObjectNode> GetChildren()
    {
        if (IsExpandable && !_loaded && ObjectId != null)
        {
            _loaded = true;
            // Add a "Loading..." placeholder node
            Children.Add(new ConsoleObjectNode(_cdpService) { Name = "Loading...", Value = "", Type = "", IsExpandable = false });

            // Start background task
            Task.Run(async () =>
            {
                try
                {
                    var @params = new JsonObject
                    {
                        ["objectId"] = ObjectId,
                        ["ownProperties"] = true
                    };
                    var response = await _cdpService.SendCommandAsync("Runtime.getProperties", @params);
                    if (response != null && response["result"] is JsonArray properties)
                    {
                        var newChildren = new List<ConsoleObjectNode>();
                        foreach (var prop in properties)
                        {
                            if (prop is JsonObject propObj)
                            {
                                var pName = propObj["name"]?.GetValue<string>() ?? "";
                                var valueObj = propObj["value"] as JsonObject;
                                string pType = "";
                                string pValue = "";
                                string? pObjectId = null;

                                if (valueObj != null)
                                {
                                    pType = valueObj["type"]?.GetValue<string>() ?? "";
                                    pValue = valueObj["description"]?.GetValue<string>() ?? valueObj["value"]?.ToString() ?? "";
                                    pObjectId = valueObj["objectId"]?.GetValue<string>();
                                }
                                else if (propObj["get"] is JsonObject getObj)
                                {
                                    // Getter accessor
                                    pType = getObj["type"]?.GetValue<string>() ?? "function";
                                    pValue = getObj["description"]?.GetValue<string>() ?? "Getter";
                                    pObjectId = getObj["objectId"]?.GetValue<string>();
                                }
                                else
                                {
                                    pValue = "undefined";
                                }

                                var pIsExpandable = pObjectId != null;

                                newChildren.Add(new ConsoleObjectNode(_cdpService)
                                {
                                    Name = pName,
                                    Value = pValue,
                                    Type = pType,
                                    ObjectId = pObjectId,
                                    IsExpandable = pIsExpandable
                                });
                            }
                        }

                        Dispatcher.UIThread.Post(() =>
                        {
                            Children.Clear();
                            foreach (var child in newChildren)
                            {
                                Children.Add(child);
                            }
                        });
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(() => Children.Clear());
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Children.Clear();
                        Children.Add(new ConsoleObjectNode(_cdpService) { Name = $"Error: {ex.Message}", IsExpandable = false });
                    });
                }
            });
        }
        return Children;
    }
}
