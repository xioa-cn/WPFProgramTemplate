using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace DataRepository.Repository;

using System.Linq.Expressions;

/// <summary>基于 EF Core DbSet 的通用仓储实现。</summary>
public class Repository<T> : IRepository<T> where T : class {
    /// <summary>当前实体对应的 DbSet。</summary>
    public DbSet<T> DbSet { get; }
    
    /// <summary>创建仓储并绑定到调用方提供的上下文。</summary>
    public Repository(DbContext context) {
        ArgumentNullException.ThrowIfNull(context);
        DbSet = context.Set<T>();
    }

    /// <inheritdoc />
    public IQueryable<T> Query(QuerySpecification<T>? specification = null)
    {
        // 只读查询默认不跟踪，更新场景通过规格显式开启跟踪。
        IQueryable<T> query = specification?.Tracking == true ? DbSet.AsTracking() : DbSet.AsNoTracking();
        if (specification?.Criteria is not null)
            query = query.Where(specification.Criteria);
        if (specification?.Include is not null)
            query = specification.Include(query);
        // 多集合 Include 时可使用拆分查询，避免笛卡尔积导致结果膨胀。
        if (specification?.SplitQuery == true)
            query = query.AsSplitQuery();
        if (specification?.OrderBy is not null)
            query = specification.OrderBy(query);
        return query;
    }

    /// <inheritdoc />
    public ValueTask<T?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        if (keyValues.Length == 0)
            throw new ArgumentException("At least one key value is required.", nameof(keyValues));
        return DbSet.FindAsync(keyValues, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> ListAsync(QuerySpecification<T>? specification = null,
        CancellationToken cancellationToken = default) => await Query(specification).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TResult>> ListAsync<TResult>(Expression<Func<T, TResult>> selector,
        QuerySpecification<T>? specification = null, CancellationToken cancellationToken = default) =>
        await Query(specification).Select(selector).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T?> FirstOrDefaultAsync(QuerySpecification<T>? specification = null,
        CancellationToken cancellationToken = default) => Query(specification).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        (predicate is null ? DbSet : DbSet.Where(predicate)).LongCountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<PagedResult<T>> PageAsync(PageRequest page, QuerySpecification<T> specification,
        CancellationToken cancellationToken = default) => PageAsync(page, specification, entity => entity, cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<TResult>> PageAsync<TResult>(PageRequest page,
        QuerySpecification<T> specification, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(selector);
        if (specification.OrderBy is null)
            throw new ArgumentException("Pagination requires ordering including a unique key.", nameof(specification));
        // 总数和当前页分别在数据库端执行，避免将全量数据加载到内存。
        var count = await CountAsync(specification.Criteria, cancellationToken);
        var items = await Query(specification).Skip(page.Offset).Take(page.PageSize).Select(selector).ToListAsync(cancellationToken);
        return new PagedResult<TResult>(items.AsReadOnly(), count, page.PageNumber, page.PageSize);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    /// <inheritdoc />
    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) =>
        DbSet.AddRangeAsync(entities, cancellationToken);

    /// <inheritdoc />
    public void Update(T entity) => DbSet.Update(entity);
    /// <inheritdoc />
    public void UpdateRange(IEnumerable<T> entities) => DbSet.UpdateRange(entities);
    /// <inheritdoc />
    public void Remove(T entity) => DbSet.Remove(entity);
    /// <inheritdoc />
    public void RemoveRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);

    /// <inheritdoc />
    public Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return DbSet.Where(predicate).ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(setters);
        return DbSet.Where(predicate).ExecuteUpdateAsync(setters, cancellationToken);
    }
}
