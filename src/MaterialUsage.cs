using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RevitParameterChecker;

public sealed class MaterialUsage
{
    private readonly HashSet<ElementId> _elementIds = new();
    private readonly HashSet<string> _categories = new();

    public string MaterialName { get; set; } = "";
    public double AreaSquareMeters { get; set; }
    public double VolumeCubicMeters { get; set; }
    public int ElementCount => _elementIds.Count;
    public IReadOnlyCollection<ElementId> ElementIds => _elementIds;
    public string CategoriesText => string.Join("、", _categories.OrderBy(name => name, System.StringComparer.CurrentCulture));
    public string AreaText => AreaSquareMeters.ToString("N2", CultureInfo.CurrentCulture);
    public string VolumeText => VolumeCubicMeters.ToString("N3", CultureInfo.CurrentCulture);

    public void AddElement(ElementId elementId, string categoryName)
    {
        _elementIds.Add(elementId);
        _categories.Add(categoryName);
    }
}
