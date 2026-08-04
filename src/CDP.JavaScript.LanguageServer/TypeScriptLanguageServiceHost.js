(function (global) {
    "use strict";

    const files = Object.create(null);
    let projectVersion = 0;
    let currentDirectory = "/workspace";

    function normalize(fileName) {
        let value = String(fileName || "").replace(/\\/g, "/");
        if (!value.startsWith("/")) value = "/" + value;
        const parts = [];
        for (const part of value.split("/")) {
            if (!part || part === ".") continue;
            if (part === "..") parts.pop();
            else parts.push(part);
        }
        return "/" + parts.join("/");
    }

    function directoryName(fileName) {
        const value = normalize(fileName);
        const slash = value.lastIndexOf("/");
        return slash <= 0 ? "/" : value.slice(0, slash);
    }

    function setFile(fileName, text, library) {
        const name = normalize(fileName);
        const previous = files[name];
        files[name] = {
            text: String(text || ""),
            version: previous ? previous.version + 1 : 1,
            library: !!library
        };
        projectVersion++;
        return name;
    }

    function fileExists(fileName) {
        return Object.prototype.hasOwnProperty.call(files, normalize(fileName));
    }

    function readFile(fileName) {
        const file = files[normalize(fileName)];
        return file ? file.text : undefined;
    }

    function directoryExists(path) {
        const prefix = normalize(path).replace(/\/$/, "") + "/";
        return Object.keys(files).some(fileName => fileName.startsWith(prefix));
    }

    function getDirectories(path) {
        const prefix = normalize(path).replace(/\/$/, "") + "/";
        const directories = new Set();
        for (const fileName of Object.keys(files)) {
            if (!fileName.startsWith(prefix)) continue;
            const rest = fileName.slice(prefix.length);
            const slash = rest.indexOf("/");
            if (slash > 0) directories.add(prefix + rest.slice(0, slash));
        }
        return Array.from(directories);
    }

    function readDirectory(path, extensions, excludes, includes, depth) {
        const prefix = normalize(path).replace(/\/$/, "") + "/";
        const maximumDepth = typeof depth === "number" ? depth : 100;
        return Object.keys(files).filter(fileName => {
            if (!fileName.startsWith(prefix)) return false;
            const relative = fileName.slice(prefix.length);
            if (relative.split("/").length - 1 > maximumDepth) return false;
            if (!extensions || extensions.length === 0) return true;
            return extensions.some(extension => fileName.endsWith(extension));
        });
    }

    const compilerOptions = {
        allowJs: true,
        checkJs: true,
        noEmit: true,
        strict: true,
        target: ts.ScriptTarget.ES2022,
        module: ts.ModuleKind.ESNext,
        moduleResolution: ts.ModuleResolutionKind.Bundler,
        jsx: ts.JsxEmit.ReactJSX,
        allowSyntheticDefaultImports: true,
        esModuleInterop: true,
        resolveJsonModule: true,
        lib: [],
        skipLibCheck: true
    };

    const host = {
        getCompilationSettings: () => compilerOptions,
        getScriptFileNames: () => Object.keys(files).filter(fileName => !files[fileName].library),
        getScriptVersion: fileName => String((files[normalize(fileName)] || {}).version || 0),
        getScriptSnapshot: fileName => {
            const text = readFile(fileName);
            return text === undefined ? undefined : ts.ScriptSnapshot.fromString(text);
        },
        getCurrentDirectory: () => currentDirectory,
        getDefaultLibFileName: () => "/lib.es2022.full.d.ts",
        getDefaultLibLocation: () => "/",
        getProjectVersion: () => String(projectVersion),
        getNewLine: () => "\n",
        useCaseSensitiveFileNames: () => true,
        fileExists,
        readFile,
        readDirectory,
        directoryExists,
        getDirectories,
        realpath: normalize
    };

    const service = ts.createLanguageService(host, ts.createDocumentRegistry());

    function offset(fileName, line, column) {
        const source = readFile(fileName) || "";
        const wantedLine = Math.max(1, Number(line) || 1);
        const wantedColumn = Math.max(1, Number(column) || 1);
        let currentLine = 1;
        let position = 0;
        while (position < source.length && currentLine < wantedLine) {
            if (source.charCodeAt(position++) === 10) currentLine++;
        }
        return Math.min(source.length, position + wantedColumn - 1);
    }

    function flatten(messageText) {
        return ts.flattenDiagnosticMessageText(messageText, "\n");
    }

    function display(parts) {
        return ts.displayPartsToString(parts || []);
    }

    function span(value) {
        return value ? { start: value.start, length: value.length } : null;
    }

    function json(value) {
        return JSON.stringify(value === undefined ? null : value);
    }

    global.__cdpTsOpenLibraries = function (serialized) {
        const libraries = JSON.parse(String(serialized || "{}"));
        for (const name of Object.keys(libraries)) setFile("/" + name, libraries[name], true);
        compilerOptions.lib = Object.keys(libraries).filter(name =>
            name === "lib.es5.d.ts" ||
            name === "lib.dom.d.ts" ||
            name === "lib.dom.iterable.d.ts" ||
            name === "lib.dom.asynciterable.d.ts" ||
            (/^lib\.es20(1[5-9]|2[0-2])\..+\.d\.ts$/.test(name) && !name.endsWith(".full.d.ts")));
        return String(ts.version || "unknown");
    };

    global.__cdpTsOpen = function (fileName, text, rootDirectory) {
        if (rootDirectory) currentDirectory = normalize(rootDirectory);
        return setFile(fileName, text, false);
    };

    global.__cdpTsClose = function (fileName) {
        const name = normalize(fileName);
        if (files[name]) {
            delete files[name];
            projectVersion++;
        }
    };

    global.__cdpTsCompletions = function (fileName, line, column) {
        const name = normalize(fileName);
        const result = service.getCompletionsAtPosition(name, offset(name, line, column), {
            includeCompletionsForModuleExports: true,
            includeCompletionsForImportStatements: true,
            includeCompletionsWithInsertText: true
        });
        return json((result && result.entries || []).map(entry => ({
            name: entry.name,
            kind: entry.kind,
            kindModifiers: entry.kindModifiers || "",
            sortText: entry.sortText || "",
            insertText: entry.insertText || entry.name,
            source: entry.source || null,
            replacementSpan: span(entry.replacementSpan)
        })));
    };

    global.__cdpTsHover = function (fileName, line, column) {
        const name = normalize(fileName);
        const result = service.getQuickInfoAtPosition(name, offset(name, line, column));
        if (!result) return "null";
        return json({
            kind: result.kind,
            kindModifiers: result.kindModifiers || "",
            textSpan: span(result.textSpan),
            displayText: display(result.displayParts),
            documentation: display(result.documentation),
            tags: (result.tags || []).map(tag => ({ name: tag.name, text: display(tag.text) }))
        });
    };

    global.__cdpTsDiagnostics = function (fileName) {
        const name = normalize(fileName);
        const diagnostics = []
            .concat(service.getCompilerOptionsDiagnostics())
            .concat(service.getSyntacticDiagnostics(name))
            .concat(service.getSemanticDiagnostics(name))
            .concat(service.getSuggestionDiagnostics(name));
        return json(diagnostics.map(diagnostic => ({
            code: diagnostic.code,
            category: diagnostic.category,
            start: diagnostic.start || 0,
            length: diagnostic.length || 0,
            message: flatten(diagnostic.messageText),
            source: diagnostic.source || "typescript"
        })));
    };

    global.__cdpTsSignatureHelp = function (fileName, line, column) {
        const name = normalize(fileName);
        const result = service.getSignatureHelpItems(name, offset(name, line, column), undefined);
        if (!result) return "null";
        return json({
            argumentIndex: result.argumentIndex,
            argumentCount: result.argumentCount,
            selectedItemIndex: result.selectedItemIndex,
            items: result.items.map(item => ({
                prefix: display(item.prefixDisplayParts),
                suffix: display(item.suffixDisplayParts),
                separator: display(item.separatorDisplayParts),
                documentation: display(item.documentation),
                parameters: item.parameters.map(parameter => ({
                    name: parameter.name,
                    display: display(parameter.displayParts),
                    documentation: display(parameter.documentation),
                    optional: !!parameter.isOptional
                }))
            }))
        });
    };

    function mapDocumentSpan(item) {
        return {
            fileName: item.fileName,
            textSpan: span(item.textSpan),
            contextSpan: span(item.contextSpan),
            originalTextSpan: span(item.originalTextSpan),
            originalContextSpan: span(item.originalContextSpan)
        };
    }

    global.__cdpTsDefinitions = function (fileName, line, column) {
        const name = normalize(fileName);
        return json((service.getDefinitionAtPosition(name, offset(name, line, column)) || [])
            .map(item => Object.assign(mapDocumentSpan(item), {
                kind: item.kind,
                name: item.name,
                containerName: item.containerName || ""
            })));
    };

    global.__cdpTsReferences = function (fileName, line, column) {
        const name = normalize(fileName);
        return json((service.getReferencesAtPosition(name, offset(name, line, column)) || [])
            .map(item => Object.assign(mapDocumentSpan(item), {
                isWriteAccess: !!item.isWriteAccess,
                isDefinition: !!item.isDefinition
            })));
    };

    global.__cdpTsRename = function (fileName, line, column) {
        const name = normalize(fileName);
        const position = offset(name, line, column);
        const info = service.getRenameInfo(name, position, { allowRenameOfImportPath: false });
        if (!info.canRename) return json({ canRename: false, error: info.localizedErrorMessage || "Rename is unavailable", locations: [] });
        const locations = service.findRenameLocations(name, position, false, false, true) || [];
        return json({
            canRename: true,
            displayName: info.displayName,
            fullDisplayName: info.fullDisplayName,
            kind: info.kind,
            triggerSpan: span(info.triggerSpan),
            locations: locations.map(mapDocumentSpan)
        });
    };

    function mapNavigationItem(item) {
        return {
            text: item.text,
            kind: item.kind,
            kindModifiers: item.kindModifiers || "",
            spans: (item.spans || []).map(span),
            nameSpan: span(item.nameSpan),
            children: (item.childItems || []).map(mapNavigationItem)
        };
    }

    global.__cdpTsSymbols = function (fileName) {
        const result = service.getNavigationTree(normalize(fileName));
        return json(result ? mapNavigationItem(result) : null);
    };

    global.__cdpTsSemanticTokens = function (fileName) {
        const name = normalize(fileName);
        const source = readFile(name) || "";
        const result = service.getEncodedSemanticClassifications(
            name,
            { start: 0, length: source.length },
            ts.SemanticClassificationFormat.TwentyTwenty);
        return json(result && result.spans || []);
    };

    global.__cdpTsFormat = function (fileName, tabSize, insertSpaces) {
        const options = {
            tabSize: Math.max(1, Number(tabSize) || 4),
            indentSize: Math.max(1, Number(tabSize) || 4),
            convertTabsToSpaces: insertSpaces !== false,
            newLineCharacter: "\n",
            semicolons: "insert",
            ensureNewLineAtEndOfFile: true
        };
        return json((service.getFormattingEditsForDocument(normalize(fileName), options) || [])
            .map(edit => ({ start: edit.span.start, length: edit.span.length, newText: edit.newText })));
    };
})(globalThis);
