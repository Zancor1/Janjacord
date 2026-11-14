using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
namespace JanjaCord.Client.Views;
public static class Ui
{
    public static readonly IBrush Background=Brush("#080809"),Sidebar=Brush("#101012"),Panel=Brush("#121214"),Card=Brush("#242428"),Accent=Brush("#A092F6"),Text=Brush("#E8E8EC"),Muted=Brush("#8E8E99"),Line=Brush("#242428"),Green=Brush("#58CB91"),Red=Brush("#F27D88");
    public static IBrush Brush(string hex)=>new SolidColorBrush(Color.Parse(hex));
    public static TextBlock Label(string text,double size=14,IBrush? color=null)=>new(){Text=text,FontSize=size,Foreground=color??Text,TextWrapping=TextWrapping.Wrap};
    public static StackPanel Stack(double spacing,params Control[] children){var p=new StackPanel{Spacing=spacing};foreach(var c in children)p.Children.Add(c);return p;}
    public static Border Box(Control child,IBrush? background=null,double padding=16,double radius=14)=>new(){Child=child,Background=background??Panel,Padding=new Thickness(padding),CornerRadius=new CornerRadius(radius)};
    public static Button Action(string text,bool primary=false)=>new(){Content=text,Background=primary?Accent:Card,Foreground=primary?Brush("#151122"):Text,Padding=new Thickness(16,11),CornerRadius=new CornerRadius(9),FontWeight=FontWeight.SemiBold,HorizontalContentAlignment=HorizontalAlignment.Center};
    public static TextBox Field(string placeholder,string? text=null)=>new(){Watermark=placeholder,Text=text,Background=Background,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(14,11),FontSize=14};
    public static Border Avatar(string? photo,string name,double size=42)
    {
        Control content;
        try{content=photo==null?Initial(name,size):new Image{Source=new Bitmap(new MemoryStream(Convert.FromBase64String(photo))),Stretch=Stretch.UniformToFill};}catch{content=Initial(name,size);}
        return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size/2),ClipToBounds=true,Background=Brush("#51457E"),Child=content};
    }
    static TextBlock Initial(string name,double size)=>new(){Text=string.IsNullOrWhiteSpace(name)?"J":name.Trim()[..1].ToUpperInvariant(),FontSize=size*.4,FontWeight=FontWeight.Bold,Foreground=Text,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    public static Grid Columns(string definitions,params Control[] children){var g=new Grid{ColumnDefinitions=new ColumnDefinitions(definitions)};for(var i=0;i<children.Length;i++){Grid.SetColumn(children[i],i);g.Children.Add(children[i]);}return g;}
    public static void ApplyStyles(Window window)
    {
        window.FontFamily=new FontFamily("Segoe UI");window.Foreground=Text;
        window.Resources["SystemControlHighlightListAccentLowBrush"]=Brush("#252527");
        window.Resources["SystemControlHighlightListAccentMediumBrush"]=Brush("#303034");
        window.Resources["SystemControlHighlightListAccentHighBrush"]=Brush("#37373C");
        window.Styles.Add(new Style(s=>s.OfType<ListBox>()){Setters={new Setter(ListBox.BackgroundProperty,Brushes.Transparent),new Setter(ListBox.BorderThicknessProperty,new Thickness(0))}});
        window.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()){Setters={new Setter(ListBoxItem.PaddingProperty,new Thickness(10)),new Setter(ListBoxItem.CornerRadiusProperty,new CornerRadius(8)),new Setter(ListBoxItem.MarginProperty,new Thickness(0,3))}});
        window.Styles.Add(new Style(s=>s.OfType<ListBoxItem>().Class(":selected")){Setters={new Setter(ListBoxItem.BackgroundProperty,Brush("#28282D")),new Setter(ListBoxItem.ForegroundProperty,Text)}});
        window.Styles.Add(new Style(s=>s.OfType<Button>().Class(":pointerover")){Setters={new Setter(Button.OpacityProperty,.85)}});
    }
    public static Control Icon(string name,double size=20,IBrush? color=null)
    {
        var data=name switch {
            "mic"=>"M12 2 A3 3 0 0 0 9 5 L9 12 A3 3 0 0 0 15 12 L15 5 A3 3 0 0 0 12 2 M5 10 L5 12 A7 7 0 0 0 19 12 L19 10 M12 19 L12 22 M8 22 L16 22",
            "more"=>"M4 12 L5 12 M11.5 12 L12.5 12 M19 12 L20 12",
            "mic-off"=>"M3 3 L21 21 M9 7 L9 12 A3 3 0 0 0 14 14 M15 9 L15 5 A3 3 0 0 0 10 3 M5 10 L5 12 A7 7 0 0 0 17 17 M19 10 L19 12 M12 19 L12 22 M8 22 L16 22",
            "audio"=>"M3 14 L3 11 A9 9 0 0 1 21 11 L21 14 M3 13 L7 13 L7 21 L3 21 Z M17 13 L21 13 L21 21 L17 21 Z",
            "camera"=>"M2 5 L16 5 L16 19 L2 19 Z M16 9 L22 6 L22 18 L16 15",
            "screen"=>"M3 3 L21 3 L21 16 L3 16 Z M12 16 L12 21 M7 21 L17 21 M12 12 L12 6 M9 9 L12 6 L15 9",
            "phone"=>"M5 3 L9 3 L10 8 L8 10 Q10 14 14 16 L16 14 L21 15 L21 19 Q21 22 18 21 Q5 18 3 6 Q2 3 5 3 Z",
            "leave"=>"M3 16 Q12 8 21 16 M3 16 L3 20 L7 20 L7 15 M17 15 L17 20 L21 20 L21 16",
            "plus"=>"M12 4 L12 20 M4 12 L20 12",
            "join"=>"M4 4 L13 4 L13 8 M13 16 L13 20 L4 20 Z M10 12 L22 12 M18 8 L22 12 L18 16",
            "user-plus"=>"M14 21 L14 19 A4 4 0 0 0 10 15 L6 15 A4 4 0 0 0 2 19 L2 21 M12 7 A4 4 0 1 1 4 7 A4 4 0 1 1 12 7 M19 8 L19 16 M15 12 L23 12",
            "users"=>"M16 21 L16 19 A4 4 0 0 0 12 15 L6 15 A4 4 0 0 0 2 19 L2 21 M13 7 A4 4 0 1 1 5 7 A4 4 0 1 1 13 7 M16 4 A4 4 0 0 1 16 12 M19 15 A4 4 0 0 1 22 19 L22 21",
            "refresh"=>"M20 7 A9 9 0 1 0 21 16 M20 2 L20 8 L14 8",
            "chevron"=>"M6 9 L12 15 L18 9",
            "hash"=>"M9 3 L7 21 M17 3 L15 21 M3 8 L21 8 M2 16 L20 16",
            "voice"=>"M3 9 L7 9 L12 4 L12 20 L7 15 L3 15 Z M16 8 Q20 12 16 16 M19 5 Q25 12 19 19",
            "send"=>"M3 3 L22 12 L3 21 L7 12 Z M7 12 L22 12",
            _=>"M6 6 L18 18 M6 18 L18 6"
        };
        var canvas=new Canvas{Width=24,Height=24};canvas.Children.Add(new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(data),Stroke=color??Muted,StrokeThickness=1.7,StrokeLineCap=PenLineCap.Round,StrokeJoin=PenLineJoin.Round});return new Viewbox{Width=size,Height=size,Child=canvas};
    }
}
