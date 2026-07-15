using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace CdpInspectorApp.Services
{
    public class LspCompletionData : ICompletionData
    {
        public string Text { get; }
        public string DescriptionText { get; }

        public LspCompletionData(string text, string description = "LSP suggestion")
        {
            Text = text;
            DescriptionText = description;
        }

        public IImage? Image => null;
        public object Content => Text;
        public object Description => DescriptionText;
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, Text);
        }
    }
}
