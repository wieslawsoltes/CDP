import { defineConfig } from "vitepress";

import { apiPackageGroups } from "./api-packages.mjs";

const guideSidebarItems = [
  {
    text: "Start Here",
    collapsed: false,
    items: [
      { text: "Overview", link: "/" },
      { text: "Getting Started", link: "/articles/getting-started" },
      { text: "Architecture", link: "/articles/architecture" },
      { text: "Package Guide", link: "/articles/packages" }
    ]
  },
  {
    text: "Supported UI Frameworks",
    collapsed: false,
    items: [
      { text: "Avalonia UI", link: "/articles/getting-started" },
      { text: "WPF (Windows Presentation Foundation)", link: "/articles/wpf-support" },
      { text: "WinUI 3", link: "/articles/winui-support" },
      { text: "Uno Platform", link: "/articles/uno-support" }
    ]
  },
  {
    text: "CDP Server Integration",
    collapsed: false,
    items: [
      { text: "DOM Domain", link: "/articles/dom-domain" },
      { text: "CSS Domain", link: "/articles/css-domain" },
      { text: "Input Domain", link: "/articles/input-domain" },
      { text: "Page Domain", link: "/articles/page-domain" },
      { text: "Overlay Domain", link: "/articles/overlay-domain" },
      { text: "Runtime Domain", link: "/articles/runtime-domain" },
      { text: "Target Domain", link: "/articles/target-domain" },
      { text: "Network Domain", link: "/articles/network-domain" },
      { text: "Memory Domain", link: "/articles/memory-domain" },
      { text: "Accessibility Domain", link: "/articles/accessibility-domain" },
      { text: "Performance Domain", link: "/articles/performance-domain" },
      { text: "Application Domain", link: "/articles/application-domain" },
      { text: "Sources Domain", link: "/articles/sources-domain" },
      { text: "Emulation Domain", link: "/articles/emulation-domain" },
      { text: "Window Chrome Domain", link: "/articles/window-chrome-domain" },
      { text: "Debugger Domain", link: "/articles/debugger-domain" },
      { text: "DOM Debugger Domain", link: "/articles/dom-debugger-domain" },
      { text: "Log Domain", link: "/articles/log-domain" },
      { text: "Selector Engine", link: "/articles/selector-engine" },
      { text: "WebDriver BiDi Support", link: "/articles/webdriver-bidi" },
      { text: "MVVM DevTools Domain", link: "/articles/mvvm-developer-tools" },
      { text: "Pre-flight Wait For Debugger", link: "/articles/wait-for-debugger" }
    ]
  },
  {
    text: "Inspector Application",
    collapsed: false,
    items: [
      { text: "Inspector App Overview", link: "/articles/inspector-app" },
      { text: "In-Process Inspector", link: "/articles/in-process-inspector" },
      { text: "Elements Panel", link: "/articles/elements-panel" },
      { text: "Console Panel", link: "/articles/console-panel" },
      { text: "Sources Panel", link: "/articles/sources-panel" },
      { text: "Network Panel", link: "/articles/network-panel" },
      { text: "Performance Panel", link: "/articles/performance-panel" },
      { text: "Memory Panel", link: "/articles/memory-panel" },
      { text: "Application Panel", link: "/articles/application-panel" },
      { text: "Simulation Panel", link: "/articles/simulation-panel" },
      { text: "Audits Panel", link: "/articles/audits-panel" },
      { text: "Events Panel", link: "/articles/events-panel" },
      { text: "Browser Inspector (WASM)", link: "/articles/browser-inspector" },
      { text: "MVVM Diagnostics Panel", link: "/articles/mvvm-panel" }
    ]
  },
  {
    text: "Recorder and Test Studio",
    collapsed: false,
    items: [
      { text: "Recorder Overview", link: "/articles/recorder-overview" },
      { text: "Recording User Actions", link: "/articles/recording-user-actions" },
      { text: "Test Studio", link: "/articles/test-studio" },
      { text: "YAML Test Format", link: "/articles/yaml-test-format" },
      { text: "Test Studio Scripting", link: "/articles/test-studio-scripting" },
      { text: "CLI Automation Tool", link: "/articles/cli-tool" },
      { text: "Code Generation", link: "/articles/code-generation" },
      { text: "Headless Test Adapter", link: "/articles/headless-test-adapter" },
      { text: "Test Reports", link: "/articles/test-reports" },
      { text: "Video Recording", link: "/articles/video-recording" }
    ]
  },
  {
    text: "OS Automation",
    collapsed: false,
    items: [
      { text: "OS Automation Overview", link: "/articles/os-automation" },
      { text: "macOS Automation", link: "/articles/macos-automation" },
      { text: "Windows Automation", link: "/articles/windows-automation" },
      { text: "Permissions and Setup", link: "/articles/permissions-setup" },
      { text: "OS Automation CDP Proxy", link: "/articles/os-automation-cdp-proxy" }
    ]
  },
  {
    text: "Editor Controls",
    collapsed: false,
    items: [
      { text: "Minimap Editor", link: "/articles/minimap-editor" },
      { text: "Node Editor", link: "/articles/node-editor" },
      { text: "Splits Layout", link: "/articles/splits-layout" }
    ]
  },
  {
    text: "AI Agents and Automation",
    collapsed: false,
    items: [
      { text: "AI Agent Integration", link: "/articles/ai-agent-integration" },
      { text: "Chrome DevTools Connection", link: "/articles/chrome-devtools-connection" },
      { text: "Google Model Context Protocol (MCP)", link: "/articles/mcp" },
      { text: "Native C# MCP Server", link: "/articles/native-mcp-server" },
      { text: "Slim Mode & WebMCP Tools", link: "/articles/webmcp-and-slimmode" },
      { text: "Playwright Integration", link: "/articles/playwright" },
      { text: "Puppeteer Integration", link: "/articles/puppeteer" },
      { text: "Selenium Integration", link: "/articles/selenium" },
      { text: "Appium Integration", link: "/articles/appium" },
      { text: "YAML Conversion", link: "/articles/yaml-conversion" },
      { text: "Headless CDP Testing", link: "/articles/headless-cdp-testing" },
      { text: "Headless Test Helpers", link: "/articles/headless" },
      { text: "Multi-Framework CDP Integration", link: "/articles/multi-framework-cdp" },
      { text: "Self-Inspection", link: "/articles/self-inspection" }
    ]
  },
  {
    text: "Operations",
    collapsed: false,
    items: [
      { text: "Samples and Tooling", link: "/articles/samples-tooling" },
      { text: "Build, Test, and Release", link: "/articles/build-test-release" },
      { text: "Troubleshooting", link: "/articles/troubleshooting" }
    ]
  }
];

const apiSidebarItems = [
  { text: "Overview", link: "/api/" }
];

export default defineConfig({
  title: "Avalonia CDP Inspector",
  description:
    "Chrome DevTools Protocol support for Avalonia UI — live inspection, automated testing, recording, replay, and AI agent integration for .NET desktop applications.",
  base: "/CDP/",
  cleanUrls: true,
  lastUpdated: true,
  head: [
    ["meta", { name: "theme-color", content: "#7c3aed" }],
    ["meta", { property: "og:type", content: "website" }],
    ["meta", { property: "og:title", content: "Avalonia CDP Inspector" }],
    [
      "meta",
      {
        property: "og:description",
        content:
          "Chrome DevTools Protocol support for Avalonia UI — live inspection, automated testing, recording, replay, and AI agent integration for .NET desktop applications."
      }
    ]
  ],
  markdown: {
    lineNumbers: true
  },
  vite: {
    build: {
      chunkSizeWarningLimit: 4096
    }
  },
  themeConfig: {
    logo: "/assets/cdp-mark.svg",
    nav: [
      { text: "Guide", link: "/articles/getting-started" },
      { text: "Packages", link: "/articles/packages" },
      { text: "API", link: "/api/" },
      { text: "GitHub", link: "https://github.com/wieslawsoltes/CDP" }
    ],
    sidebar: {
      "/articles/": guideSidebarItems,
      "/api/": [
        {
          text: "API Reference",
          items: apiSidebarItems
        },
        ...apiPackageGroups.map((group) => ({
          text: group.text,
          items: group.packages.map((pkg) => ({
            text: pkg.packageId,
            link: `/api/${pkg.slug}/`
          }))
        }))
      ],
      "/": [
        ...guideSidebarItems,
        {
          text: "API Reference",
          items: apiSidebarItems
        }
      ]
    },
    outline: {
      level: [2, 3],
      label: "On this page"
    },
    editLink: {
      pattern: "https://github.com/wieslawsoltes/CDP/edit/main/docs/:path",
      text: "Edit this page on GitHub"
    },
    docFooter: {
      prev: "Previous page",
      next: "Next page"
    },
    search: {
      provider: "local"
    },
    socialLinks: [
      { icon: "github", link: "https://github.com/wieslawsoltes/CDP" }
    ],
    footer: {
      message: "MIT Licensed",
      copyright: "Copyright Wiesław Šoltés"
    }
  }
});
