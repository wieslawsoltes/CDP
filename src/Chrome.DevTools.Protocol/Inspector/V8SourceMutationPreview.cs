using System.Security.Cryptography;
using System.Text;

namespace Chrome.DevTools.Protocol.Inspector;

/// <summary>A stable fingerprint and useful size metadata for one live-edit revision.</summary>
public sealed record V8SourceRevision(string Sha256, int CharacterCount, int LineCount)
{
    public string ShortHash => Sha256[..Math.Min(12, Sha256.Length)];

    public bool Matches(string source) =>
        string.Equals(Sha256, Create(source).Sha256, StringComparison.Ordinal);

    public static V8SourceRevision Create(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var bytes = Encoding.UTF8.GetBytes(source);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var lines = source.Length == 0 ? 0 : 1;
        foreach (var character in source)
        {
            if (character == '\n') lines++;
        }
        return new(hash, source.Length, lines);
    }
}

/// <summary>Compact edit-span statistics used by the Sources preview UI and diagnostics.</summary>
public sealed record V8SourceChangeSummary(
    int RemovedCharacters,
    int AddedCharacters,
    int RemovedLines,
    int AddedLines)
{
    internal static V8SourceChangeSummary Create(string before, string after)
    {
        var prefix = CommonPrefix(before, after);
        var suffix = CommonSuffix(before, after, prefix);
        var removed = before.AsSpan(prefix, before.Length - prefix - suffix);
        var added = after.AsSpan(prefix, after.Length - prefix - suffix);
        return new(removed.Length, added.Length, CountAffectedLines(removed), CountAffectedLines(added));
    }

    private static int CommonPrefix(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < length && left[index] == right[index]) index++;
        return index;
    }

    private static int CommonSuffix(string left, string right, int prefix)
    {
        var maximum = Math.Min(left.Length, right.Length) - prefix;
        var length = 0;
        while (length < maximum && left[^(length + 1)] == right[^(length + 1)]) length++;
        return length;
    }

    private static int CountAffectedLines(ReadOnlySpan<char> text)
    {
        if (text.Length == 0) return 0;
        var lines = 1;
        foreach (var character in text)
        {
            if (character == '\n') lines++;
        }
        return lines;
    }
}

/// <summary>Immutable preview of the original and generated changes planned for V8.</summary>
public sealed record V8SourceMutationPreview(
    V8SourceMutationKind Kind,
    string AdapterName,
    V8SourceRevision OriginalRevision,
    V8SourceRevision EditedRevision,
    V8SourceRevision GeneratedRevision,
    V8SourceRevision ResultRevision,
    V8SourceChangeSummary OriginalChange,
    V8SourceChangeSummary GeneratedChange)
{
    public string Summary
    {
        get
        {
            var strategy = Kind switch
            {
                V8SourceMutationKind.DirectScript => "direct script edit",
                V8SourceMutationKind.MappedPatch => "mapped patch",
                V8SourceMutationKind.Regenerated when AdapterName.Length > 0 => $"{AdapterName} regeneration",
                V8SourceMutationKind.Regenerated => "regeneration",
                _ => "no change"
            };
            return $"{strategy} · source {OriginalRevision.LineCount}→{EditedRevision.LineCount} lines · " +
                   $"output {GeneratedRevision.LineCount}→{ResultRevision.LineCount} lines";
        }
    }

    internal static V8SourceMutationPreview Create(
        V8SourceMutationKind kind,
        string adapterName,
        string originalSource,
        string editedSource,
        string generatedSource,
        string resultSource) =>
        new(
            kind,
            adapterName,
            V8SourceRevision.Create(originalSource),
            V8SourceRevision.Create(editedSource),
            V8SourceRevision.Create(generatedSource),
            V8SourceRevision.Create(resultSource),
            V8SourceChangeSummary.Create(originalSource, editedSource),
            V8SourceChangeSummary.Create(generatedSource, resultSource));

    public static V8SourceMutationPreview CreateDirect(string generatedSource, string editedSource) =>
        Create(V8SourceMutationKind.DirectScript, "", generatedSource, editedSource, generatedSource, editedSource);
}
