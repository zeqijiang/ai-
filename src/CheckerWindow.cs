using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RevitParameterChecker;

public sealed class CheckerWindow : Window
{
    private readonly UIDocument _uiDocument;
    private readonly ObservableCollection<MaterialUsage> _materials = new();
    private readonly DataGrid _materialsGrid;
    private readonly TextBlock _materialStatus;
    private readonly TextBlock _drawingFileName;

    public CheckerWindow(UIDocument uiDocument)
    {
        _uiDocument = uiDocument;
        Title = "BIM 交付检查";
        Width = 1040;
        Height = 680;
        MinWidth = 820;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _materialsGrid = new DataGrid
        {
            ItemsSource = _materials,
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Extended,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };
        AddMaterialColumn("材料", nameof(MaterialUsage.MaterialName), 220);
        AddMaterialColumn("使用类别", nameof(MaterialUsage.CategoriesText), -1);
        AddMaterialColumn("构件数", nameof(MaterialUsage.ElementCount), 90);
        AddMaterialColumn("面积 (m²)", nameof(MaterialUsage.AreaText), 120);
        AddMaterialColumn("体积 (m³)", nameof(MaterialUsage.VolumeText), 120);

        var tabs = new TabControl { Margin = new Thickness(14) };
        Content = tabs;
        TabItem materialsTab = BuildMaterialsTab();
        TabItem drawingTab = BuildDrawingComparisonTab();
        tabs.Items.Add(materialsTab);
        tabs.Items.Add(drawingTab);
        _materialStatus = FindMaterialStatus(materialsTab);
        _drawingFileName = FindDrawingFileName(drawingTab);
    }

    private TabItem BuildMaterialsTab()
    {
        var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(14) };
        var controls = new StackPanel { Orientation = Orientation.Vertical };
        DockPanel.SetDock(controls, Dock.Top);
        panel.Children.Add(controls);

        controls.Children.Add(new TextBlock
        {
            Text = "汇总当前模型中实际被构件使用的材料。材料清单用于了解模型内容，本身不作为问题或错误。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(buttons);
        var refreshButton = new Button { Content = "刷新材料清单", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
        refreshButton.Click += (_, _) => RefreshMaterials();
        buttons.Children.Add(refreshButton);
        var locateButton = new Button { Content = "在模型中定位", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
        locateButton.Click += (_, _) => LocateSelectedMaterials();
        buttons.Children.Add(locateButton);
        var exportButton = new Button { Content = "导出 CSV", Padding = new Thickness(14, 6, 14, 6) };
        exportButton.Click += (_, _) => ExportMaterialsCsv();
        buttons.Children.Add(exportButton);

        var status = new TextBlock { Text = "点击“刷新材料清单”读取当前模型。", Margin = new Thickness(0, 10, 0, 2) };
        controls.Children.Add(status);

        panel.Children.Add(_materialsGrid);
        return new TabItem { Header = "材料清单", Content = panel, Tag = status };
    }

    private TabItem BuildDrawingComparisonTab()
    {
        var panel = new StackPanel { Margin = new Thickness(18), Orientation = Orientation.Vertical };
        panel.Children.Add(new TextBlock
        {
            Text = "图纸对比",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "后续在这里导入题目图纸，并把图纸中的明确要求与模型构件逐项核对，例如构件材质、尺寸/长度、门窗类型和尺寸。清单中的材料不会自动判为问题；只有与题目要求不符的项目才会进入问题报告。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var chooseButton = new Button
        {
            Content = "选择参考图纸…",
            Padding = new Thickness(14, 6, 14, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        chooseButton.Click += (_, _) => ChooseDrawingFile();
        panel.Children.Add(chooseButton);

        var fileName = new TextBlock
        {
            Text = "尚未选择图纸。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 14)
        };
        panel.Children.Add(fileName);

        panel.Children.Add(new TextBlock
        {
            Text = "当前阶段：图纸比对入口已预留，尚未解析图纸或运行符合性检查。之后需要先把图纸要求整理成可核对规则；无法可靠自动判断的内容应标为人工复核。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray
        });

        return new TabItem { Header = "图纸对比（后续）", Content = panel, Tag = fileName };
    }

    private void AddMaterialColumn(string title, string property, double width)
    {
        _materialsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = title,
            Binding = new Binding(property),
            Width = width > 0
                ? new DataGridLength(width)
                : new DataGridLength(1, DataGridLengthUnitType.Star)
        });
    }

    private static TextBlock FindMaterialStatus(TabItem tab) => (TextBlock)tab.Tag;

    private static TextBlock FindDrawingFileName(TabItem tab) => (TextBlock)tab.Tag;

    private void RefreshMaterials()
    {
        _materials.Clear();
        Document document = _uiDocument.Document;
        var byMaterial = new Dictionary<long, MaterialUsage>();

        foreach (Element element in new FilteredElementCollector(document).WhereElementIsNotElementType())
        {
            Category? category = element.Category;
            if (category is null) continue;

            ICollection<ElementId> materialIds;
            ICollection<ElementId> materialIdsIncludingPaint;
            try
            {
                materialIds = element.GetMaterialIds(false);
                materialIdsIncludingPaint = element.GetMaterialIds(true);
            }
            catch
            {
                continue;
            }

            var materialsOnElement = materialIds
                .Select(id => new { Id = id, IsPaint = false })
                .Concat(materialIdsIncludingPaint
                    .Where(id => !materialIds.Contains(id))
                    .Select(id => new { Id = id, IsPaint = true }));

            foreach (var materialEntry in materialsOnElement)
            {
                ElementId materialId = materialEntry.Id;
                if (document.GetElement(materialId) is not Material material) continue;
                long key = material.Id.Value;
                if (!byMaterial.TryGetValue(key, out MaterialUsage? usage))
                {
                    usage = new MaterialUsage { MaterialName = material.Name };
                    byMaterial.Add(key, usage);
                }

                usage.AddElement(element.Id, category.Name);
                try
                {
                    usage.AreaSquareMeters += UnitUtils.ConvertFromInternalUnits(
                        element.GetMaterialArea(materialId, materialEntry.IsPaint), UnitTypeId.SquareMeters);
                }
                catch
                {
                    // Some categories do not expose a material area quantity.
                }
                try
                {
                    usage.VolumeCubicMeters += UnitUtils.ConvertFromInternalUnits(
                        element.GetMaterialVolume(materialId), UnitTypeId.CubicMeters);
                }
                catch
                {
                    // Some categories do not expose a material volume quantity.
                }
            }
        }

        foreach (MaterialUsage usage in byMaterial.Values.OrderBy(item => item.MaterialName, StringComparer.CurrentCulture))
            _materials.Add(usage);

        _materialStatus.Text = _materials.Count == 0
            ? "未读取到构件实际使用的材料。"
            : $"读取完成：{_materials.Count:N0} 种材料，覆盖 {_materials.Sum(item => item.ElementCount):N0} 个材料-构件关联。面积或体积取决于构件类别是否提供相应数据。";
    }

    private void LocateSelectedMaterials()
    {
        var ids = _materialsGrid.SelectedItems
            .OfType<MaterialUsage>()
            .SelectMany(item => item.ElementIds)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            MessageBox.Show(this, "请先选择一项或多项材料。", "定位材料", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _uiDocument.Selection.SetElementIds(ids);
            _uiDocument.ShowElements(ids);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"无法定位所选材料的构件：{exception.Message}", "定位失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportMaterialsCsv()
    {
        if (_materials.Count == 0)
        {
            MessageBox.Show(this, "请先刷新材料清单。", "导出材料清单", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出材料使用清单",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"Revit材料清单_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        var builder = new StringBuilder();
        builder.AppendLine("材料,使用类别,构件数,面积(m²),体积(m³)");
        foreach (MaterialUsage item in _materials)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                item.MaterialName,
                item.CategoriesText,
                item.ElementCount.ToString(),
                item.AreaText,
                item.VolumeText
            }.Select(CsvEscape)));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _materialStatus.Text = $"材料清单已导出：{dialog.FileName}";
    }

    private void ChooseDrawingFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择用于后续比对的参考图纸",
            Filter = "图纸文件 (*.pdf;*.dwg;*.dxf;*.png;*.jpg;*.jpeg)|*.pdf;*.dwg;*.dxf;*.png;*.jpg;*.jpeg|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        _drawingFileName.Text = $"已选择：{dialog.FileName}\n当前版本只记录选择结果，尚未读取图纸内容或执行模型对比。";
    }

    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
