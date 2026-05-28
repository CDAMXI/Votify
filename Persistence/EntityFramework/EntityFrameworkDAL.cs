using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Votify.Entities;
using Votify.Persistence;

namespace Votify.Persistence
{
    public class EntityFrameworkDAL<T> : IDAL<T> where T : class
    {
        private readonly VotifyDBContext _dbContext;
        private readonly DbSet<T> _dbSet;

        public EntityFrameworkDAL(VotifyDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = _dbContext.Set<T>();
        }

        public void Insert(T entity) => _dbSet.Add(entity);

        public void Delete(T entity) => _dbSet.Remove(entity);

        public IEnumerable<T> GetAll() => _dbSet;

        public T GetById(IComparable id) => _dbSet.Find(id);

        public bool Exists(IComparable id) => _dbSet.Find(id) != null;

        public void Clear() => _dbSet.RemoveRange(_dbSet);

        public IEnumerable<T> GetWhere(Expression<Func<T, bool>> predicate)
            => _dbSet.Where(predicate).ToList();

        public void Commit() => _dbContext.SaveChanges();

        public void Rollback() => _dbContext.Rollback();

        public void RemoveAllData() => _dbContext.RemoveAllData();

        private DbContextTransaction? _transaction;

        public void BeginTransaction()
            => _transaction = _dbContext.Database.BeginTransaction();

        public void CommitTransaction()
        {
            _transaction?.Commit();
            _transaction = null;
        }

        public void RollbackTransaction()
        {
            _transaction?.Rollback();
            _transaction = null;
        }
    }
}