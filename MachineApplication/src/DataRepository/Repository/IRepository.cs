using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace DataRepository.Repository;

/// <summary>提供基于 EF Core 的通用仓储操作。</summary>
/// <typeparam name="T">仓储管理的实体类型。</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>创建可继续组合的查询；默认使用无跟踪查询。</summary>
    IQueryable<T> Query(QuerySpecification<T>? specification = null);
    /// <summary>按主键查找实体；复合主键值必须按照模型定义顺序传入。</summary>
    ValueTask<T?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default);
    /// <summary>执行规格查询并返回实体列表。</summary>
    Task<IReadOnlyList<T>> ListAsync(QuerySpecification<T>? specification = null, CancellationToken cancellationToken = default);
    /// <summary>执行规格查询并在数据库端投影为指定结果类型。</summary>
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(Expression<Func<T, TResult>> selector, QuerySpecification<T>? specification = null, CancellationToken cancellationToken = default);
    /// <summary>返回规格查询的第一条记录；没有记录时返回 null。</summary>
    Task<T?> FirstOrDefaultAsync(QuerySpecification<T>? specification = null, CancellationToken cancellationToken = default);
    /// <summary>判断满足条件的实体是否存在。</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    /// <summary>统计满足条件的实体数量。</summary>
    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    /// <summary>执行分页查询；规格必须提供稳定的排序规则。</summary>
    Task<PagedResult<T>> PageAsync(PageRequest page, QuerySpecification<T> specification, CancellationToken cancellationToken = default);
    /// <summary>执行分页查询并在数据库端投影结果。</summary>
    Task<PagedResult<TResult>> PageAsync<TResult>(PageRequest page, QuerySpecification<T> specification, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);
    /// <summary>将实体加入当前上下文，实际保存由工作单元完成。</summary>
    ValueTask AddAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>批量加入实体，实际保存由工作单元完成。</summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    /// <summary>将实体标记为修改状态。</summary>
    void Update(T entity);
    /// <summary>批量将实体标记为修改状态。</summary>
    void UpdateRange(IEnumerable<T> entities);
    /// <summary>将实体标记为删除状态。</summary>
    void Remove(T entity);
    /// <summary>批量将实体标记为删除状态。</summary>
    void RemoveRange(IEnumerable<T> entities);
    /// <summary>在数据库端批量删除，立即执行且不需要调用 SaveChangesAsync。</summary>
    Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    /// <summary>在数据库端批量更新，立即执行且不需要调用 SaveChangesAsync。</summary>
    Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Action<UpdateSettersBuilder<T>> setters, CancellationToken cancellationToken = default);
}
