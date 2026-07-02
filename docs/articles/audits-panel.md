---
title: Audits Panel
description: Detailed documentation on the Audits Panel in CDP Inspector, covering Lighthouse-style diagnostics, accessibility audits, scoring, and automated recommendations.
---

# Audits Panel

The **Audits Panel** in the CDP Inspector provides developers and testing agents with automated diagnostic feedback on the quality, layout compliance, and accessibility metrics of the target Avalonia application. Modeled after modern web inspection frameworks (such as Google Lighthouse), it translates complex visual structure and hierarchy guidelines into simple scores and prioritized recommendations. 

Using the Audits Panel, developers can inspect their visual layouts and element attributes to identify usability issues, low contrast ratios, or missing accessibility properties without having to perform manual audits.

---

## 1. Overview of the Audits Panel Workspace

The audit system is split across the following key files in the inspector application:
*   **View Layer**: `AuditsView.axaml` builds the layout, rendering score gauges, the diagnostics data grid, and the details panel.
*   **ViewModel**: `AuditsViewModel.cs` handles diagnostic execution state, scores binding, issue list updates, and DOM sync routing.
*   **Model**: `AuditIssueModel.cs` defines the data structure representing a single violating element, containing properties like category, severity, node ID, control type, and descriptions.

The flow of an audit run is visualized below:

```text
+-----------------------------+               CDP WebSocket Request              +-----------------------------+
|      CDP Inspector App      | ==============================================>  |     Target Avalonia App     |
|                             |             Audits.runDiagnostics              |                             |
|  - Process Issues DataGrid  | <==============================================  |  - Perform Visual Analyses  |
|  - Compute Score Gauges     |            Return Diagnostic Metrics             |  - Evaluate Contrast/Sizes  |
+-----------------------------+                                                  +-----------------------------+
```

---

## 2. Core Audit Categories and Rules

The diagnostics suite analyzes the target visual tree across three main categories:

### A. Accessibility Audits
Accessibility checking focuses on making the application usable for individuals utilizing screen readers, keyboard-only input, or other assistive technologies.
*   **Contrast Ratio Compliance**: Analyzes the foreground text color against the background bounds color. The target checks if the text meets the Web Content Accessibility Guidelines (WCAG) AAA contrast ratio recommendations (minimum 4.5:1 for regular text, and 3:1 for large headings). Contrast is calculated by converting the foreground and background RGB values into relative luminance ($L$):
    $$L = 0.2126 \times R_{srgb} + 0.7152 \times G_{srgb} + 0.0722 \times B_{srgb}$$
    The ratio is then computed as $(L_{light} + 0.05) / (L_{dark} + 0.05)$.
*   **Interactive Target Size (Tap Targets)**: Buttons, sliders, text inputs, and check-boxes must be large enough to be easily activated by touch or cursor. The audit verifies that interactive elements have a minimum bound size of `44x44` pixels (or `48x48` pixels for high-density layouts) to prevent accidental taps.
*   **Accessibility Peer Properties**: Evaluates whether visual controls expose correct descriptors to screen readers. It checks for the existence of:
    *   `AutomationProperties.Name` (to label controls)
    *   `AutomationProperties.HelpText` (to provide instruction)
    *   `AutomationProperties.AccessibilityView` (to include or hide items from screen-reader sweeps)
    *   `AutomationProperties.AutomationId` (for target testing identification)

### B. Best Practices Audits
Ensures that the Avalonia visual tree is structured efficiently and matches desktop UI development practices.
*   **Visual Tree Depth Limits**: Checks if control hierarchies are nested excessively. Deeply nested trees degrade rendering performance and slow layout recalculation passes. A visual tree depth exceeding 25 levels raises a performance warning.
*   **Virtualization Verification**: Inspects list controls (like `ListBox`, `DataGrid`, or `TreeView`) to verify that items are virtualized. Non-virtualized lists containerize all backing records simultaneously, causing memory leaks and UI stutter.
*   **Unused Style Bloat**: Highlights components loading static resources or vector assets that are never visible in the active viewport layout.

### C. Layout Audits
Detects common layout misalignments, overflows, or clipping errors.
*   **Layout Overflows**: Checks if child elements overflow the container bounds without clipping or scroll bars.
*   **Grid and Panel Misalignments**: Scans grids to find column/row declarations containing conflicting star (`*`), auto, or absolute size specifications that result in hidden text.
*   **Device Scaling Artifacts**: Detects if control dimensions lead to sub-pixel borders when rendered at higher display scales (e.g. 125% or 150%), which causes visual blur.

---

## 3. Running Audits and Score Gauges

To start an audit run:
1.  Click the **Run Diagnostics Audits** button (`btnRunAudits`) on the top toolbar of the panel.
2.  The inspector sends a command message to the target endpoint:
    ```json
    {
      "id": 106,
      "method": "Audits.runDiagnostics"
    }
    ```
3.  The target performs visual diagnostics and returns a JSON payload detailing the overall category scores and listing individual issues:
    ```json
    {
      "accessibilityScore": 92,
      "bestPracticesScore": 85,
      "layoutScore": 70,
      "issues": [
        {
          "category": "Accessibility",
          "severity": "warning",
          "nodeId": 184,
          "controlType": "Button",
          "message": "Interactive tap target size is too small (24x20px). Recommended minimum is 44x44px."
        }
      ]
    }
    ```

### Severity Levels
Issues are assigned severity levels that categorize their impact on the user experience:

| Severity Level | Color Code | Description | Impact on Score |
| :--- | :---: | :--- | :---: |
| **Critical** | `#ff3333` | Severely blocks users from accessing content or controls (e.g. contrast < 2.0). | High Deduction |
| **Warning** | `#ff9800` | Violates standards but does not fully block interaction (e.g. small touch size). | Medium Deduction |
| **Info / Advice** | `#8ab4f8` | Best practice recommendations that optimize code (e.g. deeply nested layout). | Minimal Deduction |

### Visual Score Gauges
The top row of the Audits panel displays three visual score rings corresponding to the three categories. The colors match Lighthouse scoring thresholds:

*   **Green Score (90 - 100)**: Pass. Element complies with all critical guidelines. Color: `#0ccc5a`.
*   **Orange Score (50 - 89)**: Warning. Issues are detected that need manual review. Color: `#ff9800`.
*   **Red Score (0 - 49)**: Fail. Critical violations found that impact accessibility or performance. Color: `#ff3333`.

The styling binding values (`A11yBrush`, `BestPracticesBrush`, and `LayoutBrush`) are computed in `AuditsViewModel.cs` using the following method:

```csharp
private string GetScoreColor(int score)
{
    if (score >= 90) return "#0ccc5a"; // green
    if (score >= 50) return "#ff9800"; // orange
    return "#ff3333";                  // red
}
```

---

## 4. Viewing Results and Recommendations

The results are listed in the diagnostic data grid (`lstIssues`). The columns present the category tag and recommendation summary:

*   **Category Tag**: Renders a color-coded tag matching the category type:
    *   `Accessibility` tags display with a yellow brush background (`#fdd663`).
    *   `Layout` tags display with a red brush background (`#f28b82`).
    *   `Best Practices` tags display with a blue brush background (`#8ab4f8`).
*   **Message**: Summarizes the violation (e.g. contrast ratio, clipping, or missing accessibility properties).

### Issue Detail Pane
Selecting an issue inside the list populates the **Issue Details** card on the right-hand side. This panel exposes:
*   **Category**: The parent diagnostic category.
*   **Violating Control Type**: The native Avalonia class name (e.g., `Button`, `TextBlock`, `TextBox`, or `Grid`).
*   **Diagnostic Message**: Detailed steps and values to reproduce and resolve the violation.
*   **Target DOM Node ID**: The integer identifier (`NodeId`) assigned to the element in the target's DOM tree.

---

## 5. Reveal Element in Elements Tree

Analyzing a recommendation in isolation can be difficult without visual context. The Audits Panel solves this by integrating with the visual tree browser.

Clicking **Reveal Element in Elements Panel** (`btnInspectIssue`) executes the following logic:
1.  The click command fires `InspectIssueCommand` in `AuditsViewModel`.
2.  The view model retrieves the `NodeId` from the selected `AuditIssueModel`.
3.  It calls the tree navigation delegate:
    ```csharp
    _selectNodeInDomTree(SelectedIssue.NodeId);
    ```
4.  This focuses the corresponding node within the `ElementsView.axaml` tree structure.
5.  Developers can then view style attributes, inspect bounds, edit properties, or trigger highlights on the simulation page to locate the component in the application layout.

---

## 6. Resolving Common Audit Violations

### Fixing Small Tap Targets
To fix target size warnings (e.g. `Interactive tap target size is too small`), increase the control height/width or pad the tap area using transparent bounds wrappers:
```xml
<!-- Avoid this: tiny button -->
<Button Name="btnSmall" Width="20" Height="20"/>

<!-- Prefer this: compliant hit test area -->
<Button Name="btnCompliant" Width="44" Height="44" Padding="12"/>
```

### Adding Automation Mappings
For screen reader discovery, map control headers and labels using accessibility peer properties:
```xml
<!-- Incomplete TextBox definition -->
<TextBox Name="txtAddress"/>

<!-- Compliant TextBox with associated automation descriptors -->
<TextBox Name="txtAddress" 
         AutomationProperties.Name="Billing Address Input Field"
         AutomationProperties.HelpText="Enter your primary billing address."
         AutomationProperties.AutomationId="txtBillingAddress"/>
```
Doing so allows the backend `AutomationPeer` evaluation to compile valid trees, raising your overall Accessibility score.
