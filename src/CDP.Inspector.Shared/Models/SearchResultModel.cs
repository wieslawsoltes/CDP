namespace CdpInspectorApp.Models;

public class SearchResultModel
{
    public string Path { get; set; } = "";
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = "";
}
