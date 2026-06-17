namespace CdpInspectorApp.Models;

public class EventListenerModel
{
    public string Type { get; }
    public string HandlerName { get; }
    public bool UseCapture { get; }
    public string CaptureText => UseCapture ? "Capture" : "Bubble";

    public EventListenerModel(string type, string handlerName, bool useCapture)
    {
        Type = type;
        HandlerName = handlerName;
        UseCapture = useCapture;
    }
}
