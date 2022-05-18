// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerFx.Connectors
{
    /// <summary>
    /// Http handler to invoke Power Platform connectors. 
    /// </summary>
    public class PowerPlatformConnectorClient : HttpClient
    {
        private readonly HttpClient _client = new HttpClient(); // $$$ Share

        public string ConnectionId;

        public PowerPlatformConnectorClient(string endpoint)
        {
            Endpoint = endpoint;
            _client.BaseAddress = new Uri("https://" + endpoint); // $$$ Breaks sharing!

            BaseAddress = _client.BaseAddress; // Needed for callers.  
        }

        public string Endpoint;

        // Stamp on request.  Need to refresh?
        public string AuthToken; // $$$ NEeds to be callback..

        public string EnvironmentId;

        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri.ToString();
            if (url[0] != '/')
            {
                // Client has Basepath set. 
                // x-ms-request-url needs relative URL. 
                throw new InvalidOperationException($"URL should be relative for x-ms-request-url property");
            }

            url = url.Replace("{connectionId}", ConnectionId);

            var method = request.Method;
            var useCache = method == HttpMethod.Get;
            
            var req = new HttpRequestMessage(HttpMethod.Post, $"https://{Endpoint}/invoke");
            req.Headers.Add("authority", Endpoint);
            req.Headers.Add("scheme", "https");
            req.Headers.Add("path", "/invoke");
            req.Headers.Add("x-ms-client-session-id", "f4d37a97-f1c7-4c8c-80a6-f300c651568d");
            req.Headers.Add("x-ms-request-method", method.ToString());
            req.Headers.Add("authorization", "Bearer " + AuthToken);
            req.Headers.Add("x-ms-client-environment-id", "/providers/Microsoft.PowerApps/environments/" + EnvironmentId);
            req.Headers.Add("x-ms-user-agent", "PowerApps/3.21124.0 (Web AuthoringTool; AppName=<NonCloudApp>)");

            req.Headers.Add("x-ms-request-url", url); 

            // $$$ Body? 

            // $$$ Need to sue basePath

            var response = await _client.SendAsync(req, cancellationToken);
            return response;
        }
    }
}
