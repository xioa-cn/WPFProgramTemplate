using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Machine.ModuleLoad.Mapper;

/// <summary>权限模型的迁移基线，由后续正式迁移更新，不能调用运行时实体配置代替固定快照。</summary>
[DbContext(typeof(PermissionDbContext))]
public sealed class PermissionDbContextModelSnapshot : ModelSnapshot
{
    /// <summary>固定初始权限模型的字段、类型、索引和关系，供 EF 检测尚未创建迁移的模型变化。</summary>
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("Machine.ModuleLoad.Mapper.Entity.PermissionDefinition", entity =>
        {
            entity.Property<string>("Key")
                .HasMaxLength(200)
                .HasColumnType("TEXT")
                .UseCollation("NOCASE");
            entity.Property<int>("Kind").HasColumnType("INTEGER");
            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT");
            entity.HasKey("Key");
            entity.ToTable("Permissions");
        });

        modelBuilder.Entity("Machine.ModuleLoad.Mapper.Entity.PermissionGrant", entity =>
        {
            entity.Property<int>("LevelId").HasColumnType("INTEGER");
            entity.Property<string>("PermissionKey")
                .HasMaxLength(200)
                .HasColumnType("TEXT")
                .UseCollation("NOCASE");
            entity.HasKey("LevelId", "PermissionKey");
            entity.HasIndex("PermissionKey");
            entity.ToTable("Grants");
        });

        modelBuilder.Entity("Machine.ModuleLoad.Mapper.Entity.PermissionLevel", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT")
                .UseCollation("NOCASE");
            entity.Property<int>("Rank").HasColumnType("INTEGER");
            entity.HasKey("Id");
            entity.HasIndex("Name").IsUnique();
            entity.ToTable("Levels");
        });

        modelBuilder.Entity("Machine.ModuleLoad.Mapper.Entity.PermissionUser", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");
            entity.Property<bool>("IsAdministrator").HasColumnType("INTEGER");
            entity.Property<int?>("LevelId").HasColumnType("INTEGER");
            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("TEXT")
                .UseCollation("NOCASE");
            entity.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("LevelId");
            entity.HasIndex("Name").IsUnique();
            entity.ToTable("Users");
        });

        modelBuilder.Entity("Machine.ModuleLoad.Mapper.Entity.PermissionGrant", entity =>
        {
            entity.HasOne("Machine.ModuleLoad.Mapper.Entity.PermissionLevel", "Level")
                .WithMany()
                .HasForeignKey("LevelId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.HasOne("Machine.ModuleLoad.Mapper.Entity.PermissionDefinition", "Permission")
                .WithMany()
                .HasForeignKey("PermissionKey")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            entity.Navigation("Level");
            entity.Navigation("Permission");
        });

        modelBuilder.Entity("Machine.ModuleLoad.Mapper.Entity.PermissionUser", entity =>
        {
            entity.HasOne("Machine.ModuleLoad.Mapper.Entity.PermissionLevel", "Level")
                .WithMany()
                .HasForeignKey("LevelId")
                .OnDelete(DeleteBehavior.Restrict);
            entity.Navigation("Level");
        });
    }
}
