using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;

namespace CdpInspectorApp.ViewModels;

public class HtmlPreviewViewModel : ViewModelBase
{
    private readonly ICdpService _cdpService;
    private readonly ElementsViewModel _elementsVm;

    private bool _isCustomMode;
    private string _outerHtml = "<!-- No element selected -->";
    private string _customHtml = "<div>\n  <h3>Custom Sandbox</h3>\n  <p>Modify HTML &amp; CSS on the left!</p>\n</div>";
    private string _customCss = "div { padding: 12px; background-color: #222; border: 1.5px solid #ff9800; border-radius: 4px; color: #fff; }";

    public bool IsCustomMode
    {
        get => _isCustomMode;
        set
        {
            if (RaiseAndSetIfChanged(ref _isCustomMode, value))
            {
                OnPropertyChanged(nameof(HtmlText));
                OnPropertyChanged(nameof(CssText));
            }
        }
    }

    public string OuterHtml
    {
        get => _outerHtml;
        private set
        {
            if (RaiseAndSetIfChanged(ref _outerHtml, value))
            {
                if (!IsCustomMode)
                {
                    OnPropertyChanged(nameof(HtmlText));
                }
            }
        }
    }

    public string CustomHtml
    {
        get => _customHtml;
        set
        {
            if (RaiseAndSetIfChanged(ref _customHtml, value))
            {
                if (IsCustomMode)
                {
                    OnPropertyChanged(nameof(HtmlText));
                }
            }
        }
    }

    public string CustomCss
    {
        get => _customCss;
        set
        {
            if (RaiseAndSetIfChanged(ref _customCss, value))
            {
                if (IsCustomMode)
                {
                    OnPropertyChanged(nameof(CssText));
                }
            }
        }
    }

    public string HtmlText => IsCustomMode ? CustomHtml : OuterHtml;
    public string CssText => IsCustomMode ? CustomCss : string.Empty;

    public HtmlPreviewViewModel(ICdpService cdpService, ElementsViewModel elementsVm)
    {
        _cdpService = cdpService ?? throw new ArgumentNullException(nameof(cdpService));
        _elementsVm = elementsVm ?? throw new ArgumentNullException(nameof(elementsVm));

        _elementsVm.PropertyChanged += OnElementsViewModelPropertyChanged;

        _ = RefreshOuterHtmlAsync();
    }

    private void OnElementsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ElementsViewModel.SelectedNode))
        {
            _ = RefreshOuterHtmlAsync();
        }
    }

    private async Task RefreshOuterHtmlAsync()
    {
        var node = _elementsVm.SelectedNode;
        if (node == null)
        {
            OuterHtml = "<!-- No element selected -->";
            return;
        }

        int requestedNodeId = node.NodeId;

        try
        {
            var response = await _cdpService.SendCommandAsync("DOM.getOuterHTML", new JsonObject
            {
                ["nodeId"] = requestedNodeId
            });

            if (_elementsVm.SelectedNode?.NodeId != requestedNodeId)
            {
                return; // Slower old request completes after a newer request is already active
            }

            if (response != null && response["outerHTML"] != null)
            {
                OuterHtml = response["outerHTML"]?.GetValue<string>() ?? "<!-- Empty node -->";
            }
            else
            {
                OuterHtml = $"<!-- Failed to retrieve outerHTML for Node ID {requestedNodeId} -->";
            }
        }
        catch (Exception ex)
        {
            if (_elementsVm.SelectedNode?.NodeId == requestedNodeId)
            {
                OuterHtml = $"<!-- Error retrieving HTML: {ex.Message} -->";
            }
        }
    }
}
