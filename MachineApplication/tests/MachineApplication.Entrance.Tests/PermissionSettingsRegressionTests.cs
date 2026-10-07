using System.IO;
using Machine.ModuleLoad.Mapper;
using MachineApplication.Entrance.ViewModels;
using Xunit;

namespace MachineApplication.Entrance.Tests;

public sealed class PermissionSettingsRegressionTests
{
    [Fact]
    public void EditingCatalogDoesNotBroadcastIdentityChangeAndPreservesList()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PermissionEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var service = new PermissionService(Path.Combine(directory, "test.db"));
            Assert.True(service.Login("xioa", "xioa"));
            var identityChanges = 0;
            var catalogChanges = 0;
            service.Changed += (_, _) => identityChanges++;
            service.CatalogChanged += (_, _) => catalogChanges++;
            var model = new PermissionSettingsViewModel(service);
            model.NewLevelCommand.Execute(null);
            model.LevelName = "操作员";
            model.Rank = "invalid";
            model.SaveLevelCommand.Execute(null);
            Assert.True(model.IsEditorOpen);
            Assert.Empty(service.Levels());
            model.Rank = "10";
            model.SaveLevelCommand.Execute(null);
            Assert.False(model.IsEditorOpen);
            var level = Assert.Single(model.Levels);
            Assert.Same(level, model.SelectedLevel);
            model.EditLevelCommand.Execute(level);
            model.LevelName = "工程师";
            model.SaveLevelCommand.Execute(null);
            Assert.Equal("工程师", Assert.Single(model.Levels).Name);
            model.DeleteLevelCommand.Execute(model.SelectedLevel);
            Assert.Empty(model.Levels);
            Assert.Equal(0, identityChanges);
            Assert.Equal(3, catalogChanges);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }
}
