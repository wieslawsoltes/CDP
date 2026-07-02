import fs from "node:fs/promises";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { apiPackageGroups, apiPackages } from "../.vitepress/api-packages.mjs";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, "../..");
const docsRoot = path.join(repoRoot, "docs");
const apiRoot = path.join(docsRoot, "api");
const directoryBuildPropsPath = path.join(repoRoot, "Directory.Build.props");

function run(command, args, cwd = repoRoot) {
  const result = spawnSync(command, args, {
    cwd,
    stdio: "inherit"
  });

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

function runStatus(command, args, cwd = repoRoot) {
  const result = spawnSync(command, args, {
    cwd,
    stdio: "inherit"
  });

  return result.status ?? 1;
}

function warnSourceIndexFallback(pkg, reason) {
  console.warn(
    `[docs:api] Falling back to source-index API docs for ${pkg.packageId} because ${reason}.`
  );
}

function getTagValue(xml, tagName) {
  const match = xml.match(new RegExp(`<${tagName}>([\\s\\S]*?)</${tagName}>`, "i"));
  return match?.[1].trim();
}

function toPosix(value) {
  return value.split(path.sep).join("/");
}

function getProjectDirectory(projectPath) {
  return path.dirname(projectPath);
}

async function readProjectMetadata(pkg) {
  const fullProjectPath = path.join(repoRoot, pkg.project);
  const xml = await fs.readFile(fullProjectPath, "utf8");

  return {
    ...pkg,
    referenceMode: pkg.referenceMode ?? "generated",
    description: getTagValue(xml, "Description") ?? `${pkg.packageId} managed API surface.`,
    assemblyName: getTagValue(xml, "AssemblyName") ?? path.basename(fullProjectPath, ".csproj"),
    projectDirectory: getProjectDirectory(pkg.project)
  };
}

async function getTargetFramework() {
  const xml = await fs.readFile(directoryBuildPropsPath, "utf8");
  return getTagValue(xml, "TargetFramework") ?? "net10.0";
}

async function normalizeMarkdown(outputDirectory) {
  const entries = await fs.readdir(outputDirectory, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = path.join(outputDirectory, entry.name);

    if (entry.isDirectory()) {
      await normalizeMarkdown(fullPath);
      continue;
    }

    if (!entry.isFile() || !entry.name.endsWith(".md")) {
      continue;
    }

    let content = await fs.readFile(fullPath, "utf8");
    content = content.replace(/\.md\.md/g, ".md");
    content = content.replace(/!:\b([A-Za-z_][A-Za-z0-9_\.]*)/g, "`$1`");
    await fs.writeFile(fullPath, content);
  }
}

async function getNamespaceLinks(outputDirectory) {
  const entries = await fs.readdir(outputDirectory, { withFileTypes: true });

  return entries
    .filter((entry) => entry.isFile() && entry.name.endsWith("Namespace.md"))
    .map((entry) => ({
      name: entry.name.replace(/Namespace\.md$/, ""),
      link: `./${entry.name}`
    }))
    .sort((left, right) => left.name.localeCompare(right.name));
}

async function getSourceFiles(directory) {
  const entries = await fs.readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = path.join(directory, entry.name);

    if (entry.isDirectory()) {
      if (entry.name === "bin" || entry.name === "obj") {
        continue;
      }

      files.push(...(await getSourceFiles(fullPath)));
      continue;
    }

    if (entry.isFile() && entry.name.endsWith(".cs")) {
      files.push(fullPath);
    }
  }

  return files;
}

async function collectPublicTypes(pkg) {
  const sourceDirectory = path.join(repoRoot, pkg.projectDirectory);
  const sourceFiles = await getSourceFiles(sourceDirectory);
  const types = new Map();

  for (const sourceFile of sourceFiles) {
    const content = await fs.readFile(sourceFile, "utf8");
    const namespaceMatch = content.match(/^\s*namespace\s+([A-Za-z0-9_.]+)\s*[;{]/m);

    if (!namespaceMatch) {
      continue;
    }

    const namespaceName = namespaceMatch[1];
    const sourcePath = toPosix(path.relative(repoRoot, sourceFile));
    const typeRegex =
      /^\s*public\s+(?:(?:unsafe|readonly|sealed|static|partial|abstract)\s+)*(class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)/gm;
    const delegateRegex =
      /^\s*public\s+(?:(?:unsafe|readonly|sealed|static|partial|abstract)\s+)*delegate\s+.+?\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/gm;

    for (const match of content.matchAll(typeRegex)) {
      const key = `${namespaceName}:${match[1]}:${match[2]}`;
      const existing =
        types.get(key) ??
        {
          namespaceName,
          kind: match[1],
          name: match[2],
          sourcePaths: new Set()
        };

      existing.sourcePaths.add(sourcePath);
      types.set(key, existing);
    }

    for (const match of content.matchAll(delegateRegex)) {
      const key = `${namespaceName}:delegate:${match[1]}`;
      const existing =
        types.get(key) ??
        {
          namespaceName,
          kind: "delegate",
          name: match[1],
          sourcePaths: new Set()
        };

      existing.sourcePaths.add(sourcePath);
      types.set(key, existing);
    }
  }

  return Array.from(types.values())
    .map((type) => ({
      ...type,
      sourcePaths: Array.from(type.sourcePaths).sort((left, right) => left.localeCompare(right))
    }))
    .sort((left, right) => {
      const namespaceCompare = left.namespaceName.localeCompare(right.namespaceName);

      if (namespaceCompare !== 0) {
        return namespaceCompare;
      }

      return left.name.localeCompare(right.name);
    });
}

async function writePackageIndex(pkg, outputDirectory) {
  const namespaceLinks = await getNamespaceLinks(outputDirectory);
  const lines = [
    "---",
    `title: ${pkg.packageId} API`,
    "---",
    "",
    `# ${pkg.packageId} API`,
    "",
    `Generated API reference for \`${pkg.packageId}\`.`,
    "",
    pkg.description,
    "",
    "## Package",
    "",
    `- Package ID: \`${pkg.packageId}\``,
    `- Source project: \`${pkg.project}\``,
    `- Related guide: [${pkg.guideTitle}](${pkg.guideLink})`,
    "",
    "## Entry Points",
    "",
    `- [Assembly overview](./${pkg.assemblyName}.md)`,
  ];

  if (namespaceLinks.length > 0) {
    lines.push("", "## Namespaces", "");

    for (const namespaceLink of namespaceLinks) {
      lines.push(`- [${namespaceLink.name}](${namespaceLink.link})`);
    }
  }

  lines.push(
    "",
    "## Notes",
    "",
    "- This section is generated from the package's public XML documentation comments.",
    "- Use the related guide for task-oriented integration and architecture walkthroughs."
  );

  await fs.writeFile(path.join(outputDirectory, "index.md"), `${lines.join("\n")}\n`);
}

async function writeSourceIndexPackage(pkg, outputDirectory, options = {}) {
  const notes =
    options.notes ?? [
      "- This package is indexed directly from the public source files.",
      "- Detailed member pages are unavailable because the Markdown reflection generator could not load this assembly in the current environment.",
      "- The source index keeps the package discoverable from the docs site and links each public type back to the repository."
    ];
  const publicTypes = await collectPublicTypes(pkg);
  const groupedTypes = new Map();

  for (const publicType of publicTypes) {
    const bucket = groupedTypes.get(publicType.namespaceName) ?? [];
    bucket.push(publicType);
    groupedTypes.set(publicType.namespaceName, bucket);
  }

  const lines = [
    "---",
    `title: ${pkg.packageId} API`,
    "---",
    "",
    `# ${pkg.packageId} API`,
    "",
    `Source-indexed API reference for \`${pkg.packageId}\`.`,
    "",
    pkg.description,
    "",
    "## Package",
    "",
    `- Package ID: \`${pkg.packageId}\``,
    `- Source project: \`${pkg.project}\``,
    `- Related guide: [${pkg.guideTitle}](${pkg.guideLink})`,
    "",
    "## Notes",
    ""
  ];

  lines.push(...notes, "");

  for (const namespaceName of Array.from(groupedTypes.keys()).sort((left, right) => left.localeCompare(right))) {
    lines.push(`## ${namespaceName}`, "", "| Type | Kind | Source |", "| --- | --- | --- |");

    for (const publicType of groupedTypes.get(namespaceName)) {
      const sourceLinks = publicType.sourcePaths
        .map((sourcePath) => {
          const sourceUrl = `https://github.com/wieslawsoltes/CDP/blob/main/${sourcePath}`;
          return `[\`${sourcePath}\`](${sourceUrl})`;
        })
        .join("<br>");

      lines.push(`| \`${publicType.name}\` | ${publicType.kind} | ${sourceLinks} |`);
    }

    lines.push("");
  }

  await fs.writeFile(path.join(outputDirectory, "index.md"), `${lines.join("\n")}\n`);
}

async function writeApiIndex(projects) {
  const lines = [
    "---",
    "title: API Reference",
    "---",
    "",
    "# API Reference",
    "",
    "The CDP project ships a modular managed surface area across protocol, inspection, OS automation, and editor control packages. This section keeps the article-led docs intact and adds generated reference pages for the public managed APIs that ship from `src/`.",
    "",
    "Use the guide articles for workflows and architecture. Use this reference when you need exact type and member contracts.",
    "",
    "## Coverage",
    "",
    `- Managed packages covered: ${projects.length}`,
    "- Source links in generated pages point back to the repository paths on GitHub.",
    "",
    "## Package Groups",
    ""
  ];

  for (const group of apiPackageGroups) {
    lines.push(`### ${group.text}`, "", "| Package | Description | Related guide |", "| --- | --- | --- |");

    for (const pkg of projects.filter((project) => project.group === group.text)) {
      lines.push(
        `| [\`${pkg.packageId}\`](/api/${pkg.slug}/) | ${pkg.description} | [${pkg.guideTitle}](${pkg.guideLink}) |`
      );
    }

    lines.push("");
  }

  lines.push(
    "## Reference Notes",
    "",
    "- Some packages may temporarily fall back to source-indexed reference pages when the reflection generator cannot load the built assembly in the current environment."
  );

  await fs.mkdir(apiRoot, { recursive: true });
  await fs.writeFile(path.join(apiRoot, "index.md"), `${lines.join("\n")}\n`);
}

async function main() {
  const targetFramework = await getTargetFramework();
  const projects = [];
  const generatedProjects = new Set();

  for (const pkg of apiPackages) {
    projects.push(await readProjectMetadata(pkg));
  }

  await fs.rm(apiRoot, { recursive: true, force: true });
  await writeApiIndex(projects);

  run("dotnet", ["tool", "restore"]);

  for (const pkg of projects) {
    if (pkg.referenceMode === "source-index") {
      continue;
    }

    const restoreStatus = runStatus("dotnet", ["restore", pkg.project, "--nologo"]);

    if (restoreStatus !== 0) {
      warnSourceIndexFallback(pkg, "dotnet restore failed");
      continue;
    }

    const buildStatus = runStatus("dotnet", [
      "build",
      pkg.project,
      "-c",
      "Release",
      "--no-restore",
      "--nologo",
      "-p:GenerateDocumentationFile=true",
      "-p:CopyLocalLockFileAssemblies=true",
      "-p:NoWarn=1591%3B1574"
    ]);

    if (buildStatus !== 0) {
      warnSourceIndexFallback(pkg, "dotnet build failed");
      continue;
    }

    generatedProjects.add(pkg.slug);
  }

  for (const pkg of projects) {
    const outputDirectory = path.join(apiRoot, pkg.slug);
    const assemblyPath = path.join(
      repoRoot,
      pkg.projectDirectory,
      "bin",
      "Release",
      targetFramework,
      `${pkg.assemblyName}.dll`
    );
    const sourcePath = `https://github.com/wieslawsoltes/CDP/blob/main/${toPosix(pkg.projectDirectory)}/`;

    await fs.mkdir(outputDirectory, { recursive: true });

    if (pkg.referenceMode === "source-index" || !generatedProjects.has(pkg.slug)) {
      await writeSourceIndexPackage(pkg, outputDirectory, {
        notes:
          pkg.referenceMode === "source-index"
            ? [
                "- This package exposes public native callback and function-pointer signatures.",
                "- The Markdown reflection generator used for the rest of the API site cannot currently expand those signatures.",
                "- This page indexes the public source types directly so the package is still discoverable from the docs site."
              ]
            : undefined
      });
      continue;
    }

    const generationStatus = runStatus("dotnet", [
      "tool",
      "run",
      "--allow-roll-forward",
      "xmldocmd",
      assemblyPath,
      outputDirectory,
      "--namespace-pages",
      "--visibility",
      "public",
      "--clean",
      "--quiet",
      "--source",
      sourcePath
    ]);

    if (generationStatus !== 0) {
      warnSourceIndexFallback(pkg, "xmldocmd could not load the built assembly in this environment");
      await fs.rm(outputDirectory, { recursive: true, force: true });
      await fs.mkdir(outputDirectory, { recursive: true });
      await writeSourceIndexPackage(pkg, outputDirectory);
      continue;
    }

    await normalizeMarkdown(outputDirectory);
    await writePackageIndex(pkg, outputDirectory);
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
