using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Chrome.DevTools.Protocol.Domains;

public static class SourcesDomain
{
    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getWorkspaceFiles":
                {
                    var filesArray = new JsonArray();
                    string root = GetWorkspaceRoot();
                    await Task.Run(() => GetFilesRecursive(root, "", filesArray));
                    return new JsonObject { ["files"] = filesArray };
                }

            case "getFileContent":
                {
                    string relPath = @params?["path"]?.GetValue<string>() ?? "";
                    string root = GetWorkspaceRoot();
                    string fullPath = Path.GetFullPath(Path.Combine(root, relPath));
                    string relative = Path.GetRelativePath(root, fullPath);
                    if (relative.StartsWith("..") || Path.IsPathRooted(relative))
                    {
                        throw new Exception("Access denied: path is outside workspace root.");
                    }
                    if (File.Exists(fullPath))
                    {
                        string content = await File.ReadAllTextAsync(fullPath);
                        return new JsonObject { ["content"] = content };
                    }
                    throw new Exception($"File {relPath} not found.");
                }

            case "setFileContent":
                {
                    string relPath = @params?["path"]?.GetValue<string>() ?? "";
                    string content = @params?["content"]?.GetValue<string>() ?? "";
                    string root = GetWorkspaceRoot();
                    string fullPath = Path.GetFullPath(Path.Combine(root, relPath));
                    string relative = Path.GetRelativePath(root, fullPath);
                    if (relative.StartsWith("..") || Path.IsPathRooted(relative))
                    {
                        throw new Exception("Access denied: path is outside workspace root.");
                    }
                    await File.WriteAllTextAsync(fullPath, content);
                    return new JsonObject { ["success"] = true };
                }

            case "searchInWorkspace":
                {
                    string query = @params?["query"]?.GetValue<string>() ?? "";
                    bool caseSensitive = @params?["caseSensitive"]?.GetValue<bool>() ?? false;
                    var matchesArray = new JsonArray();
                    string root = GetWorkspaceRoot();
                    await Task.Run(() => SearchInWorkspaceRecursive(root, "", query, caseSensitive, matchesArray));
                    return new JsonObject { ["matches"] = matchesArray };
                }

            default:
                throw new Exception($"Method Sources.{action} is not implemented");
        }
    }

    private static string GetWorkspaceRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || 
                Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.GetFiles(dir, "*.slnx").Length > 0)
            {
                return dir;
            }
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir || string.IsNullOrEmpty(parent)) break;
            dir = parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static void GetFilesRecursive(string dir, string relativePath, JsonArray array)
    {
        string name = Path.GetFileName(dir).ToLowerInvariant();
        if (name == "bin" || name == "obj" || name == ".git" || name == ".vs" || name == ".idea" || name == "node_modules")
        {
            return;
        }

        foreach (var file in Directory.GetFiles(dir))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".cs" || ext == ".axaml" || ext == ".xaml" || ext == ".json" || ext == ".md" || ext == ".xml" || ext == ".csproj" || ext == ".docx" || ext == ".rtf" || ext == ".pptx" || ext == ".xlsx")
            {
                string fileRelPath = string.IsNullOrEmpty(relativePath) 
                    ? Path.GetFileName(file) 
                    : Path.Combine(relativePath, Path.GetFileName(file));
                
                array.Add(new JsonObject
                {
                    ["path"] = fileRelPath.Replace('\\', '/'),
                    ["name"] = Path.GetFileName(file),
                    ["size"] = new FileInfo(file).Length
                });
            }
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            string subRelPath = string.IsNullOrEmpty(relativePath) 
                ? Path.GetFileName(subDir) 
                : Path.Combine(relativePath, Path.GetFileName(subDir));
            GetFilesRecursive(subDir, subRelPath, array);
        }
    }

    private static void SearchInWorkspaceRecursive(string dir, string relativePath, string query, bool caseSensitive, JsonArray matches)
    {
        string name = Path.GetFileName(dir).ToLowerInvariant();
        if (name == "bin" || name == "obj" || name == ".git" || name == ".vs" || name == ".idea" || name == "node_modules")
        {
            return;
        }

        foreach (var file in Directory.GetFiles(dir))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".cs" || ext == ".axaml" || ext == ".xaml" || ext == ".json" || ext == ".md" || ext == ".xml" || ext == ".csproj" || ext == ".rtf")
            {
                string fileRelPath = string.IsNullOrEmpty(relativePath) 
                    ? Path.GetFileName(file) 
                    : Path.Combine(relativePath, Path.GetFileName(file));
                
                string fileRelPathClean = fileRelPath.Replace('\\', '/');
                
                try
                {
                    var lines = File.ReadLines(file);
                    int lineNumber = 1;
                    foreach (var line in lines)
                    {
                        bool matchesQuery = false;
                        if (caseSensitive)
                        {
                            matchesQuery = line.Contains(query, StringComparison.Ordinal);
                        }
                        else
                        {
                            matchesQuery = line.Contains(query, StringComparison.OrdinalIgnoreCase);
                        }

                        if (matchesQuery)
                        {
                            matches.Add(new JsonObject
                            {
                                ["path"] = fileRelPathClean,
                                ["lineNumber"] = lineNumber,
                                ["lineContent"] = line
                            });
                        }
                        lineNumber++;
                    }
                }
                catch
                {
                    // Ignore read errors
                }
            }
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            string subRelPath = string.IsNullOrEmpty(relativePath) 
                ? Path.GetFileName(subDir) 
                : Path.Combine(relativePath, Path.GetFileName(subDir));
            SearchInWorkspaceRecursive(subDir, subRelPath, query, caseSensitive, matches);
        }
    }
}
