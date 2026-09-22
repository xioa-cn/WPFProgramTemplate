using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper.Entity;

/// <summary>权限等级与权限项的多对多授权关系。</summary>
[Table("Grants"), PrimaryKey(nameof(LevelId), nameof(PermissionKey))]
public sealed class PermissionGrant
{
    public int LevelId { get; set; }
    [Required, MaxLength(200)] public string PermissionKey { get; set; } = "";
    [ForeignKey(nameof(LevelId))] public PermissionLevel Level { get; set; } = null!;
    [ForeignKey(nameof(PermissionKey))] public PermissionDefinition Permission { get; set; } = null!;
}