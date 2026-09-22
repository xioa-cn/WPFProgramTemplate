using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Machine.ModuleLoad.Mapper.Entity;
using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper;

/// <summary>本地身份认证、权限等级和账号管理服务。</summary>
public sealed class PermissionService
{
    private readonly DbContextOptions<PermissionDbContext> _options;

    public PermissionUser? CurrentUser { get; private set; }
    public event EventHandler? Changed;

    /// <summary>权限等级目录变化，不改变当前登录身份。</summary>
    public event EventHandler? CatalogChanged;

    public string DatabasePath { get; }
    public string RouterPath { get; }

    /// <summary>使用当前用户数据目录保存独立 SQLite 文件。</summary>
    public PermissionService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MachineApplication", "permissions.db"))
    {
    }

    /// <summary>指定数据库路径并初始化唯一的最高权限账号。</summary>
    public PermissionService(string path) : this(path, null)
    {
    }

    /// <summary>指定数据库与路由配置路径，便于测试隔离页面权限。</summary>
    public PermissionService(string path, string? routerPath)
    {
        DatabasePath = Path.GetFullPath(path);
        RouterPath = Path.GetFullPath(routerPath ?? Path.Combine(AppContext.BaseDirectory, "Router.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _options = new DbContextOptionsBuilder<PermissionDbContext>().UseSqlite(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = DatabasePath, ForeignKeys = true }
                .ToString()).Options;
        using var db = Open();
        // 此迁移使用人工维护的 SQLite DDL；只在首次创建时执行，不重置已有账号。
        db.Database.Migrate();
        if (!db.Users.Any())
        {
            db.Users.Add(new PermissionUser { Name = "xioa", PasswordHash = Hash("xioa"), IsAdministrator = true });
            db.SaveChanges();
        }
    }

    /// <summary>为当前操作创建独立上下文。</summary>
    private PermissionDbContext Open() => new(_options);

    /// <summary>最高权限全部放行，其他账号按路由配置中明确勾选的等级判断。</summary>
    public bool Allows(string key)
    {
        if (CurrentUser is null) return false;
        if (CurrentUser.IsAdministrator) return true;
        if (!key.StartsWith("page:", StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(RouterPath)) return false;
        // 每次读取路由权限，保存配置后立即生效；未勾选等级的页面仅最高权限可访问。
        using var document = JsonDocument.Parse(File.ReadAllText(RouterPath));
        var route = key[5..].Trim('/');
        var levelId = CurrentUser.LevelId;

        bool? Find(JsonElement nodes)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.TryGetProperty("Url", out var url) && string.Equals(url.GetString()?.Trim('/'), route,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!node.TryGetProperty("RequiredLevelIds", out var ids) || ids.ValueKind != JsonValueKind.Array)
                        return false;
                    return ids.EnumerateArray().Any(x => x.GetInt32() == levelId);
                }

                if (node.TryGetProperty("Children", out var children) && Find(children) is bool found) return found;
            }

            return null;
        }

        return document.RootElement.TryGetProperty("NavigationItems", out var items) && (Find(items) ?? false);
    }

    /// <summary>判断当前用户是否属于指定权限等级。</summary>
    public bool AllowsLevel(IEnumerable<int> levelIds) => CurrentUser?.IsAdministrator == true ||
                                                          (CurrentUser?.LevelId is int id && levelIds.Contains(id));

    /// <summary>操作执行前强制校验页面权限，防止绕过界面。</summary>
    public void Demand(string key)
    {
        if (!Allows(key)) throw new UnauthorizedAccessException("没有操作权限：" + key);
    }

    /// <summary>保护系统保留的最高权限账号，普通管理权限不可覆盖。</summary>
    private void DemandAdministrator()
    {
        if (CurrentUser?.IsAdministrator != true) throw new UnauthorizedAccessException("仅最高权限账号可以管理权限。");
    }

    /// <summary>校验账号与密码哈希，成功后加载权限快照。</summary>
    public bool Login(string name, string password)
    {
        using var db = Open();
        var user = db.Users.AsNoTracking().SingleOrDefault(x => x.Name == name.Trim());
        if (user is null || !Verify(password, user.PasswordHash)) return false;
        CurrentUser = user;
        Refresh();
        return true;
    }

    /// <summary>清除身份和授权，并通知界面。</summary>
    public void Logout()
    {
        CurrentUser = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>刷新当前身份的授权并广播变化。</summary>
    private void Refresh()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>读取自定义权限等级。</summary>
    public List<PermissionLevel> Levels()
    {
        if (CurrentUser is null) throw new UnauthorizedAccessException("请先登录。");
        using var db = Open();
        return db.Levels.AsNoTracking().OrderBy(x => x.Rank).ToList();
    }

    /// <summary>读取账号列表，不返回密码哈希。</summary>
    public List<PermissionUser> Users()
    {
        Demand("page:settings/users");
        using var db = Open();
        return db.Users.AsNoTracking().Select(x => new PermissionUser
            { Id = x.Id, Name = x.Name, LevelId = x.LevelId, IsAdministrator = x.IsAdministrator }).ToList();
    }

    /// <summary>保存等级名称和排序，页面访问范围在路由配置中指定。</summary>
    public void SaveLevel(int? id, string name, int rank)
    {
        Demand("page:settings/permissions");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var db = Open();
        using var transaction = db.Database.BeginTransaction();
        var level = id is int value ? db.Levels.Single(x => x.Id == value) : new PermissionLevel();
        level.Name = name.Trim();
        level.Rank = rank;
        if (id is null) db.Levels.Add(level);
        db.SaveChanges();
        transaction.Commit();
        NotifyCatalogChanged();
    }

    /// <summary>删除未被账号引用的等级。</summary>
    public void DeleteLevel(int id)
    {
        Demand("page:settings/permissions");
        using var db = Open();
        if (db.Users.Any(x => x.LevelId == id)) throw new InvalidOperationException("该等级仍有账号使用，请先调整账号等级。");
        db.Levels.Remove(db.Levels.Single(x => x.Id == id));
        db.SaveChanges();
        NotifyCatalogChanged();
    }

    /// <summary>新增账号或修改等级、密码，保留最高权限账号身份。</summary>
    public void SaveUser(int? id, string name, string password, int? levelId)
    {
        Demand("page:settings/users");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var db = Open();
        var user = id is { } value ? db.Users.Single(x => x.Id == value) : new PermissionUser();
        // 用户管理授权不能用于重置最高权限账号的密码。
        if (user.IsAdministrator) DemandAdministrator();
        if (!user.IsAdministrator && (levelId is null || !db.Levels.Any(x => x.Id == levelId)))
            throw new InvalidOperationException("请选择权限等级。");
        if (user.IsAdministrator && name.Trim() != user.Name) throw new InvalidOperationException("最高权限账号不能重命名。");
        if (id is null || !string.IsNullOrEmpty(password)) user.PasswordHash = Hash(password);
        user.Name = name.Trim();
        user.LevelId = user.IsAdministrator ? null : levelId;
        if (id is null) db.Users.Add(user);
        db.SaveChanges();
    }

    /// <summary>删除普通账号，禁止删除最高权限账号。</summary>
    public void DeleteUser(int id)
    {
        Demand("page:settings/users");
        using var db = Open();
        var user = db.Users.Single(x => x.Id == id);
        if (user.IsAdministrator) throw new InvalidOperationException("不能删除最高权限账号。");
        db.Users.Remove(user);
        db.SaveChanges();
    }

    /// <summary>通知等级目录变化，供路由和用户管理刷新下拉列表。</summary>
    private void NotifyCatalogChanged()
    {
        CatalogChanged?.Invoke(this, EventArgs.Empty);
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>采用随机盐与 PBKDF2 保存密码，数据库不保存明文。</summary>
    private static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    /// <summary>使用固定时间比较校验密码，避免直接比较字符串。</summary>
    private static bool Verify(string password, string value)
    {
        var parts = value.Split(':');
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]),
            Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[0]), 210000, HashAlgorithmName.SHA256,
                32));
    }
}