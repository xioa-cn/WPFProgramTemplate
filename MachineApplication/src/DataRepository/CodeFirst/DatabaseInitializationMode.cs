namespace DataRepository.CodeFirst;

/// <summary>数据库初始化方式；首次建表与迁移升级不能混用。</summary>
public enum DatabaseInitializationMode
{
    /// <summary>应用已生成的 EF 迁移，适合需要持续升级表结构的数据库。</summary>
    Migrate = 0,

    /// <summary>按当前实体模型创建新数据库的表；不会升级已有表结构，仅用于原型或可丢弃数据库。</summary>
    EnsureCreated = 1
}
