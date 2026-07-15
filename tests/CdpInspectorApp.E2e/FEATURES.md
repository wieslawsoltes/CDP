# E2E Test Suite Features Coverage Tracking

This document matches every major inspector feature and sub-feature to its corresponding E2E YAML test flow.

| Feature Area | Sub-Feature | YAML Test Flow Path | Status |
| --- | --- | --- | --- |
| **Connection** | Connect to target | `connection/connect.flow.yaml` | Active |
| | Connect then disconnect | `connection/disconnect.flow.yaml` | Active |
| | Connect invalid port fallback | `connection/connect_invalid_port.flow.yaml` | Active |
| | Reconnection cycle | `connection/reconnect.flow.yaml` | Active |
| **Simulation** | Preview image toggle | `simulation/preview.flow.yaml` | Active |
| | Zoom factor control | `simulation/zoom_control.flow.yaml` | Active |
| **Elements** | Visual tree node selection | `elements/inspect_tree.flow.yaml` | Active |
| | Computed styles inspection | `elements/computed_styles.flow.yaml` | Active |
| | Attribute modification | `elements/attribute_modification.flow.yaml` | Active |
| **Console** | C# Script evaluation | `console/eval_script.flow.yaml` | Active |
| | REPL print log | `console/repl.flow.yaml` | Active |
| | C# runtime exception | `console/script_exception.flow.yaml` | Active |
| | Clear logs history | `console/console_clear.flow.yaml` | Active |
| | Command history navigation | `console/repl_history.flow.yaml` | Active |
| | Filter logs level list | `console/filter_logs.flow.yaml` | Active |
| **Sources** | Traversal explorer | `sources/workspace_explorer.flow.yaml` | Active |
| | View source files | `sources/file_viewer.flow.yaml` | Active |
| | Search in workspace files | `sources/search_files.flow.yaml` | Active |
| | Theme selector button | `sources/theme_selector.flow.yaml` | Active |
| | Structure outline tree | `sources/structure_outline.flow.yaml` | Active |
| **Network** | Capturing outbound requests | `network/request_logging.flow.yaml` | Active |
| | View details/headers side pane | `network/request_headers.flow.yaml` | Active |
| | Clear requests history | `network/clear_requests.flow.yaml` | Active |
| | Inspect payload details | `network/request_payload.flow.yaml` | Active |
| | Filter requests search | `network/filter_requests.flow.yaml` | Active |
| **Performance** | Performance chart metrics | `performance/metrics.flow.yaml` | Active |
| | FPS metrics recording | `performance/fps_recording.flow.yaml` | Active |
| **Profiler** | Capture dotTrace profiles | `profiler/dottrace_run.flow.yaml` | Active |
| | Save and load dtp trace file | `profiler/dottrace_save_load.flow.yaml` | Active |
| | Flame chart zoom keys | `profiler/flamechart_zoom.flow.yaml` | Active |
| | Call tree filter query | `profiler/calltree_filter.flow.yaml` | Active |
| **Memory** | Take heap snapshot & GC run | `memory/dotmemory_snapshot.flow.yaml` | Active |
| | Compare snapshots | `memory/dotmemory_comparison.flow.yaml` | Active |
| | Dominator Tree navigation | `memory/dominator_tree.flow.yaml` | Active |
| | Detached controls filter | `memory/detached_controls.flow.yaml` | Active |
| **Application** | Resource dictionaries | `application/resources.flow.yaml` | Active |
| | Style theme mode toggle | `application/theme_mode.flow.yaml` | Active |
| | Resource search filter | `application/resource_search.flow.yaml` | Active |
| **Recorder** | Record user interactions | `recorder/standard_mode.flow.yaml` | Active |
| | Test Studio replay | `recorder/test_studio.flow.yaml` | Active |
| | Generate HTML report | `recorder/generate_html_report.flow.yaml` | Active |
| | Generate PDF report | `recorder/generate_pdf_report.flow.yaml` | Active |
| **XAML Engine** | Namespace & Type Registry | `XamlEngine/TypeSystem/RegisterNamespace.flow.yaml` | Active |
| | AST Parser Spacing Fidelity | `XamlEngine/Parser/ParseValidXaml.flow.yaml` | Active |
| | Error Diagnostics Recovery | `XamlEngine/ErrorRecovery/UnclosedXmlTag.flow.yaml` | Active |
| | XML Element Mutations | `XamlEngine/Mutation/SetExistingAttribute.flow.yaml` | Active |
| | Custom LSP Autocomplete | `XamlEngine/Lsp/LspGetCompletions.flow.yaml` | Active |
| | Framework Adapter Navigation | `XamlEngine/Adapters/AvaloniaAdapterTraverse.flow.yaml` | Active |
| | Live Subtree Hot-Reload | `XamlEngine/HotReload/StylingHotReload.flow.yaml` | Active |
| | Cross-Feature Composition | `XamlEngine/CrossFeature/MutationAstWhitespace.flow.yaml` | Active |
| | Real-world Visual Designer | `XamlEngine/RealWorld/VisualDesignerEdit.flow.yaml` | Active |
