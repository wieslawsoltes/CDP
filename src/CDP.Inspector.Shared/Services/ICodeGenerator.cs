namespace CdpInspectorApp.Services;

using System.Collections.Generic;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;

public interface ICodeGenerator
{
    RecordingFormat Format { get; }
    string TabHeader { get; }
    string TitleText { get; }
    string ExportButtonText { get; }
    string Generate(IEnumerable<RecordedStepModel> steps, string hostAddress);
}
