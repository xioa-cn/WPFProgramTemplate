using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Machine.ModuleLoad.Mapper;

/// <summary>首次创建权限表结构，不包含明文密码种子。</summary>
[DbContext(typeof(PermissionDbContext))]
[Migration("202609210001_InitialPermissions")]
public sealed class InitialPermissions : Migration
{
    /// <summary>创建权限等级、账号、权限目录及授权关系。</summary>
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
                                                                            CREATE TABLE Levels (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT COLLATE NOCASE NOT NULL, Rank INTEGER NOT NULL);
                                                                            CREATE UNIQUE INDEX IX_Levels_Name ON Levels(Name);
                                                                            CREATE TABLE Permissions (Key TEXT COLLATE NOCASE NOT NULL PRIMARY KEY, Name TEXT NOT NULL, Kind INTEGER NOT NULL);
                                                                            CREATE TABLE Users (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT COLLATE NOCASE NOT NULL, PasswordHash TEXT NOT NULL, IsAdministrator INTEGER NOT NULL, LevelId INTEGER NULL, FOREIGN KEY(LevelId) REFERENCES Levels(Id) ON DELETE RESTRICT);
                                                                            CREATE UNIQUE INDEX IX_Users_Name ON Users(Name);
                                                                            CREATE INDEX IX_Users_LevelId ON Users(LevelId);
                                                                            CREATE TABLE Grants (LevelId INTEGER NOT NULL, PermissionKey TEXT COLLATE NOCASE NOT NULL, PRIMARY KEY(LevelId,PermissionKey), FOREIGN KEY(LevelId) REFERENCES Levels(Id) ON DELETE CASCADE, FOREIGN KEY(PermissionKey) REFERENCES Permissions(Key) ON DELETE CASCADE);
                                                                            CREATE INDEX IX_Grants_PermissionKey ON Grants(PermissionKey);
                                                                            """);

    /// <summary>按外键依赖顺序撤销表结构。</summary>
    protected override void Down(MigrationBuilder migration) =>
        migration.Sql("DROP TABLE Grants; DROP TABLE Users; DROP TABLE Permissions; DROP TABLE Levels;");
}