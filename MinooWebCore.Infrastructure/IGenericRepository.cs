using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace MinooWebCore.Data;

public interface IGenericRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(params object[] keyValues);
    Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Run a parameterized SQL command, e.g. UPDATE/INSERT/DELETE.</summary>
    Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run raw SQL with parameters. Pass parameters separately; never concatenate user input into SQL.
    /// </summary>
    Task<int> ExecuteSqlRawAsync(
        string sql,
        object[] parameters,
        CancellationToken cancellationToken = default);

    /// <summary>Materialize an entity query from parameterized SQL.</summary>
    Task<List<TEntity>> QuerySqlAsync(
        string sql,
        object[] parameters,
        CancellationToken cancellationToken = default);
}
