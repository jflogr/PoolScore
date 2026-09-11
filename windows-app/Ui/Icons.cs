using System.Drawing.Drawing2D;

namespace PoolScoreTracker.Ui;

/// <summary>
/// Toolbar icons, drawn rather than shipped. They are simple enough that a few
/// lines of GDI+ beats carrying image files around - and it keeps them in the
/// app's own colours without a second set of assets to maintain.
/// </summary>
public static class Icons
{
    /// <summary>
    /// Drawn at 4x the size they are shown at. The coordinates below are all
    /// in a 24-unit box, scaled up on the way out, so the curves land on a much
    /// finer grid and stop looking like stair-steps on the toolbar.
    /// </summary>
    private const int Box = 24;
    private const int Scale = 4;

    private static readonly Color Stroke = ColorTranslator.FromHtml("#2b3846");

    public static Bitmap Commit { get; } = Draw(static (g, pen) =>
        g.DrawLines(pen, [new PointF(4.5f, 12.5f), new PointF(9.5f, 18f), new PointF(19.5f, 6f)]));

    public static Bitmap Undo { get; } = Draw(static (g, pen) =>
    {
        // A hook back to the left, with the head on the open end.
        g.DrawArc(pen, 5f, 7f, 15f, 13f, 200f, -230f);
        g.DrawLines(pen, [new PointF(3.5f, 6.5f), new PointF(5.5f, 11.5f), new PointF(10.5f, 9.5f)]);
    });

    public static Bitmap Swap { get; } = Draw(static (g, pen) =>
    {
        g.DrawLine(pen, 4f, 9f, 20f, 9f);
        g.DrawLines(pen, [new PointF(16.5f, 5.5f), new PointF(20f, 9f), new PointF(16.5f, 12.5f)]);

        g.DrawLine(pen, 4f, 15f, 20f, 15f);
        g.DrawLines(pen, [new PointF(7.5f, 11.5f), new PointF(4f, 15f), new PointF(7.5f, 18.5f)]);
    });

    public static Bitmap Reset { get; } = Draw(static (g, pen) =>
    {
        g.DrawArc(pen, 4.5f, 4.5f, 15f, 15f, 60f, 280f);
        g.DrawLines(pen, [new PointF(15f, 2.5f), new PointF(19.5f, 6.5f), new PointF(14.5f, 9f)]);
    });

    public static Bitmap Banner { get; } = Draw(static (g, pen) =>
    {
        g.DrawRectangle(pen, 3f, 6.5f, 18f, 11f);
        g.DrawLine(pen, 6.5f, 12f, 11f, 12f);
        g.DrawLine(pen, 13f, 12f, 17.5f, 12f);
    });

    public static Bitmap Players { get; } = Draw(static (g, pen) =>
    {
        g.DrawEllipse(pen, 4f, 4.5f, 6.5f, 6.5f);
        g.DrawArc(pen, 2f, 12.5f, 10.5f, 11f, 200f, 140f);

        g.DrawEllipse(pen, 13.5f, 5.5f, 5.5f, 5.5f);
        g.DrawArc(pen, 12f, 12.5f, 10f, 10f, 210f, 120f);
    });

    public static Bitmap NewMatch { get; } = Draw(static (g, pen) =>
    {
        g.DrawEllipse(pen, 3.5f, 3.5f, 17f, 17f);
        g.DrawLine(pen, 12f, 7.5f, 12f, 16.5f);
        g.DrawLine(pen, 7.5f, 12f, 16.5f, 12f);
    });

    private static Bitmap Draw(Action<Graphics, Pen> paint)
    {
        var bitmap = new Bitmap(Box * Scale, Box * Scale);
        bitmap.SetResolution(96f * Scale, 96f * Scale);

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.ScaleTransform(Scale, Scale);

        using var pen = new Pen(Stroke, 2.1f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        paint(g, pen);
        return bitmap;
    }
}
