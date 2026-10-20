# Revit BIM 交付检查插件（第一版）

面向 Revit 2024 的本地插件原型，提供实际使用材料清单，并预留题目图纸与模型符合性对比入口。

## 当前功能

- 汇总模型构件实际使用的材料、类别、构件数，以及 Revit 可提供的材料面积和体积。
- 按材料选择并定位模型中的相关构件。
- 将材料清单导出为 CSV。材料清单是模型内容统计，不会把某种材料自动判断为问题。
- “图纸对比（后续）”页可选择参考图纸文件，当前只记录选择结果，还没有解析图纸或运行模型对比。

## 后续图纸对比方向

后续版本将把题目要求整理为明确检查规则，再对照模型核查材质、构件尺寸/长度、门窗类型和尺寸等内容；问题报告应指出偏差对应的构件，支持在模型中定位。图纸信息无法可靠自动量化时，应标记为人工复核，避免将推测作为确定问题。

## 边界

- 不会自动修改模型。
- 当前不执行参数空值扫描，也不将材料清单条目判为问题。
- 首版使用 Revit 2024 API，无 AI 调用，不上传模型或图纸数据。

## 构建

用 Visual Studio 打开 `RevitParameterChecker.csproj`，选择 **Release** 并生成。项目引用 `D:\Revit 2024\RevitAPI.dll` 和 `RevitAPIUI.dll`，如果 Revit 安装位置不同，请修改项目文件中的 HintPath。

也可以在 PowerShell 执行：

```powershell
dotnet build .\RevitParameterChecker.csproj -c Release
```

## 注册到 Revit 2024

构建完成后，在 PowerShell 执行：

```powershell
.\install.ps1
```

脚本将生成 `.addin` 清单到当前 Windows 用户的 Revit 2024 Addins 目录。重启 Revit 后，在“BIM交付检查”选项卡中打开工具。