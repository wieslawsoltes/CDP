using System.Collections.Generic;

namespace Chrome.DevTools.Protocol;

public interface ICodeGenerator
{
    RecordingFormat Format { get; }
    string TabHeader { get; }
    string TitleText { get; }
    string ExportButtonText { get; }
    string Generate(IEnumerable<RecordedStep> steps, string hostAddress);
}
