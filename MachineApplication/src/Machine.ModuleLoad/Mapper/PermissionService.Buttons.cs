using Machine.ModuleLoad.Mapper.Entity;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper;

/// <summary>按钮规则快照；未启用限制时不保留权限定义，空授权列表则表示仅管理员可用。</summary>
public sealed record ButtonPermissionRule(string Key, string Name, bool IsRestricted, int[] LevelIds);

public sealed partial class PermissionService
{
    private readonly object _buttonSync = new();
    private Dictionary<string, HashSet<int>>? _buttonGrants;

    /// <summary>仅通知按钮授权更新，不清空导航区域或关闭当前配置页面。</summary>
    public event EventHandler? ButtonPermissionsChanged;

    /// <summary>读取指定按钮的配置；只返回已有规则，不为查看操作写入数据库。</summary>
    public IReadOnlyList<ButtonPermissionRule> GetButtonPermissions(IEnumerable<string> keys)
    {
        Demand("page:btn/auth");
        var requestedKeys = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var key in requestedKeys) ValidateButtonKey(key);
        using var db = Open();
        var definitions = db.Permissions.AsNoTracking()
            .Where(item => item.Kind == PermissionKind.Button && requestedKeys.Contains(item.Key)).ToArray();
        var grants = db.Grants.AsNoTracking().Where(item => requestedKeys.Contains(item.PermissionKey)).ToArray();
        return definitions.Select(item => new ButtonPermissionRule(item.Key, item.Name, true,
            grants.Where(grant => string.Equals(grant.PermissionKey, item.Key, StringComparison.OrdinalIgnoreCase))
                .Select(grant => grant.LevelId).ToArray())).ToArray();
    }

    /// <summary>事务内保存单个按钮的完整授权；其他页面及按钮的规则保持不变。</summary>
    public void SaveButtonPermission(ButtonPermissionRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Demand("page:btn/auth");
        ValidateButtonKey(rule.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule.Name);
        if (rule.Name.Length > 200) throw new ArgumentException("按钮名称不能超过 200 个字符。");
        var levelIds = rule.IsRestricted ? rule.LevelIds.Distinct().ToArray() : [];
        using var db = Open();
        using var transaction = db.Database.BeginTransaction();
        if (db.Levels.Count(level => levelIds.Contains(level.Id)) != levelIds.Length)
            throw new InvalidOperationException("权限等级已变化，请刷新后重新配置。");

        var definition = db.Permissions.SingleOrDefault(item => item.Key == rule.Key);
        if (definition is not null && definition.Kind != PermissionKind.Button)
            throw new InvalidOperationException("权限标识不是按钮类型。");
        var oldGrants = db.Grants.Where(item => item.PermissionKey == rule.Key).ToArray();
        if (!rule.IsRestricted)
        {
            // 删除定义代表恢复页面默认权限，与保留定义但不勾选等级明确区分。
            db.Grants.RemoveRange(oldGrants);
            if (definition is not null) db.Permissions.Remove(definition);
        }
        else
        {
            if (definition is null)
            {
                definition = new PermissionDefinition { Key = rule.Key, Kind = PermissionKind.Button };
                db.Permissions.Add(definition);
            }
            definition.Name = rule.Name.Trim();
            db.Grants.RemoveRange(oldGrants.Where(item => !levelIds.Contains(item.LevelId)));
            var existingIds = oldGrants.Select(item => item.LevelId).ToHashSet();
            foreach (var levelId in levelIds.Where(levelId => !existingIds.Contains(levelId)))
                db.Grants.Add(new PermissionGrant { LevelId = levelId, PermissionKey = rule.Key });
        }

        db.SaveChanges();
        transaction.Commit();
        InvalidateButtonPermissions();
    }

    /// <summary>按钮仅在当前页面权限之上追加限制；未配置按钮不改变原有行为。</summary>
    private bool AllowsButton(string key)
    {
        lock (_buttonSync)
        {
            if (_buttonGrants is null)
            {
                using var db = Open();
                var definitions = db.Permissions.AsNoTracking().Where(item => item.Kind == PermissionKind.Button)
                    .Select(item => item.Key).ToArray();
                var grants = db.Grants.AsNoTracking().Where(item => item.Permission.Kind == PermissionKind.Button)
                    .Select(item => new { item.PermissionKey, item.LevelId }).ToArray();
                _buttonGrants = definitions.ToDictionary(key => key,
                    key => grants.Where(item => string.Equals(item.PermissionKey, key, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.LevelId).ToHashSet(), StringComparer.OrdinalIgnoreCase);
            }

            return !_buttonGrants.TryGetValue(key, out var allowed) ||
                   CurrentUser?.LevelId is int levelId && allowed.Contains(levelId);
        }
    }

    /// <summary>提交完成后丢弃缓存，并单独通知已打开页面重新计算按钮可操作状态。</summary>
    private void InvalidateButtonPermissions()
    {
        lock (_buttonSync) _buttonGrants = null;
        ButtonPermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>限制接口只能修改按钮命名空间，不能覆盖页面权限目录。</summary>
    private static void ValidateButtonKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!key.StartsWith("button:", StringComparison.Ordinal) || key.Length > 200)
            throw new ArgumentException("无效的按钮权限标识。", nameof(key));
    }
}
