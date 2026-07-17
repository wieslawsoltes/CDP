using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chrome.DevTools.Protocol.Domains;
using Microsoft.Extensions.Logging;

namespace Chrome.DevTools.Protocol;

public class CdpDelegatingHandler : DelegatingHandler
{
    private static readonly ILogger Logger = CdpLogging.CreateLogger<CdpDelegatingHandler>();
    public CdpDelegatingHandler() : base()
    {
    }

    public CdpDelegatingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Offline Check
        if (Chrome.DevTools.Protocol.Domains.NetworkDomain.Offline)
        {
            throw new HttpRequestException("Network is offline (emulated by CDP).");
        }

        // 2. Latency Delay
        if (Chrome.DevTools.Protocol.Domains.NetworkDomain.Latency > 0)
        {
            await Task.Delay((int)Chrome.DevTools.Protocol.Domains.NetworkDomain.Latency, cancellationToken).ConfigureAwait(false);
        }

        // 3. URL Blocking Check
        var requestUrl = request.RequestUri?.ToString() ?? "";
        if (Chrome.DevTools.Protocol.Domains.NetworkDomain.IsBlocked(requestUrl))
        {
            throw new HttpRequestException("Request blocked by CDP.");
        }

        // 4. Fetch Interception
        if (FetchDomain.IsEnabled && FetchDomain.ShouldIntercept(request, out _))
        {
            var interceptionId = $"intercept-{Guid.NewGuid():N}";
            var tcs = new TaskCompletionSource<InterceptResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            FetchDomain.RegisterPendingInterception(interceptionId, tcs);

            try
            {
                FetchDomain.BroadcastRequestPaused(interceptionId, request);

                // Await TCS with timeout (default 30 seconds to prevent hanging if client disconnects or forgets to respond)
                using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    delayCts.CancelAfter(TimeSpan.FromSeconds(30));

                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.InfiniteTimeSpan, delayCts.Token)).ConfigureAwait(false);
                    if (completedTask == tcs.Task)
                    {
                        var result = await tcs.Task.ConfigureAwait(false);
                        Logger.LogInfoMessage("CdpDelegatingHandler", $"Intercept result completed with Action: {result.Action} for url: {requestUrl}");
                        switch (result.Action)
                        {
                            case InterceptAction.Fulfill:
                                return BuildMockResponse(request, result);
                            case InterceptAction.Fail:
                                throw new HttpRequestException($"Request failed by CDP: {result.ErrorReason}");
                            case InterceptAction.Continue:
                                if (!string.IsNullOrEmpty(result.ModifiedUrl))
                                {
                                    request.RequestUri = new Uri(result.ModifiedUrl);
                                }
                                if (!string.IsNullOrEmpty(result.ModifiedMethod))
                                {
                                    request.Method = new HttpMethod(result.ModifiedMethod);
                                }
                                if (result.ModifiedHeaders != null)
                                {
                                    foreach (var h in result.ModifiedHeaders)
                                    {
                                        request.Headers.Remove(h.Key);
                                        request.Headers.TryAddWithoutValidation(h.Key, h.Value);
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        Logger.LogWarningMessage("CdpDelegatingHandler", $"Intercept timed out or was cancelled for url: {requestUrl}! completedTask was not tcs.Task.");
                    }
                }
            }
            finally
            {
                FetchDomain.RemovePendingInterception(interceptionId);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private HttpResponseMessage BuildMockResponse(HttpRequestMessage request, InterceptResult result)
    {
        var response = new HttpResponseMessage((HttpStatusCode)result.ResponseCode)
        {
            RequestMessage = request
        };

        if (result.BodyBytes != null)
        {
            response.Content = new ByteArrayContent(result.BodyBytes);
        }
        else
        {
            response.Content = new StringContent(string.Empty);
        }

        if (result.ResponseHeaders != null)
        {
            foreach (var header in result.ResponseHeaders)
            {
                if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        return response;
    }
}
