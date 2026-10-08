# 工作流保存和脚本依赖

- 首次点击“保存”不弹出对话框，写入 `AppContext.BaseDirectory/workflow/workflow.workflow.json`。新建其他流程时使用递增文件名，避免覆盖已有流程。
- 后续保存写入当前文件；“另存为”选择新的 `.workflow.json` 路径，并复制独立的脚本和依赖。
- 进入工作流页面时，如果默认 `workflow.workflow.json` 存在，会自动加载；“打开”也可以选择其他工作流，包括旧版纯 JSON 文件。

```text
workflow/
  workflow.workflow.json
  workflow.workflow.json.assets/
    <保存版本 ID>/
      scripts/
        <节点 ID>.csx
        loads/*.csx
      dependencies/
        dll/<目录 ID>/*.dll
        nuget-1/
          ScriptPackages.csproj
          obj/project.assets.json
          packages/<包名>/<版本>/...
```

JSON 保存节点、连接、属性、脚本源码备份及相对资源清单。打开时以 `.csx` 文件内容为准，并设置节点和 CsxPad 的脚本目录、NuGet 工作目录。CsxPad 的包列表、补全、执行和调试使用这些依赖；安装新包后再次保存工作流即可打包。

打包递归处理有效的 `#load` 和本地文件 `#r`，将引用改成相对路径；本地 DLL 同目录的 DLL 一并复制。NuGet 包保留完整包目录与已还原资产清单，迁移后优先从随包目录读取；省略版本的 NuGet 指令优先使用已安装版本。尚未安装/还原的包需先在 CsxPad 中完成安装。脚本动态拼接的路径、任意数据文件及未通过指令声明的外部资源不在自动打包范围内。

每次保存先生成独立资源版本，再原子替换 JSON；复制失败不会破坏原工作流。历史资源版本保留在 `.assets` 内，避免影响仍在使用的 DLL。迁移或备份时必须把 JSON 和整个同名 `.assets` 文件夹一起复制，或者直接使用“另存为”。缺少必要脚本或依赖目录时打开会报错，不清空当前画布。
