using System;

namespace CDP.Document.Parser;

public static class DocumentParserFactory
{
    public static IDocumentParser GetParser(string fileExtension)
    {
        var ext = fileExtension.ToLowerInvariant().TrimStart('.');
        return ext switch
        {
            "docx" => new DocxDocumentParser(),
            "xlsx" => new XlsxDocumentParser(),
            "pptx" => new PptxDocumentParser(),
            "rtf" => new RtfDocumentParser(),
            _ => throw new NotSupportedException($"File extension '{fileExtension}' is not supported.")
        };
    }
}
