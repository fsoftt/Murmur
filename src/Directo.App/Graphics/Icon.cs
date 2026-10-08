using System.ComponentModel;
using System.Numerics;
using Microsoft.Maui.Controls.Shapes;

namespace Directo.App.Graphics;

/// <summary>
/// A stroke icon authored on a 24×24 grid (like the prototype's inline SVGs), scaled to
/// <see cref="Size"/> without changing its stroke width. Text-colored by default in both themes.
/// </summary>
public sealed class Icon : Shape
{
    public static readonly BindableProperty DataProperty = BindableProperty.Create(
        nameof(Data), typeof(Geometry), typeof(Icon), null, propertyChanged: (b, _, _) => ((Icon)b).Refresh());

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(Icon), 24d, propertyChanged: (b, _, _) => ((Icon)b).Refresh());

    public Icon()
    {
        StrokeThickness = 2;
        StrokeLineCap = PenLineCap.Round;
        StrokeLineJoin = PenLineJoin.Round;
        Aspect = Stretch.None;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        this.SetAppThemeColor(StrokeProperty, Theme.Resource("TextLight"), Theme.Resource("TextDark"));
        Refresh();
    }

    /// <summary>SVG-style path data on a 24×24 grid.</summary>
    [TypeConverter(typeof(PathGeometryConverter))]
    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public override PathF GetPath()
    {
        var path = new PathF();
        Data?.AppendPath(path);
        var scale = (float)(Size / 24);
        path.Transform(Matrix3x2.CreateScale(scale));
        return path;
    }

    private void Refresh()
    {
        WidthRequest = Size;
        HeightRequest = Size;
        Handler?.UpdateValue(nameof(IShapeView.Shape));
        InvalidateMeasure();
    }
}
