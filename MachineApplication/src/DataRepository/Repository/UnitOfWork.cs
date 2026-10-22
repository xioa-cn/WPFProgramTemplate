using DataRepository.CodeFirst;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataRepository.Repository;

/// <summary>管理同一个 DbContext 生命周期内的仓储和事务。</summary>
public interface IUnitOfWork<TContext> where TContext : DbContext {
    /// <summary>获取当前工作单元缓存的实体仓储。</summary>
    IRepository<T> Repository<T>() where T : class;

    /// <summary>提交当前上下文中的所有跟踪变更。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>开启数据库事务；调用方负责提交、回滚和释放。</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>默认工作单元实现；同一类型仓储在当前上下文中只创建一次。</summary>
public sealed class UnitOfWork<TContext> : IUnitOfWork<TContext> where TContext : DbContext {
    private readonly TContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    /// <summary>绑定调用方提供的上下文；工作单元不负责释放该上下文。</summary>
    public UnitOfWork(TContext context) {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public IRepository<T> Repository<T>() where T : class {
        // 仓储共享同一个 DbContext，保证跟踪状态和事务边界一致。
        if (!_repositories.TryGetValue(typeof(T), out var repository)) {
            repository = new Repository<T>(_context);
            _repositories.Add(typeof(T), repository);
        }

        return (IRepository<T>)repository;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        _context.Database.BeginTransactionAsync(cancellationToken);
}