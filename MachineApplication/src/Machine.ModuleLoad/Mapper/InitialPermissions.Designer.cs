using Microsoft.EntityFrameworkCore;

namespace Machine.ModuleLoad.Mapper;

public sealed partial class InitialPermissions
{
    /// <summary>初始迁移完成后的固定模型；后续实体变化不能改变此历史模型。</summary>
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
