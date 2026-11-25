# Generates a clean Observatory / O-ring network icon without downloading external assets.
# In the published EXE this bitmap becomes a real Windows ICO (16/32/48/256).
param([Parameter(Mandatory=$true)][string]$Target)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$code = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class ObserverBrandRenderer
{
    private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        GraphicsPath p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Render(string file)
    {
        const int n = 256;
        using (Bitmap bitmap = new Bitmap(n, n, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using (GraphicsPath shape = Rounded(6, 6, 244, 244, 47))
            using (LinearGradientBrush background = new LinearGradientBrush(new Point(0, 0), new Point(256, 256),
                         Color.FromArgb(255, 20, 31, 43), Color.FromArgb(255, 3, 7, 12)))
            {
                g.FillPath(background, shape);
                using (Pen rim = new Pen(Color.FromArgb(190, 42, 141, 158), 3f))
                    g.DrawPath(rim, shape);
                g.SetClip(shape);
                using (LinearGradientBrush highlight = new LinearGradientBrush(new Point(14, 12), new Point(170, 110),
                              Color.FromArgb(96, 156, 225, 233), Color.FromArgb(0, 14, 19, 29)))
                {
                    g.FillEllipse(highlight, -85, -140, 360, 250);
                }
                g.ResetClip();
            }
            // Double metallic ring: no horror-eye imagery, just a network hub / radar.
            using (Pen shadow = new Pen(Color.FromArgb(170, 0, 0, 0), 31f))
                g.DrawArc(shadow, 53, 53, 150, 150, 15, 315);
            using (Pen rim = new Pen(Color.FromArgb(225, 118, 179, 192), 16f))
                g.DrawArc(rim, 54, 54, 148, 148, 70, 242);
            using (Pen glow = new Pen(Color.FromArgb(235, 24, 225, 228), 13f))
                g.DrawArc(glow, 54, 54, 148, 148, 320, 105);
            using (Pen inner = new Pen(Color.FromArgb(130, 39, 72, 92), 3f))
                g.DrawEllipse(inner, 72, 72, 112, 112);
            using (SolidBrush center = new SolidBrush(Color.FromArgb(255, 4, 9, 15)))
                g.FillEllipse(center, 77, 77, 102, 102);
            // Signal beacon in the ring and three progressive radio arcs.
            using (SolidBrush glowNode = new SolidBrush(Color.FromArgb(65, 12, 219, 230)))
                g.FillEllipse(glowNode, 165, 100, 40, 40);
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 58, 241, 240)))
                g.FillEllipse(dot, 178, 112, 13, 13);
            using (Pen signal = new Pen(Color.FromArgb(225, 42, 221, 230), 5f))
            {
                signal.StartCap = LineCap.Round; signal.EndCap = LineCap.Round;
                g.DrawArc(signal, 134, 62, 72, 72, 285, 51);
                g.DrawArc(signal, 127, 43, 105, 105, 290, 46);
                g.DrawArc(signal, 119, 24, 139, 139, 295, 40);
            }
            using (Pen gloss = new Pen(Color.FromArgb(95, 222, 247, 250), 3f))
                g.DrawArc(gloss, 62, 63, 133, 133, 189, 65);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            bitmap.Save(file, ImageFormat.Png);
        }
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[ObserverBrandRenderer]::Render($Target)
Write-Host "Observer black-glass brand icon generated: $Target"
