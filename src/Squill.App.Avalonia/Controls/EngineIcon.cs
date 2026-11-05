using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Squill.Core;

namespace Squill.App.Avalonia.Controls;

/// <summary>Draws the logo of a database engine as vector layers read from the <c>EngineIcons.axaml</c> resources (sources in <c>docs/design/engine-icons</c>).</summary>
public sealed class EngineIcon : Control
{
    /// <summary>The engine whose logo is drawn.</summary>
    public static readonly StyledProperty<DatabaseDialect> DialectProperty = AvaloniaProperty.Register<EngineIcon, DatabaseDialect>(nameof(Dialect));

    /// <summary>Selects the MariaDB logo for a <see cref="DatabaseDialect.MySql"/> profile.</summary>
    public static readonly StyledProperty<bool> MariaDbProperty = AvaloniaProperty.Register<EngineIcon, bool>(nameof(MariaDb));

    static EngineIcon() => AffectsRender<EngineIcon>(DialectProperty, MariaDbProperty);

    /// <summary>Gets or sets whether a MySQL profile targets MariaDB, which draws the MariaDB logo.</summary>
    public bool MariaDb { get => GetValue(MariaDbProperty); set => SetValue(MariaDbProperty, value); }

    /// <summary>Gets or sets the engine whose logo is drawn.</summary>
    public DatabaseDialect Dialect { get => GetValue(DialectProperty); set => SetValue(DialectProperty, value); }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(double.IsNaN(Width) ? 18 : Width, double.IsNaN(Height) ? 18 : Height);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var resources = Application.Current!.Resources;
        var engine = Dialect == DatabaseDialect.MySql && MariaDb ? "MariaDb" : Dialect.ToString();
        if (!resources.TryGetResource($"Engine{engine}Size", null, out var size) || size is not double viewBox)
            return;
        var scale = Math.Min(Bounds.Width, Bounds.Height) / viewBox;
        using var _ = context.PushTransform(Matrix.CreateScale(scale, scale));
        for (var i = 0; resources.TryGetResource($"Engine{engine}{i}Geometry", null, out var shape); i++)
        {
            if (shape is Geometry geometry && resources.TryGetResource($"Engine{engine}{i}Fill", null, out var fill) && fill is IBrush brush)
                context.DrawGeometry(brush, null, geometry);
        }
    }
}
