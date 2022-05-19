// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Core.Tests;
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
            var testConnector = new LoggingTestServer(@"Swagger\TestOpenAPI.json");

            var httpClient = new HttpClient(testConnector)
            {
                BaseAddress = _fakeBaseAddress
            };

            var config = new PowerFxConfig();
            var apiDoc = testConnector._apiDocument;
            
            config.AddService("Test", apiDoc, httpClient);

            var engine = new RecalcEngine(config);

            testConnector.SetResponse("55");
            var r1 = await engine.EvalAsync("Test.GetKey(\"Key1\")", CancellationToken.None);            
            Assert.Equal(55.0, r1.ToObject());

            var log = testConnector._log.ToString().Trim();
            Assert.Equal("GET http://localhost:5000/Keys?keyName=Key1", log);
        }

        // We can bind without calling.
        // In this case, w edon't needd a http client at all.
        [Fact]
        public void BasicHttpBinding()
        {
            var config = new PowerFxConfig();
            var apiDoc = Helpers.ReadSwagger(@"Swagger\TestOpenAPI.json");

            // If we don't pass httpClient, we can still bind, we just can't invoke.
            config.AddService("Test", apiDoc, null);

            var engine = new Engine(config);

            var r1 = engine.Check("Test.GetKey(\"Key1\")");
            Assert.True(r1.IsSuccess);
        }
    }
}
