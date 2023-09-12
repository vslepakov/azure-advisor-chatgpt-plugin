using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Extensions
{
    internal static class HttpRequestDataExtensions
    {
        internal static HttpResponseData CreateErrorResponse(this HttpRequestData req, HttpStatusCode status, string errorMessage)
        {
            var response = req.CreateResponse(status);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString(errorMessage);

            return response;
        }

        internal static async Task<HttpResponseData> CreateTextResponseAsync(this HttpRequestData req, string payload)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain;charset=utf-8");
            await response.WriteStringAsync(payload);

            return response;
        }
    }
}
