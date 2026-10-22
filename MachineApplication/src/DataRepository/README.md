# DataRepository

基于 EF Core 10 的通用仓储与工作单元，不要求实体继承公共基类。保留 IQueryable，支持数据库端条件查询、关联加载、排序、投影和分页。

## 自动扫描实体并建表（无需 EF Tools）

EntityBase 是空标记基类。每个数据库定义自己的抽象基类，具体模型继承对应的数据库基类；无需声明 DbSet 或生成迁移文件。

```csharp
using DataRepository;
using DataRepository.CodeFirst;
using DataRepository.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public abstract class ProductDbEntity : EntityBase { }

public sealed class Product : ProductDbEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

在应用启动代码中注册并初始化：

```csharp
services.AddAutoDataRepository<ProductDbEntity>(options =>
    options.UseSqlite("Data Source=products.db"));

await serviceProvider.InitializeAutoRepositoryDatabaseAsync<ProductDbEntity>();

await using var scope = serviceProvider.CreateAsyncScope();
var unit = scope.ServiceProvider
    .GetRequiredService<IUnitOfWork<AutoRepositoryDbContext<ProductDbEntity>>>();
var products = unit.Repository<Product>();
await products.AddAsync(new Product { Name = "示例", Price = 100 });
await unit.SaveChangesAsync();
```

- ProductDbEntity 是数据库实体边界，只扫描它所在程序集中的具体子类。同程序集内其他 EntityBase 分支不会被自动注册。跨程序集模型可重写 ConfigurationAssemblies。
- EntityBase 不包含任何属性。主键由具体实体或数据库专属 Base 定义，可使用不同主键类型或 Fluent API 配置复合主键。各具体实体独立映射，默认表名为类型名，可通过 Table 特性覆盖。
- NotMapped 标记的类型不参与自动扫描。继承自带有 NotMapped 特性的类型也会被排除。导航关系仍遵循 EF 发现约定。
- 内部通过 ModelBuilder.Entity(Type) 注册模型，等价于把实体加入 DbSet 所对应的模型；仓储使用 Set<T>() 操作，不生成临时 C# 文件。
- 可以用实体特性配置字段、索引及关系；复杂映射仍可选用 IEntityTypeConfiguration。业务实体不应混用 Owned/Keyless 语义与该持久化实体基类。
- 多程序集模型可以继承 RepositoryDbContext<TBase> 并重写 ConfigurationAssemblies，返回固定的程序集集合；每种上下文的模型集合必须稳定，不能在运行时随意切换。
- 初始化使用 EnsureCreated，只创建首次数据库模型，不生成迁移文件。数据库已经存在表时，不会补建新表或同步字段变化。此实现是自动发现与首次建表，不是数据库结构差异同步引擎，也不会删除重建已有数据库。

### 多数据库隔离

```csharp
public abstract class OrderEntity : EntityBase { }
public abstract class LogEntity : EntityBase { }

public sealed class Order : OrderEntity
{
    public long Id { get; set; }
}

public sealed class LogEntry : LogEntity
{
    public Guid Id { get; set; }
}

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options)
    : RepositoryDbContext<OrderEntity>(options)
{
}

public sealed class LogDbContext(DbContextOptions<LogDbContext> options)
    : RepositoryDbContext<LogEntity>(options)
{
}
```

```csharp
services.AddDataRepository<OrderDbContext>(options => options.UseSqlite("Data Source=orders.db"));
services.AddDataRepository<LogDbContext>(options => options.UseSqlite("Data Source=logs.db"));
await serviceProvider.InitializeRepositoryDatabaseAsync<OrderDbContext>(DatabaseInitializationMode.EnsureCreated);
await serviceProvider.InitializeRepositoryDatabaseAsync<LogDbContext>(DatabaseInitializationMode.EnsureCreated);
```

映射类也按数据库实体过滤；不要在同一个映射类中实现不同数据库的实体配置。跨数据库实体导航会报错，应使用普通外部标识字段而不是 EF 导航关系。数据库基类请声明为 abstract；仅作为扫描边界，不注册为实体表。

## 手动上下文注册和获取（可选）

```csharp
using DataRepository;
using DataRepository.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
services.AddDataRepository<AppDbContext>();

await using var scope = serviceProvider.CreateAsyncScope();
var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork<AppDbContext>>();
var products = unit.Repository<Product>();
```

AppDbContext、Product 和 connectionString 为业务项目定义。注册入口只注册对应上下文的 Scoped 工作单元，不自动注册 DbContext 或无上下文限定的 IRepository<T>，避免多个数据库之间产生歧义。同一工作单元缓存各实体仓储，所有仓储共享同一个上下文。

桌面程序应为每次业务操作创建并释放作用域，不要从根容器长期持有工作单元。手动构造 Repository<T> 或 UnitOfWork<TContext> 时，上下文由调用方释放。上下文、仓储和工作单元均不支持并发调用。

## Code First 建模与建表

业务项目继承 RepositoryDbContext<TBase>。默认扫描 TBase 所在程序集，只应用属于该数据库实体的 IEntityTypeConfiguration 映射类，也可以通过实体特性或 ConfigureModel 配置模型。

```csharp
using DataRepository.CodeFirst;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public abstract class ProductDbEntity : EntityBase { }

public sealed class Product : ProductDbEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : RepositoryDbContext<ProductDbEntity>(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(product => product.Name);
    }
}
```

注册上下文与仓储，并在开始业务操作前显式初始化：

```csharp
services.AddDataRepository<AppDbContext>(options => options.UseSqlite(connectionString));

await serviceProvider.InitializeRepositoryDatabaseAsync<AppDbContext>(
    DatabaseInitializationMode.EnsureCreated);
```

EnsureCreated 根据实体模型首次建表，但不会为已有表自动增加字段或更新结构；数据库已包含任何表时也不会补建其他表。仅适合原型或可丢弃数据库，不是迁移方案。仓储不删除或重建已有数据库。

## Code First 迁移升级

### 一键生成迁移文件

提供 PowerShell 7 脚本 `Scripts/Add-Migration.ps1`。从仓库根目录运行，将示例业务项目路径和上下文名称替换为实际值：

```powershell
./src/DataRepository/Scripts/Add-Migration.ps1 InitialCreate -Project ./src/Business/Business.csproj -Context OrderDbContext
./src/DataRepository/Scripts/Add-Migration.ps1 AddOrderStatus -Project ./src/Business/Business.csproj -Context OrderDbContext
```

脚本生成迁移 `.cs`、Designer 文件及模型快照。第二次执行时使用新的迁移名称，EF 会比较当前模型与上一次快照，而不是比较数据库实际结构。

- `-StartupProject`：可选，默认与 Project 相同；启动项目需能加载业务上下文。
- `-OutputDir`：默认 Migrations，必须位于迁移项目内部。多上下文建议分别指定 Migrations/Orders、Migrations/Logs。
- `-Framework`、`-Configuration`、`-Namespace`：可选目标框架、Debug/Release 和迁移命名空间。
- `-ContextArgs`：通过 `--` 传递给设计时工厂的参数；不要把密码写在命令行上，建议使用环境变量。
- `-WhatIf`：只校验路径并预览操作，不需要安装 dotnet-ef，不构建、不生成文件。

首次使用需安装匹配版本的工具，例如 `dotnet tool install --global dotnet-ef --version 10.0.12`；若使用仓库已有的本地工具清单，则执行 `dotnet tool restore`。启动项目需要引用 `Microsoft.EntityFrameworkCore.Design` 10.0.12（PrivateAssets=all）。脚本不会自动安装工具或修改项目依赖。

请使用继承 `RepositoryDbContext<OrderEntity>` 的具体非泛型业务上下文（例如 OrderDbContext），不要传入抽象泛型基类。桌面程序建议按下文提供 `IDesignTimeDbContextFactory<OrderDbContext>`，工厂只构造上下文，不执行建表、迁移或其他数据库操作。

脚本不执行数据库更新；但 EF 工具会执行设计时工厂或启动代码，因此这些代码本身也必须避免业务副作用。生成后先审查迁移，尤其是字段重命名、删除和类型转换。

需要持续升级表结构时，从第一版开始使用迁移，不要先使用 EnsureCreated。

在包含业务上下文的项目中添加 Microsoft.EntityFrameworkCore.Design（版本与 EF Core 一致，当前为 10.0.12，设置 PrivateAssets=all），启动项目也需要引用设计时包。安装匹配版本的 dotnet-ef 工具。下面的 Business 项目名需要替换为实际业务项目路径：

```powershell
dotnet ef migrations add InitialCreate --project Business --startup-project Business --context AppDbContext
dotnet ef migrations script --idempotent --project Business --startup-project Business --context AppDbContext
dotnet ef database update --project Business --startup-project Business --context AppDbContext
```

幂等脚本支持取决于提供程序，SQLite 不支持该方式；SQLite 请使用 database update 或指定迁移范围生成脚本。

桌面应用如果无法由工具构造上下文，在业务项目中提供设计时工厂，确保与运行时使用相同的提供程序和模型配置：

```csharp
using Microsoft.EntityFrameworkCore.Design;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APP_DATABASE")
            ?? throw new InvalidOperationException("请配置 APP_DATABASE 连接字符串。");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString).Options;
        return new AppDbContext(options);
    }
}
```

部署阶段也可显式应用已经生成并编译的迁移：

```csharp
await serviceProvider.InitializeRepositoryDatabaseAsync<AppDbContext>();
```

默认模式为 Migrate，没有迁移时会报错提示生成迁移，不会静默退回 EnsureCreated。模型修改后需要再次生成、审查并应用迁移，不会仅凭修改实体就在生产环境自动改表。存在迁移时禁止使用 EnsureCreated；对历史上由 EnsureCreated 建立的数据库，需要另外制定迁移基线方案，不能直接切换模式。

迁移属于业务上下文所在项目，通用仓储项目不内置业务实体、连接字符串或迁移文件。生产环境优先在受控部署步骤应用迁移，执行前备份并审查潜在的数据丢失操作。此处只提供代码入口，尚未连接或修改任何业务数据库。

## 查询和分页

```csharp
var specification = new QuerySpecification<Product>
{
    Criteria = product => product.Price >= 100,
    OrderBy = query => query.OrderBy(product => product.Price).ThenBy(product => product.Id)
};

var page = await products.PageAsync(new PageRequest(1, 20), specification,
    product => new { product.Id, product.Name, product.Price });
```

- Query、ListAsync、FirstOrDefaultAsync 和 PageAsync 默认不跟踪；更新查询可设置 Tracking = true。
- Include 可传入 query => query.Include(...).ThenInclude(...)；SplitQuery 可开启拆分查询。Include 委托仅用于关联加载，不应添加过滤、分页等会改变根结果集的操作，否则分页总数可能不一致。
- 分页从第 1 页开始，默认每页 20 条，最多 1000 条，校验偏移量溢出。必须指定 OrderBy，调用方必须包含唯一键以保证排序稳定。
- 分页总数和数据分两次顺序查询；如需并发写入下的一致快照，应使用数据库支持的适当事务隔离级别。
- ListAsync 不限制结果条数，大数据量请使用分页，或通过 Query 组合 Take、游标条件及 EF 异步操作。
- FindAsync 接受主键数组，复合主键按 EF 模型定义的顺序传入；它优先返回已跟踪实体并跟踪数据库查询结果。它不是授权检查入口，也不能用来判断已跟踪实体是否仍满足全局过滤器。
- Query 返回的延迟查询必须在上下文存活期间执行。默认保留 EF 全局查询过滤器。

## 写入和事务

```csharp
await products.AddAsync(new Product { Name = "示例", Price = 100 });
await unit.SaveChangesAsync();

await using var transaction = await unit.BeginTransactionAsync();
try
{
    await products.ExecuteUpdateAsync(product => product.Price < 100,
        setters => setters.SetProperty(product => product.Price, 100));
    await unit.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

- AddAsync、AddRangeAsync、Update、UpdateRange、Remove 和 RemoveRange 只修改跟踪状态，必须调用工作单元 SaveChangesAsync 才会持久化。
- Update 会标记实体图为修改状态；只更新部分字段时，优先查询 Tracking = true 的实体并修改所需属性。
- ExecuteUpdateAsync、ExecuteDeleteAsync 直接执行数据库 SQL，不等待 SaveChangesAsync，不同步已跟踪实体；不要与同一实体的跟踪修改混用。
- 批量操作要求显式条件；条件恒真仍可影响整张表。删除是物理删除，不自动实现软删除或审计逻辑。
- EF 实体更新的并发异常直接向上抛出；批量 SQL 不自动检查并发令牌，需要在条件中包含版本并检查受影响行数。
- 显式事务由调用方提交、回滚和释放，不支持嵌套事务。回滚不会恢复内存中的跟踪状态，失败后应释放当前作用域重新创建。
- 配置了数据库重试执行策略时，应按 EF 提供程序约定将整个事务纳入执行策略，并保证重试操作幂等；仓储不自动重放业务操作。

按要求未添加或运行测试；已完成项目构建验证。
