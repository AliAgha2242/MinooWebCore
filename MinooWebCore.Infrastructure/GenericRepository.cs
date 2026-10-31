using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace MinooWebCore.Data;

public sealed class GenericRepository<TEntity>(MinooDbContext context) : IGenericRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet = context.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(params object[] keyValues) =>
        await _dbSet.FindAsync(keyValues);

    public Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _dbSet.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _dbSet.AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => _dbSet.Update(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task<int> ExecuteSqlAsync(
        FormattableString sql,
        CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);

    public Task<int> ExecuteSqlRawAsync(
        string sql,
        object[] parameters,
        CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);

    public Task<List<TEntity>> QuerySqlAsync(
        string sql,
        object[] parameters,
        CancellationToken cancellationToken = default) =>
        _dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync(cancellationToken);
}
