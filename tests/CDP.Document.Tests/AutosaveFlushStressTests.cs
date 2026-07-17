using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using CDP.Document.Editor;
using CDP.Document.Parser.AST;

namespace CDP.Document.Tests;

public class AutosaveFlushStressTests
{
    private void CleanupEditorTimer(DocumentEditor editor)
    {
        var timerField = typeof(DocumentEditor).GetField("_saveDebounceTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        if (timerField != null)
        {
            var timer = (Timer?)timerField.GetValue(editor);
            timer?.Dispose();
            timerField.SetValue(editor, null);
        }
        var versionField = typeof(DocumentEditor).GetField("_saveVersion", BindingFlags.NonPublic | BindingFlags.Instance);
        if (versionField != null)
        {
            versionField.SetValue(editor, -9999);
        }
    }

    private void InvokeDetach(DocumentEditor editor)
    {
        var window = new Avalonia.Controls.Window { Content = editor };
        window.Show();
        window.Content = null;
        window.Close();
    }

    private void InvokeScheduleAutoSave(DocumentEditor editor)
    {
        var scheduleMethod = typeof(DocumentEditor).GetMethod("ScheduleAutoSave", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(scheduleMethod);
        scheduleMethod.Invoke(editor, null);
    }

    private Timer? GetSaveDebounceTimer(DocumentEditor editor)
    {
        var timerField = typeof(DocumentEditor).GetField("_saveDebounceTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(timerField);
        return (Timer?)timerField.GetValue(editor);
    }

    private int GetSaveVersion(DocumentEditor editor)
    {
        var versionField = typeof(DocumentEditor).GetField("_saveVersion", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(versionField);
        return (int)versionField.GetValue(editor)!;
    }

    [AvaloniaFact]
    public async Task TestAutosave_MultipleSuccessiveEdits_DebouncesAndSavesOnce()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        var doc = new WordDocument();
        var para = new ParagraphBlock();
        var run = new TextRun { Text = "Initial" };
        para.Children.Add(run);
        doc.AddChild(para);

        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".rtf");
        File.WriteAllText(tempPath, "");
        editor.FilePath = tempPath;

        var docField = typeof(DocumentEditor).GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        try
        {
            // Do 5 rapid successive edits
            for (int i = 1; i <= 5; i++)
            {
                run.Text = $"Edit {i}";
                InvokeScheduleAutoSave(editor);
                
                var timer = GetSaveDebounceTimer(editor);
                Assert.NotNull(timer);
                Assert.Equal(i, GetSaveVersion(editor));
            }

            // At this point, the file should still be empty
            Assert.Equal("", File.ReadAllText(tempPath));

            // Wait for the timer (debounce is 500ms, let's wait 600ms)
            await Task.Delay(600);

            // Pump UI thread to let the posted SaveDocument run
            Dispatcher.UIThread.RunJobs();

            // The timer should now be cleaned up (null)
            Assert.Null(GetSaveDebounceTimer(editor));

            // Verify the document was saved with the final edit content
            string savedContent = File.ReadAllText(tempPath);
            Assert.Contains("Edit 5", savedContent);
            Assert.DoesNotContain("Edit 1", savedContent);
        }
        finally
        {
            CleanupEditorTimer(editor);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [AvaloniaFact]
    public void TestAutosave_DetachDuringSave_FlushesImmediately()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        var doc = new WordDocument();
        var para = new ParagraphBlock();
        var run = new TextRun { Text = "Initial" };
        para.Children.Add(run);
        doc.AddChild(para);

        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".rtf");
        File.WriteAllText(tempPath, "");
        editor.FilePath = tempPath;

        var docField = typeof(DocumentEditor).GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        try
        {
            run.Text = "Modified Text Before Detach";
            InvokeScheduleAutoSave(editor);

            var timer = GetSaveDebounceTimer(editor);
            Assert.NotNull(timer);

            // Verify file is still empty before detach
            Assert.Equal("", File.ReadAllText(tempPath));

            // Detach control
            InvokeDetach(editor);

            // Verify that the timer is disposed immediately
            Assert.Null(GetSaveDebounceTimer(editor));

            // Verify that the save was flushed synchronously
            string savedContent = File.ReadAllText(tempPath);
            Assert.Contains("Modified Text Before Detach", savedContent);
        }
        finally
        {
            CleanupEditorTimer(editor);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [AvaloniaFact]
    public void TestAutosave_DetachWithNoActiveTimer_NoOpsSuccessfully()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        var doc = new WordDocument();
        var para = new ParagraphBlock();
        var run = new TextRun { Text = "Initial" };
        para.Children.Add(run);
        doc.AddChild(para);

        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".rtf");
        File.WriteAllText(tempPath, "");
        editor.FilePath = tempPath;

        var docField = typeof(DocumentEditor).GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        try
        {
            // Verify no timer is active
            Assert.Null(GetSaveDebounceTimer(editor));

            // Save the file with some specific initial content manually
            File.WriteAllText(tempPath, "Initial Manual Content");

            // Detach control
            InvokeDetach(editor);

            // Verify timer remains null
            Assert.Null(GetSaveDebounceTimer(editor));

            // Verify the file was not overwritten on detach (still contains manual content)
            Assert.Equal("Initial Manual Content", File.ReadAllText(tempPath));
        }
        finally
        {
            CleanupEditorTimer(editor);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [AvaloniaFact]
    public void TestAutosave_ChangeFilePathFlushesPreviousPendingSave()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        
        var docA = new WordDocument();
        var paraA = new ParagraphBlock();
        var runA = new TextRun { Text = "DocA Initial" };
        paraA.Children.Add(runA);
        docA.AddChild(paraA);

        string tempPathA = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_A.rtf");
        string tempPathB = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_B.rtf");

        File.WriteAllText(tempPathA, "");
        File.WriteAllText(tempPathB, "");

        var docField = typeof(DocumentEditor).GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(docField);

        try
        {
            // 1. Load tempPathA
            editor.FilePath = tempPathA;
            docField.SetValue(editor, docA);

            // 2. Perform edit on A and schedule autosave
            runA.Text = "DocA Modified";
            InvokeScheduleAutoSave(editor);

            var timer = GetSaveDebounceTimer(editor);
            Assert.NotNull(timer);
            Assert.Equal("", File.ReadAllText(tempPathA));

            // 3. Change FilePath to tempPathB. This should trigger flush of A first!
            editor.FilePath = tempPathB;

            // 4. Verify path A was flushed
            string contentA = File.ReadAllText(tempPathA);
            Assert.Contains("DocA Modified", contentA);

            // 5. Verify timer was cleared after flush
            Assert.Null(GetSaveDebounceTimer(editor));
        }
        finally
        {
            CleanupEditorTimer(editor);
            if (File.Exists(tempPathA)) File.Delete(tempPathA);
            if (File.Exists(tempPathB)) File.Delete(tempPathB);
        }
    }

    [AvaloniaFact]
    public void TestAutosave_NoTimerOrThreadLeaks_StressTest()
    {
        var editor = new DocumentEditor { IsReadOnly = false };
        var doc = new WordDocument();
        var para = new ParagraphBlock();
        var run = new TextRun { Text = "Initial" };
        para.Children.Add(run);
        doc.AddChild(para);

        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".rtf");
        File.WriteAllText(tempPath, "");
        editor.FilePath = tempPath;

        var docField = typeof(DocumentEditor).GetField("_document", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(docField);
        docField.SetValue(editor, doc);

        try
        {
            // Do 100 cycles of scheduling and immediately flushing/detaching
            for (int i = 0; i < 100; i++)
            {
                run.Text = $"Stress Edit {i}";
                InvokeScheduleAutoSave(editor);
                
                var timer = GetSaveDebounceTimer(editor);
                Assert.NotNull(timer);

                if (i % 2 == 0)
                {
                    editor.Flush();
                }
                else
                {
                    InvokeDetach(editor);
                }

                Assert.Null(GetSaveDebounceTimer(editor));
            }

            // Verify final state matches last iteration
            string savedContent = File.ReadAllText(tempPath);
            Assert.Contains("Stress Edit 99", savedContent);
        }
        finally
        {
            CleanupEditorTimer(editor);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
