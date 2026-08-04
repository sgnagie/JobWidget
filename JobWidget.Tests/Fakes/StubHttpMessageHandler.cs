using System.Net;
using System.Net.Http;

namespace JobWidget.Tests.Fakes
{
    /// <summary>
    /// Returns canned responses instead of making real HTTP calls.
    /// Queue up one response per expected request.
    /// </summary>
    public class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public int CallCount { get; private set; }
        public string? LastRequestUri { get; private set; }

        public StubHttpMessageHandler RespondWith(HttpStatusCode status, string body = "")
        {
            _responses.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
            return this;
        }

        public StubHttpMessageHandler RespondWithJson(string json)
            => RespondWith(HttpStatusCode.OK, json);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri?.AbsoluteUri;
            
            if (_responses.Count == 0)
                throw new InvalidOperationException(
                    "StubHttpMessageHandler received an unexpected request.");

            return Task.FromResult(_responses.Dequeue());
        }
    }
}