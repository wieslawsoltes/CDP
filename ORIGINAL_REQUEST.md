# Original User Request

## Initial Request — 2026-07-15T23:21:34+02:00

Implement a highly reusable, high-performance HTML/CSS parsing and rendering engine based on SkiaSharp. Integrate it with the existing Markdown/Document rendering systems, add showcase pages in the gallery application, and introduce an HTML preview feature in the inspector.

Working directory: /Users/wieslawsoltes/GitHub/CDP
Integrity mode: demo

## Requirements

### R1. HTML and CSS Parser Libraries (Separate Projects)
- Create two separate C# projects in `src/`:
  - `CDP.Html.Parser`: Parses HTML source strings into a custom HTML DOM/AST representation (handling standard tags: `div`, `span`, `p`, `img`, `a`, `style`, `link`, `head`, `body`, text nodes).
  - `CDP.Css.Parser`: Parses CSS style rules into a stylesheet representation, matching standard CSS selectors (tag names, `.classes`, `#ids`, compound selectors) and extracting styling properties.
- Make both parsing libraries fully reusable and independent of the rendering context.

### R2. High-Performance SkiaSharp Layout & Rendering Engine
- Build a layout resolution engine inside `CDP.Html.Renderer` (or similar project) that resolves styles and computes layouts on top of SkiaSharp (`SKCanvas` and `SKRect` layout bounds):
  - Support basic CSS formatting (color, background, font-size, font-weight, font-style).
  - Support the standard CSS block model (margin, padding, border-width, border-color, border-radius, width, height).
  - Support layout flow types (`display: block`, `display: inline`, `display: inline-block`) and basic flex container routing (`display: flex`).
- Ensure high performance when processing and rendering complex HTML layouts.

### R3. Markdown Integration
- Wire the new HTML/CSS renderer into the `CDP.Markdown.Renderer` project.
- Ensure that block and inline HTML nodes (e.g. `<img src="..." />`, styled tags, custom CSS code inside `<style>`) are fully resolved and rendered by the HTML engine rather than being fallback-rendered as code blocks.

### R4. Gallery & Inspector Showcase Pages
- **CdpGalleryApp Showcase**:
  - Add a dedicated showcase page demonstrating standalone HTML/CSS layout rendering.
  - Update the Markdown page in the gallery to display sample markdown containing inline and block HTML components styled dynamically with CSS.
- **CdpInspectorApp Preview**:
  - Introduce an HTML preview view or tab in the inspector using the new rendering engine.

## Acceptance Criteria

### AST and Parsers
- [ ] HTML and CSS parsing libraries compile cleanly, and support nested HTML tree structures and basic CSS selectors.
- [ ] CSS style properties map correctly to AST node styles.

### Layout & Rendering Performance
- [ ] Renders HTML block/inline flows, custom borders, margins, padding, backgrounds, and styling correctly onto the Skia canvas.
- [ ] Handles layout changes and text wrapping dynamically based on available width.

### App Showcases & Tests
- [ ] Gallery app (`CdpGalleryApp`) starts, and includes the new standalone HTML rendering showcase page and updated Markdown HTML preview.
- [ ] The inspector app (`CdpInspectorApp`) features an interactive HTML preview canvas.
- [ ] Create unit tests covering HTML/CSS parser logic and style resolution matching. All unit tests pass with 0 failures.

## Follow-up — 2026-07-16T05:34:52Z

Implement a high-performance HTML/CSS parsing and rendering engine iteration, optimizing memory allocations and execution speed using modern .NET performance features.

Working directory: /Users/wieslawsoltes/GitHub/CDP
Integrity mode: demo

## Requirements

### R1. Low-Allocation Parser Optimizations
- Refactor `CDP.Html.Parser` and `CDP.Css.Parser` to use low-allocation .NET APIs.
- Utilize `ReadOnlySpan<char>` for parsing substrings, tokenization, and string slicing.
- Integrate `ArrayPool<char>` or custom buffers to avoid allocating temporary character arrays/strings.
- Transition parsing state-machines to return struct-based tokens instead of class allocations.

### R2. High-Performance Layout & Rendering
- Optimize style cascade and visual tree layout calculations in `CDP.Html.Renderer`.
- Avoid recursive allocations and reduce heap overhead during IFC (Inline Formatting Context) and BFC (Block Formatting Context) layout computations.
- Utilize lightweight layout structures and layout caches to avoid redundant size recalculations.

### R3. Automated Performance Benchmark Verification
- Create a performance comparison suite (using a custom high-precision runner or BenchmarkDotNet) that parses and layouts a complex HTML page (containing deeply nested divs, inline styles, text runs, and styling cascades).
- Compare the optimized execution time and byte allocations against the baseline implementation.

## Acceptance Criteria

### Correctness
- All existing unit tests and integration tests in `CDP.Html.Parser.Tests`, `CDP.Css.Parser.Tests`, `CDP.Html.Renderer.Tests`, `CDP.Markdown.Tests`, and `CDP.Inspector.Shared.Tests` continue to pass cleanly (0 failures).

### Performance and Allocation Reduction
- Parsing execution time is reduced by at least 30% compared to the baseline implementation.
- Memory allocations (allocated bytes per parsing iteration) are reduced by at least 50% compared to the baseline implementation.
- Garbage Collection collection counts (Gen 0/1/2) show a measurable reduction during stress iterations.

## Follow-up — 2026-07-16T08:30:41Z

Continue working on the HTML/CSS parser and HTML rendering engine, focusing on layout correctness, spec compliance, CSS positioning, floats, and CSS variables.

Working directory: /Users/wieslawsoltes/GitHub/CDP
Integrity mode: demo

## Requirements

### R1. CSS Positioning (Absolute, Relative, Fixed)
- Implement `position: absolute`, `position: relative`, and `position: fixed` support inside the style resolver and layout engine (`CDP.Html.Renderer`).
- Properly calculate box coordinate offsets (`top`, `right`, `bottom`, `left`) relative to the nearest positioned ancestor (or the viewport for fixed).
- Ensure positioned element bounds do not affect standard static BFC/IFC static flow layout positioning, but lay out out-of-flow elements relative to boundaries.

### R2. CSS Float flows (float: left / right) and clears
- Implement support for float routing (`float: left`, `float: right`) inside BFC blocks.
- Calculate wrapping boundaries for static inline text boxes next to floated block boxes.
- Support clear boundaries (`clear: left`, `clear: right`, `clear: both`) to restore static flow.

### R3. CSS Variables and calc() support
- Parse custom properties (`--var-name: value`) inside stylesheets and inline styles.
- Resolve custom property inheritance downstream via the style cascade utilizing `var(--var-name)`.
- Support basic `calc(...)` offset computations (e.g. `calc(100% - 20px)`) inside width, height, margin, and padding layout resolver rules.

### R4. layout Visual Unit Verification
- Create automated visual unit tests (under `tests/CDP.Html.Renderer.Tests/`) validating the correct coordinates and dimensions of elements styled with positioning, floats, and variables.
- Ensure all tests verify boundary boxes are exactly computed.

## Acceptance Criteria

### Correctness
- All existing parser, renderer, integration, and UI tests compile and pass cleanly (0 failures).
- Visual layout unit tests verifying positioning coordinates, floats wrapping boundaries, and variables evaluation pass with 100% success.


## Follow-up — 2026-07-16T14:45:06Z

Check, fix, reply to, and resolve all remaining 10 unresolved PR review comments on pull request #97.

Working directory: /Users/wieslawsoltes/GitHub/CDP
Integrity mode: demo

## Requirements

### R1. Unresolved Review Comment Discovery
Query all unresolved pull request review comments on pull request #97 using the GitHub CLI/GraphQL API.

### R2. Codebase Fixes
For each unresolved comment, analyze the issue described, implement a robust fix, and add/update unit tests to verify correctness. The 10 unresolved comment areas are:
1. **Include grouped shapes in PPTX save ordering** (`src/CDP.Document.Editor/DocumentEditor.cs`)
2. **Use document-global offsets for table cell hit tests** (`src/CDP.Document.Renderer/Layout/Word/WordLayoutManager.cs`)
3. **Ignore text nodes for child pseudo-classes** (`src/CDP.Html.Renderer/Style/StyleCascade.cs`)
4. **Register document namespaces before WPF tag completions** (`src/Wpf.Diagnostics.Cdp/Domains/XamlLspDomain.cs`)
5. **Respect sibling combinators during CSS matching** (`src/CDP.Html.Renderer/Style/StyleCascade.cs`)
6. **Move Markdown text handler registration to static init** (`src/CDP.Markdown.Editor/MarkdownEditor.cs`)
7. **Preserve media conditions before applying rules** (`src/CDP.Css.Parser/CssParser.cs`)
8. **Flush pending document autosaves before detaching** (`src/CDP.Document.Editor/DocumentEditor.cs`)
9. **Use real extents for inline DOCX images** (`src/CDP.Document.Renderer/Layout/Word/WordLayoutManager.cs`)
10. **Serialize table cells with the full inline writer** (`src/CDP.Document.Editor/DocumentEditor.cs`)

### R3. Reply and Resolve Thread on GitHub
For each fixed issue:
- Push the commit to the remote repository.
- Post a reply comment on the review thread containing a detailed explanation of the fix and the commit hash.
- Resolve the comment thread on GitHub via GraphQL.

## Acceptance Criteria

### Correctness
- [ ] All 10 comment areas are fully fixed.
- [ ] All solution unit tests compile and pass cleanly (0 failures) on all platforms.
- [ ] All 10 review threads are resolved on GitHub.



