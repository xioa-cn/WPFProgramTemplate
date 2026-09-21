using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

/// <summary>用户自定义的权限等级，序号仅用于排序，不会自动继承授权。</summary>
[Table("Levels"), Index(nameof(Name), IsUnique = true)]
public sealed class PermissionLevel
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    public int Rank { get; set; }
}

/// <summary>区分页面访问权限和按钮操作权限。</summary>
public enum PermissionKind { Page, Button }

/// <summary>权限目录；页面键格式为 page:路由，按钮键格式为 button:操作。</summary>
[Table("Permissions")]
public sealed class PermissionDefinition
{
    [Key, MaxLength(200)] public string Key { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    public PermissionKind Kind { get; set; }
}

/// <summary>权限等级与权限项的多对多授权关系。</summary>
[Table("Grants"), PrimaryKey(nameof(LevelId), nameof(PermissionKey))]
public sealed class PermissionGrant
{
    public int LevelId { get; set; }
    [Required, MaxLength(200)] public string PermissionKey { get; set; } = "";
    [ForeignKey(nameof(LevelId))] public PermissionLevel Level { get; set; } = null!;
    [ForeignKey(nameof(PermissionKey))] public PermissionDefinition Permission { get; set; } = null!;
}

/// <summary>本地账号；最高权限为系统保留标识，不能由普通等级授予。</summary>
[Table("Users"), Index(nameof(Name), IsUnique = true)]
public sealed class PermissionUser
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public bool IsAdministrator { get; set; }
    public int? LevelId { get; set; }
    [ForeignKey(nameof(LevelId)), DeleteBehavior(DeleteBehavior.Restrict)]
    public PermissionLevel? Level { get; set; }
}