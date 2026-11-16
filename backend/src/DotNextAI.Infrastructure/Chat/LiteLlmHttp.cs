using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNextAI.Application.Chat;
using DotNextAI.Infrastructure.Options;

namespace DotNextAI.Infrastructure.Chat;

/// <summary>
/// Direct JSON calls to the LiteLLM proxy for the routes the OpenAI SDK does not cover well (model list, image
/// generation): the key as bearer, <c>Llm:TimeoutSeconds</c>, and failures mapped like the chat's. No retries.
/// </summary>
internal static class LiteLlmHttp
{
    /// <summary>Sends the request and returns the body of a 2xx answer.</summary>
    /// <param name="path">Relative to <c>Llm:Endpoint</c>, e.g. "models" (same convention as the OpenAI SDK).</param>
    /// <param name="model">Model named in failure messages.</param>
    /// <exception cref="ChatServiceException">Any failure, mapped by <see cref="ChatFailureMapper"/>.</exception>
    public static async Task<string> SendAsync(
        IHttpClientFactory httpClientFactory,
        LlmOptions options,
        HttpMethod method,
        string path,
        object? jsonBody,
        string model,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.Endpoint!), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (jsonBody is not null)
        {
            request.Content = JsonContent.Create(jsonBody, options: JsonSerializerOptions.Web);
        }

        try
        {
            using var response = await httpClientFactory.CreateClient(LlmChatClientFactory.HttpClientName).SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw ChatFailureMapper.FromStatus(
                    (int)response.StatusCode,
                    ProxyErrorDetail.FromBody(body, options.ApiKey),
                    model,
                    errorType: ProxyErrorDetail.ErrorType(body));
            }

            return body;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw ChatFailureMapper.Timeout(options, ex);
        }
        catch (HttpRequestException ex)
        {
            throw ChatFailureMapper.Unreachable(ex);
        }
    }
}
