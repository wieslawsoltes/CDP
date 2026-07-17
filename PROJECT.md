# Project: HTML/CSS SkiaSharp Rendering Engine

## Architecture

This project implements a highly reusable, high-performance HTML/CSS parsing and rendering engine based on SkiaSharp and integrates it across the CDP Markdown and Inspector systems.

```text
+-------------------+      +-------------------+
|  CDP.Html.Parser  |      |   CDP.Css.Parser  |
+---------+---------+      +---------+---------+
          |                          |
          | (HTML DOM tree)          | (Parsed Stylesheets)
          +------------+-------------+
                       |
                       v
            +--------------------+
            | CDP.Html.Renderer  |
            +----------+---------+
                       |
                       | (SKCanvas Layout & Draw calls)
                       v
         +-------------+-------------+
         |                           |
         v                           v
+-----------------------+   +----------------------+
| CDP.Markdown.Renderer |   |   CdpInspectorApp    |
+-----------------------+   +----------------------+
         |                           |
         v                           v
+-----------------------+   +----------------------+
|     CdpGalleryApp     |   | Interactive Preview  |
+-----------------------+   +----------------------+
```

- **CDP.Html.Parser**: Independent library that parses HTML source text into a standard DOM tree representation.
- **CDP.Css.Parser**: Independent library that parses CSS styles text into a stylesheet rules representation.
- **CDP.Html.Renderer**: Independent layout and rendering engine. Combines the HTML DOM tree and CSS stylesheets to calculate layouts (flow model, flex model, borders, margin, padding, typography) and draw them onto a SkiaSharp `SKCanvas`.
- **CDP.Markdown.Renderer**: Integrates the HTML/CSS renderer to layout and render inline and block HTML components inside markdown documents.
- **CdpGalleryApp & CdpInspectorApp**: Embed layout rendering showcases and real-time interactive previews.

---

## Milestones

| # | Name | Scope | Dependencies | Status |
|---|------|-------|--------------|--------|
| 1 | Project Exploration & Setup | Design project layout, project files, slnx integration | None | DONE |
| 2 | HTML & CSS Parser Implementation | Implement `CDP.Html.Parser` & `CDP.Css.Parser` and unit tests | M1 | DONE |
| 3 | Layout & Rendering Engine | Implement `CDP.Html.Renderer` style resolution, box model calculation, and SkiaSharp rendering | M2 | DONE |
| 4 | Markdown Integration | Update `CDP.Markdown.Renderer` to draw block/inline HTML | M3 | DONE |
| 5 | Gallery Showcase Pages | Add HTML standalone showcase & update Markdown showcase | M4 | DONE |
| 6 | Inspector HTML Preview Tab | Add HTML Preview Tab, View and ViewModel in inspector | M4 | DONE |
| 7 | Verification & Test Suite Pass | E2E simulations, unit tests pass, auditor checks | M5, M6 | DONE |
| 8 | CSS Variables & calc() | Custom properties `--*`, inheritance via cascade, `var()`, and basic `calc()` arithmetic | M3 | DONE |
| 9 | CSS Positioning | Absolute, Relative, Fixed positioning bounds and offset calculation relative to ancestors | M3 | DONE |
| 10| CSS Floats and Clears | Float routing left/right, wrapping boundaries, and clearing flow | M3 | DONE |
| 11| Visual Layout Unit Tests & Audit | Automated visual unit tests validating coordinates/dimensions and forensic integrity audit | M8, M9, M10 | DONE |

---

## Interface Contracts

### HTML Parser Node Structure (`CDP.Html.Parser`)
- `HtmlNode`: Base class for DOM tree components.
  - `HtmlElement` (inherited): Contains `TagName`, `Attributes` (Dictionary), `Children` (List of `HtmlNode`).
  - `HtmlTextNode` (inherited): Contains `Text` (string).
- `HtmlDocument`: Root container containing parsed `HtmlElement` nodes.
- Parser entry point:
  ```csharp
  public static class HtmlParser
  {
      public static HtmlDocument Parse(string htmlSource);
  }
  ```

### CSS Parser Structure (`CDP.Css.Parser`)
- `CssRule`: Repesent stylesheet rules. Contains `Selectors` (List of selectors) and `Declarations` (Dictionary of CSS properties).
- Selector matching model supporting: Tag name (`div`), Class (`.class`), ID (`#id`), compound selectors (`div.class`), and selector lists.
- Parser entry point:
  ```csharp
  public static class CssParser
  {
      public static CssStyleSheet Parse(string cssSource);
  }
  ```

### Layout and Rendering Context (`CDP.Html.Renderer`)
- Resolution: Match DOM elements against CSS stylesheet rules to build computed styles.
- Layout Tree: Computes bounds and layout parameters using a standard box model (`margin`, `padding`, `border-width`, `border-radius`, `width`, `height`) and flow displays (`block`, `inline`, `inline-block`, `flex`).
- Draw:
  ```csharp
  public static class HtmlRenderer
  {
      public static void Render(SKCanvas canvas, HtmlDocument doc, CssStyleSheet stylesheet, SKRect bounds);
  }
  ```

---

## Code Layout

The new project source files must reside in:
- `src/CDP.Html.Parser/`
- `src/CDP.Css.Parser/`
- `src/CDP.Html.Renderer/`
- `tests/CDP.Html.Parser.Tests/`
- `tests/CDP.Css.Parser.Tests/`
- `tests/CDP.Html.Renderer.Tests/`
