﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventFly.Queries;
using GraphQL.Resolvers;
using GraphQL.Types;
using Newtonsoft.Json;

namespace EventFly.Web.GraphQL
{
    public sealed class GraphQueryHandler<TQuery, TResult> : IGraphQueryHandler<TQuery,TResult> where TQuery : IQuery<TResult> 
    {
        private readonly IQueryProcessor _queryProcessor;

        public GraphQueryHandler(IQueryProcessor queryProcessor)
        {
            _queryProcessor = queryProcessor;
        }

        public async Task<object> ExecuteAsync(object query)
        {
            return await ReadAsync((TQuery)query);
        }

        public FieldType GetFieldType(bool isInput)
        {
            var name = typeof(TQuery).Name;
            name = !name.EndsWith("Query") ? name : name.Substring(0, name.Length - "Query".Length);
            return  new FieldType
            {
                ResolvedType = GetQueryItemType(typeof(TResult),isInput),
                Name = name,
                Description = typeof(TQuery).GetCustomAttribute<DescriptionAttribute>()?.Description,
                Arguments = QueryParametersHelper.GetArguments(typeof(TQuery), this),
                Resolver = new FuncFieldResolver<TResult>(context => ExecuteQuery(context).GetAwaiter().GetResult()),
            };
        }

        private Task<TResult> ReadAsync(TQuery query)
        {
            return _queryProcessor.Process(query);
        }

        public async Task<object> ExecuteQuery(Dictionary<string, object> arguments)
        {
            return await ReadAsync(ParseModel<TQuery>(arguments));
        }

        private Task<TResult> ExecuteQuery(ResolveFieldContext context) => ReadAsync(ParseModel<TQuery>(context.Arguments));

        private T ParseModel<T>(Dictionary<string, object> arguments) where T : IQuery<TResult>
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(arguments));


        public IGraphType GetQueryItemType(Type modelType, bool isInput)
        {
            lock (_declaredTypes)
            {
                if (_declaredTypes.ContainsKey(modelType)) return _declaredTypes[modelType];
                var result = GetGraphTypeEx(modelType, this,isInput);
                _declaredTypes.Add(modelType, result);
                return result;
            }
        }
        private readonly Dictionary<Type,IGraphType> _declaredTypes = new Dictionary<Type, IGraphType>();

        public static Func<object,object> ConvertFunc<TParent, TRes>(Func<TParent, TRes> func) => arg => func((TParent)arg);

        private static IGraphType GetGraphTypeEx(Type propType, IGraphQueryHandler graphQuery, bool isInput)
        {
            if (propType.IsGenericType && propType.GetGenericArguments().Length == 1 && typeof(IEnumerable).IsAssignableFrom(propType))
            {
                var innerType = propType.GetGenericArguments().First();
                if (innerType != null)
                    return new ListGraphType(graphQuery.GetQueryItemType(innerType,isInput));
                return null;
            }

            return isInput ? (IGraphType) new InputObjectGraphTypeFromModel(propType, graphQuery) 
                : new ObjectGraphTypeFromModel(propType, graphQuery);
        }
    }
}