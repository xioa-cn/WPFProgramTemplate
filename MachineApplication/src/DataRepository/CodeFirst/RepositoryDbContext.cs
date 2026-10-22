using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataRepository.CodeFirst;

/// <summary>按实体基类隔离模型的 Code First 上下文。</summary>
/// <typeparam name="T">当前数据库实体共同继承的抽象基类。</typeparam>
/// <remarks>上下文只扫描 TBase 的具体子类，不会把其他数据库的实体加入当前模型。</remarks>
public abstract class RepositoryDbContext<T> : DbContext where T : EntityBase {
    /// <summary>接收业务上下文的数据库配置，不在仓储中固定数据库类型或连接字符串。</summary>
    protected RepositoryDbContext(DbContextOptions options) : base(options) {
    }

    /// <summary>需要扫描实体和映射的程序集；默认使用 TBase 所在程序集。</summary>
    protected virtual IEnumerable<Assembly> ConfigurationAssemblies => [typeof(T).Assembly];

    /// <summary>发现实体基类的具体子类，注册模型并应用映射，不需要声明 DbSet 属性。</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        // 仅扫描 TBase 的具体子类，避免把其他数据库的实体误加入当前数据库。
        var assemblies = ConfigurationAssemblies.Distinct().ToArray();
        var entityTypes = assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters
                           && type != typeof(T) && typeof(T).IsAssignableFrom(type)
                           && !type.IsDefined(typeof(NotMappedAttribute), inherit: true))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        // Entity(Type) 与声明 DbSet 属性都能把类型加入 EF 模型，无需生成临时源码。
        foreach (var entityType in entityTypes)
            modelBuilder.Entity(entityType);

        // TBase 只是扫描边界，不把抽象基类本身建成表，也不使用 EF 的继承鉴别器映射。
        foreach (var entityType in entityTypes)
            modelBuilder.Entity(entityType).HasBaseType((Type?)null);

        foreach (var assembly in assemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly, configurationType => {
                var targets = configurationType.GetInterfaces()
                    .Where(contract => contract.IsGenericType
                                       && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                    .Select(contract => contract.GenericTypeArguments[0]).ToArray();
                // 一个配置类不能同时配置其他数据库实体，否则拒绝应用该配置。
                return targets.Length > 0 && targets.All(entityTypes.Contains);
            });

        ConfigureModel(modelBuilder);

        // 导航属性和手动配置也可能引入其他数据库的实体，发现后直接报错，禁止静默跨库建表。
        var foreignEntities = modelBuilder.Model.GetEntityTypes()
            .Where(entity => typeof(EntityBase).IsAssignableFrom(entity.ClrType)
                             && (entity.ClrType == typeof(T) || !typeof(T).IsAssignableFrom(entity.ClrType)))
            .Select(entity => entity.ClrType.FullName).ToArray();
        if (foreignEntities.Length > 0)
            throw new InvalidOperationException(
                $"当前上下文包含不属于 {typeof(T).Name} 的实体：{string.Join(", ", foreignEntities)}。请移除跨数据库导航或映射。");
    }

    /// <summary>补充或覆盖模型配置；实体、主外键、索引、关系和种子数据均可在此定义。</summary>
    protected virtual void ConfigureModel(ModelBuilder modelBuilder) {
    }
}