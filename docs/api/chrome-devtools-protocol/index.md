---
title: Chrome.DevTools.Protocol API
---

# Chrome.DevTools.Protocol API

Source-indexed API reference for `Chrome.DevTools.Protocol`.

Chrome DevTools Protocol (CDP) core server and session logic.

## Package

- Package ID: `Chrome.DevTools.Protocol`
- Source project: `src/Chrome.DevTools.Protocol/Chrome.DevTools.Protocol.csproj`
- Related guide: [Architecture](/articles/architecture)

## Notes

- This package is indexed directly from the public source files.
- Detailed member pages are unavailable because the Markdown reflection generator could not load this assembly in the current environment.
- The source index keeps the package discoverable from the docs site and links each public type back to the repository.

## CDP.Automation.OS

| Type | Kind | Source |
| --- | --- | --- |
| `IOsAutomation` | interface | [`src/Chrome.DevTools.Protocol/Client/IOsAutomation.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/IOsAutomation.cs) |
| `OSNode` | class | [`src/Chrome.DevTools.Protocol/Client/OSNode.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/OSNode.cs) |
| `OSProcessMetrics` | class | [`src/Chrome.DevTools.Protocol/Client/IOsAutomation.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/IOsAutomation.cs) |
| `OSWindow` | class | [`src/Chrome.DevTools.Protocol/Client/OSWindow.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/OSWindow.cs) |

## Chrome.DevTools.Protocol

| Type | Kind | Source |
| --- | --- | --- |
| `AppiumCSharpGenerator` | class | [`src/Chrome.DevTools.Protocol/Automation/Generators/AppiumCSharpGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/Generators/AppiumCSharpGenerator.cs) |
| `AttributeSelector` | struct | [`src/Chrome.DevTools.Protocol/Selector/CssSelectorParser.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Selector/CssSelectorParser.cs) |
| `AttributeSelectorOperator` | enum | [`src/Chrome.DevTools.Protocol/Selector/CssSelectorParser.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Selector/CssSelectorParser.cs) |
| `AvaloniaHeadlessXUnitGenerator` | class | [`src/Chrome.DevTools.Protocol/Automation/Generators/AvaloniaHeadlessXUnitGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/Generators/AvaloniaHeadlessXUnitGenerator.cs) |
| `CdpDelegatingHandler` | class | [`src/Chrome.DevTools.Protocol/Server/CdpDelegatingHandler.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpDelegatingHandler.cs) |
| `CdpDispatcher` | class | [`src/Chrome.DevTools.Protocol/Server/CdpDispatcher.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpDispatcher.cs) |
| `CdpDomainRegistry` | class | [`src/Chrome.DevTools.Protocol/Server/CdpDomainRegistry.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpDomainRegistry.cs) |
| `CdpEventEventArgs` | class | [`src/Chrome.DevTools.Protocol/Client/ICdpService.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/ICdpService.cs) |
| `CdpJintEvaluator` | class | [`src/Chrome.DevTools.Protocol/Jint/CdpJintEvaluator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Jint/CdpJintEvaluator.cs) |
| `CdpLogger` | class | [`src/Chrome.DevTools.Protocol/CdpLogging.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/CdpLogging.cs) |
| `CdpLoggerExtensions` | class | [`src/Chrome.DevTools.Protocol/CdpLoggerExtensions.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/CdpLoggerExtensions.cs) |
| `CdpLogging` | class | [`src/Chrome.DevTools.Protocol/CdpLogging.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/CdpLogging.cs) |
| `CdpServer` | class | [`src/Chrome.DevTools.Protocol/Server/CdpServer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpServer.cs) |
| `CdpService` | class | [`src/Chrome.DevTools.Protocol/Client/CdpService.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/CdpService.cs) |
| `CdpSession` | class | [`src/Chrome.DevTools.Protocol/Server/CdpSession.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpSession.cs) |
| `CdpTabTarget` | class | [`src/Chrome.DevTools.Protocol/Server/CdpServer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpServer.cs) |
| `CdpTargetSession` | class | [`src/Chrome.DevTools.Protocol/Server/CdpTargetSession.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpTargetSession.cs) |
| `ConsoleRedirector` | class | [`src/Chrome.DevTools.Protocol/Server/CdpServer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpServer.cs) |
| `CssSelectorParser` | class | [`src/Chrome.DevTools.Protocol/Selector/CssSelectorParser.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Selector/CssSelectorParser.cs) |
| `FlowCommandCatalog` | class | [`src/Chrome.DevTools.Protocol/Automation/FlowCommandCatalog.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/FlowCommandCatalog.cs) |
| `FlowCommandValueKind` | enum | [`src/Chrome.DevTools.Protocol/Automation/FlowCommandCatalog.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/FlowCommandCatalog.cs) |
| `ICdpService` | interface | [`src/Chrome.DevTools.Protocol/Client/ICdpService.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/ICdpService.cs) |
| `ICdpTarget` | interface | [`src/Chrome.DevTools.Protocol/Server/ICdpTarget.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/ICdpTarget.cs) |
| `ICodeGenerator` | interface | [`src/Chrome.DevTools.Protocol/Automation/Generators/ICodeGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/Generators/ICodeGenerator.cs) |
| `ITelemetryProvider` | interface | [`src/Chrome.DevTools.Protocol/Automation/ITelemetryProvider.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/ITelemetryProvider.cs) |
| `JintObjectWrapper` | class | [`src/Chrome.DevTools.Protocol/Server/CdpSession.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpSession.cs) |
| `NetworkReportItem` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |
| `NetworkTelemetryProvider` | class | [`src/Chrome.DevTools.Protocol/Automation/NetworkTelemetryProvider.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/NetworkTelemetryProvider.cs) |
| `NodeMap` | class | [`src/Chrome.DevTools.Protocol/Selector/NodeMap.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Selector/NodeMap.cs) |
| `OsAutomationCdpSession` | class | [`src/Chrome.DevTools.Protocol/Client/OsAutomationCdpSession.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/OsAutomationCdpSession.cs) |
| `OsAutomationProvider` | class | [`src/Chrome.DevTools.Protocol/Client/OsAutomationProvider.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/OsAutomationProvider.cs) |
| `ParsedStep` | class | [`src/Chrome.DevTools.Protocol/Automation/RecordingParser.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/RecordingParser.cs) |
| `PerformanceTelemetryProvider` | class | [`src/Chrome.DevTools.Protocol/Automation/PerformanceTelemetryProvider.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/PerformanceTelemetryProvider.cs) |
| `PlaywrightGenerator` | class | [`src/Chrome.DevTools.Protocol/Automation/Generators/PlaywrightGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/Generators/PlaywrightGenerator.cs) |
| `PuppeteerGenerator` | class | [`src/Chrome.DevTools.Protocol/Automation/Generators/PuppeteerGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/Generators/PuppeteerGenerator.cs) |
| `QueuedTextWriter` | class | [`src/Chrome.DevTools.Protocol/Server/CdpServer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/CdpServer.cs) |
| `RecordedStep` | class | [`src/Chrome.DevTools.Protocol/Automation/RecordedStep.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/RecordedStep.cs) |
| `RecordingFormat` | enum | [`src/Chrome.DevTools.Protocol/Automation/RecordingFormat.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/RecordingFormat.cs) |
| `RecordingParser` | class | [`src/Chrome.DevTools.Protocol/Automation/RecordingParser.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/RecordingParser.cs) |
| `RunMetricSample` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |
| `ScreencastReconstructor` | class | [`src/Chrome.DevTools.Protocol/Client/ScreencastReconstructor.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/ScreencastReconstructor.cs) |
| `SeleniumCSharpGenerator` | class | [`src/Chrome.DevTools.Protocol/Automation/Generators/SeleniumCSharpGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/Generators/SeleniumCSharpGenerator.cs) |
| `StepReportItem` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |
| `TargetItem` | class | [`src/Chrome.DevTools.Protocol/Client/TargetItem.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Client/TargetItem.cs) |
| `TelemetryRegistry` | class | [`src/Chrome.DevTools.Protocol/Automation/ITelemetryProvider.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/ITelemetryProvider.cs) |
| `TestRunReportData` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |
| `TestStudioReportGenerator` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |
| `TestStudioReportOptions` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |
| `TestStudioStep` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioStep.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioStep.cs) |
| `TestStudioStepConverter` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioStepConverter.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioStepConverter.cs) |
| `TestStudioYamlParser` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioYamlParser.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioYamlParser.cs) |
| `TiledScreencastProducer` | class | [`src/Chrome.DevTools.Protocol/Server/TiledScreencastProducer.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Server/TiledScreencastProducer.cs) |
| `VideoFrameItem` | class | [`src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Automation/TestStudioReportGenerator.cs) |

## Chrome.DevTools.Protocol.Domains

| Type | Kind | Source |
| --- | --- | --- |
| `AdsDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/AdsDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/AdsDomain.cs) |
| `AutofillDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/AutofillDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/AutofillDomain.cs) |
| `BackgroundServiceDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/BackgroundServiceDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/BackgroundServiceDomain.cs) |
| `BrowserDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/BrowserDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/BrowserDomain.cs) |
| `CastDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/CastDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/CastDomain.cs) |
| `ConsoleDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/ConsoleDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/ConsoleDomain.cs) |
| `CrashReportContextDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/CrashReportContextDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/CrashReportContextDomain.cs) |
| `DeviceAccessDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/DeviceAccessDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/DeviceAccessDomain.cs) |
| `DeviceOrientationDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/DeviceOrientationDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/DeviceOrientationDomain.cs) |
| `DOMSnapshotDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/DOMSnapshotDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/DOMSnapshotDomain.cs) |
| `DOMStorageDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/DOMStorageDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/DOMStorageDomain.cs) |
| `EmulationDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/EmulationDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/EmulationDomain.cs) |
| `EventBreakpointsDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/EventBreakpointsDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/EventBreakpointsDomain.cs) |
| `FetchDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs) |
| `FileSystemDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/FileSystemDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/FileSystemDomain.cs) |
| `HttpKeyValueObserver` | class | [`src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs) |
| `IndexedDBDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/IndexedDBDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/IndexedDBDomain.cs) |
| `InspectorDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/InspectorDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/InspectorDomain.cs) |
| `InterceptAction` | enum | [`src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs) |
| `InterceptResult` | class | [`src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs) |
| `LogDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/LogDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/LogDomain.cs) |
| `MediaDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/MediaDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/MediaDomain.cs) |
| `NetworkDiagnosticObserver` | class | [`src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs) |
| `NetworkDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/NetworkDomain.cs) |
| `PerformanceTimelineDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/PerformanceTimelineDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/PerformanceTimelineDomain.cs) |
| `PreloadDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/PreloadDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/PreloadDomain.cs) |
| `ProfilerDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs) |
| `ProfilerState` | class | [`src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs) |
| `ProfileSpan` | struct | [`src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/ProfilerDomain.cs) |
| `RequestPattern` | class | [`src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/FetchDomain.cs) |
| `SchemaDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/SchemaDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/SchemaDomain.cs) |
| `SourcesDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/SourcesDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/SourcesDomain.cs) |
| `SystemInfoDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/SystemInfoDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/SystemInfoDomain.cs) |
| `TargetDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/TargetDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/TargetDomain.cs) |
| `TetheringDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/TetheringDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/TetheringDomain.cs) |
| `TracingDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/TracingDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/TracingDomain.cs) |
| `WebAudioDomain` | class | [`src/Chrome.DevTools.Protocol/Domains/WebAudioDomain.cs`](https://github.com/wieslawsoltes/CDP/blob/main/src/Chrome.DevTools.Protocol/Domains/WebAudioDomain.cs) |

