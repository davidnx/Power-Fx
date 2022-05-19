// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Core.Functions;
using Microsoft.PowerFx.Core.Localization;
using Microsoft.PowerFx.Core.Types;
using Microsoft.PowerFx.Core.Utils;
using Microsoft.PowerFx.Types;
using static Microsoft.PowerFx.Core.Localization.TexlStrings;

namespace Microsoft.PowerFx.Connectors
{
    // $$$ Replace with real ServiceFunction from PAClient.
    internal class ServiceFunction : TexlFunction, IAsyncTexlFunction
    {
        // $$$ Post operations should be behavior 
        public override bool IsSelfContained => true;

        public HttpFunctionInvoker _invoker;

        public ServiceFunction(string functionNamespace, string name, FormulaType returnType, params FormulaType[] paramTypes)
: this(functionNamespace, name, returnType._type, Array.ConvertAll(paramTypes, x => x._type))
        {
        }

        public ServiceFunction(string functionNamespace, string name, DType returnType, params DType[] paramTypes)
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

        // Caller by interpreter on invoke. 
        // $$$ Replace with IR rewrite.
        public Task<FormulaValue> InvokeAsync(FormulaValue[] args, CancellationToken cancel)
        {
            var cacheScope = Namespace.Name.Value;
            return _invoker.InvokeAsync(cacheScope, cancel, args);
        }
    }
}
