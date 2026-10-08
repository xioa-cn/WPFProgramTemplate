namespace WorkFlowCore.Nodes.Script;

[System.ComponentModel.DisplayName("脚本方法")]
[ST.Library.UI.NodeEditor.XTNode("脚本", "xioa", "1327916255@qq.com", "https://github.com/xioa-cn/",
    "调用类定义中的公开方法；未连接类定义时使用自身 CsxPad 脚本。实例模式每次创建并释放一个无参实例。")]
public sealed class ScriptMethodNode : ScriptNode, ST.Library.UI.NodeEditor.IEditorExecutableNode,
    ST.Library.UI.NodeEditor.IEditorNodeReadiness
{
    public ScriptMethodNode() : base("脚本方法")
    {
        DefinitionInput = InputOptions.Add("类定义", typeof(ScriptClassDefinition), true);
        DefinitionInput.HasDefaultValue = true;
        ArgumentsInput = InputOptions.Add("参数数组", typeof(object), true);
        ArgumentsInput.HasDefaultValue = true;
        foreach (var input in new[] { DefinitionInput, ArgumentsInput })
            input.DataTransfer += (_, args) =>
                input.Data = args.Status == ST.Library.UI.NodeEditor.ConnectionStatus.Connected
                    ? args.TargetOption.Data
                    : null;
        Output = OutputOptions.Add("返回值", typeof(object), false);
        Output.Description = "方法返回值，void 和 null 返回值均为 null。";
        Completed = OutputOptions.Add("完成", typeof(object), false);
        Completed.Description = "仅方法成功后输出流程信号，void 方法也能触发后续流程。";
    }

    [ST.Library.UI.NodeEditor.XTNodeProperty("方法名", "要调用的 public 方法名称；支持重载、可选参数及异步方法。")]
    public string MethodName { get; set; } = "Execute";

    [ST.Library.UI.NodeEditor.XTNodeProperty("静态方法", "启用调用 static 方法；关闭则每次创建无参实例，调用后释放。")]
    public bool IsStatic { get; set; } = true;

    [ST.Library.UI.NodeEditor.XTNodeProperty("参数（JSON 数组）", "未连接且未赋值时使用，例如 [1,\"文本\",true]；[] 表示无参数。")]
    public string ArgumentsJson { get; set; } = "[]";

    public ST.Library.UI.NodeEditor.XTNodeOption DefinitionInput { get; }
    public ST.Library.UI.NodeEditor.XTNodeOption ArgumentsInput { get; }
    public ST.Library.UI.NodeEditor.XTNodeOption Output { get; }
    public ST.Library.UI.NodeEditor.XTNodeOption Completed { get; }

    private ScriptClassDefinition? ReadDefinition(bool allowPending)
    {
        if (DefinitionInput.Data is ScriptClassDefinition definition)
        {
            definition.Validate();
            return definition;
        }

        if (DefinitionInput.ConnectionCount == 0) return GetDefinition();
        if (allowPending) return null;
        throw new InvalidOperationException("类定义输入尚未收到数据，请先执行类定义节点。");
    }

    private object[]? ReadArguments(bool allowPending)
    {
        if (ArgumentsInput.Data is object[] arguments) return arguments;
        if (ArgumentsInput.Data is not null) throw new ArgumentException("参数输入必须是 object[] 数组。");
        if (ArgumentsInput.ConnectionCount > 0)
        {
            if (allowPending) return null;
            throw new InvalidOperationException("参数数组输入尚未收到数据。");
        }

        using var document = System.Text.Json.JsonDocument.Parse(ArgumentsJson);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new ArgumentException("参数必须是 JSON 数组。");
        return document.RootElement.EnumerateArray().Select(ConvertArgument).ToArray();
    }

    private static object ConvertArgument(System.Text.Json.JsonElement element) => element.ValueKind switch
    {
        System.Text.Json.JsonValueKind.Null => null!,
        System.Text.Json.JsonValueKind.String => element.GetString()!,
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        System.Text.Json.JsonValueKind.Number when element.TryGetInt32(out var integer) => integer,
        System.Text.Json.JsonValueKind.Number when element.TryGetInt64(out var longInteger) => longInteger,
        System.Text.Json.JsonValueKind.Number => element.GetDouble(),
        System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(ConvertArgument).ToArray(),
        System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => ConvertArgument(property.Value)),
        _ => throw new ArgumentException("不支持的参数类型。")
    };

    public ST.Library.UI.NodeEditor.EditorNodeReadinessResult CanExecute(
        ST.Library.UI.NodeEditor.EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(MethodName);
            ReadDefinition(true);
            ReadArguments(true);
            return ST.Library.UI.NodeEditor.EditorNodeReadinessResult.Ready();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ST.Library.UI.NodeEditor.EditorNodeReadinessResult.NotReady(exception.Message);
        }
    }

    public ST.Library.UI.NodeEditor.EditorNodeExecutionResult Execute(
        ST.Library.UI.NodeEditor.EditorExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Output.Data = null;
        Completed.Data = null;
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(MethodName);
            var definition = ReadDefinition(false)!;
            var arguments = ReadArguments(false)!;
            var methodName = MethodName.Trim();
            var isStatic = IsStatic;
            var result = Task.Run(async () =>
            {
                if (isStatic)
                    return await CSharpScriptCore.Core.CSharpScriptRun.RunStaticMethodAsync(
                        definition.Code, definition.ClassName, methodName, definition.CreateOptions(), arguments,
                        context.CancellationToken).ConfigureAwait(false);
                await using var instance = await CSharpScriptCore.Core.CSharpScriptService
                    .GetServiceFromClassAsync<object>(
                        definition.Code, definition.ClassName, definition.CreateOptions(),
                        cancellationToken: context.CancellationToken).ConfigureAwait(false);
                if (!instance.Success) return (CSharpScriptCore.Models.ScriptResult)instance;
                return await CSharpScriptCore.Core.CSharpScriptService.ScriptClassExecuteMethodAsync<object, object>(
                    instance, methodName, arguments, context.CancellationToken).ConfigureAwait(false);
            }).GetAwaiter().GetResult();
            if (!result.Success) return Failure(result, context);
            context.CancellationToken.ThrowIfCancellationRequested();
            Output.TransferData(result.ReturnValue);
            Completed.TransferData(new ST.Library.UI.NodeEditor.EditorFlowSignal(context.ExecutionId));
            RuntimeText = Convert.ToString(result.ReturnValue, System.Globalization.CultureInfo.InvariantCulture) ??
                          "null";
            return ST.Library.UI.NodeEditor.EditorNodeExecutionResult.Success(
                $"{definition.ClassName}.{methodName} 调用成功。", Output, Completed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Output.Data = null;
            Completed.Data = null;
            RuntimeText = "方法调用失败";
            return ST.Library.UI.NodeEditor.EditorNodeExecutionResult.Failure(exception.Message);
        }
    }
}