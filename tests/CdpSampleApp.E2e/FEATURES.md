# CdpSampleApp E2E Test Suite Features Coverage Matrix

This document tracks all features and user interaction scenarios in the target application `CdpSampleApp` and lists the corresponding E2E YAML test flow files.

| Feature Category | User Scenario | Test Flow File | Status |
|---|---|---|---|
| **Home Page** | Clicking standard buttons | `home/click.flow.yaml` | Active |
| | Sending outbound HTTP request | `home/send_http.flow.yaml` | Active |
| | Typing text in textbox | `home/input.flow.yaml` | Active |
| | Selecting checkbox option | `home/toggle.flow.yaml` | Active |
| | Dragging/interacting with slider | `home/slider.flow.yaml` | Active |
| | Toggling options via radio buttons | `home/radio_button.flow.yaml` | Active |
| **Scroll Page** | Vertical scroll in container | `scroll/vertical_scroll.flow.yaml` | Active |
| **Navigation** | Switching tabs via TabControl | `navigation/tab_switch.flow.yaml` | Active |
| | Navigation via direct URL and going back | `navigation/url_navigation.flow.yaml` | Active |
| **Gestures Page** | Double click, long press, clear, drag & drop | `home/gestures.flow.yaml` | Active |
| **Asserts & Keys Page** | Key presses, visibility assertions, toggles | `home/asserts_keys.flow.yaml` | Active |
