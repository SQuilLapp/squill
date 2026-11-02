using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Squill.App.WinUI.Views;

/// <summary>Draws the line icons of <c>Styles/Icons.xaml</c> as stroked paths.</summary>
internal static class Icons
{
    /// <summary>Creates an icon element.</summary>
    /// <param name="key">The resource key of the icon geometry, for example <c>SquillIconSearch</c>.</param>
    /// <param name="size">The width and height in logical pixels.</param>
    /// <param name="stroke">The stroke brush.</param>
    /// <returns>The icon, a 24 x 24 canvas scaled to <paramref name="size"/>.</returns>
    public static FrameworkElement Create(string key, double size, Brush stroke) =>
        new Viewbox
        {
            Width = size,
            Height = size,
            Child = new Canvas { Width = 24, Height = 24, Children = { Shape(key, stroke) } },
        };

    /// <summary>Changes the stroke of an icon made by <see cref="Create"/>.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="stroke">The new brush.</param>
    public static void SetStroke(FrameworkElement icon, Brush stroke) =>
        ((Microsoft.UI.Xaml.Shapes.Path)((Canvas)((Viewbox)icon).Child).Children[0]).Stroke = stroke;

    private static Microsoft.UI.Xaml.Shapes.Path Shape(string key, Brush stroke) => new()
    {
        Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), (string)Application.Current.Resources[key]),
        Stroke = stroke,
        StrokeThickness = 1.5,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };
}
