using ST.Library.UI.NodeEditor;

namespace WorkFlowCore.Nodes.NodeSystem;

public static class SystemNodeCatalog
{
    public static IReadOnlyList<Type> NodeTypes { get; } = Array.AsReadOnly<Type>(
    [
        typeof(BitConverterFromBytesNode),
        typeof(BitConverterToBytesNode),
        typeof(BitConverterToStringNode),
        typeof(ConvertFromBase64StringNode),
        typeof(ConvertToBase64StringNode),
        typeof(ConvertToBoolNode),
        typeof(ConvertToDateTimeNode),
        typeof(ConvertToDoubleNode),
        typeof(ConvertToInt32Node),
        typeof(ConvertToInt64Node),
        typeof(ConvertToStringNode),
        typeof(DateTimeAddNode),
        typeof(DateTimeCompareNode),
        typeof(DateTimeCreateNode),
        typeof(DateTimeEndOfDayNode),
        typeof(DateTimeGetTicksNode),
        typeof(DateTimeNowNode),
        typeof(DateTimeParseNode),
        typeof(DateTimeStartOfDayNode),
        typeof(DateTimeTodayNode),
        typeof(DateTimeUtcNowNode),
        typeof(DirectoryCreateNode),
        typeof(DirectoryDeleteNode),
        typeof(DirectoryExistsNode),
        typeof(DirectoryGetFilesNode),
        typeof(DirectoryGetSubDirectoriesNode),
        typeof(EnvironmentExitNode),
        typeof(EnvironmentGetOsVersionNode),
        typeof(FileAppendAllTextNode),
        typeof(FileCopyNode),
        typeof(FileDeleteNode),
        typeof(FileExistsNode),
        typeof(FileGetSizeNode),
        typeof(FileGetTimeNode),
        typeof(FileMoveNode),
        typeof(FileReadAllLinesNode),
        typeof(FileReadAllTextNode),
        typeof(FileWriteAllTextNode),
        typeof(GetCurrentDirectoryNode),
        typeof(GetEnvironmentVariableNode),
        typeof(GetExecutableDirectoryNode),
        typeof(GetExecutablePathNode),
        typeof(GetSpecialFolderNode),
        typeof(GetTempFileNameNode),
        typeof(GetTempPathNode),
        typeof(GetUserNameNode),
        typeof(GuidNewGuidNode),
        typeof(GuidParseNode),
        typeof(GuidToStringNode),
        typeof(HttpClientGetNode),
        typeof(HttpClientPostNode),
        typeof(JsonDeserializeNode),
        typeof(JsonGetPropertyNode),
        typeof(JsonSerializeNode),
        typeof(PathChangeExtensionNode),
        typeof(PathCombineNode),
        typeof(PathGetDirectoryNameNode),
        typeof(PathGetExtensionNode),
        typeof(PathGetFileNameNode),
        typeof(PathGetFileNameWithoutExtensionNode),
        typeof(PathHasExtensionNode),
        typeof(SerialPortNode),
        typeof(TcpClientNode),
        typeof(TcpClientSendNode),
        typeof(TcpListenerNode),
        typeof(TcpServerNode),
        typeof(TcpServerSendNode),
        typeof(UdpClientNode),
    ]);

    public static void Register(XTNodeEditorPannel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        foreach (var type in NodeTypes) panel.AddXTNode(type);
    }
}
