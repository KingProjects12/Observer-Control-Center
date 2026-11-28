using System.Windows;
using System.Windows.Media;

namespace Observer.ControlCenter.Services;

// Decorative vector artwork is confined to the header and the island;
// tables, console and status semantics remain unchanged.
internal static class ThemeArtwork
{
    internal static Brush Header(ThemePreset p)
    {
        static Color C(string hex)=>(Color)ColorConverter.ConvertFromString(hex)!;
        var group=new DrawingGroup();
        var bg=new LinearGradientBrush(C(p.Background),C(p.End),new Point(0,0),new Point(1,1));
        bg.GradientStops.Insert(1,new GradientStop(C(p.Panel),.56));
        group.Children.Add(new GeometryDrawing(bg,null,new RectangleGeometry(new Rect(0,0,1000,64))));
        var accent=new SolidColorBrush(C(p.Accent)){Opacity=.12};
        var secondary=new SolidColorBrush(C(p.Secondary)){Opacity=.10};
        var pen=new Pen(accent,1.1);var pen2=new Pen(secondary,1);
        void Path(string d,Brush? fill=null,Pen? stroke=null)=>group.Children.Add(new GeometryDrawing(fill,stroke,Geometry.Parse(d)));
        void Circle(double x,double y,double r,Brush? fill=null,Pen? stroke=null)=>group.Children.Add(new GeometryDrawing(fill,stroke,new EllipseGeometry(new Point(x,y),r,r)));
        void Line(double x,double y,double x2,double y2,Pen stroke)=>group.Children.Add(new GeometryDrawing(null,stroke,new LineGeometry(new Point(x,y),new Point(x2,y2))));
        switch(p.Art)
        {
            case "grid":case "carbon":case "scan":
                for(int x=350;x<1000;x+=p.Art=="carbon"?16:32)Line(x,0,p.Art=="carbon"?x+64:x,64,pen2);
                for(int y=8;y<64;y+=p.Art=="scan"?5:16)Line(350,y,1000,y,pen);
                break;
            case "circuit":
                for(int i=0;i<9;i++){int x=390+i*62,y=8+(i%5)*10;Path($"M{x},0 L{x},{y} L{x+25},{y} L{x+40},{y+14} L{x+60},{y+14}",null,pen);Circle(x+60,y+14,2,secondary);}
                break;
            case "facets":
                Path("M380,64 L570,0 L675,64 Z M580,0 L720,0 L810,64 L675,64 Z",secondary);
                Path("M720,0 L890,0 L810,64 Z M890,0 L1000,0 L1000,64 L810,64 Z",accent);break;
            case "rings":case "orbit":case "eclipse":
                for(int i=0;i<5;i++)Circle(790,32,15+i*16,null,i%2==0?pen:pen2);
                if(p.Art=="orbit")group.Children.Add(new GeometryDrawing(null,pen,new EllipseGeometry(new Point(790,32),130,13)));
                break;
            case "petals":case "bubbles":case "rain":
                var random=new Random(19);
                for(int i=0;i<30;i++){int x=random.Next(400,995),y=random.Next(0,64);if(p.Art=="rain")Line(x,y,x-7,y+12,pen);else if(p.Art=="petals")group.Children.Add(new GeometryDrawing(i%2==0?accent:secondary,null,new EllipseGeometry(new Point(x,y),4,2)));else Circle(x,y,random.Next(2,8),null,pen);}
                break;
            case "city":
                for(int i=0;i<15;i++){int x=470+i*36,h=12+(i*17)%43;group.Children.Add(new GeometryDrawing(i%2==0?accent:secondary,null,new RectangleGeometry(new Rect(x,64-h,30,h))));}break;
            case "dunes":
                Path("M350,64 Q490,10 640,43 Q800,72 1000,16 L1000,64 Z",secondary);
                Path("M490,64 Q660,24 810,47 Q930,54 1000,33 L1000,64 Z",accent);break;
            case "mountain":
                Path("M400,64 L500,22 L570,48 L675,5 L770,45 L860,15 L1000,64 Z",secondary);
                Path("M550,64 L650,35 L720,57 L835,25 L1000,64 Z",accent);break;
            case "fault":
                Path("M420,0 L540,28 L620,12 L760,52 L860,22 L1000,43",null,pen);
                Path("M550,64 L650,40 L720,48 L845,10 L930,30 L1000,14",null,pen2);break;
        }
        group.ClipGeometry=new RectangleGeometry(new Rect(0,0,1000,64));
        var brush=new DrawingBrush(group){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,1000,64)};
        brush.Freeze();return brush;
    }
}
