# Original User Request

## Initial Request — 2026-07-15T09:57:26+02:00

Build a new Avalonia sample gallery application showcasing all workspace custom controls (markdown, rich documents, split layout, node editor, accordion, charts) using a new custom WinUI/Fluent-like navigation control packaged in a new csproj.

Working directory: /Users/wieslawsoltes/GitHub/CDP
Integrity mode: demo

## Requirements

### R1. Custom Fluent Navigation Control & NuGet Package
- Create a new Avalonia library project `src/CDP.FluentNavigation/CDP.FluentNavigation.csproj`.
- Implement a custom `NavigationView` control mimicking the WinUI/Fluent design:
  - Hierarchical navigation item support (groups, parent-child expansion).
  - Collapsible pane mode (expanded, compact icon-only, overlay for narrow screens).
  - Header, footer menu items (e.g. Settings, User Profile), and search box integration.
  - Custom transition animations on item selection.
- Configure the csproj for NuGet packaging (pack settings, description, license/tags).
- Build the project and package it using `dotnet pack`. Update the root `README.md` to document how to import and use the navigation control.

### R2. Gallery Showcase Application
- Create a new premium Avalonia app project `samples/CdpGalleryApp/CdpGalleryApp.csproj`.
- Reference and use the new `CDP.FluentNavigation` library to drive the shell layout and navigation between showcase pages.
- Follow premium design aesthetics (sleek dark mode, Outfit or Inter font, smooth gradients, glassmorphism, responsive adaptive layouts).

### R3. Comprehensive Showcase Pages
Implement highly detailed sample pages for each of the following controls:
- **Markdown Page**: Showcases the custom Markdown editor and renderer, demonstrating checkbox toggling, syntax coloring, clickable links, and mode toggling.
- **Rich Documents Page**: Showcases Word (header/footers, footnotes), Excel (gridlines, formulas, spans), PowerPoint (master slides, charts, resizing handles), and RTF parsing/rendering.
- **Split Layout Page**: Demonstrates multi-pane layouts utilizing the `SuperSplitBox` control.
- **Node Editor Page**: Displays an interactive node graph editor showing control nodes and connection flows.
- **Accordion Page**: Displays expandable/collapsible accordions with custom styling.
- **Charts Page**: Renders beautiful bar, line, and pie charts with rich visual aesthetics.

### R4. Automated Layout & Integration Tests
- Create a test project `tests/CDP.Gallery.Tests/` to verify:
  - `NavigationView` item selection changes fire the correct event args and navigation commands.
  - Pane state transitions (expanded to compact) behave correctly on size changes.
  - Page-to-page visual loading and routing success.

## Acceptance Criteria

### NavigationView Design & Packaging
- [ ] `CDP.FluentNavigation` compiles cleanly, packages into a `.nupkg` via `dotnet pack`, and compiles with default compiled bindings.
- [ ] Root `README.md` is updated with code snippets illustrating `NavigationView` XAML integration.
- [ ] The pane toggles between expanded, compact, and overlay modes on size changes or button clicks.

### Gallery Application & Pages
- [ ] `CdpGalleryApp` launches successfully and references the packaged navigation control.
- [ ] Detailed pages are present for Markdown, Rich Documents, Split Layout, Node Editor, Accordion, and Charts.
- [ ] Controls are interactive on screen (e.g. cells edit in Excel view, markdown checkbox toggles, nodes connect, split panes resize).

### Automated Testing
- [ ] The unit/layout tests in `tests/CDP.Gallery.Tests/` execute and pass with 100% success.
