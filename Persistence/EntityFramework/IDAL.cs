using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Votify.Entities; 

namespace Votify.Persistence
{
    // El genérico <T> ahora define el tipo de entidad para todo el repositorio
    public interface IDAL<T> where T : class
    {
        // Operaciones CRUD del Repositorio
        void Insert(T entity); 
        void Delete(T entity); 
        IEnumerable<T> GetAll(); 
        T GetById(IComparable id); 
        bool Exists(IComparable id); 
        void Clear();
        IEnumerable<T> GetWhere(Expression<Func<T, bool>> predicate); 

        // Operaciones de Transacción (Unit of Work)
        void Commit();
        void Rollback(); 
        void RemoveAllData(); 
        void BeginTransaction(); 
        void CommitTransaction(); 
        void RollbackTransaction(); 
    }
}