using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TileGen.Core;

namespace TileGen.App.ViewModels;

/// <summary>
/// One tab of a material column: the family selector, the ramp editor and the parameter rows, generated from the
/// schema so that a new preset field appears here without GUI code. The garbage tab also carries the mask rows.
/// </summary>
public sealed partial class MaterialEditorViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> FamilyNames = new[] { FieldStructure.FamilyName, CellsStructure.FamilyName, PatternStructure.FamilyName };

    private readonly string _prefix;
    private readonly Action<Func<TileType, TileType>> _edit;
    private readonly Dictionary<string, ParameterRowViewModel> _rowsByPath = new(StringComparer.Ordinal);
    private bool _suppress;

    public MaterialEditorViewModel(string prefix, string title, TileType type, Action<Func<TileType, TileType>> edit)
    {
        _prefix = prefix;
        _edit = edit;
        Title = title;
        var material = Get(type);
        Ramp = new RampEditorViewModel(material.Ramp, ramp => _edit(t => Set(t, Get(t) with { Ramp = ramp })));
        _selectedFamily = material.Structure.Family;
        Rebuild(type);
    }

    public string Title { get; }
    public IReadOnlyList<string> Families => FamilyNames;
    public RampEditorViewModel Ramp { get; }
    public ObservableCollection<ParameterRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string _selectedFamily;

    /// <summary>Syncs the rows to a new model state without applying anything back, which is what every edit from any source ends in.</summary>
    public void Refresh(TileType type)
    {
        var material = Get(type);
        _suppress = true;
        try
        {
            if (material.Structure.Family != SelectedFamily || !RowsMatch(type))
            {
                SelectedFamily = material.Structure.Family;
                Rebuild(type);
            }
            else
            {
                foreach (var d in Describe(type))
                {
                    _rowsByPath[d.Path].Refresh(d.Param);
                }
            }
            Ramp.Refresh(material.Ramp);
        }
        finally
        {
            _suppress = false;
        }
    }

    partial void OnSelectedFamilyChanged(string value)
    {
        if (_suppress)
        {
            return;
        }
        Structure structure = value switch
        {
            FieldStructure.FamilyName => new FieldStructure(),
            CellsStructure.FamilyName => new CellsStructure(),
            PatternStructure.FamilyName => new PatternStructure(),
            _ => throw new NotSupportedException($"Unknown family '{value}'."),
        };
        _edit(t => Set(t, Get(t) with { Structure = structure }));
    }

    private IEnumerable<ParameterDescriptor> Describe(TileType type)
    {
        if (_prefix == TileTypeParameters.GarbagePrefix)
        {
            // The mask scale and softness belong to the tile type, and the garbage tab is where a user looks for them.
            foreach (var d in TileTypeParameters.Enumerate(type).Where(d => !d.Path.Contains('.')))
            {
                yield return d;
            }
        }
        foreach (var d in TileTypeParameters.EnumerateMaterial(_prefix, Get(type)))
        {
            yield return d;
        }
    }

    private bool RowsMatch(TileType type)
    {
        var paths = Describe(type).Select(d => d.Path).ToList();
        return paths.Count == Rows.Count && paths.SequenceEqual(Rows.Select(r => r.Path));
    }

    private void Rebuild(TileType type)
    {
        Rows.Clear();
        _rowsByPath.Clear();
        foreach (var d in Describe(type))
        {
            var row = new ParameterRowViewModel(d, r => _edit(t => TileTypeParameters.With(t, r.Path, r.ToParam())));
            Rows.Add(row);
            _rowsByPath[d.Path] = row;
        }
    }

    private Material Get(TileType type)
    {
        return _prefix == TileTypeParameters.BasePrefix ? type.Base : type.Garbage;
    }

    private TileType Set(TileType type, Material material)
    {
        return _prefix == TileTypeParameters.BasePrefix ? type with { Base = material } : type with { Garbage = material };
    }
}
