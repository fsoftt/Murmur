namespace Murmur.App.Graphics;

/// <summary>Rounded-square progress ring around the QR code, emptying as the invite expires.</summary>
public sealed class CountdownRing : GraphicsView
{
    public static readonly BindableProperty FractionProperty = BindableProperty.Create(
        nameof(Fraction), typeof(double), typeof(CountdownRing), 1d, propertyChanged: (b, _, _) => ((CountdownRing)b).Invalidate());

    public CountdownRing()
    {
        Drawable = new RingDrawable(this);
    }

    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    private sealed class RingDrawable(CountdownRing owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            const float Stroke = 6;
            const float Radius = 40;
            var rect = new RectF(Stroke / 2, Stroke / 2, dirtyRect.Width - Stroke, dirtyRect.Height - Stroke);
            canvas.StrokeSize = Stroke;
            canvas.StrokeLineCap = LineCap.Round;

            canvas.StrokeColor = Theme.Get("Surface2");
            canvas.DrawRoundedRectangle(rect, Radius);

            // Perimeter of the rounded rectangle, used to dash the remaining fraction.
            var straight = 2 * (rect.Width - 2 * Radius) + 2 * (rect.Height - 2 * Radius);
            var perimeter = straight + (float)(2 * Math.PI * Radius);
            var visible = (float)Math.Clamp(owner.Fraction, 0, 1) * perimeter;
            if (visible <= 0)
            {
                return;
            }

            canvas.StrokeColor = Theme.Get("Accent");
            canvas.StrokeDashPattern = [visible / Stroke, perimeter / Stroke];
            canvas.DrawRoundedRectangle(rect, Radius);
        }
    }
}
