using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Votify.Persistence;

namespace Votify.Tests
{
    /// <summary>
    /// Repositorio en memoria que implementa IDAL&lt;T&gt; para pruebas unitarias.
    /// Sustituye a EntityFrameworkDAL sin necesitar base de datos.
    /// </summary>
    public class InMemoryDAL<T> : IDAL<T> where T : class
    {
        private readonly List<T> _store = new();
        private readonly Func<T, IComparable>? _keySelector;

        /// <param name="keySelector">Función que extrae la clave primaria de la entidad.
        /// Si es null, GetById siempre devuelve null.</param>
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

        // Operaciones de transacción — no-op en memoria
        public void Commit() { }
        public void Rollback() { }
        public void RemoveAllData() => _store.Clear();
        public void BeginTransaction() { }
        public void CommitTransaction() { }
        public void RollbackTransaction() { }
    }
}
