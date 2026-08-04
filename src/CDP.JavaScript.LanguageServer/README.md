# Chrome.DevTools.JavaScript.LanguageServer

Embeds Microsoft's TypeScript language service and standard library declarations
for standalone JavaScript, JSX, TypeScript, and TSX editor tooling. The service
runs in-process and does not require Node.js or a globally installed `tsserver`.

It provides completion, quick info, diagnostics, signature help, definitions,
references, rename locations, document symbols, semantic classifications, and
formatting edits over in-memory project documents.

The embedded TypeScript compiler and standard-library declarations are
copyright Microsoft Corporation and licensed under Apache-2.0. Their license is
included in the NuGet package at `third-party/TypeScript-LICENSE.txt`.
