namespace Murmur.App.Graphics;

/// <summary>Draws the 8×8 mirrored visual fingerprint of a safety number.</summary>
public sealed class FingerprintView : GraphicsView
{
    public static readonly BindableProperty CellsProperty = BindableProperty.Create(
        nameof(Cells), typeof(IReadOnlyList<int>), typeof(FingerprintView), null, propertyChanged: (b, _, _) => ((FingerprintView)b).Invalidate());

    private static readonly Color[] Palette =
    [
        Color.FromArgb("#2563EB"), Color.FromArgb("#34D399"), Color.FromArgb("#7C3AED"), Color.FromArgb("#F59E0B"),
    ];

    public FingerprintView()
    {
        Drawable = new FingerprintDrawable(this);
    }

    public IReadOnlyList<int>? Cells
    {
        get => (IReadOnlyList<int>?)GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    private sealed class FingerprintDrawable(FingerprintView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Theme.Get("Surface2");
            canvas.FillRoundedRectangle(dirtyRect, 16);
            if (owner.Cells is not { Count: 64 } cells)
            {
                return;
            }

            var size = dirtyRect.Width / 8;
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] < 0)
                {
                    continue;
                }

                canvas.FillColor = Palette[cells[i] % Palette.Length];
                canvas.FillRectangle((i % 8) * size, (i / 8) * size, size + 0.5f, size + 0.5f);
            }
        }
    }
}
