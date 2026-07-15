using System.IO;
using System.Threading.Tasks;
using CDP.Document.Parser.AST;

namespace CDP.Document.Parser;

public interface IDocumentParser
{
    Task<DocumentRoot> ParseAsync(Stream stream);
    DocumentRoot Parse(Stream stream);
}
