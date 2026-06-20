# Test Studio User Stories

This document lists the user stories for the Test Studio, detailing how developer/tester roles interact with the new interface to author, debug, and run visual test cases.

---

## Story 1: Switching Between Standard Recorder and Test Studio
As a test developer,
I want to toggle between the Standard Puppeteer Recorder and the Test Studio,
So that I can choose between exporting a raw Playwright/Puppeteer script or building a high-level YAML-based flow.
- **Acceptance Criteria**:
  - A visual toggle selector is available at the top of the **Recorder** tab.
  - Toggling to "Test Studio" loads the Test Studio view, replacing the standard recorder panels.
  - Toggling back restores the Standard Recorder exactly as it was.
  - Session state (recording active, current target connection) is preserved across toggles.

---

## Story 2: Interactive Test Step Authoring (Click-to-Add)
As a developer testing my application,
I want to select elements on the interactive Screencast or within the Elements tree and instantly generate test steps,
So that I don't have to manually write selectors and commands.
- **Acceptance Criteria**:
  - When a control node is selected (via inspect mode on the screencast, or in the Elements tree), its computed selector is displayed in the Test Studio actions panel.
  - The actions panel shows quick-add buttons: `Tap`, `Input Text`, `Assert Visible`, and `Assert Not Visible`.
  - Clicking `Tap` appends a `- tapOn: <selector>` step to the active flow.
  - Clicking `Input Text` prompts for text (via an adjacent text input field) and appends `- inputText: { selector: <selector>, text: <value> }`.
  - Clicking `Assert Visible` appends `- assertVisible: <selector>`.

---

## Story 3: Reordering and Modifying Test Steps
As an automation engineer,
I want to rearrange, edit, and delete test steps in the interactive list,
So that I can customize and correct the flow without editing raw YAML.
- **Acceptance Criteria**:
  - The steps list displays each step with its action type and details clearly.
  - Each step item has "Move Up" and "Move Down" buttons to change its execution order.
  - Each step item has a "Delete" button to remove it.
  - Clicking "Edit" on a step opens an inline textbox or popover to modify its selector/value directly.
  - Any modifications in the interactive list immediately update the synchronized YAML text pane.

---

## Story 4: Live YAML Synchronization & Manual Editing
As a power user,
I want to copy-paste or write YAML flows directly in a text pane,
So that I can quickly load external test scripts or make manual batch edits.
- **Acceptance Criteria**:
  - A tab or splitter pane in the Test Studio displays the raw YAML script text.
  - Editing the text and clicking "Apply YAML" parses the text.
  - If parsing is successful, the interactive step list is rebuilt to match the YAML.
  - If parsing fails, a red warning message displays the syntax error details, and the active step list is preserved.

---

## Story 5: Step-by-Step Debugging & Run Controls
As a developer debugging a failing test,
I want to pause, step over, and visually inspect test execution,
So that I can identify exactly which step fails and why.
- **Acceptance Criteria**:
  - Execution controls are available: Play/Resume, Pause, Step Over, and Stop.
  - During execution, the currently running step is highlighted with a blue background/spinner.
  - Completed steps are marked with a green checkmark.
  - Failed steps are marked with a red cross, and the execution pauses, displaying the error log.
  - The developer can right-click or click a "Run from here" button on a specific step to execute the test flow starting at that step.
  - Detailed logs (timestamps, execution warnings, successes) are shown in a bottom log console.
