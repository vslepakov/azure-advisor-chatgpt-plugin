using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;

namespace Extensions
{
    public  class DefaultAzureCredentialsAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly TokenRequestContext _tokenRequestContext;
        private readonly DefaultAzureCredential _credentials;

        public DefaultAzureCredentialsAuthorizationMessageHandler()
        {
            _tokenRequestContext = new(new[] { "https://management.azure.com/.default" });
            _credentials = new DefaultAzureCredential();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var tokenResult = await _credentials.GetTokenAsync(_tokenRequestContext, cancellationToken).ConfigureAwait(false);
            var authorizationHeader = new AuthenticationHeaderValue("Bearer", tokenResult.Token);
            request.Headers.Authorization = authorizationHeader;
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
