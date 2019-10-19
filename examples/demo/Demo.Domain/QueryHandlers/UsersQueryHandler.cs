﻿using System;
using System.Linq;
using EventFly.Queries;
using Demo.Domain.ReadModels;
using Demo.Queries;
using EventFly.ReadModels;

namespace Demo.Domain.QueryHandlers
{
    public class UsersQueryHandler : QueryHandler<UsersQuery, UsersResult>
    {
        private readonly IQueryableReadModelStorage<UsersInfoReadModel> _storage;

        public UsersQueryHandler(IReadModelStorage<UsersInfoReadModel> storage)
        {
            _storage = storage as IQueryableReadModelStorage<UsersInfoReadModel>;
        }
        public override UsersResult ExecuteQuery(UsersQuery query)
        {
            var items = _storage?.Items
                .Where(i => query.NameFilter == null || i.UserName.StartsWith(query.NameFilter, StringComparison.InvariantCultureIgnoreCase))
                .Select(i => new UserInfo {Id = i.Id, Name = i.UserName}).ToList();

            return new UsersResult(items, 100);
        }
    }
}