namespace CDP.Markdown.Renderer.Layout;

public interface ITextMeasurer
{
    float MeasureText(string text, TextStyle style);
    float[] GetCharacterWidths(string text, TextStyle style);
    float GetLineHeight(TextStyle style);
}
