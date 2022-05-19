// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.OpenApi.Models;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx.Connectors
{
    internal class OpenApiParser
    {
        // Parse an OpenApiDocument and return functions. 
        public static List<ServiceFunction> Parse(
            string functionNamespace, 
            OpenApiDocument openApiDocument, 
            HttpMessageInvoker httpClient = null, 
            ICachingHttpClient cache = null)
        {
            if (openApiDocument == null)
            {
                throw new ArgumentNullException(nameof(openApiDocument));
            }

            if (string.IsNullOrWhiteSpace(functionNamespace))
            {
                throw new ArgumentException(nameof(functionNamespace));
            }

            var newFunctions = new List<ServiceFunction>();

            var basePath = openApiDocument.GetBasePath();
            
            foreach (var kv in openApiDocument.Paths)
            {
                var path = kv.Key;
                var ops = kv.Value;

                foreach (var kv2 in ops.Operations)
                {
                    var verb = kv2.Key.ToHttpMethod(); // "GET"
                    var op = kv2.Value;

                    if (op.IsTrigger())
                    {
                        continue;
                    }

                    // $$$ Pull description/summary for intellisense? (get for ServiceFunction)
                    var operationName = op.OperationId ?? path.Replace("/", string.Empty);

                    var paramTypes = new List<FormulaType>();
                    var requiredParams = new List<OpenApiParameter>();

                    foreach (var param in op.Parameters)
                    {
                        if (param.IsInternal())
                        {
                            continue;
                        }
                        
                        var paramType = param.Schema.ToFormulaType();
                        paramTypes.Add(paramType);
                        requiredParams.Add(param);
                    }

                    if (op.RequestBody != null)
                    {
                        // RequestBody can be ambiguous- treat as a single record? splat as parameters?
                        // $$$ Pull this impl from ServiceFunction
                        throw new NotImplementedException($"Request body");
                    }

                    var returnType = op.GetReturnType();

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

                    newFunctions.Add(function);
                }
            }

            return newFunctions;
        }
    }
}
