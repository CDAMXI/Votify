using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Votify.Entities;

namespace Votify.Persistence
{
    public interface IDAL<T> where T : class
    {
        void Insert(T entity);

        void Delete(T entity);

        IEnumerable<T> GetAll();

        T GetById(IComparable id);

        bool Exists(IComparable id);

        void Clear();

        IEnumerable<T> GetWhere(Expression<Func<T, bool>> predicate);

        void Commit();

        void Rollback();

        void RemoveAllData();

        void BeginTransaction();

        void CommitTransaction();

        void RollbackTransaction();
    }
}