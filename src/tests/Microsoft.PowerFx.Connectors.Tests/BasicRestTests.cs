// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.PowerFx.Connectors;
using Microsoft.PowerFx.Core.Tests;
using Microsoft.PowerFx.Types;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.PowerFx.Tests
{
    // Simulate calling basic REST services, such as ASP.Net + Swashbuckle. 
    public class BasicRestTests : PowerFxTest
    {
        // Must set the BaseAddress on an httpClient, even if we don't actually use it. 
        // All the Send() methods will enforce this. 
        private static readonly Uri _fakeBaseAddress = new Uri("http://localhost:5000");
                
        [Fact]
        public async Task BasicHttpCall()
        {
            var testConnector = new TestServer();
            var httpClient = new HttpClient(testConnector)
            {
                BaseAddress = _fakeBaseAddress
            };

            var config = new PowerFxConfig();
            var doc = testConnector.GetSwagger();
            
            config.AddService("Test", doc, httpClient);

            var engine = new RecalcEngine(config);
            var r1 = await engine.EvalAsync("Test.GetKey(\"Key1\")", CancellationToken.None);            
            Assert.Equal(TestServer.ReturnValue, r1.ToObject());

            var log = testConnector._log.ToString().Trim();
            Assert.Equal("GET http://localhost:5000/Keys?keyName=Key1", log);
        }

        // Simulate a test connector. 
        private class TestServer : HttpMessageHandler
        {
            public StringBuilder _log = new StringBuilder();

            public OpenApiDocument GetSwagger()
            {
                return Helpers.ReadSwagger(@"Swagger\TestOpenAPI.json");
            }

            public static double ReturnValue = 55.0;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var method = request.Method;
                var url = request.RequestUri.ToString();

                _log.AppendLine($"{method} {url}");

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var json = JsonConvert.SerializeObject(ReturnValue);
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");

                return response;
            }
        }
    }
}
