// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.PowerFx.Connectors;
using Microsoft.PowerFx.Core.Functions;
using Microsoft.PowerFx.Core.Localization;
using Microsoft.PowerFx.Core.Types;
using Microsoft.PowerFx.Core.Utils;
using Microsoft.PowerFx.Types;
using static Microsoft.PowerFx.Core.Localization.TexlStrings;

namespace Microsoft.PowerFx
{
    public static class ConfigExtensions
    {
        // - Must be HttpClient since we need to set the BaseAddress
        // $$$ HttpClient only needed if we want to invoke... (not needed for binding)

        // $$$ Return the functions that we did add?
        public static void AddService(this PowerFxConfig config, string functionNamespace, OpenApiDocument openApiDocument, HttpClient httpClient)
        {
            if (openApiDocument == null)
            {
                throw new ArgumentNullException(nameof(openApiDocument));
            }

            if (string.IsNullOrWhiteSpace(functionNamespace))
            {
                throw new ArgumentException(nameof(functionNamespace));
            }

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

                    var returnType = model.GetReturnType(op);
                    
                    if (basePath != null)
                    {
                        path = basePath + path;
                    }

                    var invoker = (httpClient == null) ? 
                        null : 
                        new HttpFunctionInvoker(httpClient, verb, path, requiredParams.ToArray());

                    var function = new MyTexlFunction(functionNamespace, operationName, returnType, paramTypes.ToArray())
                    {
                        _invoker = invoker
                    };

                    config.AddFunction(function);
                }
            }
        }

        // $$$ Add Cache for HttpGet...
        // $$$ Replace with ServiceFunction 
        private class MyTexlFunction : TexlFunction, IAsyncTexlFunction
        {
            // $$$ Post operations should be behavior 
            public override bool IsSelfContained => true;

            public HttpFunctionInvoker _invoker;
            
            public MyTexlFunction(string functionNamespace, string name, FormulaType returnType, params FormulaType[] paramTypes)
    : this(functionNamespace, name, returnType._type, Array.ConvertAll(paramTypes, x => x._type))
            {
            }

            public MyTexlFunction(string functionNamespace, string name, DType returnType, params DType[] paramTypes)
                : base(DPath.Root.Append(new DName(functionNamespace)), name, name, SG("Custom func " + name), FunctionCategories.MathAndStat, returnType, 0, paramTypes.Length, paramTypes.Length, paramTypes)
            {
            }

            public override IEnumerable<TexlStrings.StringGetter[]> GetSignatures()
            {
                yield return new[] { SG("Arg 1") };
            }

            public static StringGetter SG(string text)
            {
                return (string locale) => text;
            }

            public Task<FormulaValue> InvokeAsync(FormulaValue[] args, CancellationToken cancel)
            {
                return _invoker.InvokeAsync(cancel, args);
            }
        }
    }
}
