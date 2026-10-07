using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using I18nExtensions;
using Machine.ModuleLoad.Mapper;
using MachineApplication.Entrance.Utils;
using MachineApplication.Entrance.ViewModels;
using Xunit;

namespace MachineApplication.Entrance.Tests;

[CollectionDefinition("Management language", DisableParallelization = true)]
public sealed class ManagementLanguageCollection { }

[Collection("Management language")]
public sealed class ManagementLanguageTests
{
    [Fact]
    public void SwitchingLanguageUpdatesOpenEditorsAndPreservesInput()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "ManagementLanguage", Guid.NewGuid().ToString("N"));
            string? originalCulture = null;
            try
            {
                var lang = ViewModelLocator.EntranceLang;
                originalCulture = LanguageManager.Instance.CurrentCulture;
                LanguageManager.Instance.ChangeLang("zh");
                var service = new PermissionService(Path.Combine(directory, "permissions.db"));
                Assert.True(service.Login("xioa", "xioa"));
                var levels = new PermissionSettingsViewModel(service);
                var users = new UserManagementViewModel(service);
                levels.NewLevelCommand.Execute(null);
                levels.LevelName = "自定义等级";
                levels.Rank = "invalid";
                levels.SaveLevelCommand.Execute(null);
                users.NewUserCommand.Execute(null);
                users.UserName = "custom-user";
                users.SaveUser("");
                Assert.Equal("新增权限等级", levels.EditorTitle);
                LanguageManager.Instance.ChangeLang("en");
                Assert.Equal("Add permission level", levels.EditorTitle);
                Assert.Equal("Enter a valid integer for the sort order.", levels.EditorError);
                Assert.Equal("Add user", users.EditorTitle);
                Assert.Equal("A password is required for new users.", users.EditorError);
                Assert.Equal("自定义等级", levels.LevelName);
                Assert.Equal("custom-user", users.UserName);
                Assert.True(levels.IsEditorOpen);
                Assert.True(users.IsEditorOpen);
                var converter = new ManagementFormatConverter();
                Assert.Equal("3 users", converter.Convert([lang.Management_UserCount, 3], typeof(string), null!, CultureInfo.InvariantCulture));
                levels.Rank = "1";
                levels.SaveLevelCommand.Execute(null);
                Assert.Equal("Permission level saved.", levels.Status);
                LanguageManager.Instance.ChangeLang("zh");
                Assert.Equal("权限等级已保存。", levels.Status);
                Assert.Equal("新增用户", users.EditorTitle);
                Assert.Equal("共 3 个用户", converter.Convert([lang.Management_UserCount, 3], typeof(string), null!, CultureInfo.InvariantCulture));
                foreach (var property in lang.GetType().GetProperties().Where(p => p.Name.StartsWith("Management_")))
                    Assert.NotEqual(property.Name, property.GetValue(lang));
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (originalCulture is not null) LanguageManager.Instance.ChangeLang(originalCulture);
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
