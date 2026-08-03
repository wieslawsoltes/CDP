namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>
/// Builds safe V8 live-edit patches for an original source represented by a source map.
/// Source maps describe positions rather than a compiler transformation, so mutations are
/// accepted only when the mapped generated window is textually identical to the original.
/// </summary>
public sealed class V8SourceMutationEngine
{
    private readonly IReadOnlyList<IV8SourceRegenerator> _regenerators;

    public V8SourceMutationEngine(IEnumerable<IV8SourceRegenerator>? regenerators = null)
    {
        _regenerators = regenerators?.ToArray() ?? [];
    }

    public IReadOnlyList<IV8SourceRegenerator> Regenerators => _regenerators;

    /// <summary>
    /// Builds a patch using the mapping-preserving fallback. Use <see cref="CreatePatchAsync"/>
    /// to enable compiler-backed regeneration for transformed or multiline edits.
    /// </summary>
    public V8SourceMutationResult CreatePatch(
        V8SourceMap sourceMap,
        int sourceIndex,
        string originalSource,
        string editedSource,
        string generatedSource)
    {
        ArgumentNullException.ThrowIfNull(sourceMap);
        ArgumentNullException.ThrowIfNull(originalSource);
        ArgumentNullException.ThrowIfNull(editedSource);
        ArgumentNullException.ThrowIfNull(generatedSource);

        if (sourceIndex < 0 || sourceIndex >= sourceMap.Sources.Count)
        {
            return V8SourceMutationResult.Rejected("The original source index is outside the source map.");
        }

        var prefixLength = GetCommonPrefixLength(originalSource, editedSource);
        if (prefixLength == originalSource.Length && prefixLength == editedSource.Length)
        {
            return V8SourceMutationResult.NoChange(generatedSource, sourceMap);
        }

        var suffixLength = GetCommonSuffixLength(originalSource, editedSource, prefixLength);
        var originalEndOffset = originalSource.Length - suffixLength;
        var editedEndOffset = editedSource.Length - suffixLength;
        var originalStart = GetPosition(originalSource, prefixLength);
        var originalEnd = GetPosition(originalSource, originalEndOffset);
        var replacement = editedSource[prefixLength..editedEndOffset];
        if (originalStart.Line != originalEnd.Line || replacement.Contains('\n') || replacement.Contains('\r'))
        {
            return V8SourceMutationResult.Rejected(
                "Source-mapped live edit currently requires one mapping-preserving line mutation.");
        }

        var sourceEntries = sourceMap.Entries
            .Where(entry => entry.SourceIndex == sourceIndex)
            .OrderBy(entry => entry.OriginalLine)
            .ThenBy(entry => entry.OriginalColumn)
            .ToArray();
        var startAnchor = sourceEntries.LastOrDefault(entry =>
            Compare(entry.OriginalLine, entry.OriginalColumn, originalStart.Line, originalStart.Column) <= 0);
        if (startAnchor is null)
        {
            return V8SourceMutationResult.Rejected("The edited range has no preceding source-map anchor.");
        }

        var endAnchor = sourceEntries.FirstOrDefault(entry =>
            Compare(entry.OriginalLine, entry.OriginalColumn, originalEnd.Line, originalEnd.Column) >= 0 &&
            !ReferenceEquals(entry, startAnchor));
        var originalAnchorStartOffset = GetOffset(originalSource, startAnchor.OriginalLine, startAnchor.OriginalColumn);
        var generatedAnchorStartOffset = GetOffset(generatedSource, startAnchor.GeneratedLine, startAnchor.GeneratedColumn);
        int originalAnchorEndOffset;
        int generatedAnchorEndOffset;
        if (endAnchor is not null)
        {
            originalAnchorEndOffset = GetOffset(originalSource, endAnchor.OriginalLine, endAnchor.OriginalColumn);
            generatedAnchorEndOffset = GetOffset(generatedSource, endAnchor.GeneratedLine, endAnchor.GeneratedColumn);
        }
        else
        {
            originalAnchorEndOffset = originalSource.Length;
            generatedAnchorEndOffset = generatedAnchorStartOffset + (originalAnchorEndOffset - originalAnchorStartOffset);
            if (generatedAnchorEndOffset > generatedSource.Length)
            {
                return V8SourceMutationResult.Rejected("The generated source ends before the mapped edit window.");
            }
        }

        if (prefixLength < originalAnchorStartOffset || originalEndOffset > originalAnchorEndOffset ||
            generatedAnchorEndOffset < generatedAnchorStartOffset)
        {
            return V8SourceMutationResult.Rejected("The edited range crosses an unsupported source-map boundary.");
        }

        var originalWindow = originalSource[originalAnchorStartOffset..originalAnchorEndOffset];
        var generatedWindow = generatedSource[generatedAnchorStartOffset..generatedAnchorEndOffset];
        if (!string.Equals(originalWindow, generatedWindow, StringComparison.Ordinal))
        {
            return V8SourceMutationResult.Rejected(
                "This mapped region was transformed by the compiler and cannot be patched without its compiler adapter.");
        }

        var relativeStart = prefixLength - originalAnchorStartOffset;
        var relativeEnd = originalEndOffset - originalAnchorStartOffset;
        var patchedWindow = string.Concat(originalWindow.AsSpan(0, relativeStart), replacement,
            originalWindow.AsSpan(relativeEnd));
        var patchedGeneratedSource = string.Concat(
            generatedSource.AsSpan(0, generatedAnchorStartOffset),
            patchedWindow,
            generatedSource.AsSpan(generatedAnchorEndOffset));
        var generatedEditStartOffset = generatedAnchorStartOffset + relativeStart;
        var generatedEditEndOffset = generatedAnchorStartOffset + relativeEnd;
        var generatedStart = GetPosition(generatedSource, generatedEditStartOffset);
        var generatedEnd = GetPosition(generatedSource, generatedEditEndOffset);
        if (generatedStart.Line != generatedEnd.Line)
        {
            return V8SourceMutationResult.Rejected("The mapped generated edit crosses a line boundary.");
        }

        var updatedMap = sourceMap.WithSingleLineMutation(
            sourceIndex,
            editedSource,
            originalStart.Line,
            originalStart.Column,
            originalEnd.Column,
            replacement.Length,
            generatedStart.Line,
            generatedStart.Column,
            generatedEnd.Column,
            replacement.Length);
        return V8SourceMutationResult.Ready(
            patchedGeneratedSource,
            updatedMap,
            new V8SourceMutationRange(originalStart.Line, originalStart.Column, originalEnd.Line, originalEnd.Column),
            new V8SourceMutationRange(generatedStart.Line, generatedStart.Column, generatedEnd.Line, generatedEnd.Column));
    }

    /// <summary>
    /// Builds a mapping-preserving patch when possible, otherwise asks the first compatible
    /// compiler adapter to regenerate the complete JavaScript script and source map.
    /// </summary>
    public async ValueTask<V8SourceMutationResult> CreatePatchAsync(
        V8SourceMap sourceMap,
        int sourceIndex,
        string originalSource,
        string editedSource,
        string generatedSource,
        string? sourceUrl = null,
        string? generatedUrl = null,
        CancellationToken cancellationToken = default)
    {
        var mappedPatch = CreatePatch(sourceMap, sourceIndex, originalSource, editedSource, generatedSource);
        if (mappedPatch.CanApply || _regenerators.Count == 0 ||
            sourceIndex < 0 || sourceIndex >= sourceMap.Sources.Count)
        {
            return mappedPatch;
        }

        var request = new V8SourceRegenerationRequest(
            sourceMap,
            sourceIndex,
            sourceUrl ?? sourceMap.ResolveSourceUrl(sourceIndex),
            generatedUrl ?? sourceMap.File,
            originalSource,
            editedSource,
            generatedSource);
        foreach (var regenerator in _regenerators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!regenerator.CanRegenerate(request)) continue;

            V8SourceRegenerationResult regenerated;
            try
            {
                regenerated = await regenerator.RegenerateAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return V8SourceMutationResult.Rejected($"{regenerator.Name} regeneration failed: {ex.Message}");
            }

            if (!regenerated.Success)
            {
                return V8SourceMutationResult.Rejected(
                    $"{regenerator.Name} regeneration failed: {regenerated.Message}");
            }
            if (regenerated.SourceMap is null)
            {
                return V8SourceMutationResult.Rejected(
                    $"{regenerator.Name} regeneration did not return a source map.");
            }
            if (sourceIndex >= regenerated.SourceMap.Sources.Count)
            {
                return V8SourceMutationResult.Rejected(
                    $"{regenerator.Name} regeneration returned an incompatible source map.");
            }
            if (sourceIndex >= regenerated.SourceMap.SourcesContent.Count ||
                !string.Equals(regenerated.SourceMap.SourcesContent[sourceIndex], editedSource, StringComparison.Ordinal))
            {
                return V8SourceMutationResult.Rejected(
                    $"{regenerator.Name} regeneration source map does not contain the edited source.");
            }

            return V8SourceMutationResult.Regenerated(
                regenerated.GeneratedSource,
                regenerated.SourceMap,
                string.IsNullOrWhiteSpace(regenerated.Message)
                    ? $"Source regenerated by {regenerator.Name}."
                    : regenerated.Message);
        }

        return V8SourceMutationResult.Rejected(
            $"{mappedPatch.Message} No registered compiler adapter supports '{request.SourceUrl}'.");
    }

    private static int GetCommonPrefixLength(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < length && left[index] == right[index]) index++;
        return index;
    }

    private static int GetCommonSuffixLength(string left, string right, int prefixLength)
    {
        var maximum = Math.Min(left.Length, right.Length) - prefixLength;
        var length = 0;
        while (length < maximum && left[^(length + 1)] == right[^(length + 1)]) length++;
        return length;
    }

    private static V8SourceMutationPosition GetPosition(string text, int offset)
    {
        if (offset < 0 || offset > text.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        var line = 0;
        var column = 0;
        for (var index = 0; index < offset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                column = 0;
            }
            else if (text[index] != '\r')
            {
                column++;
            }
        }
        return new V8SourceMutationPosition(line, column);
    }

    private static int GetOffset(string text, int line, int column)
    {
        if (line < 0 || column < 0) throw new ArgumentOutOfRangeException(nameof(line));
        var currentLine = 0;
        var offset = 0;
        while (currentLine < line && offset < text.Length)
        {
            if (text[offset++] == '\n') currentLine++;
        }
        if (currentLine != line) throw new ArgumentOutOfRangeException(nameof(line));
        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0) lineEnd = text.Length;
        var target = offset + column;
        if (target > lineEnd) throw new ArgumentOutOfRangeException(nameof(column));
        return target;
    }

    private static int Compare(int leftLine, int leftColumn, int rightLine, int rightColumn)
    {
        var line = leftLine.CompareTo(rightLine);
        return line != 0 ? line : leftColumn.CompareTo(rightColumn);
    }

    private sealed record V8SourceMutationPosition(int Line, int Column);
}

public sealed record V8SourceMutationRange(int StartLine, int StartColumn, int EndLine, int EndColumn);

public enum V8SourceMutationKind
{
    None,
    MappedPatch,
    Regenerated
}

public sealed record V8SourceMutationResult(
    bool CanApply,
    bool HasChanges,
    string Message,
    string GeneratedSource,
    V8SourceMap? UpdatedSourceMap,
    V8SourceMutationRange? OriginalRange,
    V8SourceMutationRange? GeneratedRange,
    V8SourceMutationKind Kind)
{
    internal static V8SourceMutationResult Ready(
        string generatedSource,
        V8SourceMap updatedSourceMap,
        V8SourceMutationRange originalRange,
        V8SourceMutationRange generatedRange) =>
        new(true, true, "Source-mapped mutation is ready for V8 validation.", generatedSource, updatedSourceMap,
            originalRange, generatedRange, V8SourceMutationKind.MappedPatch);

    internal static V8SourceMutationResult Regenerated(
        string generatedSource,
        V8SourceMap updatedSourceMap,
        string message) =>
        new(true, true, message, generatedSource, updatedSourceMap, null, null, V8SourceMutationKind.Regenerated);

    internal static V8SourceMutationResult NoChange(string generatedSource, V8SourceMap sourceMap) =>
        new(true, false, "The source is unchanged.", generatedSource, sourceMap, null, null, V8SourceMutationKind.None);

    internal static V8SourceMutationResult Rejected(string message) =>
        new(false, false, message, "", null, null, null, V8SourceMutationKind.None);
}
