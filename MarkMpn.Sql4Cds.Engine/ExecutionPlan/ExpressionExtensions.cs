﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarkMpn.Sql4Cds.Engine.Visitors;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Xrm.Sdk;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    static class ExpressionExtensions
    {
        public static Type GetType(this TSqlFragment expr, NodeSchema schema)
        {
            if (expr is ColumnReferenceExpression col)
                return GetType(col, schema);
            else if (expr is IdentifierLiteral guid)
                return GetType(guid, schema);
            else if (expr is IntegerLiteral i)
                return GetType(i, schema);
            else if (expr is MoneyLiteral money)
                return GetType(money, schema);
            else if (expr is NullLiteral n)
                return GetType(n, schema);
            else if (expr is NumericLiteral num)
                return GetType(num, schema);
            else if (expr is RealLiteral real)
                return GetType(real, schema);
            else if (expr is StringLiteral str)
                return GetType(str, schema);
            else if (expr is BooleanExpression b)
                return GetType(b, schema);
            else if (expr is BinaryExpression bin)
                return GetType(bin, schema);
            else
                throw new NotSupportedQueryFragmentException("Unhandled expression type", expr);
        }

        public static object GetValue(this TSqlFragment expr, Entity entity, NodeSchema schema)
        {
            if (expr is ColumnReferenceExpression col)
                return GetValue(col, entity, schema);
            else if (expr is IdentifierLiteral guid)
                return GetValue(guid, entity, schema);
            else if (expr is IntegerLiteral i)
                return GetValue(i, entity, schema);
            else if (expr is MoneyLiteral money)
                return GetValue(money, entity, schema);
            else if (expr is NullLiteral n)
                return GetValue(n, entity, schema);
            else if (expr is NumericLiteral num)
                return GetValue(num, entity, schema);
            else if (expr is RealLiteral real)
                return GetValue(real, entity, schema);
            else if (expr is StringLiteral str)
                return GetValue(str, entity, schema);
            else if (expr is BooleanExpression b)
                return GetValue(b, entity, schema);
            else if (expr is BinaryExpression bin)
                return GetValue(bin, entity, schema);
            else
                throw new NotSupportedQueryFragmentException("Unhandled expression type", expr);
        }

        public static Type GetType(this BooleanExpression b, NodeSchema schema)
        {
            if (b is BooleanBinaryExpression bin)
                return GetType(bin, schema);
            else if (b is BooleanComparisonExpression cmp)
                return GetType(cmp, schema);
            else if (b is BooleanParenthesisExpression paren)
                return GetType(paren, schema);
            else
                throw new NotSupportedQueryFragmentException("Unhandled expression type", b);
        }

        public static bool GetValue(this BooleanExpression b, Entity entity, NodeSchema schema)
        {
            if (b is BooleanBinaryExpression bin)
                return GetValue(bin, entity, schema);
            else if (b is BooleanComparisonExpression cmp)
                return GetValue(cmp, entity, schema);
            else if (b is BooleanParenthesisExpression paren)
                return GetValue(paren, entity, schema);
            else
                throw new NotSupportedQueryFragmentException("Unhandled expression type", b);
        }

        private static Type GetType(ColumnReferenceExpression col, NodeSchema schema)
        {
            var name = col.GetColumnName();

            if (!schema.ContainsColumn(name, out name))
                throw new QueryExecutionException(col, "Unknown column");

            return schema.Schema[name];
        }

        private static object GetValue(ColumnReferenceExpression col, Entity entity, NodeSchema schema)
        {
            var name = col.GetColumnName();

            if (!schema.ContainsColumn(name, out name))
                throw new QueryExecutionException(col, "Unknown column");

            entity.Attributes.TryGetValue(name, out var value);
            return value;
        }

        private static Type GetType(IdentifierLiteral guid, NodeSchema schema)
        {
            return typeof(Guid);
        }

        private static Guid GetValue(IdentifierLiteral guid, Entity entity, NodeSchema schema)
        {
            return new Guid(guid.Value);
        }

        private static Type GetType(IntegerLiteral i, NodeSchema schema)
        {
            return typeof(int);
        }

        private static int GetValue(IntegerLiteral i, Entity entity, NodeSchema schema)
        {
            return Int32.Parse(i.Value);
        }

        private static Type GetType(MoneyLiteral money, NodeSchema schema)
        {
            return typeof(decimal);
        }

        private static decimal GetValue(MoneyLiteral money, Entity entity, NodeSchema schema)
        {
            return Decimal.Parse(money.Value);
        }

        private static Type GetType(NullLiteral n, NodeSchema schema)
        {
            return typeof(object);
        }

        private static object GetValue(NullLiteral n, Entity entity, NodeSchema schema)
        {
            return null;
        }

        private static Type GetType(NumericLiteral num, NodeSchema schema)
        {
            return typeof(decimal);
        }

        private static decimal GetValue(NumericLiteral num, Entity entity, NodeSchema schema)
        {
            return Decimal.Parse(num.Value);
        }

        private static Type GetType(RealLiteral real, NodeSchema schema)
        {
            return typeof(float);
        }

        private static float GetValue(RealLiteral real, Entity entity, NodeSchema schema)
        {
            return Single.Parse(real.Value);
        }

        private static Type GetType(StringLiteral str, NodeSchema schema)
        {
            return typeof(string);
        }

        private static string GetValue(StringLiteral str, Entity entity, NodeSchema schema)
        {
            return str.Value;
        }

        private static Type GetType(BooleanComparisonExpression cmp, NodeSchema schema)
        {
            var lhs = cmp.FirstExpression.GetType(schema);
            var rhs = cmp.SecondExpression.GetType(schema);

            if (!SqlTypeConverter.CanMakeConsistentTypes(lhs, rhs, out var type))
                throw new NotSupportedQueryFragmentException($"No implicit conversion exists for types {lhs} and {rhs}", cmp);

            if (!typeof(IComparable).IsAssignableFrom(type))
                throw new NotSupportedQueryFragmentException($"Values of type {type} cannot be compared", cmp);

            return typeof(bool);
        }

        private static bool GetValue(BooleanComparisonExpression cmp, Entity entity, NodeSchema schema)
        {
            var lhs = cmp.FirstExpression.GetValue(entity, schema);
            var rhs = cmp.SecondExpression.GetValue(entity, schema);

            if (lhs == null || rhs == null)
                return false;

            SqlTypeConverter.MakeConsistentTypes(ref lhs, ref rhs);

            var comparison = CaseInsensitiveComparer.Default.Compare(lhs, rhs);

            switch (cmp.ComparisonType)
            {
                case BooleanComparisonType.Equals:
                    return comparison == 0;

                case BooleanComparisonType.GreaterThan:
                    return comparison > 0;

                case BooleanComparisonType.GreaterThanOrEqualTo:
                case BooleanComparisonType.NotLessThan:
                    return comparison >= 0;

                case BooleanComparisonType.LessThan:
                    return comparison < 0;

                case BooleanComparisonType.LessThanOrEqualTo:
                case BooleanComparisonType.NotGreaterThan:
                    return comparison <= 0;

                case BooleanComparisonType.NotEqualToBrackets:
                case BooleanComparisonType.NotEqualToExclamation:
                    return comparison != 0;

                default:
                    throw new QueryExecutionException(cmp, "Unknown comparison type");
            }
        }

        private static Type GetType(BooleanBinaryExpression bin, NodeSchema schema)
        {
            bin.FirstExpression.GetType(schema);
            bin.SecondExpression.GetType(schema);

            return typeof(bool);
        }

        private static bool GetValue(BooleanBinaryExpression bin, Entity entity, NodeSchema schema)
        {
            var lhs = bin.FirstExpression.GetValue(entity, schema);

            if (bin.BinaryExpressionType == BooleanBinaryExpressionType.And && !lhs)
                return false;

            if (bin.BinaryExpressionType == BooleanBinaryExpressionType.Or && lhs)
                return true;

            var rhs = bin.SecondExpression.GetValue(entity, schema);
            return rhs;
        }

        private static Type GetType(BooleanParenthesisExpression paren, NodeSchema schema)
        {
            paren.Expression.GetType(schema);

            return typeof(bool);
        }

        private static bool GetValue(BooleanParenthesisExpression paren, Entity entity, NodeSchema schema)
        {
            return paren.Expression.GetValue(entity, schema);
        }

        private static Type GetType(BinaryExpression bin, NodeSchema schema)
        {
            var lhs = bin.FirstExpression.GetType(schema);
            var rhs = bin.SecondExpression.GetType(schema);

            if (!SqlTypeConverter.CanMakeConsistentTypes(lhs, rhs, out var type))
                throw new NotSupportedQueryFragmentException($"No implicit conversion exists for types {lhs} and {rhs}", bin);

            var typeCategory = SqlTypeConverter.GetCategory(type);

            switch (bin.BinaryExpressionType)
            {
                case BinaryExpressionType.Add:
                    // Can be used on any numeric type except bit
                    if ((typeCategory == SqlTypeCategory.ExactNumerics || typeCategory == SqlTypeCategory.ApproximateNumerics) && type != typeof(bool))
                        return type;
                    break;

                case BinaryExpressionType.Multiply:
                    // Can be used on any numeric type
                    if (typeCategory == SqlTypeCategory.ExactNumerics || typeCategory == SqlTypeCategory.ApproximateNumerics)
                        return type;
                    break;
            }

            throw new NotSupportedQueryFragmentException($"Operator {bin.BinaryExpressionType} is not defined for expressions of type {type}", bin);
        }

        private static object GetValue(BinaryExpression bin, Entity entity, NodeSchema schema)
        {
            var lhs = bin.FirstExpression.GetValue(entity, schema);
            var rhs = bin.SecondExpression.GetValue(entity, schema);

            if (lhs == null || rhs == null)
                return null;

            var type = SqlTypeConverter.MakeConsistentTypes(ref lhs, ref rhs);

            switch (bin.BinaryExpressionType)
            {
                case BinaryExpressionType.Add:
                    if (type == typeof(long))
                        return (long)lhs + (long)rhs;

                    if (type == typeof(int))
                        return (int)lhs + (int)rhs;

                    if (type == typeof(decimal))
                        return (decimal)lhs + (decimal)rhs;

                    if (type == typeof(double))
                        return (double)lhs + (double)rhs;

                    if (type == typeof(float))
                        return (float)lhs + (float)rhs;
                    break;

                case BinaryExpressionType.Multiply:
                    if (type == typeof(long))
                        return (long)lhs * (long)rhs;

                    if (type == typeof(int))
                        return (int)lhs * (int)rhs;

                    if (type == typeof(decimal))
                        return (decimal)lhs * (decimal)rhs;

                    if (type == typeof(bool))
                        return (bool)lhs && (bool)rhs;

                    if (type == typeof(double))
                        return (double)lhs * (double)rhs;

                    if (type == typeof(float))
                        return (float)lhs * (float)rhs;
                    break;

                default:
                    throw new QueryExecutionException(bin, "Unsupported operator");
            }

            throw new QueryExecutionException(bin, $"Operator {bin.BinaryExpressionType} is not defined for expressions of type {type}");
        }

        public static BooleanExpression RemoveCondition(this BooleanExpression expr, BooleanExpression remove)
        {
            if (expr == remove)
                return null;

            if (expr is BooleanBinaryExpression binary)
            {
                if (binary.FirstExpression == remove)
                    return binary.SecondExpression;

                if (binary.SecondExpression == remove)
                    return binary.FirstExpression;

                var clone = new BooleanBinaryExpression
                {
                    BinaryExpressionType = binary.BinaryExpressionType,
                    FirstExpression = binary.FirstExpression.RemoveCondition(remove),
                    SecondExpression = binary.SecondExpression.RemoveCondition(remove)
                };

                return clone;
            }

            if (expr is BooleanParenthesisExpression paren)
            {
                if (paren.Expression == remove)
                    return null;

                return new BooleanParenthesisExpression { Expression = paren.Expression.RemoveCondition(remove) };
            }

            return expr;
        }

        public static string GetColumnName(this ColumnReferenceExpression col)
        {
            return String.Join(".", col.MultiPartIdentifier.Identifiers.Select(id => id.Value));
        }

        public static IEnumerable<string> GetColumns(this TSqlFragment fragment)
        {
            var visitor = new ColumnCollectingVisitor();
            fragment.Accept(visitor);

            return visitor.Columns
                .Select(col => col.GetColumnName())
                .Distinct();
        }
    }
}
