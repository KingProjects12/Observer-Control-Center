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
    internal int VisibleCardCount=>_cards.Values.Count(card=>card.Visibility==Visibility.Visible);
    private static readonly string[] Collections={"all","original","studio","worlds","lab"};
    private static string T(string en,string ru)=>UiText.T(en,ru);
    internal ThemeSettingsWindow(Action<string> select,Action<bool> snow)
    {
        _select=select;InitializeComponent();DarkTitleBar.Apply(this);
        Title=Heading.Text=T("Appearance settings","Настройки оформления");
        Description.Text=T("Choose a theme. Changes apply immediately; your workspace and results stay intact.","Выберите тему. Оформление изменится сразу, рабочие данные сохранятся.");
        ThemesTab.Header=T("Themes","Темы");EffectsTab.Header=T("Effects","Эффекты");
        EffectsTab.Visibility=ThemeCatalog.All.Any(p=>p.HasSnow)?Visibility.Visible:Visibility.Collapsed;
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
        CollectionLabel.Text=T("Collection","Коллекция");
        SearchLabel.Text=T("Search themes","Поиск тем");
        CollectionFilter.ItemsSource=new[]{T("All themes","Все темы"),T("Core themes","Основные темы"),T("Studio / technical","Студия / техника"),T("Worlds / atmosphere","Миры / атмосфера"),T("Lab / experimental","Лаборатория / эксперимент")};
        CollectionFilter.SelectedIndex=0;
        CollectionFilter.SelectionChanged+=(_,_)=>RefreshFilter();
        ThemeSearch.TextChanged+=(_,_)=>RefreshFilter();
        RefreshFilter();RefreshSelection();
    }
    internal void SetFilter(int index,string query){CollectionFilter.SelectedIndex=index;ThemeSearch.Text=query;RefreshFilter();}
    private void RefreshFilter()
    {
        int index=Math.Clamp(CollectionFilter.SelectedIndex,0,Collections.Length-1);
        string q=ThemeSearch.Text.Trim();
        foreach(var preset in ThemeCatalog.All)
        {
            bool show=(index==0||preset.Collection==Collections[index])&&(q.Length==0||preset.En.Contains(q,StringComparison.OrdinalIgnoreCase)||preset.Ru.Contains(q,StringComparison.OrdinalIgnoreCase)||preset.Id.Contains(q,StringComparison.OrdinalIgnoreCase));
            _cards[preset.Id].Visibility=show?Visibility.Visible:Visibility.Collapsed;
        }
        ResultsLabel.Text=VisibleCardCount==0?T("No matching themes","Темы не найдены"):T("Themes: ","Темы: ")+VisibleCardCount+" / "+CardCount;
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
            case "wave":case "signal":case "glass":
                Shape("M0,60 Q40,10 95,48 T184,25 L184,43 Q145,80 95,61 T0,76 Z",p.Accent,.22);
                Shape("M0,25 Q52,75 110,33 T184,50 L184,62 Q145,44 110,48 T0,42 Z",p.Secondary,.22);break;
        }
        if(p.Collection!="original")
        {
            // Match the live header artwork while keeping the preview readable.
            switch(p.Art)
            {
                case "grid":case "carbon":case "scan":
                    for(int x=0;x<184;x+=12)Line(x,0,p.Art=="carbon"?x+60:x,84,p.Secondary,.28);
                    for(int y=0;y<84;y+=p.Art=="scan"?5:12)Line(0,y,184,y,p.Accent,.18);break;
                case "circuit":
                    for(int i=0;i<8;i++){int x=4+i*24,y=8+i%4*13;Line(x,0,x,y,p.Accent,.65);Line(x,y,x+14,y,p.Secondary,.65);Dot(x+12,y-2,4,p.Accent,.8);}break;
                case "facets":
                    Shape("M0,0 L70,0 L0,64 Z M70,0 L118,84 L0,64 Z",p.Accent,.23);Shape("M70,0 L184,0 L118,84 Z M118,84 L184,0 L184,84 Z",p.Secondary,.24);break;
                case "rings":case "orbit":case "eclipse":
                    for(int i=0;i<4;i++){double r=12+i*9;var ring=new Ellipse{Width=r*2,Height=r*2,Stroke=B(i%2==0?p.Accent:p.Secondary),StrokeThickness=1.4,Opacity=.45};Canvas.SetLeft(ring,145-r);Canvas.SetTop(ring,42-r);c.Children.Add(ring);}
                    if(p.Art=="eclipse")Dot(130,26,30,p.Background);if(p.Art=="orbit"){var orbit=new Ellipse{Width=80,Height=19,Stroke=B(p.Accent),Opacity=.7,RenderTransform=new RotateTransform(-24,40,9)};Canvas.SetLeft(orbit,105);Canvas.SetTop(orbit,33);c.Children.Add(orbit);}break;
                case "petals":case "bubbles":case "rain":
                    for(int i=0;i<27;i++){int x=rng.Next(0,180),y=rng.Next(0,80);if(p.Art=="rain")Line(x,y,x-6,y+10,p.Accent,.45);else if(p.Art=="petals"){var petal=new Ellipse{Width=8,Height=4,Fill=B(p.Accent),Opacity=.6,RenderTransform=new RotateTransform(rng.Next(180),4,2)};Canvas.SetLeft(petal,x);Canvas.SetTop(petal,y);c.Children.Add(petal);}else {var bubble=new Ellipse{Width=5+i%8,Height=5+i%8,Stroke=B(p.Secondary),Opacity=.55};Canvas.SetLeft(bubble,x);Canvas.SetTop(bubble,y);c.Children.Add(bubble);}}break;
                case "city":
                    for(int i=0;i<12;i++){int h=15+(i*17)%47;Rect(i*16,84-h,13,h,i%2==0?p.Field:p.Stroke,1,.9);for(int k=0;k<h/9;k++)Rect(i*16+3,88-h+k*9,3,3,p.Accent,0,.7);}break;
                case "dunes":
                    Shape("M0,84 Q38,20 97,53 Q140,72 184,31 L184,84 Z",p.Secondary,.45);Shape("M0,84 Q77,48 110,66 Q158,79 184,55 L184,84 Z",p.Accent,.38);break;
                case "mountain":
                    Dot(140,9,19,p.Accent,.8);Shape("M0,84 L36,31 L62,58 L102,14 L148,64 L169,30 L184,84 Z",p.Secondary,.42);Shape("M0,84 L52,55 L76,73 L129,36 L184,84 Z",p.Field);break;
                case "fault":
                    Shape("M0,0 L71,0 L55,24 L100,48 L64,84 L0,84 Z",p.Secondary,.2);Shape("M73,0 L76,0 L60,24 L105,48 L69,84 L64,84 L100,48 L55,24 Z",p.Accent,.8);break;
            }
        }
        // Island-shaped label and miniature app surfaces show the actual palette.
        Rect(62,7,60,15,"#080B10",8,.94);Dot(70,12,5,p.Accent);Rect(82,13,25,3,p.Secondary,1,.9);
        Rect(44,30,96,40,p.Panel,6,.94);Rect(50,36,37,8,p.Field,3);Rect(91,36,43,8,p.Accent,3);
        Rect(50,49,84,4,p.Stroke,2);Rect(50,57,64,3,p.Secondary,2,.8);
        return c;
    }
}
