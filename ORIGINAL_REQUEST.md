# Original User Request

## Initial Request — 2026-07-17T13:53:44+02:00

Fix and fully implement a state-of-the-art WYSIWYG editor for Chrome DevTools Protocol (CDP) in the Avalonia inspector app, supporting visual element selection, resize/drag operations, property updates, drag-and-drop toolbox integration, and mutation propagation. Refactor the WYSIWYG designer into a standalone, shippable, and reusable `.csproj`.

Working directory: /Users/wieslawsoltes/GitHub/CDP
Integrity mode: demo

## Requirements

### R1. Standalone WYSIWYG Project Refactoring
- [ ] Refactor the existing project `src/CDP.Inspector.Wysiwyg` to be completely standalone. It must compile and build independently without referencing `CdpInspectorApp` or `CDP.Inspector.Shared`.
- [ ] Move all reusable custom designer controls, overlay elements, and toolbox models/catalog structures into this library.
- [ ] The `CDP.Inspector.Shared` project should reference `CDP.Inspector.Wysiwyg`, not vice-versa.

### R2. Selection and Hit-Testing
- [ ] Fix element selection on the designer overlay canvas. Ensure pointer coordinates are correctly translated based on the aspect ratio and bounds of the preview image (`imgDesignerScreenshot`) relative to `DeviceWidth` and `DeviceHeight`.
- [ ] Call `DOM.getNodeForLocation` to identify the selected control. Display correct selection boxes, margins, padding, and accessibility/ID badges.

### R3. Drag and Resize Layout Positioning
- [ ] Implement drag operations on the selection overlay to relocate elements:
  - If the parent container is a Canvas, update `Canvas.Left` and `Canvas.Top`.
  - If the parent container is a Grid, StackPanel, or other layout, adjust the element's `Margin`.
- [ ] Implement handle resize dragging on the selection overlay to change the element's `Width` and `Height`.
- [ ] Ensure that changes during drag/resize are updated in real time visually, and propagate to the live app via CDP on release.

### R4. Drag and Drop from Toolbox
- [ ] Support drag-and-drop from the Toolbox catalog onto the designer overlay.
- [ ] Dragging a toolbox item and dropping it on the canvas should insert the default XAML fragment for that control at the dropped location within the hovered/selected container.

### R5. Mutation Propagation and File Sync
- [ ] Ensure all edits (size, position, margin, padding, toolbox additions, deletions) propagate via CDP to the target app's live visual tree.
- [ ] Ensure workspace files are notified/synchronized so that changes made on the designer canvas are correctly saved to local source files.

## Acceptance Criteria

### Compilation & Reusability
- [ ] `CDP.Inspector.Wysiwyg` compiles cleanly without referencing `CdpInspectorApp` or `CDP.Inspector.Shared`.
- [ ] The full solution builds successfully.

### WYSIWYG Interactions
- [ ] Clicking on the designer canvas successfully highlights and selects elements.
- [ ] Dragging an element or its resize handles updates the element's size/position in the target app.
- [ ] Dragging and dropping an item from the Toolbox inserts the new control in the target app's layout.
- [ ] Property changes in the properties panel propagate successfully to the target app.

### E2E Verification
- [ ] An E2E YAML test flow (e.g. `tests/CdpInspectorApp.E2e/simulation/wysiwyg_designer.flow.yaml`) is created or updated to automate verification of selecting, moving, and adding controls via the designer, and runs successfully with 0 failures using `cdp-cli run`.
