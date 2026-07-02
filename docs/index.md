---
layout: home

hero:
  name: Avalonia CDP Inspector
  text: Chrome DevTools Protocol for .NET desktop applications
  tagline: Live visual tree inspection, automated UI testing, interaction recording and replay, AI agent integration, OS-level automation, and comprehensive diagnostics for Avalonia applications.
  image:
    src: /assets/cdp-mark.svg
    alt: Avalonia CDP Inspector
  actions:
    - theme: brand
      text: Get Started
      link: /articles/getting-started
    - theme: alt
      text: Package Guide
      link: /articles/packages
    - theme: alt
      text: API Reference
      link: /api/
    - theme: alt
      text: GitHub
      link: https://github.com/wieslawsoltes/CDP

features:
  - title: Live Visual Tree Inspection
    details: "Walk the Avalonia visual tree as a Chrome DevTools-compatible DOM document. Hover elements to highlight them in real-time with padding, margin, and content overlays."
  - title: Interaction Recording and Replay
    details: "Record user interactions — clicks, text input, scrolls — and replay them with full Test Studio support, YAML-based test definitions, and code generation for Puppeteer, Playwright, Selenium, Appium, and Avalonia Headless."
  - title: AI Agent Integration
    details: "Provide AI coding agents with selector-driven DOM queries, screenshot verification, C# runtime evaluation, and input simulation through standard CDP WebSocket connections."
  - title: In-Process and Standalone Inspector
    details: "Embed the inspector directly in your app with a single line of code, or run the standalone inspector application as a .NET global tool, from source, or in the browser via WebAssembly."
  - title: OS-Level Automation
    details: "Automate any desktop application using native accessibility APIs on macOS, Windows, and Linux — without requiring a CDP server in the target process."
  - title: Comprehensive Test Reports
    details: "Generate HTML and PDF test reports with step-by-step screenshots, execution timing, pass/fail status, and optional video recording of test playback."
  - title: Editor Controls Library
    details: "Standalone Avalonia controls including a minimap-enabled text editor, graph node editor with MSAGL layout, and dynamic splits layout container — no CDP dependency required."
  - title: Full CDP Domain Coverage
    details: "DOM, CSS, Input, Page, Overlay, Runtime, Target, Network, Sources, Application, Memory, Recorder, Accessibility, Performance, Audits, and more — all mapped to Avalonia concepts."
---

## Documentation

<div class="home-docs-grid">
  <section class="home-docs-group home-docs-group-primary">
    <p class="home-docs-eyebrow">Start here</p>
    <h3>Plan your integration</h3>
    <p>Choose the right package, understand the architecture, and explore the CDP API surface.</p>
    <ul>
      <li><a href="articles/getting-started">Getting Started</a></li>
      <li><a href="articles/architecture">Architecture</a></li>
      <li><a href="articles/packages">Package Guide</a></li>
      <li><a href="api/">API Reference</a></li>
    </ul>
  </section>
  <section class="home-docs-group">
    <p class="home-docs-eyebrow">CDP Domains</p>
    <h3>Protocol domain reference</h3>
    <p>Detailed documentation for each CDP domain: DOM inspection, styling, input, screenshots, runtime evaluation, and more.</p>
    <ul>
      <li><a href="articles/dom-domain">DOM Domain</a></li>
      <li><a href="articles/css-domain">CSS Domain</a></li>
      <li><a href="articles/input-domain">Input Domain</a></li>
      <li><a href="articles/runtime-domain">Runtime Domain</a></li>
      <li><a href="articles/page-domain">Page Domain</a></li>
      <li><a href="articles/selector-engine">Selector Engine</a></li>
      <li><a href="articles/network-domain">Network Domain</a></li>
      <li><a href="articles/memory-domain">Memory Domain</a></li>
    </ul>
  </section>
  <section class="home-docs-group">
    <p class="home-docs-eyebrow">Inspector</p>
    <h3>Inspector application</h3>
    <p>Chrome DevTools-style panels for visual inspection, console, network monitoring, performance profiling, and more.</p>
    <ul>
      <li><a href="articles/inspector-app">Inspector Overview</a></li>
      <li><a href="articles/in-process-inspector">In-Process Inspector</a></li>
      <li><a href="articles/elements-panel">Elements Panel</a></li>
      <li><a href="articles/console-panel">Console Panel</a></li>
      <li><a href="articles/simulation-panel">Simulation Panel</a></li>
      <li><a href="articles/browser-inspector">Browser Inspector (WASM)</a></li>
    </ul>
  </section>
  <section class="home-docs-group">
    <p class="home-docs-eyebrow">Recording &amp; Testing</p>
    <h3>Recorder and Test Studio</h3>
    <p>Record user interactions, build test suites with YAML, generate automation code, and run headless CI/CD tests.</p>
    <ul>
      <li><a href="articles/recorder-overview">Recorder Overview</a></li>
      <li><a href="articles/recording-user-actions">Recording User Actions</a></li>
      <li><a href="articles/test-studio">Test Studio</a></li>
      <li><a href="articles/yaml-test-format">YAML Test Format</a></li>
      <li><a href="articles/code-generation">Code Generation</a></li>
      <li><a href="articles/headless-test-adapter">Headless Test Adapter</a></li>
      <li><a href="articles/test-reports">Test Reports</a></li>
      <li><a href="articles/video-recording">Video Recording</a></li>
    </ul>
  </section>
  <section class="home-docs-group">
    <p class="home-docs-eyebrow">Automation</p>
    <h3>OS and AI automation</h3>
    <p>Drive any desktop application through native accessibility APIs or connect AI agents via standard CDP protocols.</p>
    <ul>
      <li><a href="articles/os-automation">OS Automation</a></li>
      <li><a href="articles/ai-agent-integration">AI Agent Integration</a></li>
      <li><a href="articles/chrome-devtools-connection">Chrome DevTools Connection</a></li>
      <li><a href="articles/self-inspection">Self-Inspection</a></li>
    </ul>
  </section>
  <section class="home-docs-group">
    <p class="home-docs-eyebrow">Operations</p>
    <h3>Build, test, and ship</h3>
    <p>Use sample tooling, validate release workflows, and diagnose common integration issues.</p>
    <ul>
      <li><a href="articles/samples-tooling">Samples and Tooling</a></li>
      <li><a href="articles/build-test-release">Build, Test, and Release</a></li>
      <li><a href="articles/troubleshooting">Troubleshooting</a></li>
    </ul>
  </section>
</div>
