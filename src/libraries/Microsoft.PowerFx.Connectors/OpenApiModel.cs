// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx.Connectors
{
    internal class OpenApiModel
    {
        // $$$ consistent naming here...
        private readonly OpenApiDocument _apiDocument;

        public OpenApiModel(OpenApiDocument apiDocument)
        {
            _apiDocument = apiDocument;
        }

        public FormulaType ToType(OpenApiSchema schema)
        {
            switch (schema.Type)
            {
                case "string": return FormulaType.String;
                case "number": return FormulaType.Number;
                case "boolean": return FormulaType.Boolean;
                case "integer": return FormulaType.Number;
                case "array":
                    var elementType = ToType(schema.Items);
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
                        var propType = ToType(kv.Value);

                        obj = obj.Add(propName, propType);
                    }

                    return obj;
            }

            throw new NotImplementedException($"{schema.Type}");
        }

        public HttpMethod ToMethod(OperationType key)
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

        public FormulaType GetReturnType(OpenApiOperation op)
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
                    var responseType = ToType(response.Schema);
                    return responseType;                    
                }
            }

            // Returns something, but not json. 
            throw new InvalidOperationException($"Unsupported return type");
        }
    }
}
