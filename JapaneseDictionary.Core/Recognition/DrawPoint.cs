namespace JapaneseDictionary.Core.Recognition;

/// <summary>A single point of a captured pen/finger stroke. Deliberately not
/// tied to any UI framework's point type (e.g. Microsoft.Maui.Graphics.PointF)
/// so this module has no platform dependency.</summary>
public readonly record struct DrawPoint(float X, float Y);
