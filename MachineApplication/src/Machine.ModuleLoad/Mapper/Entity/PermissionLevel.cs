using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper.Entity;

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