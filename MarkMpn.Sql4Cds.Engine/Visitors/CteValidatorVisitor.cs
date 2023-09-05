﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace MarkMpn.Sql4Cds.Engine.Visitors
{
    /// <summary>
    /// Checks the properties of a CTE to ensure it is valid
    /// </summary>
    /// <remarks>
    /// https://learn.microsoft.com/en-us/sql/t-sql/queries/with-common-table-expression-transact-sql?view=sql-server-ver16
    /// </remarks>
    class CteValidatorVisitor : TSqlFragmentVisitor
    {
        private int _cteReferenceCount;
        private FunctionCall _scalarAggregate;
        private ScalarSubquery _subquery;
        private QualifiedJoin _outerJoin;

        public string Name { get; private set; }

        public bool IsRecursive { get; private set; }

        public override void Visit(CommonTableExpression node)
        {
            Name = node.ExpressionName.Value;

            base.Visit(node);
        }

        public override void Visit(FromClause node)
        {
            _cteReferenceCount = 0;

            base.Visit(node);

            // The FROM clause of a recursive member must refer only one time to the CTE expression_name.
            if (_cteReferenceCount > 1)
                throw new NotSupportedQueryFragmentException($"Recursive member of a common table expression '{Name}' has multiple recursive references.", node);
        }

        public override void Visit(NamedTableReference node)
        {
            if (node.SchemaObject.Identifiers.Count == 1 &&
                node.SchemaObject.BaseIdentifier.Value.Equals(Name, StringComparison.OrdinalIgnoreCase))
            {
                IsRecursive = true;
                _cteReferenceCount++;

                // The following items aren't allowed in the CTE_query_definition of a recursive member:
                // A hint applied to a recursive reference to a CTE inside a CTE_query_definition.
                if (node.TableHints.Count > 0)
                    throw new NotSupportedQueryFragmentException($"Hints are not allowed on recursive common table expression (CTE) references. Consider removing hint from recursive CTE reference '{node.SchemaObject.ToSql()}'.", node.TableHints[0]);
            }

            base.Visit(node);
        }

        public override void Visit(QualifiedJoin node)
        {
            base.Visit(node);

            if (node.QualifiedJoinType != QualifiedJoinType.Inner)
                _outerJoin = node;
        }

        public override void ExplicitVisit(BinaryQueryExpression node)
        {
            base.ExplicitVisit(node);

            // UNION ALL is the only set operator allowed between the last anchor member and first recursive member, and when combining multiple recursive members.
            if (IsRecursive && (node.BinaryQueryExpressionType != BinaryQueryExpressionType.Union || !node.All))
                throw new NotSupportedQueryFragmentException($"Recursive common table expression '{Name}' does not contain a top-level UNION ALL operator", node);
        }

        public override void Visit(QuerySpecification node)
        {
            _scalarAggregate = null;
            _subquery = null;
            _outerJoin = null;

            base.Visit(node);

            // The following clauses can't be used in the CTE_query_definition:
            // ORDER BY (except when a TOP clause is specified)
            if (node.OrderByClause != null && node.TopRowFilter == null)
                throw new NotSupportedQueryFragmentException("The ORDER BY clause is invalid in views, inline functions, derived tables, subqueries, and common table expressions, unless TOP, OFFSET or FOR XML is also specified", node.OrderByClause);

            // FOR BROWSE
            if (node.ForClause is BrowseForClause)
                throw new NotSupportedQueryFragmentException("The FOR BROWSE clause is no longer supported in views", node.ForClause);

            if (IsRecursive)
            {
                // The following items aren't allowed in the CTE_query_definition of a recursive member:
                // SELECT DISTINCT
                if (node.UniqueRowFilter == UniqueRowFilter.Distinct)
                    throw new NotSupportedQueryFragmentException($"DISTINCT operator is not allowed in the recursive part of a recursive common table expression '{Name}'.", node);

                // GROUP BY
                if (node.GroupByClause != null)
                    throw new NotSupportedQueryFragmentException($"GROUP BY, HAVING, or aggregate functions are not allowed in the recursive part of a recursive common table expression '{Name}'", node.GroupByClause);

                // TODO: PIVOT

                // HAVING
                if (node.HavingClause != null)
                    throw new NotSupportedQueryFragmentException($"GROUP BY, HAVING, or aggregate functions are not allowed in the recursive part of a recursive common table expression '{Name}'", node.HavingClause);

                // Scalar aggregation
                if (_scalarAggregate != null)
                    throw new NotSupportedQueryFragmentException($"GROUP BY, HAVING, or aggregate functions are not allowed in the recursive part of a recursive common table expression '{Name}'", _scalarAggregate);

                // TOP
                if (node.TopRowFilter != null || node.OffsetClause != null)
                    throw new NotSupportedQueryFragmentException($"The TOP or OFFSET operator is not allowed in the recursive part of a recursive common table expression '{Name}'", (TSqlFragment)node.TopRowFilter ?? node.OffsetClause);

                // LEFT, RIGHT, OUTER JOIN (INNER JOIN is allowed)
                if (_outerJoin != null)
                    throw new NotSupportedQueryFragmentException($"Outer join is not allowed in the recursive part of a recursive common table expression '{Name}'", _outerJoin);

                // Subqueries
                if (_subquery != null)
                    throw new NotSupportedQueryFragmentException("Recursive references are not allowed in subqueries", _subquery);
            }
        }

        public override void Visit(SelectStatement node)
        {
            base.Visit(node);

            // The following clauses can't be used in the CTE_query_definition:
            // INTO
            if (node.Into != null)
                throw new NotSupportedQueryFragmentException("INTO is not supported in CTEs", node.Into);

            // OPTION clause with query hints
            if (node.OptimizerHints.Count > 0)
                throw new NotSupportedQueryFragmentException("Optimizer hints are not supported in CTEs", node.OptimizerHints[0]);
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            var count = _cteReferenceCount;

            base.ExplicitVisit(node);

            if (_cteReferenceCount > count)
                _subquery = node;
        }
    }
}
