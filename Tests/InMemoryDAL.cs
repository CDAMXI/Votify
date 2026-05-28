using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Votify.Persistence;

namespace Votify.Tests
{
    public class InMemoryDAL<T> : IDAL<T> where T : class
    {
        private readonly List<T> _store = new();

        private readonly Func<T, IComparable>? _keySelector;

        public InMemoryDAL(Func<T, IComparable>? keySelector = null)
        {
            _keySelector = keySelector;
        }

        public void Insert(T entity) => _store.Add(entity);

        public void Delete(T entity) => _store.Remove(entity);

        public IEnumerable<T> GetAll() => _store.ToList();

        public T GetById(IComparable id)
        {
            if (_keySelector == null) return null!;
            return _store.FirstOrDefault(e => _keySelector(e).Equals(id))!;
        }

        public bool Exists(IComparable id) => GetById(id) is not null;

        public void Clear() => _store.Clear();

        public IEnumerable<T> GetWhere(Expression<Func<T, bool>> predicate)
            => _store.Where(predicate.Compile()).ToList();

        public void Commit() { /* no-op: cambios son inmediatos en memoria */ }

        public void Rollback() { /* no-op */ }

        public void RemoveAllData() => _store.Clear();

        public void BeginTransaction() { /* no-op */ }

        public void CommitTransaction() { /* no-op */ }

        public void RollbackTransaction() { /* no-op */ }
    }
}