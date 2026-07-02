export const apiPackageGroups = [
  {
    text: "Core Protocol",
    packages: [
      {
        packageId: "Chrome.DevTools.Protocol",
        slug: "chrome-devtools-protocol",
        project: "src/Chrome.DevTools.Protocol/Chrome.DevTools.Protocol.csproj",
        guideTitle: "Architecture",
        guideLink: "/articles/architecture"
      },
      {
        packageId: "Chrome.DevTools.Avalonia",
        slug: "chrome-devtools-avalonia",
        project: "src/Avalonia.Diagnostics.Cdp/Avalonia.Diagnostics.Cdp.csproj",
        guideTitle: "Getting Started",
        guideLink: "/articles/getting-started"
      }
    ]
  },
  {
    text: "Inspector And Diagnostics",
    packages: [
      {
        packageId: "Chrome.DevTools.Inspector.Shared",
        slug: "chrome-devtools-inspector-shared",
        project: "src/CDP.Inspector.Shared/CDP.Inspector.Shared.csproj",
        guideTitle: "Inspector App",
        guideLink: "/articles/inspector-app"
      },
      {
        packageId: "Chrome.DevTools.DiagnosticTools",
        slug: "chrome-devtools-diagnostictools",
        project: "src/CDP.DiagnosticTools/CDP.DiagnosticTools.csproj",
        guideTitle: "In-Process Inspector",
        guideLink: "/articles/in-process-inspector"
      }
    ]
  },
  {
    text: "OS Automation",
    packages: [
      {
        packageId: "Chrome.DevTools.Automation.OS",
        slug: "chrome-devtools-automation-os",
        project: "src/CDP.Automation.OS/CDP.Automation.OS.csproj",
        guideTitle: "OS Automation",
        guideLink: "/articles/os-automation"
      }
    ]
  },
  {
    text: "Editor Controls",
    packages: [
      {
        packageId: "Chrome.DevTools.Editor.Minimap",
        slug: "chrome-devtools-editor-minimap",
        project: "src/CDP.Editor.Minimap/CDP.Editor.Minimap.csproj",
        guideTitle: "Minimap Editor",
        guideLink: "/articles/minimap-editor"
      },
      {
        packageId: "Chrome.DevTools.Editor.Nodes",
        slug: "chrome-devtools-editor-nodes",
        project: "src/CDP.Editor.Nodes/CDP.Editor.Nodes.csproj",
        guideTitle: "Node Editor",
        guideLink: "/articles/node-editor"
      },
      {
        packageId: "Chrome.DevTools.Editor.Nodes.Msagl",
        slug: "chrome-devtools-editor-nodes-msagl",
        project: "src/CDP.Editor.Nodes.Msagl/CDP.Editor.Nodes.Msagl.csproj",
        guideTitle: "Node Editor",
        guideLink: "/articles/node-editor"
      },
      {
        packageId: "Chrome.DevTools.Editor.Splits",
        slug: "chrome-devtools-editor-splits",
        project: "src/CDP.Editor.Splits/CDP.Editor.Splits.csproj",
        guideTitle: "Splits Layout",
        guideLink: "/articles/splits-layout"
      }
    ]
  }
];

export const apiPackages = apiPackageGroups.flatMap((group) =>
  group.packages.map((pkg) => ({
    ...pkg,
    group: group.text
  }))
);
