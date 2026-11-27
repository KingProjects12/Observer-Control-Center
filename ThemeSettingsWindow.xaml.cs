using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Observer.ControlCenter.Services;
using ShapePath=System.Windows.Shapes.Path;

namespace Observer.ControlCenter;

public partial class ThemeSettingsWindow : Window
{
    private readonly Action<string> _select;
    private readonly Dictionary<string,Button> _cards=new();
    internal int CardCount=>_cards.Count;
    private static string T(string en,string ru)=>UiText.T(en,ru);
    internal ThemeSettingsWindow(Action<string> select,Action<bool> snow)
    {
        _select=select;InitializeComponent();DarkTitleBar.Apply(this);
        Title=Heading.Text=T("Appearance settings","Настройки оформления");
        Description.Text=T("Choose a theme. Changes apply immediately; your workspace and results stay intact.","Выберите тему. Оформление изменится сразу, рабочие данные сохранятся.");
        ThemesTab.Header=T("Themes","Темы");EffectsTab.Header=T("Effects","Эффекты");
        EffectsHeading.Text=T("Seasonal effects","Сезонные эффекты");
        SnowOption.Content=T("Falling snow in Winter and New Year","Падающие снежинки в темах «Зима» и «Новый год»");
        SnowOption.IsChecked=AppearanceTheme.SnowEnabled;
        SnowOption.Checked+=(_,_)=>snow(true);SnowOption.Unchecked+=(_,_)=>snow(false);
        EffectsHint.Text=T("Snow is limited to the header and stops when the window is hidden. Windows reduced-motion settings are respected.","Снег только в шапке. При скрытии окна эффект останавливается. Учитываются системные настройки анимации Windows.");
        DoneButton.Content=T("Done","Готово");
        foreach(var preset in ThemeCatalog.All)
        {
            var preview=new Border{CornerRadius=new CornerRadius(10),Height=84,ClipToBounds=true,Child=new Viewbox{Stretch=Stretch.UniformToFill,Child=Preview(preset)}};
            var title=new TextBlock{Text=preset.Name,FontSize=13,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,0),TextWrapping=TextWrapping.Wrap};
            var stack=new StackPanel();stack.Children.Add(preview);stack.Children.Add(title);
            var card=new Button{Style=(Style)FindResource("FlatButton"),Padding=new Thickness(8),Margin=new Thickness(4),Content=stack,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center,MinHeight=124,ToolTip=preset.Name};
            AutomationProperties.SetName(card,preset.Name);card.Click+=(_,_)=>Choose(preset.Id);_cards.Add(preset.Id,card);Gallery.Children.Add(card);
        }
        RefreshSelection();
    }
    internal void Choose(string id){_select(id);RefreshSelection();}
    private void RefreshSelection()
    {
        foreach(var pair in _cards)pair.Value.Tag=pair.Key==AppearanceTheme.Current.Id?"selected":null;
        CurrentLabel.Text=T("Selected: ","Выбрано: ")+AppearanceTheme.Current.Name;
    }
    private void Done_Click(object sender,RoutedEventArgs e)=>Close();
    private static SolidColorBrush B(string hex)=>new((Color)ColorConverter.ConvertFromString(hex)!);
    private static Canvas Preview(ThemePreset p)
    {
        var gradient=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(p.Background)!,(Color)ColorConverter.ConvertFromString(p.End)!,new Point(0,0),new Point(1,1));
        var c=new Canvas{Width=184,Height=84,Background=gradient,ClipToBounds=true};
        void Rect(double x,double y,double w,double h,string color,double radius=0,double opacity=1)
        {var r=new Rectangle{Width=w,Height=h,Fill=B(color),RadiusX=radius,RadiusY=radius,Opacity=opacity};Canvas.SetLeft(r,x);Canvas.SetTop(r,y);c.Children.Add(r);}
        void Dot(double x,double y,double size,string color,double opacity=1)
        {var e=new Ellipse{Width=size,Height=size,Fill=B(color),Opacity=opacity};Canvas.SetLeft(e,x);Canvas.SetTop(e,y);c.Children.Add(e);}
        void Shape(string data,string color,double opacity=1)
        {c.Children.Add(new ShapePath{Data=Geometry.Parse(data),Fill=B(color),Opacity=opacity});}
        void Line(double x1,double y1,double x2,double y2,string color,double opacity=1)
        {c.Children.Add(new Line{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=B(color),StrokeThickness=1.2,Opacity=opacity});}
        var rng=new Random(18);
        switch(p.Art)
        {
            case "spring":
                for(int i=0;i<16;i++){double x=i%2==0?rng.Next(2,40):rng.Next(144,180),y=rng.Next(24,76);Line(x+3,y+5,x+3,84,p.Accent,.7);for(int k=0;k<5;k++)Dot(x+3+4*Math.Cos(k*1.257),y+3+4*Math.Sin(k*1.257),6,i%3==0?"#F6D580":p.Secondary);Dot(x+4,y+4,4,"#FFF0C2");}
                break;
            case "summer":
                Dot(12,20,25,"#F6D580");Shape("M0,48 Q48,40 95,50 T184,44 L184,84 L0,84 Z","#287891");Shape("M0,70 Q85,57 184,64 L184,84 L0,84 Z","#C4A66D");Shape("M134,34 L174,34 Q154,7 134,34 Z","#EFA19B");Line(154,32,154,66,"#FFF4DB");break;
            case "autumn":
                for(int i=0;i<22;i++){double x=rng.Next(0,180),y=rng.Next(8,78);var leaf=new Ellipse{Width=12,Height=5,Fill=B(i%2==0?p.Accent:p.Secondary),Opacity=.7,RenderTransform=new RotateTransform(rng.Next(-70,70),6,2.5)};Canvas.SetLeft(leaf,x);Canvas.SetTop(leaf,y);c.Children.Add(leaf);}break;
            case "winter":case "newyear":
                Shape("M0,74 Q38,62 75,76 T184,69 L184,84 L0,84 Z","#BCD6E8",.85);
                for(int i=0;i<30;i++)Dot(rng.Next(1,180),rng.Next(3,73),rng.Next(2,5),"#EFF8FF",.7);
                if(p.Art=="newyear"){Shape("M158,16 L143,44 L151,44 L137,65 L179,65 L165,44 L173,44 Z","#247B58");Rect(156,65,5,11,"#795346");Dot(156,13,6,"#FFD98C");Dot(149,45,4,"#F8D58C");Dot(165,55,4,"#F5A5AD");Dot(155,58,4,"#E1F9E7");}break;
            case "wave":case "signal":case "glass":
                Shape("M0,60 Q40,10 95,48 T184,25 L184,43 Q145,80 95,61 T0,76 Z",p.Accent,.22);
                Shape("M0,25 Q52,75 110,33 T184,50 L184,62 Q145,44 110,48 T0,42 Z",p.Secondary,.22);break;
            case "stars":
                for(int i=0;i<36;i++)Dot(rng.Next(1,180),rng.Next(3,80),rng.Next(1,4),i%2==0?p.Accent:p.Secondary,.65);break;
        }
        // Island-shaped label and miniature app surfaces show the actual palette.
        Rect(62,7,60,15,"#080B10",8,.94);Dot(70,12,5,p.Accent);Rect(82,13,25,3,p.Secondary,1,.9);
        Rect(44,30,96,40,p.Panel,6,.94);Rect(50,36,37,8,p.Field,3);Rect(91,36,43,8,p.Accent,3);
        Rect(50,49,84,4,p.Stroke,2);Rect(50,57,64,3,p.Secondary,2,.8);
        return c;
    }
}
