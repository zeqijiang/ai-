using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace RevitParameterChecker;

[Transaction(TransactionMode.Manual)]
public sealed class EntryPoint : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        const string tabName = "BIM交付检查";
        try { application.CreateRibbonTab(tabName); }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { }

        RibbonPanel panel = application.CreateRibbonPanel(tabName, "模型检查");
        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        var buttonData = new PushButtonData(
            "OpenParameterChecker",
            "材料与\n图纸检查",
            assemblyPath,
            typeof(CheckCommand).FullName!);
        panel.AddItem(buttonData);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}

[Transaction(TransactionMode.Manual)]
public sealed class CheckCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
    {
        var window = new CheckerWindow(commandData.Application.ActiveUIDocument);
        new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
        window.ShowDialog();
        return Result.Succeeded;
    }
}
