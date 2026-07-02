---
title: Build, Test, and Release
---

# Build, Test, and Release

This guide covers the development workflow for building, testing, and publishing the CDP Avalonia project and its NuGet packages.

## Prerequisites

- **.NET 10 SDK** or later
- **Node.js 18+** (for documentation site only)
- **Git** for version control

## Building

### Build All Projects

```bash
dotnet build
```

### Build Specific Project

```bash
dotnet build src/Avalonia.Diagnostics.Cdp/Avalonia.Diagnostics.Cdp.csproj
dotnet build samples/CdpInspectorApp/CdpInspectorApp.csproj
```

### Build Configuration

The solution uses central package management via `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Avalonia" Version="11.*" />
    <!-- ... -->
  </ItemGroup>
</Project>
```

## Testing

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Project

```bash
dotnet test tests/Avalonia.Diagnostics.Cdp.Tests/
```

### View Layout Tests

Layout tests verify that XAML views parse correctly and all resource keys resolve:

```bash
dotnet test tests/Avalonia.Diagnostics.Cdp.Tests/ViewsLayoutTests.cs
```

### Test Categories

| Category | Coverage |
|----------|----------|
| Selector Tests | CSS selector parsing, attribute matching, presence selectors |
| DOM Tests | Document structure, querySelector, getBoxModel |
| Runtime Tests | C# expression evaluation, document facade |
| Protocol Tests | JSON-RPC message shapes, response format |
| Layout Tests | XAML parsing, resource resolution, view construction |
| Integration Tests | End-to-end CDP client/server scenarios |

### Running Tests in CI

```yaml
# GitHub Actions
- name: Run tests
  run: dotnet test --configuration Release --logger trx --results-directory TestResults

- name: Publish test results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: test-results
    path: TestResults/
```

## Publishing NuGet Packages

### Pack All Packages

```bash
dotnet pack --configuration Release
```

### Pack Specific Package

```bash
dotnet pack src/Avalonia.Diagnostics.Cdp/ -c Release
dotnet pack src/Chrome.DevTools.Protocol/ -c Release
```

### Package Output

NuGet packages are output to `bin/Release/`:
- `Chrome.DevTools.Avalonia.{version}.nupkg`
- `Chrome.DevTools.Protocol.{version}.nupkg`
- `Chrome.DevTools.Automation.OS.{version}.nupkg`
- `Chrome.DevTools.Inspector.Shared.{version}.nupkg`
- `Chrome.DevTools.DiagnosticTools.{version}.nupkg`
- `Chrome.DevTools.Editor.Minimap.{version}.nupkg`
- `Chrome.DevTools.Editor.Nodes.{version}.nupkg`
- `Chrome.DevTools.Editor.Nodes.Msagl.{version}.nupkg`
- `Chrome.DevTools.Editor.Splits.{version}.nupkg`

### Push to NuGet

```bash
dotnet nuget push bin/Release/*.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY
```

## Publishing the Inspector Global Tool

The Inspector is published as a .NET global tool:

```bash
dotnet pack src/Chrome.DevTools.Inspector/ -c Release
dotnet nuget push bin/Release/Chrome.DevTools.Inspector.*.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY
```

Users install with:
```bash
dotnet tool install -g Chrome.DevTools.Inspector
```

## Documentation Site

### Development Server

```bash
cd docs
npm install
npm run docs:dev
```

### Build Documentation

```bash
npm run docs:build
```

### Preview Built Site

```bash
npm run docs:preview
```

## GitHub Actions Workflow

The repository includes a CI/CD workflow at `.github/workflows/docs.yml`:

```yaml
name: Documentation

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build and test
        run: |
          dotnet build
          dotnet test --configuration Release

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: npm
          cache-dependency-path: docs/package-lock.json

      - name: Build documentation
        working-directory: docs
        run: |
          npm ci
          npm run docs:build

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: docs/.vitepress/dist

  deploy:
    needs: build
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

## Release Checklist

1. Update version numbers in project files
2. Run full test suite: `dotnet test`
3. Build documentation: `npm run docs:build`
4. Pack NuGet packages: `dotnet pack -c Release`
5. Test packages locally
6. Push to NuGet
7. Tag release in Git
8. Deploy documentation

## Next Steps

- [Samples and Tooling](/articles/samples-tooling) — Project structure and samples
- [Troubleshooting](/articles/troubleshooting) — Common issues and solutions
- [Architecture](/articles/architecture) — System architecture overview
