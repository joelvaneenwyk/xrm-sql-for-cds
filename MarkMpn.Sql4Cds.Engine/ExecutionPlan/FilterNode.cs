﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Xrm.Sdk;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    /// <summary>
    /// Applies a filter to the data stream
    /// </summary>
    public class FilterNode : BaseNode
    {
        /// <summary>
        /// The filter to apply
        /// </summary>
        public BooleanExpression Filter { get; set; }

        /// <summary>
        /// The data source to select from
        /// </summary>
        public IExecutionPlanNode Source { get; set; }

        public override IEnumerable<Entity> Execute(IOrganizationService org, IAttributeMetadataCache metadata, IQueryExecutionOptions options)
        {
            foreach (var entity in Source.Execute(org, metadata, options))
            {
                if (Filter.GetValue(entity))
                    yield return entity;
            }
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            yield return Source;
        }

        public override NodeSchema GetSchema(IAttributeMetadataCache metadata)
        {
            return Source.GetSchema(metadata);
        }

        public override IEnumerable<string> GetRequiredColumns()
        {
            return Filter.GetColumns();
        }
    }
}
