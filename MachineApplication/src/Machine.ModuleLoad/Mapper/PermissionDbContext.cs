using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Machine.ModuleLoad.Mapper.Entity;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper;

/// <summary>权限数据库上下文，每次数据库操作单独创建并释放。</summary>
public sealed class PermissionDbContext(DbContextOptions<PermissionDbContext> options) : DbContext(options)
{
    public DbSet<PermissionUser> Users => Set<PermissionUser>();
    public DbSet<PermissionLevel> Levels => Set<PermissionLevel>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<PermissionGrant> Grants => Set<PermissionGrant>();

    /// <summary>只补充特性无法表达的大小写规则；表、主外键及索引均由特性声明。</summary>
    protected override void OnModelCreating(ModelBuilder model) => ConfigureModel(model);

    /// <summary>供上下文与初始迁移共享表结构配置。</summary>
    internal static void ConfigureModel(ModelBuilder model)
    {
        model.Entity<PermissionUser>().Property(x => x.Name).UseCollation("NOCASE");
        model.Entity<PermissionLevel>().Property(x => x.Name).UseCollation("NOCASE");
        model.Entity<PermissionDefinition>().Property(x => x.Key).UseCollation("NOCASE");
        model.Entity<PermissionGrant>().Property(x => x.PermissionKey).UseCollation("NOCASE");
    }
}







