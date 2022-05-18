// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.OpenApi.Models;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx.Connectors
{
    // Given Power Fx arguments, translate into a HttpRequestMessage and invoke.
    internal class HttpFunctionInvoker
    {
        private readonly HttpMessageInvoker _httpClient;
        private readonly HttpMethod _method;
        private readonly string _path;
        private readonly OpenApiParameter[] _parameters;
        private readonly FormulaType _returnType;
        private readonly ICachingHttpClient _cache;

        // Parameters must match function args exactly. 
        public HttpFunctionInvoker(HttpMessageInvoker httpClient, HttpMethod method, string path, FormulaType returnType, OpenApiParameter[] parameters, ICachingHttpClient cache = null)
        {
            _httpClient = httpClient;
            _method = method;
            _path = path;
            _parameters = parameters;
            _cache = cache ?? NonCachingClient.Instance;
            _returnType = returnType;
        }

        public HttpRequestMessage BuildRequest(FormulaValue[] args)
        {
            var path = _path;
                        
            var query = new StringBuilder();

            for (var i = 0; i < _parameters.Length; i++)
            {
                var value = args[i].ToObject().ToString();

                var param = _parameters[i];
                switch (param.In.Value)
                {
                    case ParameterLocation.Path:
                        path = path.Replace("{" + param.Name + "}", HttpUtility.UrlEncode(value));
                        break;

                    case ParameterLocation.Query:
                        query.Append((query.Length == 0) ? "?" : "&");
                        query.Append(param.Name);
                        query.Append('=');
                        query.Append(HttpUtility.UrlEncode(value));
                        break;

                    default:
                        throw new NotImplementedException($"{param.In}");
                }
            }

            var url = path + query.ToString();

            // $$$ Not handling Body yet...
            var request = new HttpRequestMessage(_method, url);
            return request;
        }

        public async Task<FormulaValue> DecodeResponseAsync(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var msg = $"Connector called failed {response.StatusCode}): " + json;

                // $$$ Do any connectors have 40x behavior here in their response code?
                // or 201 long-ops behavior?

                // $$$ Still type this. 
                return FormulaValue.NewError(new ExpressionError
                {
                    Kind = ErrorKind.Unknown,
                    Message = msg
                });
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return FormulaValue.NewBlank(_returnType);
            }

            // $$$ Proper marshalling?,  use _returnType;
            var result = FormulaValue.FromJson(json);            

            return result;
        }

        public async Task<FormulaValue> InvokeAsync(string cacheScope, CancellationToken cancel, FormulaValue[] args)
        {
            FormulaValue result;
            var request = BuildRequest(args);

            var key = request.RequestUri.ToString();

            if (request.Method != HttpMethod.Get)
            {
                _cache.Reset(cacheScope);
                key = null; // don't bother caching
            }
                        
            var result2 = await _cache.TryGetAsync(cacheScope, key, async () =>
            {
                var response = await _httpClient.SendAsync(request, cancel);
                result = await DecodeResponseAsync(response);
                return result;
            });

            return result2;
        }
    }
}
