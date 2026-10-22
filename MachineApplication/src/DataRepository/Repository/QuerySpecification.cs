using System.Linq.Expressions;

namespace DataRepository.Repository;

/// <summary>描述实体查询条件、关联加载、排序及跟踪行为。</summary>
/// <typeparam name="T">查询实体类型。</typeparam>
public sealed record QuerySpecification<T> where T : class
{
    /// <summary>数据库端过滤条件。</summary>
    public Expression<Func<T, bool>>? Criteria { get; init; }
    /// <summary>关联加载委托，例如 query =&gt; query.Include(x =&gt; x.Children)。</summary>
    public Func<IQueryable<T>, IQueryable<T>>? Include { get; init; }
    /// <summary>排序委托；分页时必须设置，并且应包含唯一键。</summary>
    public Func<IQueryable<T>, IOrderedQueryable<T>>? OrderBy { get; init; }
    /// <summary>是否启用实体跟踪；默认 false，适合只读查询。</summary>
    public bool Tracking { get; init; }
    /// <summary>是否使用拆分查询加载多个关联集合。</summary>
    public bool SplitQuery { get; init; }
}
