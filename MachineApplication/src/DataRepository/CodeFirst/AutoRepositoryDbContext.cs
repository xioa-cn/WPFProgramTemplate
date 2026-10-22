using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DataRepository.CodeFirst;

/// <summary>按数据库实体基类自动扫描模型的现成上下文。</summary>
/// <typeparam name="TBase">当前数据库实体共同继承的抽象基类。</typeparam>
/// <remarks>也可以直接继承 RepositoryDbContext&lt;TBase&gt; 定义业务上下文。</remarks>
public sealed class AutoRepositoryDbContext<TBase> : RepositoryDbContext<TBase> where TBase : EntityBase {
    /// <summary>创建自动扫描上下文，连接字符串和数据库提供程序由注册代码指定。</summary>
    public AutoRepositoryDbContext(DbContextOptions<AutoRepositoryDbContext<TBase>> options) : base(options) {
    }

    /// <summary>只扫描实体基类所在程序集。</summary>
    protected override IEnumerable<Assembly> ConfigurationAssemblies => [typeof(TBase).Assembly];
}

// ### 1. 首次准备
//
// dotnet tool install --global dotnet-ef --version 10.0.12
// dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.12
//
// ### 2. 生成迁移文件
//
// dotnet ef migrations add InitialCreate --context OrderDbContext
//
// 将 OrderDbContext 替换为你的具体上下文类名，不是 RepositoryDbContext<T>。
//
// ### 3. 应用到数据库
//
// dotnet ef database update --context OrderDbContext
//
// ### 4. 后续修改实体后
//
//     dotnet ef migrations add AddOrderStatus --context OrderDbContext
//     dotnet ef database update --context OrderDbContext
//
// 每次使用新的迁移名称即可。
//
// 如果从解决方案目录执行，或启动项目不同：
//
// dotnet ef migrations add InitialCreate --context OrderDbContext --project ./src/YourBusiness/YourBusiness.csproj --startup-project ./YourStartup/YourStartup.csproj
//
// database update 同样可以指定这两个参数。启动项目需引用 Design 包，EF 工具必须能创建设计时上下文。
//
// 注意：如果已有数据库是通过 EnsureCreated 建立的，不能直接假定初始迁移可应用，需要先处理迁移基线，避免重复建表。