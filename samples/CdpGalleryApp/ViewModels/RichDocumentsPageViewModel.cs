using System;
using System.IO;
using System.Windows.Input;

namespace CdpGalleryApp.ViewModels;

public class RichDocumentsPageViewModel : ViewModelBase
{
    private string? _filePath;
    private string _docxPath;
    private string _xlsxPath;
    private string _pptxPath;
    private string _rtfPath;

    public string? FilePath
    {
        get => _filePath;
        set => RaiseAndSetIfChanged(ref _filePath, value);
    }

    public string DocxPath => _docxPath;
    public string XlsxPath => _xlsxPath;
    public string PptxPath => _pptxPath;
    public string RtfPath => _rtfPath;

    public RichDocumentsPageViewModel()
    {
        // Pre-generate all documents to temp paths
        _docxPath = MockDocumentGenerator.GenerateDocx();
        _xlsxPath = MockDocumentGenerator.GenerateXlsx();
        _pptxPath = MockDocumentGenerator.GeneratePptx();
        _rtfPath = MockDocumentGenerator.GenerateRtf();

        // Default to Docx
        _filePath = _docxPath;
    }

    public void LoadDocx()
    {
        FilePath = _docxPath;
    }

    public void LoadXlsx()
    {
        FilePath = _xlsxPath;
    }

    public void LoadPptx()
    {
        FilePath = _pptxPath;
    }

    public void LoadRtf()
    {
        FilePath = _rtfPath;
    }
}
