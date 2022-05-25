﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Core.IR;
using Microsoft.PowerFx.Core.Tests;
using Microsoft.PowerFx.Interpreter.Tests;
using Microsoft.PowerFx.Types;
using Xunit;

namespace Microsoft.PowerFx.Tests
{
    public class TableMarshalTypeTests
    {
        [Fact]
        public void DataTableCheckTest()
        {
            var engine = new RecalcEngine();

            // FormulaType
            // Is this a valid usage of the UnknownType?
            var fType = new TableType()
                .Add(new NamedFormulaType("Column1", FormulaType.Unknown))
                .Add(new NamedFormulaType("Column2", FormulaType.Unknown))
                .Add(new NamedFormulaType("Column3", FormulaType.Unknown));

            var executionScope = new RecordType()
                .Add("RobinTable", fType);

            var validationResult = engine.Check("Index(RobinTable, 1).Column1", executionScope);
            Assert.Equal(FormulaType.Unknown, validationResult.ReturnType);
        }

        // Table of heterogenous cells. 
        private DataTable CreateDataTable()
        {
            var table = new DataTable();

            table.Columns.Add(string.Empty, typeof(object));
            table.Columns.Add(string.Empty, typeof(object));
            table.Columns.Add(string.Empty, typeof(object));

            var row1 = new object[] { 101, "str1b", true };
            var row2 = new object[] { 201, "str202", "str203" };
            var row3 = new object[] { 301, 302, 303 };

            table.Rows.Add(row1);
            table.Rows.Add(row2);
            table.Rows.Add(row3);

            return table;
        }

        [Fact]
        public void DataTableEvalTest()
        {
            var engine = new RecalcEngine();

            var table = CreateDataTable();

            var cache = new TypeMarshallerCache()
                .WithDynamicMarshallers(new DataTableMarshallerProvider());
                        
            var robinTable = cache.Marshal(table);

            engine.UpdateVariable("robintable", robinTable);

            var result1 = engine.Eval("Value(Index(robintable, 1).Column1)"); // 101
            var result2 = engine.Eval("Text(Index(robintable, 2).Column2)"); // "str202"

            Assert.Equal(101.0, result1.ToObject());
            Assert.Equal("str202", result2.ToObject());

            var result3 = engine.Eval("Sum(robintable, Value(ThisRecord.Column1))");
            Assert.Equal(101.0 + 201 + 301, result3.ToObject());
        }

        [Fact]
        public void ListCheckTest()
        {
            var engine = new RecalcEngine();

            //FormulaType
            // Is this a valid representation of a List<object> type to a FormulaType?
            var fType = new TableType()
                .Add(new NamedFormulaType("Value", FormulaType.Unknown));

            var executionScope = new RecordType()
                .Add("RobinList", fType);

            var validationResult = engine.Check("Index(RobinList, 1).Value", executionScope);

            Assert.Equal(FormulaType.Unknown, validationResult.ReturnType);
        }

        [Fact]
        public void ListHeterogenousArray()
        {
            var engine = new RecalcEngine();

            // Marshalling will fail.
            var list = new List<object> { 1, "string", true };

            var cache = new TypeMarshallerCache()
                .NewPrepend(new ObjectListMarshallerProvider());

            var robinList = cache.Marshal(list);

            engine.UpdateVariable("robinList", robinList);

            var result1 = engine.Eval("Value(Index(robinList, 1).Value)");
            var result2 = engine.Eval("Text(Index(robinList, 2).Value)");

            Assert.Equal(1.0, result1.ToObject());
            Assert.Equal("string", result2.ToObject());
        }

        // Marshal a heterogenous List<object> to Table of UntypedValue.
        private class ObjectListMarshallerProvider : ITypeMarshallerProvider
        {
            public bool TryGetMarshaller(Type type, TypeMarshallerCache cache, int maxDepth, out ITypeMarshaller marshaller)
            {
                if (type == typeof(List<object>))
                {
                    marshaller = new ObjectListMarshaller();
                    return true;
                }

                marshaller = null;
                return false;
            }

            private class ObjectListMarshaller : ITypeMarshaller
            {
                public FormulaType Type => _type.ToTable();

                private readonly RecordType _type;

                public ObjectListMarshaller()
                {
                    _type = new RecordType().Add(TableValue.ValueName, FormulaType.UntypedObject);
                }

                public FormulaValue Marshal(object value)
                {
                    var list = (IEnumerable<object>)value;
                    
                    var fxRecords = new List<RecordValue>();
                    foreach (var item in list)
                    {                        
                        var objFx = WrapDotNetObjectAsUntypedValue(item);
                        
                        var record = FormulaValue.NewRecordFromFields(new NamedValue(TableValue.ValueName, objFx));
                        fxRecords.Add(record);
                    }

                    return FormulaValue.NewTable(_type, fxRecords.ToArray());
                }
            }
        }

        private static UntypedObjectValue WrapDotNetObjectAsUntypedValue(object item)
        {
            // Would be nice if this was easier...
            UntypedObjectValue objFx = FormulaValue.New(new ScenarioDotNetObjectWrapper.Wrapper(item));
            return objFx;
        }
    }
}
