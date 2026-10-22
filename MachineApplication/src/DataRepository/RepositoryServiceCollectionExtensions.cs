using DataRepository.Repository;
using DataRepository.CodeFirst;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DataRepository;

/// <summary>提供仓储模型的依赖注入注册扩展。</summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>注册按实体基类自动发现模型的上下文及工作单元。</summary>
    /// <typeparam name="TBase">当前数据库实体共同继承的抽象基类。</typeparam>
    /// <param name="services">服务注册集合。</param>
    /// <param name="configureDatabase">数据库提供程序和连接配置。</param>
    /// <remarks>注册不修改数据库；启动后显式调用 InitializeAutoRepositoryDatabaseAsync 完成首次建表。</remarks>
    public static IServiceCollection AddAutoDataRepository<TBase>(this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDatabase) where TBase : EntityBase =>
        services.AddDataRepository<AutoRepositoryDbContext<TBase>>(configureDatabase);

    /// <summary>同时注册业务上下文和仓储工作单元；注册时不会连接数据库或修改表结构。</summary>
    /// <param name="services">服务注册集合。</param>
    /// <param name="configureDatabase">数据库提供程序及连接配置，例如 options =&gt; options.UseSqlite(connectionString)。</param>
    /// <typeparam name="TContext">业务上下文类型；构造函数应接收 DbContextOptions&lt;TContext&gt;。</typeparam>
    public static IServiceCollection AddDataRepository<TContext>(this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDatabase) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDatabase);
        services.AddDbContext<TContext>(configureDatabase);
        return services.AddDataRepository<TContext>();
    }

    /// <summary>以 Scoped 生命周期注册指定上下文的工作单元。</summary>
    public static IServiceCollection AddDataRepository<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IUnitOfWork<TContext>, UnitOfWork<TContext>>();
        return services;
    }
}
