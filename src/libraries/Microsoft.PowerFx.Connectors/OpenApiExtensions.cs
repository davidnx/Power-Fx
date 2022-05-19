// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Net.Http;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx.Connectors
{
    internal static class OpenApiExtensions
    {
        public static string GetBasePath(this OpenApiDocument openApiDocument)
        {
            string basePath = null;
            if (openApiDocument?.Servers.Count == 1)
            {
                // This is a full URL that will pull in 'basePath' property from connectors. 
                // Extract BasePath back out from this. 
                var fullPath = openApiDocument.Servers[0].Url;
                var uri = new Uri(fullPath);
                basePath = uri.PathAndQuery;
            }

            return basePath;
        }

        public static bool IsTrigger(this OpenApiOperation op)
        {
            var isTrigger = op.Extensions.ContainsKey("x-ms-trigger");
            return isTrigger;
        }

        public static bool IsInternal(this OpenApiParameter param)
        {
            var exts = param.Extensions;
            if (exts.TryGetValue("x-ms-visibility", out var val))
            {
                if (val is OpenApiString valStr)
                {
                    if (valStr.Value == "internal")
                    {
                        // connectionId is a well-known internal param, stamped later. 
                        // $$$ be aware of any other internal params
                        if (param.Name != "connectionId")
                        {
                            throw new NotImplementedException();
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        public static FormulaType ToFormulaType(this OpenApiSchema schema)
        {
            switch (schema.Type)
            {
                case "string": return FormulaType.String;
                case "number": return FormulaType.Number;
                case "boolean": return FormulaType.Boolean;
                case "integer": return FormulaType.Number;
                case "array":
                    var elementType = schema.Items.ToFormulaType();
                    if (elementType is RecordType r)
                    {
                        return r.ToTable();
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                case "object":
                    var obj = new RecordType();
                    foreach (var kv in schema.Properties)
                    {
                        var propName = kv.Key;
                        var propType = kv.Value.ToFormulaType();

                        obj = obj.Add(propName, propType);
                    }

                    return obj;
            }

            throw new NotImplementedException($"{schema.Type}");
        }

        public static HttpMethod ToHttpMethod(this OperationType key)
        {
            switch (key)
            {
                case OperationType.Get: return HttpMethod.Get;
                case OperationType.Put: return HttpMethod.Put;
                case OperationType.Post: return HttpMethod.Post;
                case OperationType.Delete: return HttpMethod.Delete;
                case OperationType.Options: return HttpMethod.Options;
                case OperationType.Head: return HttpMethod.Head;
                case OperationType.Trace: return HttpMethod.Trace;
                default:
                    return new HttpMethod(key.ToString());
            }
        }

        public static FormulaType GetReturnType(this OpenApiOperation op)
        {
            var responses = op.Responses;
            var response200 = responses["200"];

            if (response200.Content.Count == 0)
            {
                // No return type. Void() method. 
                return FormulaType.Blank;
            }

            // Responses is a list by content-type. Find "application/json"
            foreach (var kv3 in response200.Content)
            {
                var mediaType = kv3.Key;
                var response = kv3.Value;

                if (mediaType == "application/json")
                {
                    var responseType = response.Schema.ToFormulaType();
                    return responseType;                    
                }
            }

            // Returns something, but not json. 
            throw new InvalidOperationException($"Unsupported return type");
        }
    }
}
