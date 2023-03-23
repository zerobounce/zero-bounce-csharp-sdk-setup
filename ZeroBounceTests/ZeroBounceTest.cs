using ZeroBounceSDK;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Headers;

namespace ZeroBounceTests;

class ZeroBounceTest : ZeroBounce
{
    protected static readonly new ZeroBounceTest _instance = new();
    public static new ZeroBounceTest Instance => _instance;

    public void SetHttpClient(HttpClient client)
    {
        _client = client;
    }

    public void MockResponse(string response)
    {

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        // Setup Protected method on HttpMessageHandler mock.
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                HttpResponseMessage httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response)
                };
                httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                return httpResponse;
            });

        _instance.SetHttpClient(new HttpClient(mockHttpMessageHandler.Object));
    }
}