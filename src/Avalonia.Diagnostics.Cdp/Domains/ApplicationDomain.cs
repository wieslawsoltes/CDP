using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;

namespace Avalonia.Diagnostics.Cdp.Domains;

public static class ApplicationDomain
{
    private static void ScanDatabases(string dir, System.Collections.Generic.List<string> result)
    {
        try
        {
            var files = System.IO.Directory.GetFiles(dir, "*.*");
            foreach (var f in files)
            {
                var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".sqlite" || ext == ".db")
                {
                    result.Add(f);
                }
            }

            var subdirs = System.IO.Directory.GetDirectories(dir);
            foreach (var d in subdirs)
            {
                var name = System.IO.Path.GetFileName(d).ToLowerInvariant();
                if (name == "bin" || name == "obj" || name == ".git" || name == ".gemini" || name == "publish")
                {
                    continue;
                }
                ScanDatabases(d, result);
            }
        }
        catch
        {
            // Ignore access errors
        }
    }

    public static async Task<JsonObject> HandleAsync(CdpSession session, string action, JsonObject @params)
    {
        switch (action)
        {
            case "getDatabases":
                {
                    var databases = new System.Collections.Generic.List<string>();
                    var currentDir = System.IO.Directory.GetCurrentDirectory();
                    ScanDatabases(currentDir, databases);
                    
                    var dbArray = new JsonArray();
                    foreach (var db in databases)
                    {
                        dbArray.Add(db);
                    }
                    return new JsonObject { ["databases"] = dbArray };
                }

            case "getDatabaseTableNames":
                {
                    string databasePath = @params["databasePath"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(databasePath))
                    {
                        throw new Exception("Database path is required.");
                    }

                    var tables = new System.Collections.Generic.List<string>();
                    using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    tables.Add(reader.GetString(0));
                                }
                            }
                        }
                    }

                    var tablesArray = new JsonArray();
                    foreach (var tbl in tables)
                    {
                        tablesArray.Add(tbl);
                    }
                    return new JsonObject { ["tables"] = tablesArray };
                }

            case "executeSQL":
                {
                    string databasePath = @params["databasePath"]?.GetValue<string>() ?? "";
                    string query = @params["query"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(databasePath))
                    {
                        throw new Exception("Database path is required.");
                    }
                    if (string.IsNullOrEmpty(query))
                    {
                        throw new Exception("SQL query is required.");
                    }

                    var columnsArray = new JsonArray();
                    var rowsArray = new JsonArray();

                    using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
                    {
                        connection.Open();
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = query;
                            bool isSelect = query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                                            query.TrimStart().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);

                            if (isSelect)
                            {
                                using (var reader = command.ExecuteReader())
                                {
                                    int fieldCount = reader.FieldCount;
                                    for (int i = 0; i < fieldCount; i++)
                                    {
                                        columnsArray.Add(reader.GetName(i));
                                    }

                                    while (reader.Read())
                                    {
                                        var rowArray = new JsonArray();
                                        for (int i = 0; i < fieldCount; i++)
                                        {
                                            if (reader.IsDBNull(i))
                                            {
                                                rowArray.Add(null);
                                            }
                                            else
                                            {
                                                var val = reader.GetValue(i);
                                                if (val is long l) rowArray.Add(l);
                                                else if (val is double d) rowArray.Add(d);
                                                else if (val is bool b) rowArray.Add(b);
                                                else rowArray.Add(val.ToString());
                                            }
                                        }
                                        rowsArray.Add(rowArray);
                                    }
                                }
                            }
                            else
                            {
                                int rowsAffected = command.ExecuteNonQuery();
                                columnsArray.Add("Rows Affected");
                                var rowArray = new JsonArray { rowsAffected };
                                rowsArray.Add(rowArray);
                            }
                        }
                    }

                    return new JsonObject
                    {
                        ["columns"] = columnsArray,
                        ["rows"] = rowsArray
                    };
                }

            case "getResources":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var array = new JsonArray();
                    if (Application.Current != null)
                    {
                        foreach (var key in Application.Current.Resources.Keys)
                        {
                            if (key == null) continue;
                            Application.Current.Resources.TryGetValue(key, out var val);
                            array.Add(new JsonObject
                            {
                                ["key"] = key.ToString(),
                                ["type"] = val?.GetType().FullName ?? "null",
                                ["value"] = val?.ToString() ?? "null"
                            });
                        }
                    }
                    return new JsonObject { ["resources"] = array };
                });

            case "setResource":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Application.Current == null) return new JsonObject();

                    string key = @params["key"]?.GetValue<string>() ?? "";
                    string valStr = @params["value"]?.GetValue<string>() ?? "";
                    if (string.IsNullOrEmpty(key)) throw new Exception("Resource key is required.");

                    object parsedValue = valStr;
                    if (Application.Current.Resources.TryGetValue(key, out var existingVal) && existingVal != null)
                    {
                        var t = existingVal.GetType();
                        if (t == typeof(double))
                        {
                            if (double.TryParse(valStr, out double d)) parsedValue = d;
                        }
                        else if (t == typeof(int))
                        {
                            if (int.TryParse(valStr, out int i)) parsedValue = i;
                        }
                        else if (t == typeof(bool))
                        {
                            if (bool.TryParse(valStr, out bool b)) parsedValue = b;
                        }
                        else if (t == typeof(Media.Color))
                        {
                            if (Media.Color.TryParse(valStr, out var color)) parsedValue = color;
                        }
                        else if (t == typeof(Media.SolidColorBrush))
                        {
                            if (Media.Color.TryParse(valStr, out var color)) parsedValue = new Media.SolidColorBrush(color);
                        }
                    }
                    else
                    {
                        if (valStr.StartsWith("#") && (valStr.Length == 7 || valStr.Length == 9))
                        {
                            if (Media.Color.TryParse(valStr, out var color))
                            {
                                parsedValue = new Media.SolidColorBrush(color);
                            }
                        }
                        else if (double.TryParse(valStr, out double d))
                        {
                            parsedValue = d;
                        }
                        else if (bool.TryParse(valStr, out bool b))
                        {
                            parsedValue = b;
                        }
                    }

                    Application.Current.Resources[key] = parsedValue;
                    return new JsonObject();
                });

            case "deleteResource":
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    string key = @params["key"]?.GetValue<string>() ?? "";
                    if (Application.Current != null && !string.IsNullOrEmpty(key))
                    {
                        Application.Current.Resources.Remove(key);
                    }
                    return new JsonObject();
                });

            default:
                throw new Exception($"Method Application.{action} is not implemented");
        }
    }
}
