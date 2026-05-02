using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Votify.Entities; 
using Votify.Persistence; 

namespace Votify.Persistence
{
    // La clase ahora implementa la interfaz tipada IDAL<T>
    public class EntityFrameworkDAL<T> : IDAL<T> where T : class
    {
        private readonly VotifyDBContext dbContext; 
        private readonly DbSet<T> _dbSet; 

        public EntityFrameworkDAL(VotifyDBContext dbContext) 
        {
            this.dbContext = dbContext; 
            this._dbSet = dbContext.Set<T>(); 
        }

        public void Insert(T entity)
        {
            _dbSet.Add(entity); 
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity); 
        }

        public IEnumerable<T> GetAll()
        {
            return _dbSet; 
        }

        public T GetById(IComparable id)
        {
            return _dbSet.Find(id); 
        }

        public bool Exists(IComparable id)
        {
            return _dbSet.Find(id) != null; 
        }

        public void Clear()
        {
            _dbSet.RemoveRange(_dbSet); 
        }

        public IEnumerable<T> GetWhere(Expression<Func<T, bool>> predicate)
        {
            return _dbSet.Where(predicate).ToList(); 
        }


        public void Commit()
        {
            dbContext.SaveChanges(); 
        }

        public void Rollback()
        {
            dbContext.Rollback(); 
        }

        public void RemoveAllData()
        {
            dbContext.RemoveAllData(); 
        }

        private DbContextTransaction _transaction; 

        public void BeginTransaction()
        {
            _transaction = dbContext.Database.BeginTransaction(); 
        }

        public void CommitTransaction()
        {
            _transaction.Commit(); 
        }

        public void RollbackTransaction()
        {
            _transaction.Rollback();
        }
    }
}