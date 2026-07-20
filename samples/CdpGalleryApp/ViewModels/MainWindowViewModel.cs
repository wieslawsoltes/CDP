using System;

namespace CdpGalleryApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private object? _currentPage;

    public MarkdownPageViewModel MarkdownVm { get; } = new();
    public RichDocumentsPageViewModel RichDocsVm { get; } = new();
    public SplitLayoutPageViewModel SplitLayoutVm { get; } = new();
    public NodeEditorPageViewModel NodeEditorVm { get; } = new();
    public ChartsPageViewModel ChartsVm { get; } = new();
    public HtmlPageViewModel HtmlVm { get; } = new();
    public PdfPageViewModel PdfVm { get; } = new();

    public object? CurrentPage
    {
        get => _currentPage;
        set => RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public MainWindowViewModel()
    {
        // Default to Pdf page
        CurrentPage = PdfVm;
    }

    public void NavigateTo(string tag)
    {
        CurrentPage = tag switch
        {
            "Markdown" => MarkdownVm,
            "RichDocuments" => RichDocsVm,
            "SplitLayout" => SplitLayoutVm,
            "NodeEditor" => NodeEditorVm,
            "Accordion" => null, // Accordion is a standalone page defined in AccordionPage.axaml
            "Charts" => ChartsVm,
            "Html" => HtmlVm,
            "Pdf" => PdfVm,
            "Settings" => null,
            _ => CurrentPage
        };

        if (tag == "Accordion")
        {
            CurrentPage = new AccordionPageViewModel();
        }
        else if (tag == "Settings")
        {
            CurrentPage = new SettingsPageViewModel();
        }
    }
}

public class AccordionPageViewModel : ViewModelBase
{
}

public class SettingsPageViewModel : ViewModelBase
{
}
