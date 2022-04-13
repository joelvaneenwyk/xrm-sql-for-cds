﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Xrm.Sdk;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    class SelectDataReader : IDataReader
    {
        private readonly List<SelectColumn> _columnSet;
        private readonly IDisposable _timer;
        private readonly INodeSchema _schema;
        private readonly IEnumerator<Entity> _source;
        private readonly IDictionary<string, object> _parameterValues;
        private Entity _row;
        private bool _closed;
        private int _rowCount;

        private static readonly Dictionary<Type, Type> _typeConversions;
        private static readonly Dictionary<Type, Func<object, object>> _typeConversionFuncs;

        static SelectDataReader()
        {
            _typeConversions = new Dictionary<Type, Type>();
            _typeConversionFuncs = new Dictionary<Type, Func<object, object>>();

            // https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings
            AddTypeConversion<SqlInt64, long>(v => v.Value);
            AddTypeConversion<SqlBinary, byte[]>(v => v.Value);
            AddTypeConversion<SqlBoolean, bool>(v => v.Value);
            AddTypeConversion<SqlString, string>(v => v.Value);
            AddTypeConversion<SqlDateTime, DateTime>(v => v.Value);
            AddTypeConversion<SqlDecimal, decimal>(v => v.Value);
            AddTypeConversion<SqlBytes, byte[]>(v => v.Value);
            AddTypeConversion<SqlDouble, double>(v => v.Value);
            AddTypeConversion<SqlInt32, int>(v => v.Value);
            AddTypeConversion<SqlMoney, decimal>(v => v.Value);
            AddTypeConversion<SqlSingle, float>(v => v.Value);
            AddTypeConversion<SqlInt16, short>(v => v.Value);
            AddTypeConversion<SqlByte, byte>(v => v.Value);
            AddTypeConversion<SqlGuid, Guid>(v => v.Value);
        }

        private static void AddTypeConversion<TSql, TClr>(Func<TSql, TClr> func)
        {
            _typeConversions[typeof(TSql)] = typeof(TClr);
            _typeConversionFuncs[typeof(TSql)] = v => func((TSql)v);
        }

        public SelectDataReader(List<SelectColumn> columnSet, Timer timer, INodeSchema schema, IEnumerable<Entity> source, IDictionary<string, object> parameterValues)
        {
            _columnSet = columnSet;
            _timer = timer.Run();
            _schema = schema;
            _source = source.GetEnumerator();
            _parameterValues = parameterValues;
        }

        public object this[int i] => ToClrType(GetRawValue(i));

        public object this[string name] => this[GetOrdinal(name)];

        public int Depth => 0;

        public bool IsClosed => _closed;

        public int RecordsAffected => -1;

        public int FieldCount => _columnSet.Count;

        public void Close()
        {
            if (_source is IDisposable disposable)
                disposable.Dispose();

            _timer.Dispose();
            _closed = true;
        }

        public void Dispose()
        {
            Close();
        }

        public bool GetBoolean(int i)
        {
            return (bool)this[i];
        }

        public byte GetByte(int i)
        {
            return (byte)this[i];
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            var bytes = (byte[])this[i];

            if (buffer == null)
                return bytes.Length;

            length = (int)Math.Min(length, bytes.Length - fieldOffset);
            Array.Copy(bytes, fieldOffset, buffer, bufferoffset, length);
            return length;
        }

        public char GetChar(int i)
        {
            return (char)this[i];
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            var chars = ((string)this[i]).ToCharArray();

            if (buffer == null)
                return chars.Length;

            length = (int)Math.Min(length, chars.Length - fieldoffset);
            Array.Copy(chars, fieldoffset, buffer, bufferoffset, length);
            return length;
        }

        public IDataReader GetData(int i)
        {
            throw new NotSupportedException();
        }

        public string GetDataTypeName(int i)
        {
            // Return T-SQL data type name
            var sqlType = _schema.Schema[_columnSet[i].SourceColumn];

            if (sqlType is SqlDataTypeReference sql)
                return sql.SqlDataTypeOption.ToString().ToLower();

            return String.Join(".", ((UserDataTypeReference)sqlType).Name.Identifiers.Select(id => id.Value));
        }

        public DateTime GetDateTime(int i)
        {
            return (DateTime)this[i];
        }

        public decimal GetDecimal(int i)
        {
            return (decimal)this[i];
        }

        public double GetDouble(int i)
        {
            return (double)this[i];
        }

        public Type GetFieldType(int i)
        {
            return ToClrType(_schema.Schema[_columnSet[i].SourceColumn].ToNetType(out _));
        }

        public float GetFloat(int i)
        {
            return (float)this[i];
        }

        public Guid GetGuid(int i)
        {
            var value = this[i];
            if (value is SqlEntityReference er)
                return (Guid)er;
            else
                return (Guid)value;
        }

        public short GetInt16(int i)
        {
            return (short)this[i];
        }

        public int GetInt32(int i)
        {
            return (int)this[i];
        }

        public long GetInt64(int i)
        {
            return (long)this[i];
        }

        public string GetName(int i)
        {
            return _columnSet[i].OutputColumn;
        }

        public int GetOrdinal(string name)
        {
            for (var i = 0; i < _columnSet.Count; i++)
            {
                if (_columnSet[i].OutputColumn.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private Type ToClrType(Type type)
        {
            if (_typeConversions.TryGetValue(type, out var clr))
                return clr;

            return type;
        }

        private object ToClrType(INullable value)
        {
            if (value.IsNull)
                return DBNull.Value;

            if (_typeConversionFuncs.TryGetValue(value.GetType(), out var func))
                return func(value);

            return value;
        }

        public DataTable GetSchemaTable()
        {
            var schemaTable = new DataTable();
            schemaTable.Columns.Add("ColumnName", typeof(string));
            schemaTable.Columns.Add("ColumnOrdinal", typeof(int));
            schemaTable.Columns.Add("ColumnSize", typeof(int));
            schemaTable.Columns.Add("NumericPrecision", typeof(int));
            schemaTable.Columns.Add("NumericScale", typeof(int));
            schemaTable.Columns.Add("IsUnique", typeof(bool));
            schemaTable.Columns.Add("IsKey", typeof(bool));
            schemaTable.Columns.Add("BaseServerName", typeof(string));
            schemaTable.Columns.Add("BaseCatalogName", typeof(string));
            schemaTable.Columns.Add("BaseColumnName", typeof(string));
            schemaTable.Columns.Add("BaseSchemaName", typeof(string));
            schemaTable.Columns.Add("BaseTableName", typeof(string));
            schemaTable.Columns.Add("DataType", typeof(Type));
            schemaTable.Columns.Add("AllowDBNull", typeof(bool));
            schemaTable.Columns.Add("ProviderType", typeof(Type));
            schemaTable.Columns.Add("IsAliased", typeof(bool));
            schemaTable.Columns.Add("IsExpression", typeof(bool));
            schemaTable.Columns.Add("IsIdentity", typeof(bool));
            schemaTable.Columns.Add("IsAutoIncrement", typeof(bool));
            schemaTable.Columns.Add("IsRowVersion", typeof(bool));
            schemaTable.Columns.Add("IsHidden", typeof(bool));
            schemaTable.Columns.Add("IsLong", typeof(bool));
            schemaTable.Columns.Add("IsReadOnly", typeof(bool));
            schemaTable.Columns.Add("ProviderSpecificDataType", typeof(Type));
            schemaTable.Columns.Add("DataTypeName", typeof(string));
            schemaTable.Columns.Add("XmlSchemaCollectionDatabase", typeof(string));
            schemaTable.Columns.Add("XmlSchemaCollectionOwningSchema", typeof(string));
            schemaTable.Columns.Add("XmlSchemaCollectionName", typeof(string));
            schemaTable.Columns.Add("UdtAssemblyQualifiedName", typeof(string));
            schemaTable.Columns.Add("NonVersionedProviderType", typeof(string));
            schemaTable.Columns.Add("IsColumnSet", typeof(bool));

            for (var i = 0; i < _columnSet.Count; i++)
            {
                var column = _columnSet[i];
                var sqlType = _schema.Schema[_columnSet[i].SourceColumn];
                var providerType = sqlType.ToNetType(out _);
                var type = ToClrType(providerType);
                var size = sqlType.GetSize();

                schemaTable.Rows.Add(new object[]
                {
                    column.OutputColumn,  // ColumnName
                    i,                    // ColumnOrdinal
                    size,                 // ColumnSize
                    255,                  // NumericPrecision
                    255,                  // NumericScale
                    false,                // IsUnique
                    false,                // IsKey
                    DBNull.Value,         // BaseServerName
                    DBNull.Value,         // BaseCatalogName
                    DBNull.Value,         // BaseColumnName
                    DBNull.Value,         // BaseSchemaName
                    DBNull.Value,         // BaseTableName
                    type,                 // DataType
                    true,                 // AllowDBNull
                    providerType,         // ProviderType
                    false,                // IsAliased
                    false,                // IsExpression
                    false,                // IsIdentity
                    false,                // IsAutoIncrement
                    false,                // IsRowVersion
                    false,                // IsHidden
                    false,                // IsLong
                    true,                 // IsReadOnly
                    type,                 // ProviderSpecificDataType
                    GetDataTypeName(i),   // DataTypeName
                    DBNull.Value,         // XmlSchemaCollectionDatabase
                    DBNull.Value,         // XmlSchemaCollectionOwningSchema
                    DBNull.Value,         // XmlSchemaCollectionName
                    type.AssemblyQualifiedName, // UdtAssemblyQualifiedName
                    providerType,         // NonVersionedProviderType TODO: Convert to SqlDbType
                    false                 // IsColumnSet
                });
            }

            return schemaTable;
        }

        public string GetString(int i)
        {
            return (string)this[i];
        }

        public object GetValue(int i)
        {
            return this[i];
        }

        public int GetValues(object[] values)
        {
            for (var i = 0; i < values.Length && i < _columnSet.Count; i++)
                values[i] = this[i];

            return Math.Min(values.Length, _columnSet.Count);
        }

        public bool IsDBNull(int i)
        {
            return GetRawValue(i).IsNull;
        }

        private INullable GetRawValue(int i)
        {
            return (INullable)_row[_columnSet[i].SourceColumn];
        }

        public bool NextResult()
        {
            Close();
            return false;
        }

        public bool Read()
        {
            if (IsClosed)
                return false;

            if (!_source.MoveNext())
            {
                _parameterValues["@@ROWCOUNT"] = (SqlInt32) _rowCount;
                Close();
                return false;
            }

            _row = _source.Current;
            _rowCount++;
            return true;
        }
    }
}
