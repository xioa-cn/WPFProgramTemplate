# Microsoft Agent Framework 中文入门

这是一份面向 C# 初学者的文本聊天基础实现，不是重新写一个大框架。
沿用项目已有的 `Microsoft.Agents.AI` 和 `Microsoft.Agents.AI.OpenAI` **1.24.0**，没有升级 AI 依赖。
消息显示新增 `Markdig 1.1.2` 和 `AvalonEdit 6.3.1.120`；前者解析 Markdown，后者只负责代码展示。
注意包名是 **Agents（复数）**，不是 `Microsoft.Agent.AI`。

文件使用 UTF-8 中文。文件授权功能不自动调用模型，需由用户在界面启动任务。

本实现不调用带 `ExperimentalAttribute` 标记的 API，也不使用 `NoWarn` 或 `#pragma warning disable` 屏蔽诊断。
已读取当前安装包的程序集元数据：`GetResponsesClient()` 和接受 Responses 客户端的 `AsIChatClient(model)` 重载带有 `OPENAI001` 实验性标记，因此移除了该分支。
当前只内置 Chat Completions 适配；多模型、中转、流式输出与多轮对话仍然保留。

## 直接使用调试页面

启动 `MachineApplication.Startup` 后，在左侧菜单进入 **AI 调试 → 模型配置 / 对话调试 / 多 AI 聊天室 / 群聊配置**。
已添加项目引用、`modules.xml` 模块加载配置、`Router.json` 菜单和模块路由注册。
如果使用自己保存过的输出目录 `Router.json`，需要同步四个路由：`ai/models`、`ai/chat`、`ai/workflow`（现在是群聊）、`ai/group-chat/settings`。

### 多 AI 聊天室与独立配置页

聊天室采用即时通讯式布局：AI 消息靠左，自己的绿色气泡靠右；昵称首字和彩色头像区分发言人。时间提示仅在首条、跨日或间隔至少 5 分钟时显示；宽屏消息区限制阅读宽度，群聊正文使用紧凑的微软雅黑排版。底部输入区保留 Enter 换行、Ctrl+Enter 发送；“开始连续聊天”开关让启用成员按顺序持续接话，关闭后立即请求取消，不再发起下一次调用。先确认跨模型数据传递与持续调用费用；有草稿时先发送草稿，空聊天按群约定和人设自动开场。每次回复后间隔 1 秒，正常对话不设轮数上限；连续两轮所有成员都失败或返回空内容时自动停止，防止无效调用。离开页面、关闭程序或撤回授权都会停止；重启不自动恢复。取消是否立即在服务端停止生成及计费由服务商决定。右侧群信息栏在页面宽度不足 1040 时收起，可始终通过顶部设置按钮管理成员。配色跟随宿主明暗主题，自己的消息使用固定浅绿色与深色文字。

1. 在模型配置页配置并保存模型。群聊复用内存中的模型与密钥，不单独复制密钥。
2. 在群聊配置页编辑群名、群约定、1–8 位群友的昵称、人设、模型和参与开关；保存到 `%LOCALAPPDATA%/MachineApplication/AI/group-chat.json`。模型按名称、地址、模型编号精确匹配，缺失或歧义时必须重新选择，不会静默改用其他服务商。
3. 回到聊天室，确认跨模型数据传递后发送消息（Ctrl+Enter，Enter 换行）。每轮按列表顺序发言，后面的群友能看到前面的回复，不再是分析、审查、定稿流水线。人设用于群聊，会替代单模型配置里的系统提示词。
4. 普通发送按照配置执行 1–3 轮、最多 24 次成员调用（不含 SDK 内部重试）；连续模式忽略轮数上限，需用户手动开启，会持续产生调用与可能的费用。成员单次出错跳过并继续下一位；连续两轮全部失败时停止。关闭开关或离开聊天室会取消当前请求，保留收到的文字。
5. 最近最多 40 条、16000 个文本字符作为共享上下文，消息界面最多保留 200 条，单次输入 4000 字、单条回复上限 8000 字；长历史裁剪后较早内容可能遗忘。成员请求总截止时间使用其模型配置的超时秒数。上下文限制不是服务商 token 限制的精确替代。
6. 群聊仅支持文字消息，不包含语音播放、自动朗读或音色配置。旧群聊配置中的语音字段在读取时忽略，下次保存时移除；群友、人设和模型配置保留。

群聊配置不包含 API 密钥、跨模型授权或聊天记录；聊天记录仅在本次应用运行中保留。编辑成员会清除授权。群聊不注册文件或命令工具，模型文字不会自动作为命令执行。原协同页面与服务源码保留以便迁移，但不再注册为菜单入口。

### 模型配置页

1. 点击“新增模型”，填写配置名称、API 基础地址和服务商提供的模型编号。
2. 在掩码框输入密钥，或填写保存密钥的环境变量名称。直接输入的密钥优先。
3. 填写超时秒数和系统提示词。当前只支持 Chat Completions，不使用实验性 AI API。
4. “检查格式”只做本地检查；“测试连接”会发送真实问题，**可能产生费用**。
5. 点击“保存配置”保存所有模型；删除后也需要点击保存才会更新文件。

非密钥配置文件位于 `%LOCALAPPDATA%\MachineApplication\AI\models.json`，代码使用 UTF-8。
直接输入的密钥带有 `JsonIgnore` 标记，不会写入文件，重启后需要重新输入或读取环境变量。
环境变量输入框填写的是**名称**，不是密钥本身。例如填 `DEMOKEY`，需要先在 Windows 的“环境变量”设置中创建变量：名称为 `DEMOKEY`，值为你的真实 API Key；上方直接 Key 框可以留空。
页面按“当前进程 → 当前 Windows 用户 → 系统”的顺序寻找非空值，找不到时明确提示变量未配置或值为空，不要求同时填写两种凭据。
程序启动后新建的用户级或系统级变量也会被读取；但当前进程已经继承的非空旧值优先，修改已有变量后仍建议重新启动 IDE 和程序。
无鉴权的本机服务可输入 `local`。不要将官方密钥随意交给陌生中转站。
加载配置失败时不会自动覆盖原文件，界面会提示先备份，再决定是否保存。

### 对话调试页

- 选择模型，输入问题后点击发送，或按 **Enter**；**Shift+Enter** 换行，同时兼容 **Ctrl+Enter** 发送。输入法处理中的按键不触发发送，空白内容和生成期间也不会重复发送。
- 默认流式显示，可取消“流式”勾选以查看完整返回模式。
- 点击“停止”取消当前请求；“新会话”清空显示记录和模型上下文。
- 消息文本可以选择、复制；停留在底部时自动跟随新内容，向上翻阅时不强制滚动。
- 请求期间禁用切换模型、修改配置和重复发送，防止上下文混用。
- 改配置或切模型会丢弃旧上下文，不将旧聊天内容发送到新的服务商。
- 请求失败或取消后保留已显示的片段，下一次发送使用新会话。
- 离开页面时取消未完成的请求；空闲时在两个页面之间切换可以保留会话。
- HTTP 错误显示中文分类，不显示服务商的原始错误响应，以免泄漏敏感信息。
- 页面不保存聊天记录，不自动发送请求，不会调用设备或执行模型返回的代码。

页面与业务代码分开，便于学习：

```text
Views/AgentModelConfigPage.xaml       模型配置页面
Views/AgentChatPage.xaml              对话调试页面
ViewModels/AgentDebugViewModel.cs      配置命令、请求状态、取消及会话生命周期
Models/AgentModelProfile.cs           可绑定的模型配置及内存密钥
Models/AgentChatMessage.cs            可绑定的显示消息
Services/AgentProfileStore.cs         非密钥配置的本地存储
```

### 旧版多模型协同实现（已退出菜单，以下为保留代码说明）

界面采用双栏工作台：左侧集中配置任务与角色，右侧显示步骤卡片和 Markdown 阅读区，底部固定数据传递确认与运行/停止按钮。窄窗口自动切换为上下布局。页面专用样式位于 `Views/AgentWorkflowTheme.xaml`，全部颜色引用宿主动态主题资源，支持运行时切换明暗主题和主色，不覆盖其他页面样式。输出生成时跟随底部，向上阅读时暂停跟随；示例按钮只填入任务，不自动发起请求。

1. 在“模型配置”页配置所需服务及密钥，然后打开“多模型协同”。模型列表共享当前内存配置，无须重复输入密钥。
2. 输入任务，选择“并行分析 → 汇总”或“方案 → 审查 → 定稿”。
3. 为方案分析员、风险审查员、汇总定稿员分别选择模型，可重复选择同一个模型；每个角色仍使用独立会话。
4. 展开“角色提示词”可修改分工。这些提示词替代模型配置页的系统提示词，不会修改原模型配置。
5. 勾选跨模型数据传递和调用费用确认，点击“开始任务”。切换步骤可查看各自的流式输出，完成后自动选中汇总结果，可复制原始 Markdown。

并行流程让两个分析角色独立处理原始任务，全部成功后交给汇总员；串行流程把方案交给审查员，再将方案和审查结果交给定稿员。上游结果通过 JSON 作为参考资料传递，不作为系统指令，也不广播历史会话。提示词提醒不是安全隔离。文件输出模式由本地程序校验、预览并确认写入，不向模型开放任意磁盘、脚本或设备执行工具。

运行限制：最多并行 2 个步骤、3 次角色调用（不含底层 SDK 的重试）、输入上限 16000 字符。编排层不再按 32000 字符截断输出，续跑也允许复用超过该长度的成功结果；实际生成长度仍受服务商的输出 token 和上下文窗口限制，也受本机内存约束。界面发起的协同任务不限制总运行时长，不再在 5 分钟时自动停止；单次网络操作仍使用模型配置页的网络超时设置，服务商也可能有自己的超时。任一步骤失败会取消其他运行中的步骤并跳过后续步骤；空文本视为失败。停止或离开页面时取消整个任务，等待请求退出后释放会话，保留已接收内容。编排服务的可选 Timeout 参数仅供调用方显式指定，默认不启用计时取消。

任务开始前校验全部模型并复制连接配置，运行中修改源配置不会改变正在执行的任务。失败或取消后保留成功步骤，点击“重试并继续”使用原始任务、角色配置和授权目录，只重跑失败、取消或尚未执行的步骤；定稿员失败不会重新调用已成功的分析员和审查员。重试会重新生成该步骤的完整输出，不与上次残缺内容拼接，不自动无限重试。续跑期间锁定任务配置，需修改任务或模型时点击“新任务”并确认放弃续跑记录。

文件清单校验失败也会将定稿步骤标为失败，保留上游结果供继续生成，仍须预览并明确确认才写入。当前续跑记录仅在本次应用运行内保留，不支持程序重启恢复。角色选择、提示词和对话结果不自动保存到磁盘。文件模式只有明确确认的文件变更会写入授权目录；命令模式另有逐次执行确认。当前是固定模板编排，不包含任意 DAG 编辑器、动态群聊或持久化检查点。

### 通用命令工具（dotnet / node / npm / python / pip 等）

1. 在多模型协同页勾选“命令工具（每次执行前确认）”，选择工作目录。它与“生成项目文件”模式互斥，避免命令已写入后又提交旧文件清单覆盖。
2. 正常配置三个角色，给出实际任务，例如“用 dotnet new wpf 创建 DemoApp，不创建测试项目”或“用 npm 创建前端项目并安装依赖”。分析员和审查员先给方案，只有定稿员接收真正的 `execute_command` 函数工具，避免并行角色同时修改项目。
3. 在“待批准的命令”中核对完整命令、解释器、目录和目的，点击“批准本次执行”或“拒绝”。选择目录、同意模型费用都不等于批准任意命令。未批准不启动进程，拒绝结果返回模型，不自动执行替代命令。
4. 命令通过系统 Windows PowerShell 以非交互、无配置、隐藏窗口方式运行；可以调用本机 PATH 中已安装的工具，不限固定 CLI 名单。`npm` 建议使用 `npm.cmd`，`pip` 建议使用 `python -m pip`；不支持 Bash 语法、PowerShell 7 专有语法或交互输入。工具未安装或 PATH 不正确时，真实错误会返回模型，程序不会偷偷安装工具。
5. stdout、stderr 按流写入“实际操作记录”，真实退出码及完整输出返回模型，模型可基于结果继续请求下一条命令。界面仅保留最近 150 条显示记录，当前任务的工具结果及续跑命令记录保留在内存中；不添加 32000 字符截断或命令总超时。单个角色可能进行多轮模型请求和多次工具调用，费用不再仅对应三次请求，仍受服务商上下文与本机资源限制。
6. “停止”及离开页面会取消待批准请求，并尝试结束当前命令的进程树。失败续跑保留成功角色和此前命令结果，要求定稿员先核对已产生的副作用，再决定后续动作；不是进程断点恢复，也不会自动重放命令。命令写入不提供文件模式的覆盖备份或自动回滚，请使用版本控制。

**安全边界：这不是系统级沙箱。** 只校验初始工作目录在选择目录内且不含重解析点，不阻止已批准命令访问目录外、网络、环境变量、删除文件或运行依赖的安装脚本。命令继承应用进程的当前用户权限；目录选择不是文件访问隔离。不要批准不理解的命令，也不要让命令读取或输出密钥。命令输出将发给所选模型服务商。后台/脱离父进程的任务可能无法完整回收，不应通过本工具启动长期后台服务。

所选定稿模型及中转必须支持 Chat Completions 工具调用；只返回命令文本不等于执行成功。工具能力只接入多模型协同页，普通对话页仍不执行命令。服务入口为 `Services/AgentCommandSession.cs`，审批状态由 `AgentWorkflowViewModel` 管理，`AgentCore` / `AgentStepExecutor` 负责注册函数工具。

### 授权生成与写入项目文件

1. 勾选“生成项目文件（确认后写入）”，点击“选择授权工作目录”，选择已存在的本机项目目录。仅在提示词中写路径不构成授权；不允许磁盘根目录、网络目录和重解析点。
2. 描述要生成的文件、语言和要求，正常选择模型并启动协同。最终定稿员输出包含相对路径及完整内容的 JSON 文件清单；生成阶段不修改磁盘。
3. 文件清单通过校验后，点击“预览并写入”。逐项检查当前内容与拟写入内容，勾选确认，再点击“确认写入全部文件”。关闭或取消预览不会写入。
4. “实际操作记录”显示实际写入文件和备份路径。覆盖已有文件前，在同目录创建随机命名的 `.agent-backup-*.bak` 备份。备份可能含敏感信息，请自行妥善保管；程序不自动删除。

边界：一次 1–20 个文件；只允许常见源代码及文本配置扩展名；单文件最多 256 KB，总计最多 1 MB，仍受模型单步输出上限限制。现有文件需可按 UTF-8 解码，写入为 UTF-8 无 BOM。不允许路径穿越、绝对文件路径、隐藏路径、符号链接、目录联接、bin/obj/node_modules，也不允许删除文件。预览后文件内容改变时拒绝覆盖；切换目录、关闭文件模式或重新生成会废弃旧预览。

文件按顺序逐个写入，不提供跨文件事务：失败或取消时保留已写入文件，明确报告数量，不自动回滚。请使用版本控制，并避免其他进程在写入期间替换目录或修改文件；路径检查不是针对恶意本机进程的操作系统级沙箱。此功能不会自动读取并上传整个现有项目，也不会编译或运行生成代码；修改已有项目时应先提供必要源码上下文并仔细核对覆盖预览。

实现入口：`Services/AgentOrchestrationService.cs` 控制流程；`IAgentStepExecutor` 隔离模型调用，默认实现每个步骤创建并释放独立 `AgentConversation`；`ViewModels/AgentWorkflowViewModel.cs` 管理界面状态，不复用单模型聊天的会话。

离线测试：`dotnet test tests/AIAgentFrameworkModule.Tests/AIAgentFrameworkModule.Tests.csproj`。测试覆盖并行汇合、顺序依赖、配置快照、取消、超时、失败清理、输出限制，以及离屏 WPF 页面操作和窄屏布局；不发真实模型请求。

### 调试日志

模块已接入宿主的 `Machine.ModuleLoad.Logger.GlobalLogger`，不新建第二套文件日志：

- `GlobalLogger.Info/Warn/Error/Success` 写入所选文件日志，同时输出到调试日志；`GlobalLogger.DebuggerLogger?.Debug/Trace` 只输出调试日志，适合高频流程。
- 前缀按职责分组：`[AI.Module]` 模块注册与路由、`[AI.Config]` 配置读写与校验、`[AI.Agent]` 会话工厂、`[AI.Chat]` 请求与流式接收、`[AI.View]` 页面创建与离开。
- 附加调试器启动时会启用 `DebuggerLogger` 并另开控制台；未附加调试器时这些调用直接跳过，不产生额外开销。
- 只记录模型编号、基础地址、字符数与耗时，**不记录 API Key 和聊天正文**，避免密钥或内容进入日志文件。

### 对话显示与界面优化

对话页参考提供的截图：右侧用户气泡、左侧助手正文、底部圆角输入框。
页面颜色全部引用宿主的 MaterialDesign 主题资源（`MaterialDesign.Brush.*`），跟随明暗基色与主色一起切换，
不在模块内固定深色，也不覆盖宿主或其他模块的主题；代码块底色跟随主题，语法配色在深色与浅色两套之间自动切换。
顶部新对话按钮和模型配置入口均可直接使用。
模型页使用“连接设置 / 访问凭据 / 助手行为”三组卡片；窄窗口把模型列表移到表单上方。

支持的消息格式：

- Markdown 标题、段落、粗体、斜体、删除线、引用、分隔线、有序/无序列表、任务列表。
- 管道表格、链接、行内代码、围栏代码块。HTML 标签仅显示文字，不作为网页执行。
- Unicode Emoji 和常见 `:smile:`、`:heart:` 等短码；代码块、行内代码不做表情替换。
- 输入框提供常用表情选择器；也可以使用 Windows 的 Win + . 输入更多 Emoji。
- 代码块带语言标签、行号、滚动条和独立复制按钮；整条回答可复制原始 Markdown。
- 提供 C#、JS/TS、Python、SQL、JSON、XML/XAML/HTML 等常见代码的轻量词法高亮。
- 未识别的代码语言仍然显示原文并支持复制，不会执行模型返回的代码。
- 链接只允许 HTTP/HTTPS，点击后确认地址再打开。Markdown 图片 `![说明](图片地址)` 在回答结束后异步展示，保留说明文字，失败可重试；支持 PNG/JPEG/GIF/BMP 等系统可解码图片（GIF 显示静态帧），以及这些格式的 Base64 data 图片。
- 远程图片会向图片服务器发起独立请求，不附带模型 API Key 或 Cookie；禁止本地文件和内网地址（包括重定向），单图限制 8 MB、4000 万像素，预览最长边 1280 像素，最多并行加载 3 张，超时 20 秒。离开可视区域后取消未完成加载。
- 流式片段先进入 StringBuilder，每 33ms 合并一次属性通知；控件合并 Dispatcher 渲染任务，不再叠加第二层定时等待。生成中轻量追加原始文字，结束、取消或失败后再一次性渲染 Markdown 和代码高亮，避免反复创建代码编辑器。
- 消息列表使用像素滚动和回收式虚拟化，只创建可视区域及相邻缓存页的消息控件；用户向上翻阅时不强制跳回底部。
- 气泡先确定实际宽度，再按同一宽度测量正文高度，避免测量和排列阶段使用不同宽度造成反复换行；短消息仍按内容收缩。
- 已完成的消息在页面重新显示时不重复排版。代码框按行数收缩，仅在实际存在横向滚动条时预留对应高度；复制仍保留原始代码。
- 等待第一段回答时显示“正在思考”，这只是等待状态，不展示或推测模型的内部推理过程。

显示边界：暂不包含 LaTeX 公式排版、Mermaid 图、HTML 页面渲染、SVG 渲染和附件上传。普通文字链接和 HTML img 标签不自动转成图片；服务商的非文本图片附件尚未接入。
Emoji 的具体字形与是否彩色由 Windows 字体和 WPF 渲染能力决定，不保证与浏览器完全一致。
轻量词法高亮不是编译器或代码检查器；用户气泡按原始文字显示，助手回答才使用 Markdown 排版。

渲染代码位于 `Views/Controls/MarkdownMessageView.cs`、`Views/Controls/CodeHighlighting.cs`，
颜色和基础控件样式统一放在 `Views/AgentTheme.xaml`。新增包的 API 已参考本机包文档，未进行构建或界面验证。

## 1. 先理解几个概念

| 名词 | 初学者可以怎样理解 |
| --- | --- |
| 模型（Model） | 真正生成回答的程序；模型编号由服务商提供 |
| API | 你的 C# 程序与模型服务通信的接口，不是网页聊天页面 |
| Endpoint | 请求发到哪台服务器、哪个 API 基础路径 |
| API Key | 访问那个服务商的凭据，应保密，不等同于网页账号密码 |
| 提示词（Prompt） | 发给模型的文字要求 |
| 系统提示词（Instructions） | 给助手设定角色、语气和回答规则，不是权限隔离机制 |
| Agent | 在模型外面加上角色、会话、工具等能力的助手；协同页可显式启用需审批的命令工具 |
| Session | 一段对话的状态，让后续问题能使用前面的上下文 |
| 流式输出 | 不等整段回答完成，每生成一部分就返回一部分 |
| Token | 模型处理文字的计量单位，不是一个汉字固定等于一个 Token |
| 中转站 | 代为接收、转发 API 请求的服务，可能看到密钥和聊天内容 |

调用关系：

```text
你的业务代码
  → AgentCore 创建助手
  → AgentConversation 提供 AskAsync / AskStreamingAsync
  → ChatClientAgent 管理助手行为和会话
  → IChatClient 统一聊天接口
  → OpenAI SDK 的 Chat Completions 客户端
  → 官方服务 / 兼容服务 / 中转站 / 本机服务
```

## 2. 推荐阅读顺序

1. `Agent/AgentModelOptions.cs`：认识地址、模型、密钥等配置。
2. `Examples/AgentExamples.cs`：先看如何调用，不急着理解全部内部实现。
3. `Agent/AgentCore.cs`：理解模型协议如何转换成统一接口。
4. `Agent/AgentConversation.cs`：理解会话、普通回答和流式输出。
5. `AgentFrameworkModule.cs`：了解如何注册到现有模块的服务容器。

代码使用普通类、属性、if、async/await 和 try/finally。
没有引入反射注册、复杂泛型工厂、自动路由、向量数据库或多 Agent 编排。
方法、配置属性和用到的 SDK 对象、方法、属性在使用处都有中文说明。

## 3. 最小调用

下面的代码放在一个 `async Task` 或 `async Task<string>` 方法内部。
调用文件需要导入 `AIAgentFrameworkModule.Agent` 命名空间。

```csharp
// 配置只是数据，创建配置不会发送请求。
AgentModelOptions options = new AgentModelOptions();
options.Endpoint = "https://你的服务商域名/v1";
options.Model = "服务商提供的模型编号";
options.ApiKey = Environment.GetEnvironmentVariable("AI_API_KEY") ?? "";
options.ApiType = AgentApiType.ChatCompletions;
options.Instructions = "你是一位 C# 老师，请用中文解释，示例尽量简单。";

AgentCore core = new AgentCore();

// using 会在当前作用域结束时自动释放会话。
using AgentConversation chat = core.CreateChat(options);

// 第一次提问才真正访问模型，可能产生费用。
string answer = await chat.AskAsync("什么是 C# 的类？");

// 同一个 chat 保留上下文，不要每次提问都重新 CreateChat。
string nextAnswer = await chat.AskAsync("请把刚才的解释再简化一点。");

// 开始新的话题，不再使用前面的本地会话。
await chat.ResetAsync();
```

示例地址和模型编号都是占位符，必须换成服务商实际提供的值。
修改 `options` 不会改变已经创建的会话；切换配置后需要重新 `CreateChat`。

## 4. 多模型和中转怎样适配

**先判断接口协议，再填写地址、模型、密钥。品牌名称不能替代协议适配。**

| 连接方式 | 如何配置 |
| --- | --- |
| OpenAI Chat Completions | 基础地址使用官方 API 地址，选择 `ChatCompletions` |
| 仅支持 Responses 的服务 | 本版本不接入，避免使用当前依赖中的实验性 API |
| 提供 Chat Completions 兼容接口的其他模型服务 | 填该服务的基础地址、模型编号、密钥，选择 `ChatCompletions` |
| 中转站 | 填中转站基础地址、中转模型编号和中转密钥，而不是盲目复用官方密钥 |
| 无鉴权的本机兼容服务 | 填本机实际地址，密钥可以填 `local`，模型填写本机加载的编号 |
| 仅提供其他原生协议的服务 | 确认对应适配器及其 API 没有实验性标记后，获得 `IChatClient`，使用工厂的第二个重载 |

例如你想连接 DeepSeek、通义、Gemini、Claude 等模型时，先向服务商确认它提供的是原生接口还是兼容接口。
本实现没有声称原生协议互通，也没有内置这些服务商全部专用 SDK。
兼容协议只代表请求形式可以适配，具体模型、流式输出、提示词等功能仍需服务端支持。

```csharp
// firstOptions、secondOptions 是分别填写好的两份 AgentModelOptions。
AgentCore core = new AgentCore();
using AgentConversation firstChat = core.CreateChat(firstOptions);
using AgentConversation secondChat = core.CreateChat(secondOptions);

string firstAnswer = await firstChat.AskAsync("解释 C# 接口。");
string secondAnswer = await secondChat.AskAsync("解释 C# 接口。");
```

两个对象拥有独立历史。更换模型时默认开始新会话，不偷偷把旧模型的聊天内容发给另一个服务商。

### 中转地址容易填错的地方

- 应填写 API **基础地址**，不要包含 `/chat/completions` 或 `/responses`。
- 如果服务商给出的完整接口是 `https://relay.example.com/api/v1/chat/completions`，这里填 `https://relay.example.com/api/v1`。
- 不会擅自添加 `/v1`，也不会丢弃服务商提供的 `/api` 等前缀。
- 远程地址要求 HTTPS；只有本机回环地址允许 HTTP。
- 当前实现不包含 HTTP/SOCKS 网络代理配置，中转 API 与网络代理不是一回事。
- 自定义鉴权头、URL 查询鉴权、特殊私有字段或非兼容协议，需要对应适配器，不能保证只换地址就可用。

## 5. 流式输出与取消

```csharp
// chat 是已经创建的 AgentConversation。
// CancellationTokenSource 是 .NET 的取消控制器，using 在作用域结束时释放它。
using CancellationTokenSource cancellation = new CancellationTokenSource();
cancellation.CancelAfter(TimeSpan.FromSeconds(60));

string fullAnswer = "";
try
{
    // 每个 text 都是新增片段，不是完整回答；实际界面可逐段追加到显示属性。
    await foreach (string text in chat.AskStreamingAsync("请介绍 C# 属性。", cancellation.Token))
    {
        fullAnswer += text;
    }
}
catch (OperationCanceledException)
{
    // 请求结束后才可以重置；不要把取消的令牌再传给重置操作。
    await chat.ResetAsync();
}
```

如果做“停止生成”按钮，将 `CancellationTokenSource` 保存在页面字段中，按钮调用它的 `Cancel()`。
停止后先等待提问方法结束，再释放会话。取消不代表服务商一定没有处理请求或计费。

流式循环里不要对同一会话调用 `AskAsync` 或 `ResetAsync` 并等待，否则会等待自己占用的锁。
要开始下一轮或重置，先离开 `await foreach` 循环。
提前 `break`、网络失败或取消都不保证当前轮历史完整，必要时先重置。
不要在还使用流式数据时提前释放 `chat`。

## 6. 现成示例怎么调用

先配置 `AI_ENDPOINT`、`AI_MODEL`、`AI_API_KEY` 三个环境变量。
目前仅内置 Chat Completions；如果服务只支持 Responses，不能通过修改 `ApiType` 接入。
环境变量在应用启动时继承，修改后通常需要重新启动 IDE 或宿主程序。
不要把真实密钥写在共享脚本或终端历史中。

```csharp
// 调用文件导入 AIAgentFrameworkModule.Examples 命名空间。
string answer = await AgentExamples.AskOnceAsync();
string historyAnswers = await AgentExamples.ChatWithHistoryAsync();

// 两份模型配置由你提供；此示例会分别发送一次请求。
string comparison = await AgentExamples.CompareModelsAsync(
    firstOptions, secondOptions, "请用一句话解释接口。");
```

这些示例方法不会自动执行，也没有增加启动请求；调试页面通过独立 ViewModel 调用框架。
WPF 页面中可通过 `await` 调用；实际聊天页面应保存会话字段供多轮使用，而不是每次按钮点击都创建新会话。
不要用 `.Result`、`.Wait()` 同步阻塞界面。

## 7. SDK 对象和方法速查

| 对象或方法 | 来源 | 在这里做什么 |
| --- | --- | --- |
| `ApiKeyCredential` | System.ClientModel | 保存鉴权凭据，交给 SDK 生成请求头 |
| `OpenAIClientOptions.Endpoint` | OpenAI | 配置实际请求的基础地址 |
| `OpenAIClientOptions.NetworkTimeout` | OpenAI / System.ClientModel | 设置网络操作超时 |
| `OpenAIClient` | OpenAI | 创建具体协议客户端 |
| `GetChatClient(model)` | OpenAI | 创建指定模型的聊天补全客户端 |
| `AsIChatClient()` | Microsoft.Extensions.AI.OpenAI | 将 ChatClient 适配为统一接口，仅使用非实验性重载 |
| `IChatClient` | Microsoft.Extensions.AI.Abstractions | 统一各种模型客户端的聊天能力 |
| `ChatClientAgent` | Microsoft.Agents.AI | 基于统一聊天接口创建助手 |
| `AIAgent` | Microsoft.Agents.AI.Abstractions | 助手的公共基类 |
| `CreateSessionAsync()` / `AgentSession` | Microsoft.Agents.AI | 创建、承载会话状态 |
| `RunAsync()` / `AgentResponse.Text` | Microsoft.Agents.AI | 运行助手，读取完整结果的文本 |
| `RunStreamingAsync()` / `AgentResponseUpdate.Text` | Microsoft.Agents.AI | 流式运行，读取当前更新的文本 |
| `IChatClient.Dispose()` | Microsoft.Extensions.AI.Abstractions | 释放客户端资源 |
| `AddSingleton<AgentCore>()` | Microsoft.Extensions.DependencyInjection | 在现有模块服务容器中注册共享工厂 |

工厂可以单例，但不要将所有用户的会话注册成同一个单例。
通过配置创建的客户端由会话释放；外部传入 `IChatClient` 默认由调用者管理生命周期。

## 8. 常见问题与边界

- 缺少配置：会抛出带中文说明的参数异常，先检查模型、密钥和基础地址。
- HTTP 401/403：检查密钥、权限、是否把不同服务商的密钥混用了。
- HTTP 404：检查基础路径、模型编号，并确认服务支持 Chat Completions。
- HTTP 400：检查服务端是否支持请求中的角色、模型或接口功能。
- HTTP 429：检查服务商限流、额度和账单，不要无上限重试。
- 超时或无响应：检查网络、服务可用性和超时设置；通过取消令牌控制整个调用时限。
- 不吞掉 SDK 异常，也不把错误当成模型回答；由界面层处理并显示适合用户的提示。
- 不记录密钥；服务商错误消息也可能包含请求细节，不要直接公开完整异常。
- 普通对话暂不包含语音、文件上传、工具调用、知识库、自动截断长历史和持久化；Markdown 图片展示和协同命令工具见前文。
- 上下文会增长，也可能增加费用或超过模型限制；学习阶段定期 `ResetAsync()`。
- 文本回答不等于可靠事实。普通回答不会作为代码运行；协同命令模式只在用户逐次批准函数调用后执行，未提供专门的 PLC 或设备操作工具。

## 9. 文档依据

实现签名参考当前机器已安装 NuGet 包的 XML 文档，主要为：

```text
%USERPROFILE%\.nuget\packages\microsoft.agents.ai\1.24.0\lib\net10.0\Microsoft.Agents.AI.xml
%USERPROFILE%\.nuget\packages\microsoft.agents.ai.abstractions\1.24.0\lib\net10.0\Microsoft.Agents.AI.Abstractions.xml
%USERPROFILE%\.nuget\packages\microsoft.extensions.ai.openai\10.10.1\lib\net10.0\Microsoft.Extensions.AI.OpenAI.xml
%USERPROFILE%\.nuget\packages\openai\2.14.0\lib\net10.0\OpenAI.xml
```

本次在线文档检索没有返回可用内容，所以没有以在线搜索结果宣称支持某个服务商的当前模型或套餐。
后续升级包时请重新确认 API 签名及实验性标记，不要通过屏蔽诊断继续使用实验性 API；实际连接结果由你在本机验证。
