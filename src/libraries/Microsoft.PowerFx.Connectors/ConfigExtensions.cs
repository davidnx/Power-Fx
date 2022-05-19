// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.PowerFx.Connectors;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx
{
    public static class ConfigExtensions
    {
        /// <summary>
        /// Add functions for each operation in the <see cref="OpenApiDocument"/>. 
        /// Functions names will be 'functionNamespace.operationName'.
        /// Functions are invoked via rest via the httpClient. The client must handle authentication. 
        /// </summary>
        /// <param name="config"></param>
        /// <param name="functionNamespace"></param>
        /// <param name="openApiDocument"></param>
        /// <param name="httpClient"></param>
        /// <param name="cache"></param>
        public static IReadOnlyList<FunctionInfo> AddService(this PowerFxConfig config, string functionNamespace, OpenApiDocument openApiDocument, HttpMessageInvoker httpClient, ICachingHttpClient cache = null)
        {
            if (openApiDocument == null)
            {
                throw new ArgumentNullException(nameof(openApiDocument));
            }

            if (string.IsNullOrWhiteSpace(functionNamespace))
            {
                throw new ArgumentException(nameof(functionNamespace));
            }

            var newFunctions = new List<FunctionInfo>();

            string basePath = null;
            if (openApiDocument?.Servers.Count == 1)
            {
                // This is a full URL that will pull in 'basePath' property from connectors. 
                // Extract BasePath back out from this. 
                var fullPath = openApiDocument.Servers[0].Url;
                var uri = new Uri(fullPath);
                basePath = uri.PathAndQuery;
            }

            var model = new OpenApiModel(openApiDocument);

            foreach (var kv in openApiDocument.Paths)
            {
                var path = kv.Key;
                var ops = kv.Value;

                foreach (var kv2 in ops.Operations)
                {
                    var verb = model.ToMethod(kv2.Key); // "GET"
                    var op = kv2.Value;

                    var isTrigger = op.Extensions.ContainsKey("x-ms-trigger");
                    if (isTrigger)
                    {
                        continue;
                    }

                    // $$$ Pull description/summary for intellisense? (get for ServiceFunction)
                    var operationName = op.OperationId ?? path.Replace("/", string.Empty);

                    var paramTypes = new List<FormulaType>();
                    var requiredParams = new List<OpenApiParameter>();

                    foreach (var param in op.Parameters)
                    {
                        var exts = param.Extensions;
                        if (exts.TryGetValue("x-ms-visibility", out var val))
                        {
                            if (val is OpenApiString valStr)
                            {
                                if (valStr.Value == "internal")
                                {
                                    // connectionId is a well-known internal param, stamped later. 
                                    if (param.Name != "connectionId")
                                    {
                                        throw new NotImplementedException();
                                    }

                                    continue;
                                }
                            }
                        }

                        var paramType = model.ToType(param.Schema);
                        paramTypes.Add(paramType);
                        requiredParams.Add(param);
                    }

                    if (op.RequestBody != null)
                    {
                        // RequestBody can be ambiguous- treat as a single record? splat as parameters?
                        // $$$ Pull this impl from ServiceFunction
                        throw new NotImplementedException($"Request body");
                    }

                    var returnType = model.GetReturnType(op);
                    
                    if (basePath != null)
                    {
                        path = basePath + path;
                    }

                    var invoker = (httpClient == null) ? 
                        null : 
                        new HttpFunctionInvoker(httpClient, verb, path, returnType, requiredParams.ToArray(), cache);

                    var function = new ServiceFunction(functionNamespace, operationName, returnType, paramTypes.ToArray())
                    {
                        _invoker = invoker
                    };

                    config.AddFunction(function);
                    newFunctions.Add(new FunctionInfo(function));
                }
            }

            return newFunctions;
        }
    }
}
