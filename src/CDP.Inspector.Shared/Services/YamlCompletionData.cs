using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace CdpInspectorApp.Services;

public class YamlCompletionData : ICompletionData
{
    public string Text { get; }
    public string DescriptionText { get; }

    public YamlCompletionData(string text, string description = "YAML completion suggestion")
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
        if (YamlIntelliSenseProvider.IsCommand(Text))
        {
            DocumentLine line = textArea.Document.GetLineByOffset(completionSegment.Offset);
            string fullText = textArea.Document.Text;
            int properIndent = YamlIntelliSenseProvider.GetProperIndentation(fullText, completionSegment.Offset);
            
            string indentStr = new string(' ', properIndent);
            string insertText = $"{indentStr}- {Text}";
            
            int replaceStart = line.Offset;
            int replaceLength = completionSegment.EndOffset - replaceStart;
            
            textArea.Document.Replace(replaceStart, replaceLength, insertText);
        }
        else
        {
            textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, Text);
        }
    }
}
