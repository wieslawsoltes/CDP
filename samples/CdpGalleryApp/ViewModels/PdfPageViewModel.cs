using System;
using System.IO;
using System.Windows.Input;
using CDP.Pdf.Editor;
using CdpGalleryApp.ViewModels;

namespace CdpGalleryApp.ViewModels;

public class PdfPageViewModel : ViewModelBase
{
    private string? _filePath;
    private PdfEditMode _editMode = PdfEditMode.Select;
    private bool _isReadOnly = false;
    private double _zoomScale = 1.0;
    private string? _selectedCommentText;

    public string? FilePath
    {
        get => _filePath;
        set => RaiseAndSetIfChanged(ref _filePath, value);
    }

    public PdfEditMode EditMode
    {
        get => _editMode;
        set => RaiseAndSetIfChanged(ref _editMode, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    public double ZoomScale
    {
        get => _zoomScale;
        set => RaiseAndSetIfChanged(ref _zoomScale, value);
    }

    public string? SelectedCommentText
    {
        get => _selectedCommentText;
        set => RaiseAndSetIfChanged(ref _selectedCommentText, value);
    }

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SetSelectModeCommand { get; }
    public ICommand SetDirectSelectModeCommand { get; }
    public ICommand SetPanModeCommand { get; }
    public ICommand SetAddTextModeCommand { get; }
    public ICommand SetAddShapeModeCommand { get; }
    public ICommand SetHighlightModeCommand { get; }
    public ICommand SetUnderlineModeCommand { get; }
    public ICommand SetStickyNoteModeCommand { get; }
    public ICommand SetPencilModeCommand { get; }
    public ICommand SetAddFormFieldModeCommand { get; }

    public ICommand RotatePageCommand { get; }
    public ICommand InsertPageCommand { get; }
    public ICommand DeletePageCommand { get; }
    
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand FitWidthCommand { get; }
    public ICommand FitPageCommand { get; }

    public event Action<string>? RequestLoad;
    public event Action<string>? RequestSave;
    
    // Commands to talk to the view (which talks to the editor control)
    public event Action<int>? RequestRotatePage;
    public event Action? RequestInsertPage;
    public event Action? RequestDeletePage;
    public event Action? RequestFitWidth;
    public event Action? RequestFitPage;

    public PdfPageViewModel()
    {
        OpenCommand = new RelayCommand(OpenDocument);
        SaveCommand = new RelayCommand(SaveDocument);
        SetSelectModeCommand = new RelayCommand(() => EditMode = PdfEditMode.Select);
        SetDirectSelectModeCommand = new RelayCommand(() => EditMode = PdfEditMode.DirectSelect);
        SetPanModeCommand = new RelayCommand(() => EditMode = PdfEditMode.Pan);
        SetAddTextModeCommand = new RelayCommand(() => EditMode = PdfEditMode.AddText);
        SetAddShapeModeCommand = new RelayCommand(() => EditMode = PdfEditMode.AddShape);
        SetHighlightModeCommand = new RelayCommand(() => EditMode = PdfEditMode.Highlight);
        SetUnderlineModeCommand = new RelayCommand(() => EditMode = PdfEditMode.Underline);
        SetStickyNoteModeCommand = new RelayCommand(() => EditMode = PdfEditMode.StickyNote);
        SetPencilModeCommand = new RelayCommand(() => EditMode = PdfEditMode.Pencil);
        SetAddFormFieldModeCommand = new RelayCommand(() => EditMode = PdfEditMode.AddFormField);

        RotatePageCommand = new RelayCommand<string>(deg => { if(int.TryParse(deg, out int d)) RequestRotatePage?.Invoke(d); });
        InsertPageCommand = new RelayCommand(() => RequestInsertPage?.Invoke());
        DeletePageCommand = new RelayCommand(() => RequestDeletePage?.Invoke());

        ZoomInCommand = new RelayCommand(() => ZoomScale = Math.Min(4.0, ZoomScale + 0.1));
        ZoomOutCommand = new RelayCommand(() => ZoomScale = Math.Max(0.5, ZoomScale - 0.1));
        FitWidthCommand = new RelayCommand(() => RequestFitWidth?.Invoke());
        FitPageCommand = new RelayCommand(() => RequestFitPage?.Invoke());
    }

    private void OpenDocument()
    {
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "document.pdf");
        RequestLoad?.Invoke(defaultPath);
    }

    private void SaveDocument()
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            RequestSave?.Invoke(FilePath);
        }
    }

    // Nested helper class matching other view models
    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    
    private class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        public RelayCommand(Action<T?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter == null ? default : (T)Convert.ChangeType(parameter, typeof(T)));
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
