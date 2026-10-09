using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ST.Library.UI.NodeEditor;

public partial class XTNodeEditor
{
    private readonly Dictionary<string, Type> _registeredTypes = new(StringComparer.Ordinal);

    /// <summary>显式登记允许反序列化的节点类型；画布文件不能自行加载任意程序集。</summary>
    public void RegisterNodeType(Type type)
    {
        if (!typeof(XTNode).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) is null)
            throw new ArgumentException("节点必须是有公共无参构造函数的具体 XTNode 类型。", nameof(type));
        _registeredTypes[TypeKey(type)] = type;
        // 仅为已登记的内置中转节点保留旧文件类型别名，不扩大可反序列化类型的范围。
        if (type == typeof(XTNodeHub))
            _registeredTypes[$"{type.Assembly.GetName().Name}:ST.Library.UI.NodeEditor.STNodeHub"] = type;
    }
    private static string TypeKey(Type type) => $"{type.Assembly.GetName().Name}:{type.FullName}";
    public bool LoadAssembly(string file)
    {
        foreach (var type in LoadableTypes(Assembly.LoadFrom(Path.GetFullPath(file))))
            if (typeof(XTNode).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) is not null)
                RegisterNodeType(type);
        return true;
    }
    public int LoadAssembly(string[] files)
    {
        var count = 0;
        foreach (var file in files) if (LoadAssembly(file)) count++;
        return count;
    }
    internal static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException exception) { return exception.Types.OfType<Type>(); }
    }
    public Type[] GetTypes() => _registeredTypes.Values.Distinct().ToArray();

    /// <summary>WPF 画布格式保存节点布局、自定义属性和连接顺序，不保存运行时端口数据。</summary>
    public byte[] GetCanvasData()
    {
        VerifyAccess();
        var document = new CanvasDocument { OffsetX = CanvasOffsetX, OffsetY = CanvasOffsetY, Scale = CanvasScale, VerticalPorts = VerticalPorts };
        foreach (var node in Nodes)
            document.Nodes.Add(new NodeDocument
            {
                Type = TypeKey(node.GetType()), Id = node.Guid, Left = node.Left, Top = node.Top,
                Width = node.Width, Height = node.Height, Mark = node.Mark, LockLocation = node.LockLocation,
                LockOption = node.LockOption, Properties = node.SaveState()
            });
        foreach (var connection in _connections)
            document.Connections.Add(new ConnectionDocument
            {
                OutputNode = connection.Output.Owner!.Guid, InputNode = connection.Input.Owner!.Guid,
                OutputIndex = connection.Output.Owner.OutputOptions.IndexOf(connection.Output),
                InputIndex = connection.Input.Owner.InputOptions.IndexOf(connection.Input)
            });
        return JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = true });
    }
    public void SaveCanvas(Stream stream) { ArgumentNullException.ThrowIfNull(stream); stream.Write(GetCanvasData()); }
    public void SaveCanvas(string file)
    {
        var data = GetCanvasData();
        var target = Path.GetFullPath(file);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, data); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void LoadCanvas(string file)
    {
        using var stream = File.OpenRead(file);
        LoadCanvas(stream);
    }
    public void LoadCanvas(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = stream.Read(chunk, 0, chunk.Length)) != 0)
        {
            if (buffer.Length + count > 32 * 1024 * 1024) throw new InvalidDataException("画布文件超过 32MB 限制。");
            buffer.Write(chunk, 0, count);
        }
        LoadCanvas(buffer.ToArray());
    }

    /// <summary>先在独立编辑器构建并校验，成功后才替换现有画布；失败不清空用户工作。</summary>
    public void LoadCanvas(byte[] data)
    {
        VerifyAccess();
        LoadCanvas(ParseCanvasData(data));
    }

    internal static CanvasDocument ParseCanvasData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > 32 * 1024 * 1024) throw new InvalidDataException("画布文件超过 32MB 限制。");
        if (data.AsSpan().StartsWith("STND"u8))
            throw new InvalidDataException("此文件是 WinForms STND 二进制格式，请先在旧版本导出业务数据后迁移为 WPF 画布格式。");
        var document = JsonSerializer.Deserialize<CanvasDocument>(data) ?? throw new InvalidDataException("空画布文件。");
        // 旧版格式标识仅用于兼容读取，重新保存时自动写入新格式标识。
        if (document.Format is not ("XTNode.WPF" or "STNode.WPF") || document.Version != 1 || document.Nodes is null || document.Connections is null)
            throw new InvalidDataException("不支持的 WPF 画布格式或版本。");
        if (!float.IsFinite(document.Scale) || document.Scale is < .2f or > 3f ||
            !float.IsFinite(document.OffsetX) || !float.IsFinite(document.OffsetY) || document.Nodes.Count > 10000 || document.Connections.Count > 100000)
            throw new InvalidDataException("画布尺寸或节点数量无效。");
        return document;
    }

    internal void LoadCanvas(CanvasDocument document)
    {
        VerifyAccess();
        var staging = new XTNodeEditor { VerticalPorts = document.VerticalPorts };
        var nodes = new Dictionary<Guid, XTNode>();
        foreach (var item in document.Nodes)
        {
            if (!_registeredTypes.TryGetValue(item.Type, out var type)) throw new InvalidDataException($"请先注册节点类型：{item.Type}");
            if (!double.IsFinite(item.Left) || !double.IsFinite(item.Top) || !double.IsFinite(item.Width) || !double.IsFinite(item.Height) || item.Width < 24 || item.Height < 24)
                throw new InvalidDataException("节点布局无效。");
            var node = (XTNode)Activator.CreateInstance(type)!;
            if (item.Id == Guid.Empty || !nodes.TryAdd(item.Id, node)) throw new InvalidDataException("节点标识重复或为空。");
            node.Guid = item.Id;
            node.Location = new(item.Left, item.Top);
            node.Size = new(item.Width, item.Height);
            node.Mark = item.Mark ?? "";
            node.OnLoadNode(item.Properties ?? []);
            staging.Nodes.Add(node);
        }
        foreach (var connection in document.Connections)
        {
            if (!nodes.TryGetValue(connection.OutputNode, out var output) || !nodes.TryGetValue(connection.InputNode, out var input) ||
                connection.OutputIndex < 0 || connection.OutputIndex >= output.OutputOptions.Count || connection.InputIndex < 0 || connection.InputIndex >= input.InputOptions.Count)
                throw new InvalidDataException("连接引用了不存在的节点或端口。");
            var status = output.OutputOptions[connection.OutputIndex].ConnectOption(input.InputOptions[connection.InputIndex]);
            if (status != ConnectionStatus.Connected) throw new InvalidDataException($"连接校验失败：{status}");
        }
        foreach (var item in document.Nodes)
        {
            nodes[item.Id].LockLocation = item.LockLocation;
            nodes[item.Id].LockOption = item.LockOption;
        }
        // 转移已经校验的节点与连接，避免再次触发连接回调或动态增删端口。
        Nodes.Clear();
        // 校验通过后才切换方向；旧文件缺少此字段时按左右模式加载。
        SetCurrentValue(VerticalPortsProperty, document.VerticalPorts);
        var connections = staging._connections.ToArray();
        foreach (var node in nodes.Values)
        {
            staging._surface.Children.Remove(node);
            node.Owner = null;
            Nodes.Add(node);
        }
        staging._connections.Clear();
        _connections.AddRange(connections);
        CanvasOffsetX = document.OffsetX; CanvasOffsetY = document.OffsetY; CanvasScale = document.Scale;
        UpdateTransform();
        foreach (var node in Nodes) node.OnEditorLoadCompleted();
    }

    private void OnNodeDrop(object sender, DragEventArgs args)
    {
        if (args.Data.GetData(typeof(Type)) is not Type type) return;
        RegisterNodeType(type);
        var node = (XTNode)Activator.CreateInstance(type)!;
        node.Location = ControlToCanvas(args.GetPosition(this));
        Nodes.Add(node);
        SetActiveNode(node);
        args.Handled = true;
    }
    public NodeFindInfo FindNodeFromPoint(Point point) => new() { Node = HitNode(point), NodeOption = HitOption(point) };
    public XTNode[] GetSelectedNode() => Nodes.Where(node => node.IsSelected).ToArray();
    public int DeleteSelectedNodes()
    {
        var selected = GetSelectedNode();
        foreach (var node in selected) Nodes.Remove(node);
        return selected.Length;
    }
    public bool AddSelectedNode(XTNode node) { if (node.Owner != this) return false; node.IsSelected = true; return true; }
    public bool RemoveSelectedNode(XTNode node) { if (node.Owner != this) return false; node.IsSelected = false; return true; }
    public Point ControlToCanvas(Point point) => ToWorld(point);
    public Point CanvasToControl(Point point) => new(point.X * CanvasScale + CanvasOffsetX, point.Y * CanvasScale + CanvasOffsetY);
    public Rect CanvasToControl(Rect rectangle) => new(CanvasToControl(rectangle.TopLeft), new Size(rectangle.Width * CanvasScale, rectangle.Height * CanvasScale));
    public Rect ControlToCanvas(Rect rectangle) => new(ControlToCanvas(rectangle.TopLeft), new Size(rectangle.Width / CanvasScale, rectangle.Height / CanvasScale));
    public double CanvasToControl(double number, bool isX) => number * CanvasScale + (isX ? CanvasOffsetX : CanvasOffsetY);
    public double ControlToCanvas(double number, bool isX) => (number - (isX ? CanvasOffsetX : CanvasOffsetY)) / CanvasScale;
    public void BuildLinePath() => InvalidateVisual();
    public void Invalidate() => InvalidateVisual();
    public Rect CanvasValidBounds => Nodes.Aggregate(Rect.Empty, (bounds, node) => { bounds.Union(node.Rectangle); return bounds; });
    public ConnectionInfo[] GetConnectionInfo(XTNode node, bool input = true, bool output = true) => _connections
        .Where(connection => input && connection.Input.Owner == node || output && connection.Output.Owner == node).ToArray();

    /// <summary>使用 WPF RenderTargetBitmap 导出画布区域，不创建 GDI 位图。</summary>
    public BitmapSource GetCanvasImage(Rect rectangle, double scale = 1)
    {
        VerifyAccess();
        if (rectangle.IsEmpty || rectangle.Width <= 0 || rectangle.Height <= 0 || !double.IsFinite(scale) || scale <= 0 ||
            rectangle.Width * scale > 16384 || rectangle.Height * scale > 16384) throw new ArgumentOutOfRangeException(nameof(rectangle));
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(scale, scale));
            context.PushTransform(new TranslateTransform(-rectangle.X, -rectangle.Y));
            foreach (var connection in _connections) DrawConnection(context, connection);
            foreach (var node in Nodes)
            {
                context.PushTransform(new TranslateTransform(node.Left, node.Top));
                node.OnDrawNode(new(context, 1));
                foreach (var control in node.Children.OfType<XTNodeControl>())
                    if (control.Visable && control.ActualWidth > 0 && control.ActualHeight > 0)
                        context.DrawRectangle(new VisualBrush(control), null, new Rect(control.Left, control.Top + node.TitleHeight, control.ActualWidth, control.ActualHeight));
                context.Pop();
            }
            context.Pop(); context.Pop();
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(rectangle.Width * scale), (int)Math.Ceiling(rectangle.Height * scale), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public sealed class CanvasDocument
    {
        public string Format { get; set; } = "XTNode.WPF";
        public int Version { get; set; } = 1;
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float Scale { get; set; } = 1;
        public bool VerticalPorts { get; set; }
        public List<NodeDocument> Nodes { get; set; } = [];
        public List<ConnectionDocument> Connections { get; set; } = [];
    }
    public sealed class NodeDocument
    {
        public string Type { get; set; } = "";
        public Guid Id { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Mark { get; set; } = "";
        public bool LockOption { get; set; }
        public bool LockLocation { get; set; }
        public Dictionary<string, byte[]> Properties { get; set; } = [];
    }
    public sealed class ConnectionDocument
    {
        public Guid InputNode { get; set; }
        public Guid OutputNode { get; set; }
        public int InputIndex { get; set; }
        public int OutputIndex { get; set; }
    }
}
