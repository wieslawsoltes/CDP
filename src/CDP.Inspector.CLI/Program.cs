using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using CdpInspectorApp.Models;
using CdpInspectorApp.Services;
using CdpInspectorApp.ViewModels;
using Chrome.DevTools.Protocol;

namespace CDP.Inspector.CLI;

public class Program
{
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<CliApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public static async Task<int> RunCliAsync(string[] args)
    {
        var hostOption = new Option<string>(new[] { "--host", "-h" }, () => "http://127.0.0.1:9222", "CDP server host address");
        var targetIdOption = new Option<string>(new[] { "--target", "-t" }, "Target WebSocket page ID");
        var targetNameOption = new Option<string>(new[] { "--target-name", "-n" }, "Match target window name/title");

        var rootCommand = new RootCommand("CDP Inspector CLI runner and automation tool");

        // 1. list-targets command
        var listTargetsCommand = new Command("list-targets", "List active CDP targets on the host") { hostOption };
        listTargetsCommand.SetHandler(async (context) =>
        {
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var cdp = new CdpService();
            try
            {
                var targets = await cdp.GetTargetsAsync(host);
                if (targets == null || targets.Count == 0)
                {
                    Console.WriteLine("No active CDP targets found.");
                    return;
                }
                Console.WriteLine($"Found {targets.Count} targets on {host}:");
                for (int i = 0; i < targets.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {targets[i].Title} (ID: {targets[i].Id})");
                    Console.WriteLine($"   WS: {targets[i].WebSocketUrl}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error listing targets: {ex.Message}");
            }
        });
        rootCommand.AddCommand(listTargetsCommand);

        // 2. run command
        var testPathArg = new Argument<string>("test-path", "Path to a YAML test flow file or directory containing a suite of YAML files");
        var outputDirOption = new Option<string>(new[] { "--output-dir", "-o" }, () => "TestReports", "Directory to write output reports");
        var videoOption = new Option<bool>(new[] { "--video", "-v" }, "Record execution video frames");
        var reportOption = new Option<bool>(new[] { "--report", "-r" }, "Generate HTML/PDF reports");
        var timeoutOption = new Option<int>("--timeout", () => 30000, "Timeout for step execution in milliseconds");
        var envOption = new Option<string[]>(new[] { "--env", "-e" }, "Environment variables (e.g. -e KEY=VAL)") { AllowMultipleArgumentsPerToken = true };
        var autoLaunchOption = new Option<string>("--auto-launch", "Path to executable to auto-launch");
        var autoLaunchArgsOption = new Option<string>("--auto-launch-args", "Arguments to pass to auto-launched app");

        var runCommand = new Command("run", "Run a single test flow or a suite of flows")
        {
            testPathArg, hostOption, targetIdOption, targetNameOption, outputDirOption, videoOption, reportOption, timeoutOption, envOption, autoLaunchOption, autoLaunchArgsOption
        };

        runCommand.SetHandler(async (context) =>
        {
            var testPath = context.ParseResult.GetValueForArgument(testPathArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var targetId = context.ParseResult.GetValueForOption(targetIdOption);
            var targetName = context.ParseResult.GetValueForOption(targetNameOption);
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption) ?? "TestReports";
            var video = context.ParseResult.GetValueForOption(videoOption);
            var report = context.ParseResult.GetValueForOption(reportOption);
            var timeout = context.ParseResult.GetValueForOption(timeoutOption);
            var env = context.ParseResult.GetValueForOption(envOption);
            var autoLaunch = context.ParseResult.GetValueForOption(autoLaunchOption);
            var autoLaunchArgs = context.ParseResult.GetValueForOption(autoLaunchArgsOption);

            var parsedEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (env != null)
            {
                foreach (var item in env)
                {
                    var parts = item.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        parsedEnv[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            var cdp = new CdpService();
            var connection = new ConnectionViewModel(cdp) { HostAddress = host };
            var testStudio = new TestStudioViewModel(cdp)
            {
                Connection = connection,
                IsRecordVideoEnabled = video,
                IsGenerateReportEnabled = report,
                OutputDirectory = outputDir
            };

            foreach (var kv in parsedEnv)
            {
                testStudio.CliEnv[kv.Key] = kv.Value;
            }

            // Real-time logging of Test Studio steps
            testStudio.Logs.CollectionChanged += (s, ev) =>
            {
                if (ev.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && ev.NewItems != null)
                {
                    foreach (var item in ev.NewItems)
                    {
                        Console.WriteLine(item);
                    }
                }
            };

            try
            {
                if (!string.IsNullOrEmpty(autoLaunch))
                {
                    Console.WriteLine($"Auto-launching application: {autoLaunch}");
                    var launcher = new AppLauncherService();
                    await launcher.AutoLaunchAppAsync(cdp, connection, autoLaunch, autoLaunchArgs ?? "", msg => Console.WriteLine($"[Launcher] {msg}"), CancellationToken.None);
                }

                TargetItem? target = null;
                if (string.IsNullOrEmpty(autoLaunch))
                {
                    target = await ResolveTargetAsync(cdp, host, targetId, targetName);
                    Console.WriteLine($"Connecting to target: {target.Title} (ID: {target.Id})");
                    connection.SelectedTarget = target;
                    await connection.ConnectAsync();
                }
                else
                {
                    // For auto-launched targets, they automatically connect or list targets. Let's wait up to 5s for target
                    int retries = 50;
                    while (!cdp.IsConnected && retries > 0)
                    {
                        await Task.Delay(100);
                        retries--;
                    }
                    if (!cdp.IsConnected)
                    {
                        throw new Exception("Timed out waiting for auto-launched app to connect to CDP.");
                    }
                }

                if (Directory.Exists(testPath))
                {
                    Console.WriteLine($"Running test suite in directory: {testPath}");
                    await testStudio.RunSuite(testPath);
                    Console.WriteLine($"--- Suite Execution Summary ---");
                    Console.WriteLine($"Passed Flows: {testStudio.SuitePassCount}");
                    Console.WriteLine($"Failed Flows: {testStudio.SuiteFailCount}");
                    Environment.ExitCode = testStudio.SuiteFailCount == 0 ? 0 : 1;
                }
                else if (File.Exists(testPath))
                {
                    Console.WriteLine($"Running test flow: {testPath}");
                    testStudio.WorkspaceRootPath = Path.GetDirectoryName(Path.GetFullPath(testPath));
                    testStudio.LoadFlowFile(testPath);
                    
                    var playTask = testStudio.PlayAsync();
                    var delayTask = Task.Delay(timeout);
                    var completedTask = await Task.WhenAny(playTask, delayTask);

                    if (completedTask == delayTask)
                    {
                        testStudio.Stop();
                        var cleanupDelay = Task.Delay(5000);
                        await Task.WhenAny(playTask, cleanupDelay);
                        throw new TimeoutException($"Test flow timed out after {timeout / 1000.0:F1} seconds.");
                    }
                    else
                    {
                        await playTask;
                    }

                    Console.WriteLine($"--- Flow Execution Summary ---");
                    int passed = 0, failed = 0;
                    foreach (var step in testStudio.Steps)
                    {
                        if (step.Status == StepStatus.Passed)
                        {
                            passed++;
                        }
                        else
                        {
                            failed++;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Step Failed: {step.ActionDisplay} - {step.ErrorMessage}");
                            Console.ResetColor();
                        }
                    }
                    Console.WriteLine($"Passed Steps: {passed}");
                    Console.WriteLine($"Failed Steps: {failed}");
                    Environment.ExitCode = failed == 0 ? 0 : 1;
                }
                else
                {
                    throw new FileNotFoundException($"The specified test path '{testPath}' was not found.");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Execution failed: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
            finally
            {
                if (cdp.IsConnected)
                {
                    await cdp.DisconnectAsync();
                }
                try { AppLauncherService.KillAllLaunchedProcesses(); } catch { }
            }
        });
        rootCommand.AddCommand(runCommand);

        // 3. hierarchy command
        var typeOption = new Option<string>("--type", () => "accessibility", "Hierarchy type: accessibility or visual");
        var formatOption = new Option<string>("--format", () => "text", "Output format: text or json");
        var hierarchyCommand = new Command("hierarchy", "Dump visual/accessibility tree of target app")
        {
            hostOption, targetIdOption, targetNameOption, typeOption, formatOption
        };
        hierarchyCommand.SetHandler(async (context) =>
        {
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var targetId = context.ParseResult.GetValueForOption(targetIdOption);
            var targetName = context.ParseResult.GetValueForOption(targetNameOption);
            var type = context.ParseResult.GetValueForOption(typeOption) ?? "accessibility";
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "text";

            var cdp = new CdpService();
            try
            {
                var target = await ResolveTargetAsync(cdp, host, targetId, targetName);
                await cdp.ConnectAsync(host, target);

                if (type.Equals("visual", StringComparison.OrdinalIgnoreCase))
                {
                    var doc = await cdp.SendCommandAsync("DOM.getDocument", new JsonObject { ["depth"] = -1, ["pierce"] = true });
                    var rootNode = doc["root"]?.AsObject();
                    if (rootNode != null)
                    {
                        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(rootNode.ToString());
                        }
                        else
                        {
                            PrintDomNode(rootNode, "");
                        }
                    }
                }
                else
                {
                    var tree = await cdp.SendCommandAsync("Accessibility.getFullAXTree");
                    var nodes = tree["nodes"]?.AsArray();
                    if (nodes != null)
                    {
                        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(nodes.ToString());
                        }
                        else
                        {
                            var nodeMap = new Dictionary<string, JsonObject>();
                            foreach (var node in nodes)
                            {
                                if (node is JsonObject obj && obj.ContainsKey("nodeId"))
                                {
                                    nodeMap[obj["nodeId"]!.ToString()] = obj;
                                }
                            }
                            if (nodes.Count > 0 && nodes[0] is JsonObject rootObj)
                            {
                                PrintAxNode(rootObj, nodeMap, "");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving hierarchy: {ex.Message}");
                Environment.ExitCode = 1;
            }
            finally
            {
                await cdp.DisconnectAsync();
            }
        });
        rootCommand.AddCommand(hierarchyCommand);

        // 4. eval command
        var expressionArg = new Argument<string>("expression", "C# expression to evaluate");
        var evalCommand = new Command("eval", "Evaluate C# expression on the target")
        {
            expressionArg, hostOption, targetIdOption, targetNameOption
        };
        evalCommand.SetHandler(async (context) =>
        {
            var expression = context.ParseResult.GetValueForArgument(expressionArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var targetId = context.ParseResult.GetValueForOption(targetIdOption);
            var targetName = context.ParseResult.GetValueForOption(targetNameOption);

            var cdp = new CdpService();
            try
            {
                var target = await ResolveTargetAsync(cdp, host, targetId, targetName);
                await cdp.ConnectAsync(host, target);
                var response = await cdp.SendCommandAsync("Runtime.evaluate", new JsonObject
                {
                    ["expression"] = expression,
                    ["returnByValue"] = true
                });
                var result = response["result"]?.AsObject();
                var exceptionDetails = response["exceptionDetails"]?.AsObject();

                if (exceptionDetails != null)
                {
                    var text = exceptionDetails["text"]?.ToString() ?? "Exception occurred during evaluation";
                    var exceptionVal = exceptionDetails["exception"]?.AsObject();
                    var desc = exceptionVal != null && exceptionVal.ContainsKey("description")
                        ? exceptionVal["description"]?.ToString()
                        : null;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(desc ?? text);
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }

                if (result != null && result["subtype"]?.ToString() == "error")
                {
                    var desc = result["description"]?.ToString() ?? result.ToString();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(desc);
                    Console.ResetColor();
                    Environment.ExitCode = 1;
                    return;
                }

                if (result != null)
                {
                    if (result.ContainsKey("value"))
                    {
                        Console.WriteLine(result["value"]?.ToString());
                    }
                    else
                    {
                        Console.WriteLine(result.ToString());
                    }
                }
                else
                {
                    Console.WriteLine(response.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Evaluation failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
            finally
            {
                await cdp.DisconnectAsync();
            }
        });
        rootCommand.AddCommand(evalCommand);

        // 5. action command
        var actionCommand = new Command("action", "Simulate a single interaction action on the target")
        {
            hostOption, targetIdOption, targetNameOption
        };

        var tapSelectorArg = new Argument<string>("selector", "Target control selector");
        var tapSub = new Command("tap", "Tap/click on element") { tapSelectorArg };
        tapSub.SetHandler(async (context) =>
        {
            var selector = context.ParseResult.GetValueForArgument(tapSelectorArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var tId = context.ParseResult.GetValueForOption(targetIdOption);
            var tName = context.ParseResult.GetValueForOption(targetNameOption);
            await ExecuteActionAsync("tapOn", selector, null, host, tId, tName);
        });

        var inputSelectorArg = new Argument<string>("selector", "Target control selector");
        var inputTextArg = new Argument<string>("text", "Text to insert");
        var inputSub = new Command("input", "Type text into element") { inputSelectorArg, inputTextArg };
        inputSub.SetHandler(async (context) =>
        {
            var selector = context.ParseResult.GetValueForArgument(inputSelectorArg);
            var text = context.ParseResult.GetValueForArgument(inputTextArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var tId = context.ParseResult.GetValueForOption(targetIdOption);
            var tName = context.ParseResult.GetValueForOption(targetNameOption);
            await ExecuteActionAsync("inputText", selector, text, host, tId, tName);
        });

        var clearSelectorArg = new Argument<string>("selector", "Target control selector");
        var clearSub = new Command("clear", "Clear text in element") { clearSelectorArg };
        clearSub.SetHandler(async (context) =>
        {
            var selector = context.ParseResult.GetValueForArgument(clearSelectorArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var tId = context.ParseResult.GetValueForOption(targetIdOption);
            var tName = context.ParseResult.GetValueForOption(targetNameOption);
            await ExecuteActionAsync("clearText", selector, null, host, tId, tName);
        });

        var assertSelectorArg = new Argument<string>("selector", "Target control selector");
        var assertSub = new Command("assert-visible", "Assert element is visible") { assertSelectorArg };
        assertSub.SetHandler(async (context) =>
        {
            var selector = context.ParseResult.GetValueForArgument(assertSelectorArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var tId = context.ParseResult.GetValueForOption(targetIdOption);
            var tName = context.ParseResult.GetValueForOption(targetNameOption);
            await ExecuteActionAsync("assertVisible", selector, null, host, tId, tName);
        });

        var scrollSelectorArg = new Argument<string>("selector", "Target control selector");
        var scrollDirectionArg = new Argument<string>("direction", "Direction: up, down, left, right");
        var scrollSub = new Command("scroll", "Scroll element") { scrollSelectorArg, scrollDirectionArg };
        scrollSub.SetHandler(async (context) =>
        {
            var selector = context.ParseResult.GetValueForArgument(scrollSelectorArg);
            var direction = context.ParseResult.GetValueForArgument(scrollDirectionArg);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var tId = context.ParseResult.GetValueForOption(targetIdOption);
            var tName = context.ParseResult.GetValueForOption(targetNameOption);
            await ExecuteActionAsync("scroll", selector, $"direction={direction}", host, tId, tName);
        });

        actionCommand.AddCommand(tapSub);
        actionCommand.AddCommand(inputSub);
        actionCommand.AddCommand(clearSub);
        actionCommand.AddCommand(assertSub);
        actionCommand.AddCommand(scrollSub);
        rootCommand.AddCommand(actionCommand);

        // 6. logs command
        var logTypeOption = new Option<string>("--type", () => "all", "Log types to capture: console, network, events, all");
        var logsCommand = new Command("logs", "Stream real-time console log, network requests, and events from target")
        {
            hostOption, targetIdOption, targetNameOption, logTypeOption
        };
        logsCommand.SetHandler(async (context) =>
        {
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var targetId = context.ParseResult.GetValueForOption(targetIdOption);
            var targetName = context.ParseResult.GetValueForOption(targetNameOption);
            var logType = context.ParseResult.GetValueForOption(logTypeOption) ?? "all";

            var cdp = new CdpService();
            try
            {
                var target = await ResolveTargetAsync(cdp, host, targetId, targetName);
                await cdp.ConnectAsync(host, target);

                Console.WriteLine($"Streaming logs from {target.Title} (ID: {target.Id}). Press Ctrl+C to stop...");

                cdp.EventReceived += (s, ev) =>
                {
                    bool show = false;
                    string prefix = "[Event]";
                    if (ev.Method.StartsWith("Console.", StringComparison.OrdinalIgnoreCase))
                    {
                        prefix = "[Console]";
                        show = logType.Equals("all", StringComparison.OrdinalIgnoreCase) || logType.Equals("console", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (ev.Method.StartsWith("Network.", StringComparison.OrdinalIgnoreCase))
                    {
                        prefix = "[Network]";
                        show = logType.Equals("all", StringComparison.OrdinalIgnoreCase) || logType.Equals("network", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        show = logType.Equals("all", StringComparison.OrdinalIgnoreCase) || logType.Equals("events", StringComparison.OrdinalIgnoreCase);
                    }

                    if (show)
                    {
                        Console.WriteLine($"{prefix} {ev.Method}: {ev.Params?.ToString()}");
                    }
                };

                // Enable domain logging
                await cdp.SendCommandAsync("Runtime.enable");
                await cdp.SendCommandAsync("Log.enable");
                await cdp.SendCommandAsync("Network.enable");

                // Sleep indefinitely or until cancellation
                var tcs = new TaskCompletionSource<bool>();
                Console.CancelKeyPress += (s, ev) =>
                {
                    ev.Cancel = true;
                    tcs.TrySetResult(true);
                };
                await tcs.Task;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Log streaming failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
            finally
            {
                await cdp.DisconnectAsync();
            }
        });
        rootCommand.AddCommand(logsCommand);

        // 7. mcp command
        var mcpCommand = new Command("mcp", "Start native MCP stdio server")
        {
            hostOption, targetIdOption, targetNameOption
        };
        mcpCommand.SetHandler(async (context) =>
        {
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var targetId = context.ParseResult.GetValueForOption(targetIdOption);
            var targetName = context.ParseResult.GetValueForOption(targetNameOption);

            var cdp = new CdpService();
            try
            {
                Console.Error.WriteLine($"[MCP] Connecting to target on host: {host}...");
                var target = await ResolveTargetAsync(cdp, host, targetId, targetName);
                Console.Error.WriteLine($"[MCP] Resolved target: {target.Title} (ID: {target.Id})");
                await cdp.ConnectAsync(host, target);
                Console.Error.WriteLine($"[MCP] Connected to CDP. Starting stdio MCP server loop...");

                while (true)
                {
                    var line = await Console.In.ReadLineAsync();
                    if (line == null)
                    {
                        Console.Error.WriteLine("[MCP] Stdin closed (EOF). Exiting...");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var json = JsonNode.Parse(line) as JsonObject;
                        if (json == null)
                        {
                            Console.Error.WriteLine($"[MCP] Invalid JSON received: {line}");
                            continue;
                        }

                        var id = json["id"];
                        var method = json["method"]?.GetValue<string>();
                        var @params = json["params"] as JsonObject;

                        if (method != null)
                        {
                            await HandleMcpMethodAsync(cdp, host, target, id, method, @params);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[MCP] Error handling message: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP Fatal] Connection or runtime failure: {ex.Message}\n{ex.StackTrace}");
                Environment.ExitCode = 1;
            }
            finally
            {
                if (cdp.IsConnected)
                {
                    await cdp.DisconnectAsync();
                }
            }
        });
        rootCommand.AddCommand(mcpCommand);

        // 8. db command
        var dbPathOption = new Option<string>(new[] { "--database", "-d" }, "Path to the target SQLite database");
        var queryArg = new Argument<string>("query", "SQL query to execute");
        var dbCommand = new Command("db", "Execute SQLite SQL query on the target application")
        {
            queryArg, dbPathOption, hostOption, targetIdOption, targetNameOption
        };
        dbCommand.SetHandler(async (context) =>
        {
            var query = context.ParseResult.GetValueForArgument(queryArg);
            var dbPath = context.ParseResult.GetValueForOption(dbPathOption);
            var host = context.ParseResult.GetValueForOption(hostOption) ?? "http://127.0.0.1:9222";
            var targetId = context.ParseResult.GetValueForOption(targetIdOption);
            var targetName = context.ParseResult.GetValueForOption(targetNameOption);

            var cdp = new CdpService();
            try
            {
                var target = await ResolveTargetAsync(cdp, host, targetId, targetName);
                await cdp.ConnectAsync(host, target);

                if (string.IsNullOrEmpty(dbPath))
                {
                    var dbsResponse = await cdp.SendCommandAsync("Application.getDatabases");
                    var databases = dbsResponse["databases"]?.AsArray();
                    if (databases == null || databases.Count == 0)
                    {
                        throw new Exception("No SQLite databases found on the target application. Please specify one using -d/--database.");
                    }

                    if (databases.Count == 1)
                    {
                        dbPath = databases[0]?.GetValue<string>();
                        Console.WriteLine($"Auto-detected database: {dbPath}");
                    }
                    else
                    {
                        Console.WriteLine("Multiple SQLite databases found on the target:");
                        for (int i = 0; i < databases.Count; i++)
                        {
                            Console.WriteLine($"{i + 1}. {databases[i]?.GetValue<string>()}");
                        }
                        throw new Exception("Please specify which database to use via the --database or -d option.");
                    }
                }

                if (string.IsNullOrEmpty(dbPath))
                {
                    throw new Exception("Database path is required.");
                }

                var sqlResult = await cdp.SendCommandAsync("Application.executeSQL", new JsonObject
                {
                    ["databasePath"] = dbPath,
                    ["query"] = query
                });

                var columns = new List<string>();
                var colsNode = sqlResult["columns"]?.AsArray();
                if (colsNode != null)
                {
                    foreach (var col in colsNode)
                    {
                        columns.Add(col?.ToString() ?? "");
                    }
                }

                var rows = new List<List<string>>();
                var rowsNode = sqlResult["rows"]?.AsArray();
                if (rowsNode != null)
                {
                    foreach (var rowVal in rowsNode)
                    {
                        var rowArr = rowVal?.AsArray();
                        var rowList = new List<string>();
                        if (rowArr != null)
                        {
                            foreach (var val in rowArr)
                            {
                                rowList.Add(val?.ToString() ?? "NULL");
                            }
                        }
                        rows.Add(rowList);
                    }
                }

                PrintDataGrid(columns, rows);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Database query failed: {ex.Message}");
                Console.ResetColor();
                Environment.ExitCode = 1;
            }
            finally
            {
                await cdp.DisconnectAsync();
            }
        });
        rootCommand.AddCommand(dbCommand);

        Environment.ExitCode = 0;
        var exitCode = await rootCommand.InvokeAsync(args);
        return exitCode != 0 ? exitCode : Environment.ExitCode;
    }

    private static async Task<TargetItem> ResolveTargetAsync(ICdpService cdp, string host, string? targetId, string? targetName)
    {
        var targets = await cdp.GetTargetsAsync(host);
        if (targets == null || targets.Count == 0)
        {
            throw new Exception($"No active targets found on {host}.");
        }

        if (!string.IsNullOrEmpty(targetId))
        {
            var match = targets.FirstOrDefault(t => t.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new Exception($"Target with ID '{targetId}' was not found.");
            }
            return match;
        }

        if (!string.IsNullOrEmpty(targetName))
        {
            var match = targets.FirstOrDefault(t => t.Title.Contains(targetName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new Exception($"Target with title containing '{targetName}' was not found.");
            }
            return match;
        }

        if (targets.Count == 1)
        {
            return targets[0];
        }

        Console.WriteLine("Warning: Multiple targets available. Auto-connecting to the first target:");
        Console.WriteLine($"-> {targets[0].Title} ({targets[0].Id})");
        return targets[0];
    }

    private static async Task ExecuteActionAsync(string actionName, string selector, string? value, string host, string? targetId, string? targetName)
    {
        var cdp = new CdpService();
        try
        {
            var target = await ResolveTargetAsync(cdp, host, targetId, targetName);
            var connection = new ConnectionViewModel(cdp) { HostAddress = host };
            var testStudio = new TestStudioViewModel(cdp) { Connection = connection };

            connection.SelectedTarget = target;
            await connection.ConnectAsync();

            var step = new TestStudioStepModel
            {
                Action = actionName,
                Selector = selector,
                Value = value
            };

            Console.WriteLine($"Executing Action: {actionName} on selector: {selector} (Value: {value})");
            await testStudio.ExecuteSingleStepAsync(step, CancellationToken.None);
            Console.WriteLine("Action executed successfully.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Action execution failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
        finally
        {
            await cdp.DisconnectAsync();
        }
    }

    private static void PrintDomNode(JsonObject node, string indent)
    {
        var nodeName = node["nodeName"]?.ToString() ?? "";
        var nodeId = node["nodeId"]?.ToString() ?? "";
        var attributes = node["attributes"]?.AsArray();
        
        var attrStr = "";
        if (attributes != null)
        {
            for (int i = 0; i < attributes.Count; i += 2)
            {
                if (i + 1 < attributes.Count)
                {
                    attrStr += $" {attributes[i]}=\"{attributes[i+1]}\"";
                }
            }
        }
        Console.WriteLine($"{indent}<{nodeName}{attrStr}> (ID: {nodeId})");
        
        var children = node["children"]?.AsArray();
        if (children != null)
        {
            foreach (var child in children)
            {
                if (child is JsonObject childObj)
                {
                    PrintDomNode(childObj, indent + "  ");
                }
            }
        }
    }

    private static void PrintAxNode(JsonObject node, Dictionary<string, JsonObject> nodeMap, string indent)
    {
        var role = node["role"]?["value"]?.ToString() ?? node["role"]?.ToString() ?? "node";
        var name = node["name"]?["value"]?.ToString() ?? node["name"]?.ToString() ?? "";
        var value = node["value"]?["value"]?.ToString() ?? "";
        
        Console.WriteLine($"{indent}{role} name=\"{name}\" value=\"{value}\"");
        
        var childIds = node["childIds"]?.AsArray();
        if (childIds != null)
        {
            foreach (var childId in childIds)
            {
                var idStr = childId?.ToString();
                if (idStr != null && nodeMap.TryGetValue(idStr, out var childNode))
                {
                    PrintAxNode(childNode, nodeMap, indent + "  ");
                }
            }
        }
    }

    public static async Task HandleMcpMethodAsync(ICdpService cdp, string host, TargetItem target, JsonNode? id, string method, JsonObject? @params)
    {
        if (method == "initialize")
        {
            var result = new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject()
                },
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = "cdp-mcp-server",
                    ["version"] = "1.0.0"
                }
            };
            SendMcpResponse(id, result);
            return;
        }

        if (method == "tools/list")
        {
            var tools = GetMcpToolsList();
            SendMcpResponse(id, new JsonObject { ["tools"] = tools });
            return;
        }

        if (method == "tools/call")
        {
            if (@params == null)
            {
                SendMcpError(id, -32602, "Missing params for tools/call");
                return;
            }

            var toolName = @params["name"]?.GetValue<string>();
            var toolArgs = @params["arguments"] as JsonObject ?? new JsonObject();

            if (string.IsNullOrEmpty(toolName))
            {
                SendMcpError(id, -32602, "Tool name is required");
                return;
            }

            try
            {
                var response = await ExecuteMcpToolAsync(cdp, host, target, toolName, toolArgs);
                SendMcpResponse(id, response);
            }
            catch (Exception ex)
            {
                SendMcpResponse(id, new JsonObject
                {
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = $"Error executing tool {toolName}: {ex.Message}"
                        }
                    },
                    ["isError"] = true
                });
            }
            return;
        }

        SendMcpError(id, -32601, $"Method not found: {method}");
    }

    public static async Task<JsonObject> ExecuteMcpToolAsync(ICdpService cdp, string host, TargetItem target, string toolName, JsonObject args)
    {
        switch (toolName)
        {
            case "dom_query":
                {
                    var selector = args["selector"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(selector))
                    {
                        throw new Exception("selector argument is required.");
                    }

                    var doc = await cdp.SendCommandAsync("DOM.getDocument", new JsonObject { ["depth"] = -1, ["pierce"] = true });
                    var rootNodeId = doc["root"]?["nodeId"]?.GetValue<int>() ?? 1;

                    var queryResult = await cdp.SendCommandAsync("DOM.querySelector", new JsonObject
                    {
                        ["nodeId"] = rootNodeId,
                        ["selector"] = selector
                    });

                    var nodeId = queryResult["nodeId"]?.GetValue<int>() ?? 0;
                    if (nodeId == 0)
                    {
                        return new JsonObject
                        {
                            ["content"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "text",
                                    ["text"] = $"Element '{selector}' not found."
                                }
                            }
                        };
                    }

                    var descResult = await cdp.SendCommandAsync("DOM.describeNode", new JsonObject
                    {
                        ["nodeId"] = nodeId
                    });
                    var nodeInfo = descResult["node"]?.AsObject();

                    var attrsResult = await cdp.SendCommandAsync("DOM.getAttributes", new JsonObject
                    {
                        ["nodeId"] = nodeId
                    });
                    var attributes = attrsResult["attributes"]?.AsArray();

                    var formattedAttributes = new JsonObject();
                    if (attributes != null)
                    {
                        for (int i = 0; i < attributes.Count; i += 2)
                        {
                            if (i + 1 < attributes.Count)
                            {
                                formattedAttributes[attributes[i]!.ToString()] = attributes[i + 1]?.DeepClone();
                            }
                        }
                    }

                    JsonObject? boxModel = null;
                    try
                    {
                        var boxResult = await cdp.SendCommandAsync("DOM.getBoxModel", new JsonObject
                        {
                            ["nodeId"] = nodeId
                        });
                        boxModel = boxResult["model"]?.AsObject();
                    }
                    catch { }

                    var detailObj = new JsonObject
                    {
                        ["nodeId"] = nodeId,
                        ["nodeName"] = nodeInfo?["nodeName"]?.DeepClone(),
                        ["localName"] = nodeInfo?["localName"]?.DeepClone(),
                        ["attributes"] = formattedAttributes
                    };
                    if (boxModel != null)
                    {
                        detailObj["boxModel"] = boxModel.DeepClone();
                    }

                    return new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = detailObj.ToJsonString()
                            }
                        }
                    };
                }

            case "evaluate":
                {
                    var expression = args["expression"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(expression))
                    {
                        throw new Exception("expression argument is required.");
                    }

                    var response = await cdp.SendCommandAsync("Runtime.evaluate", new JsonObject
                    {
                        ["expression"] = expression,
                        ["returnByValue"] = true
                    });

                    var result = response["result"]?.AsObject();
                    var exceptionDetails = response["exceptionDetails"]?.AsObject();

                    if (exceptionDetails != null)
                    {
                        var text = exceptionDetails["text"]?.ToString() ?? "Exception occurred during evaluation";
                        var exceptionVal = exceptionDetails["exception"]?.AsObject();
                        var desc = exceptionVal != null && exceptionVal.ContainsKey("description")
                            ? exceptionVal["description"]?.ToString()
                            : null;
                        return new JsonObject
                        {
                            ["content"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "text",
                                    ["text"] = $"Evaluation failed: {desc ?? text}"
                                }
                            },
                            ["isError"] = true
                        };
                    }

                    string resultText = "";
                    if (result != null)
                    {
                        if (result.ContainsKey("value"))
                        {
                            resultText = result["value"]?.ToString() ?? "null";
                        }
                        else
                        {
                            resultText = result.ToString();
                        }
                    }
                    else
                    {
                        resultText = response.ToString();
                    }

                    return new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = resultText
                            }
                        }
                    };
                }

            case "screenshot":
                {
                    var response = await cdp.SendCommandAsync("Page.captureScreenshot", new JsonObject());
                    var base64Data = response["data"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(base64Data))
                    {
                        throw new Exception("Failed to capture screenshot - data was null or empty.");
                    }

                    return new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "image",
                                ["data"] = base64Data,
                                ["mimeType"] = "image/png"
                            }
                        }
                    };
                }

            case "tap":
            case "input_text":
            case "clear_text":
            case "scroll":
                {
                    var selector = args["selector"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(selector))
                    {
                        throw new Exception("selector argument is required.");
                    }

                    var connection = new ConnectionViewModel(cdp) { HostAddress = host };
                    var testStudio = new TestStudioViewModel(cdp) { Connection = connection };
                    connection.SelectedTarget = target;
                    await connection.ConnectAsync();

                    string actionName = "";
                    string? value = null;

                    if (toolName == "tap")
                    {
                        actionName = "tapOn";
                    }
                    else if (toolName == "input_text")
                    {
                        actionName = "inputText";
                        value = args["text"]?.GetValue<string>();
                        if (value == null)
                        {
                            throw new Exception("text argument is required for input_text tool.");
                        }
                    }
                    else if (toolName == "clear_text")
                    {
                        actionName = "clearText";
                    }
                    else if (toolName == "scroll")
                    {
                        actionName = "scroll";
                        var dir = args["direction"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(dir))
                        {
                            throw new Exception("direction argument is required for scroll tool.");
                        }
                        value = $"direction={dir}";
                    }

                    var step = new TestStudioStepModel
                    {
                        Action = actionName,
                        Selector = selector,
                        Value = value
                    };

                    await testStudio.ExecuteSingleStepAsync(step, CancellationToken.None);

                    return new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = $"Successfully executed action '{actionName}' on selector '{selector}'."
                            }
                        }
                    };
                }

            default:
                throw new Exception($"Tool '{toolName}' is not implemented.");
        }
    }

    public static JsonArray GetMcpToolsList()
    {
        return new JsonArray
        {
            new JsonObject
            {
                ["name"] = "dom_query",
                ["description"] = "Query elements in the target application using a selector and returns their details",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["selector"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The CSS-like selector (e.g., '#btnClickMe', '[Name=\\\"textBox1\\\"]')"
                        }
                    },
                    ["required"] = new JsonArray { "selector" }
                }
            },
            new JsonObject
            {
                ["name"] = "evaluate",
                ["description"] = "Evaluates a C# expression on the target application",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["expression"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "C# expression to evaluate (e.g., 'Window.Title')"
                        }
                    },
                    ["required"] = new JsonArray { "expression" }
                }
            },
            new JsonObject
            {
                ["name"] = "screenshot",
                ["description"] = "Captures a screenshot of the target application window",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject()
                }
            },
            new JsonObject
            {
                ["name"] = "tap",
                ["description"] = "Simulates a click/tap on the element matching the selector",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["selector"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Selector of the control to tap"
                        }
                    },
                    ["required"] = new JsonArray { "selector" }
                }
            },
            new JsonObject
            {
                ["name"] = "input_text",
                ["description"] = "Types text into the element matching the selector",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["selector"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Selector of the control to type into"
                        },
                        ["text"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The text to insert"
                        }
                    },
                    ["required"] = new JsonArray { "selector", "text" }
                }
            },
            new JsonObject
            {
                ["name"] = "clear_text",
                ["description"] = "Clears text of the element matching the selector",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["selector"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Selector of the control to clear"
                        }
                    },
                    ["required"] = new JsonArray { "selector" }
                }
            },
            new JsonObject
            {
                ["name"] = "scroll",
                ["description"] = "Scrolls the element matching the selector in a given direction",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["selector"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Selector of the control to scroll"
                        },
                        ["direction"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Direction (up, down, left, right)"
                        }
                    },
                    ["required"] = new JsonArray { "selector", "direction" }
                }
            }
        };
    }

    private static void SendMcpResponse(JsonNode? id, JsonObject result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result
        };
        Console.WriteLine(response.ToJsonString());
    }

    private static void SendMcpError(JsonNode? id, int code, string message)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
        Console.WriteLine(response.ToJsonString());
    }

    private static void PrintDataGrid(List<string> columns, List<List<string>> rows)
    {
        if (columns == null || columns.Count == 0)
        {
            Console.WriteLine("No columns found.");
            return;
        }

        int[] widths = new int[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            widths[i] = columns[i].Length;
        }

        foreach (var row in rows)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (i < row.Count && row[i] != null)
                {
                    widths[i] = Math.Max(widths[i], row[i].Length);
                }
            }
        }

        void PrintDivider()
        {
            Console.Write("+");
            for (int i = 0; i < columns.Count; i++)
            {
                Console.Write(new string('-', widths[i] + 2));
                Console.Write("+");
            }
            Console.WriteLine();
        }

        PrintDivider();
        Console.Write("|");
        for (int i = 0; i < columns.Count; i++)
        {
            Console.Write($" {columns[i].PadRight(widths[i])} |");
        }
        Console.WriteLine();
        PrintDivider();

        foreach (var row in rows)
        {
            Console.Write("|");
            for (int i = 0; i < columns.Count; i++)
            {
                string val = (i < row.Count && row[i] != null) ? row[i] : "";
                Console.Write($" {val.PadRight(widths[i])} |");
            }
            Console.WriteLine();
        }
        PrintDivider();
    }
}

public class CliApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            Task.Run(async () =>
            {
                int exitCode = 0;
                try
                {
                    exitCode = await Program.RunCliAsync(desktop.Args ?? Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Unhandled CLI error: {ex}");
                    Console.ResetColor();
                    exitCode = 1;
                }
                finally
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown(exitCode));
                }
            });
        }
        
        base.OnFrameworkInitializationCompleted();
    }
}
