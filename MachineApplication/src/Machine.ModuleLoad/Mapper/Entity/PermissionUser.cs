using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper.Entity;

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