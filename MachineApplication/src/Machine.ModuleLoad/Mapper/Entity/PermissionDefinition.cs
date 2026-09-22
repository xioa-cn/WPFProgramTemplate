using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Machine.ModuleLoad.Mapper.Entity;

/// <summary>权限目录；页面键格式为 page:路由，按钮键格式为 button:操作。</summary>
[Table("Permissions")]
public sealed class PermissionDefinition
{
    [Key, MaxLength(200)] public string Key { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    public PermissionKind Kind { get; set; }
}
