using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataRepository.CodeFirst;

/// <summary>提供独立于业务仓储操作的 Code First 数据库初始化入口。</summary>
public static class DatabaseInitializationExtensions
{
    /// <summary>根据自动扫描的模型直接创建数据库及表，不需要 EF Tools 或迁移文件。</summary>
    /// <typeparam name="TModelMarker">当前数据库实体共同继承的基类。</typeparam>
    /// <param name="services">应用服务提供程序。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>仅用于首次建库建表；数据库已有表时不会补建新表或更新字段，不会删除数据。</remarks>
    public static Task InitializeAutoRepositoryDatabaseAsync<TModelMarker>(this IServiceProvider services,
        CancellationToken cancellationToken = default) where TModelMarker : EntityBase =>
        services.InitializeRepositoryDatabaseAsync<AutoRepositoryDbContext<TModelMarker>>(
            DatabaseInitializationMode.EnsureCreated, cancellationToken);

    /// <summary>创建独立作用域并初始化数据库；应在开始业务查询之前显式调用。</summary>
    /// <typeparam name="TContext">已注册的业务上下文类型。</typeparam>
    /// <param name="services">应用服务提供程序。</param>
    /// <param name="mode">默认应用迁移；首次直接建表必须显式选择 EnsureCreated。</param>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <remarks>不会自动生成迁移、删除数据库或覆盖已有数据。数据库权限和迁移异常直接向调用方传播。</remarks>
    public static async Task InitializeRepositoryDatabaseAsync<TContext>(this IServiceProvider services,
        DatabaseInitializationMode mode = DatabaseInitializationMode.Migrate,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        if (!context.Model.GetEntityTypes().Any())
            throw new InvalidOperationException("没有发现实体，请检查模型程序集以及 EntityBase 继承关系。");
        if (!context.Database.IsRelational())
            throw new NotSupportedException("Code First 初始化入口仅支持关系型数据库提供程序。");

        // 在执行数据库写入之前检查迁移配置，避免无迁移时误以为已经完成建表。
        var hasMigrations = context.Database.GetMigrations().Any();
        if (mode == DatabaseInitializationMode.Migrate)
        {
            if (!hasMigrations)
                throw new InvalidOperationException("未找到迁移。请先通过 dotnet ef migrations add 创建初始迁移；临时数据库可显式使用 EnsureCreated 模式。");

            await context.Database.MigrateAsync(cancellationToken);
            return;
        }

        // 已有迁移的模型只能走迁移路径，不创建绕过迁移历史的数据库。
        if (hasMigrations)
            throw new InvalidOperationException("当前上下文已有迁移，请使用 Migrate 模式，不能混用 EnsureCreated。");

        await context.Database.EnsureCreatedAsync(cancellationToken);
    }
}
