using System.IO;
using System.Text.Json;
using Machine.ModuleLoad.Mapper;
using Xunit;

namespace MachineApplication.Entrance.Tests;

/// <summary>使用真实 SQLite 验证首次初始化、页面权限隔离和重新打开数据库。</summary>
public sealed class PermissionTests
{
    /// <summary>默认最高权限账号仅首次初始化，普通等级只能访问路由中明确勾选的页面。</summary>
    [Fact]
    public void SqlitePermissionsPersistAndRejectUnauthorizedActions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MachinePermissions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "test.db");
        var routerPath = Path.Combine(directory, "Router.json");
        try
        {
            var service = new PermissionService(path, routerPath);
            Assert.False(service.Allows("page:home"));
            Assert.False(service.Login("xioa", "wrong"));
            Assert.True(service.Login("xioa", "xioa"));
            Assert.True(service.Allows("unknown:new-feature"));
            Assert.Empty(service.Levels());
            Assert.Single(service.Users());

            service.SaveLevel(null, "操作员", 999);
            var level = Assert.Single(service.Levels());
            service.SaveUser(null, "operator", "password", level.Id);
            WriteRouter(routerPath, ("home", [level.Id]));

            service.Logout();
            Assert.True(service.Login("operator", "password"));
            Assert.True(service.Allows("page:home"));
            Assert.False(service.Allows("page:settings/permissions"));
            Assert.Throws<UnauthorizedAccessException>(() => service.Demand("page:settings/permissions"));
            Assert.Throws<UnauthorizedAccessException>(() => service.SaveLevel(null, "越权", 0));

            Assert.True(service.Login("xioa", "xioa"));
            service.SaveLevel(null, "用户管理员", 1);
            service.SaveLevel(null, "权限管理员", 2);
            var userManagerLevel = service.Levels().Single(x => x.Name == "用户管理员");
            var permissionManagerLevel = service.Levels().Single(x => x.Name == "权限管理员");
            service.SaveUser(null, "user-manager", "password", userManagerLevel.Id);
            service.SaveUser(null, "permission-manager", "password", permissionManagerLevel.Id);
            WriteRouter(routerPath,
                ("home", [level.Id]),
                ("settings/users", [userManagerLevel.Id]),
                ("settings/permissions", [permissionManagerLevel.Id]));

            Assert.True(service.Login("user-manager", "password"));
            Assert.True(service.Allows("page:settings/users"));
            Assert.False(service.Allows("page:settings/permissions"));
            Assert.NotEmpty(service.Users());
            Assert.Throws<UnauthorizedAccessException>(() => service.SaveLevel(null, "越权", 0));
            service.SaveUser(null, "new-user", "password", level.Id);
            Assert.Throws<UnauthorizedAccessException>(() => service.SaveUser(1, "xioa", "attack", null));

            Assert.True(service.Login("permission-manager", "password"));
            Assert.False(service.Allows("page:settings/users"));
            Assert.Throws<UnauthorizedAccessException>(() => service.Users());
            Assert.Throws<UnauthorizedAccessException>(() => service.SaveUser(null, "越权", "password", level.Id));
            service.SaveLevel(null, "新等级", 3);
            Assert.Equal(4, service.Levels().Count);

            var reopened = new PermissionService(path, routerPath);
            Assert.True(reopened.Login("xioa", "xioa"));
            Assert.Equal(5, reopened.Users().Count);
            Assert.Equal(4, reopened.Levels().Count);
            var admin = reopened.Users().Single(x => x.IsAdministrator);
            Assert.Throws<InvalidOperationException>(() => reopened.DeleteUser(admin.Id));
            reopened.SaveUser(admin.Id, "xioa", "new-password", null);
            Assert.False(new PermissionService(path, routerPath).Login("xioa", "xioa"));
            Assert.True(new PermissionService(path, routerPath).Login("xioa", "new-password"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    /// <summary>把页面访问等级写入独立路由文件，避免污染测试输出目录中的 Router.json。</summary>
    private static void WriteRouter(string path, params (string Url, int[] LevelIds)[] pages)
    {
        var items = pages.Select(page => new
        {
            LanguageKey = page.Url,
            Icon = "FileOutline",
            Url = page.Url,
            RequiredLevelIds = page.LevelIds
        });
        File.WriteAllText(path, JsonSerializer.Serialize(new { NavigationItems = items }));
    }
}
