using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Machine.ModuleLoad.Mapper.Entity;
using Machine.ModuleLoad.Logger;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper;

/// <summary>权限数据库上下文，每次数据库操作单独创建并释放。</summary>
public sealed class PermissionDbContext(DbContextOptions<PermissionDbContext> options) : DbContext(options)
{
    public DbSet<PermissionUser> Users => Set<PermissionUser>();
    public DbSet<PermissionLevel> Levels => Set<PermissionLevel>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<PermissionGrant> Grants => Set<PermissionGrant>();

    /// <summary>统一记录迁移、查询与增删改 SQL，覆盖所有创建上下文的入口。</summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseGlobalLogger();
    }

    /// <summary>只补充特性无法表达的大小写规则；表、主外键及索引均由特性声明。</summary>
    protected override void OnModelCreating(ModelBuilder model) => ConfigureModel(model);

    /// <summary>运行时实体配置；迁移及快照使用独立的固定模型，不引用此方法。</summary>
    internal static void ConfigureModel(ModelBuilder model)
    {
        model.Entity<PermissionUser>().Property(x => x.Name).UseCollation("NOCASE");
        model.Entity<PermissionLevel>().Property(x => x.Name).UseCollation("NOCASE");
        model.Entity<PermissionDefinition>().Property(x => x.Key).UseCollation("NOCASE");
        model.Entity<PermissionGrant>().Property(x => x.PermissionKey).UseCollation("NOCASE");
    }
}
