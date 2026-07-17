using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDP.Integration.Tests;

public class MockHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private Task? _listenTask;

    public string Url { get; }

    public MockHttpServer()
    {
        _listener = new HttpListener();
        _cts = new CancellationTokenSource();

        // Dynamically find a free port
        int port = GetFreePort();
        Url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Url);
    }

    private static int GetFreePort()
    {
        return PortFinderHelper.GetFreePort();
    }

    public void Start()
    {
        _listener.Start();
        _listenTask = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested && _listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context));
            }
        }
        catch (ObjectDisposedException) { }
        catch (HttpListenerException) { }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath ?? "";
        var query = request.Url?.Query ?? "";
        var method = request.HttpMethod;

        string responseString = "{}";
        int statusCode = 200;

        try
        {
            // Read input body to ensure request stream is consumed
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                await reader.ReadToEndAsync();
            }

            // TestMo Mocks
            if (path.Contains("/api/v1/projects") && method == "GET")
            {
                if (path.Contains("/milestones"))
                {
                    responseString = "[{\"id\": \"ms-1\", \"name\": \"Milestone 1\"}]";
                }
                else if (path.EndsWith("/runs"))
                {
                    responseString = "[{\"id\": \"plan-1\", \"name\": \"Plan 1\"}]";
                }
                else if (path.Contains("/configurations"))
                {
                    responseString = "[{\"id\": \"cfg-1\", \"name\": \"Config 1\"}]";
                }
                else
                {
                    responseString = "[{\"id\": \"proj-1\", \"name\": \"Project 1\"}]";
                }
            }
            else if (path.Contains("/api/v1/projects/proj-1/cases") && method == "POST")
            {
                responseString = "{\"id\": \"case-123\"}";
            }
            else if (path.Contains("/api/v1/projects/proj-1/runs") && method == "POST")
            {
                responseString = "{\"id\": \"run-456\"}";
            }
            else if (path.Contains("/api/v1/runs/run-456/results") && method == "POST")
            {
                responseString = "{\"id\": \"result-789\"}";
            }
            else if (path.Contains("/api/v1/results/result-789/attachments") && method == "POST")
            {
                responseString = "{\"id\": \"attach-111\"}";
            }

            // TestRail Mocks
            else if (path.Contains("/index.php") && query.Contains("api/v2/get_projects") && method == "GET")
            {
                responseString = "[{\"id\": 1, \"name\": \"Project 1\"}]";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/get_milestones/") && method == "GET")
            {
                responseString = "[{\"id\": 101, \"name\": \"Milestone 1\"}]";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/get_plans/") && method == "GET")
            {
                responseString = "[{\"id\": 201, \"name\": \"Plan 1\"}]";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/get_configs/") && method == "GET")
            {
                responseString = "[{\"id\": 301, \"name\": \"Config Group 1\", \"configs\": [{\"id\": 1, \"name\": \"Config 1\"}]}]";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/add_plan_entry/") && method == "POST")
            {
                responseString = "{\"id\": 444, \"runs\": [{\"id\": 456}]}";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/add_case/") && method == "POST")
            {
                responseString = "{\"id\": 123}";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/add_run/") && method == "POST")
            {
                responseString = "{\"id\": 456}";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/add_result_for_case/") && method == "POST")
            {
                responseString = "{\"id\": 789}";
            }
            else if (path.Contains("/index.php") && query.Contains("api/v2/add_attachment_to_result/") && method == "POST")
            {
                responseString = "{\"id\": 111}";
            }

            // Qase Mocks
            else if (path.Contains("/v1/project") && method == "GET")
            {
                responseString = "{\"status\": true, \"result\": {\"entities\": [{\"code\": \"PRJ\"}]}}";
            }
            else if (path.Contains("/v1/milestone/PRJ") && method == "GET")
            {
                responseString = "{\"status\": true, \"result\": {\"entities\": [{\"id\": 101, \"title\": \"Milestone 1\"}]}}";
            }
            else if (path.Contains("/v1/plan/PRJ") && method == "GET")
            {
                responseString = "{\"status\": true, \"result\": {\"entities\": [{\"id\": 201, \"title\": \"Plan 1\"}]}}";
            }
            else if (path.Contains("/v1/configuration/PRJ") && method == "GET")
            {
                responseString = "{\"status\": true, \"result\": [{\"id\": 301, \"title\": \"Group 1\", \"configurations\": [{\"id\": 1, \"title\": \"Config 1\"}]}]}";
            }
            else if (path.Contains("/v1/case/PRJ") && method == "POST")
            {
                responseString = "{\"status\": true, \"result\": {\"id\": 123}}";
            }
            else if (path.Contains("/v1/run/PRJ") && method == "POST")
            {
                responseString = "{\"status\": true, \"result\": {\"id\": 456}}";
            }
            else if (path.Contains("/v1/attachment/PRJ") && method == "POST")
            {
                responseString = "{\"status\": true, \"result\": [{\"hash\": \"attach-hash\"}]}";
            }
            else if (path.Contains("/v1/result/PRJ/456") && method == "POST")
            {
                responseString = "{\"status\": true, \"result\": {\"hash\": \"res-hash\"}}";
            }

            // Xray Mocks
            else if (path.Contains("/api/v2/authenticate") && method == "POST")
            {
                responseString = "\"mock-token\"";
            }
            else if (path.Contains("/api/v2/jira/projects/PRJ/versions") && method == "GET")
            {
                responseString = "[{\"id\": \"v-1\", \"name\": \"Version 1\"}]";
            }
            else if (path.Contains("/api/v2/jira/projects/PRJ/testplans") && method == "GET")
            {
                responseString = "[{\"key\": \"plan-1\", \"summary\": \"Plan 1\"}]";
            }
            else if (path.Contains("/api/v2/jira/projects/PRJ/environments") && method == "GET")
            {
                responseString = "[{\"name\": \"Env 1\"}]";
            }
            else if (path.Contains("/api/v2/import/test") && method == "POST")
            {
                responseString = "{\"id\": \"TEST-123\"}";
            }
            else if (path.Contains("/api/v2/import/execution") && method == "POST")
            {
                responseString = "{\"id\": \"EXEC-456\"}";
            }

            // Zephyr Mocks
            else if (path.Contains("/v1/testcases") && method == "GET")
            {
                responseString = "[]";
            }
            else if (path.Contains("/v1/testcases") && method == "POST")
            {
                responseString = "{\"key\": \"ZEP-123\"}";
            }
            else if (path.Contains("/v1/testcycles") && method == "GET")
            {
                responseString = "{\"values\": [{\"key\": \"cycle-1\", \"name\": \"Cycle 1\"}]}";
            }
            else if (path.Contains("/v1/testplans") && method == "GET")
            {
                responseString = "{\"values\": [{\"key\": \"plan-1\", \"name\": \"Plan 1\"}]}";
            }
            else if (path.Contains("/v1/environments") && method == "GET")
            {
                responseString = "{\"values\": [{\"name\": \"Env 1\"}]}";
            }
            else if (path.Contains("/v1/testcycles") && method == "POST")
            {
                responseString = "{\"key\": \"ZEP-RUN\"}";
            }
            else if (path.Contains("/v1/testruns") && method == "POST")
            {
                responseString = "{\"key\": \"ZEP-RUN\"}";
            }
            else if (path.Contains("/v1/testruns/ZEP-RUN/testresults") && method == "POST")
            {
                responseString = "[]";
            }
            else
            {
                statusCode = 404;
                responseString = "{\"error\": \"Not Found\"}";
            }
        }
        catch (Exception ex)
        {
            statusCode = 500;
            responseString = $"{{\"error\": \"{ex.Message}\"}}";
        }

        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;

        try
        {
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch { }
        _cts.Dispose();
    }
}

// Helper to resolve free port (we override/cast standard endpoint)
internal static class PortFinderHelper
{
    public static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
