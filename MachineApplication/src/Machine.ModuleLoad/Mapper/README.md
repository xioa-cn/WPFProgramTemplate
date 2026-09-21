# 动态权限

数据层位于 Machine.ModuleLoad/Mapper，EF Core + SQLite，表结构优先使用数据注解特性。
首次启动仅初始化最高权限账号 xioa，密码 xioa；后续启动不覆盖密码。
数据库路径：%LOCALAPPDATA%/MachineApplication/permissions.db。

## 两个独立管理入口

- 用户管理：page:settings/users。负责创建、修改和删除普通账号。
- 权限配置：page:settings/permissions。只录入权限等级名称和排序，不配置页面或按钮授权。
- 页面访问范围在路由配置中勾选允许的等级；未勾选时仅最高权限账号可访问。
- xioa 不受普通授权限制，普通管理账号不能重置或删除最高权限账号。
- 等级数字用于排序，不自动继承权限。

## 页面接入

NavigationService.Register 会登记 page:路由。实际导航、缓存复用和返回都会校验。
身份变化时清空旧区域内容与返回历史。未授权路由会显示拒绝提示。
业务方法必须调用 PermissionService.Demand 校验；不要只依赖界面禁用。

## 数据维护

每次操作创建和释放独立 DbContext，初始结构通过 InitialPermissions 迁移创建。
后续表结构变更需新增迁移，不要修改已执行的初始迁移。
密码使用随机盐 PBKDF2-SHA256 哈希，账号列表查询不返回哈希。
用户管理和权限配置使用不同视图模型，避免页面互相读取不属于自身授权的数据。
