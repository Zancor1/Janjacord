using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Input;
using Avalonia.Layout;
using JanjaCord.Client.Media;
namespace JanjaCord.Client.Views;
public sealed class ConcurrentFrameQueue
{
    readonly ConcurrentDictionary<string,VideoPixels> latest=new();
    public void Put(string id,string kind,VideoPixels frame)=>latest[id+":"+kind]=frame;
    public IEnumerable<KeyValuePair<string,VideoPixels>> Drain(){foreach(var key in latest.Keys)if(latest.TryRemove(key,out var pixels))yield return new(key,pixels);}
    public void Clear()=>latest.Clear();
}
public sealed class VideoTile:IDisposable
{
    readonly Image image=new(){Stretch=Stretch.Uniform,Height=176,Width=304,Name="VideoPreview"};
    WriteableBitmap? bitmap;
    readonly TextBlock caption=Ui.Label("",12);
    Window? fullscreen;
    Image? fullImage;
    public Border Root {get;}
    public VideoTile(string title){caption.Text=title;var expand=Ui.Action("⛶");expand.Name="ExpandVideo";expand.Padding=new Thickness(7,2);expand.Click+=(_,_)=>OpenFullscreen();ToolTip.SetTip(expand,"Tela cheia · dois cliques no vídeo");Root=Ui.Box(Ui.Stack(7,image,Ui.Columns("*,Auto",caption,expand)),Ui.Card,9,12);Root.Margin=new Thickness(5);Root.DoubleTapped+=(_,e)=>{e.Handled=true;OpenFullscreen();};}
    public void SetTitle(string title){caption.Text=title;if(fullscreen!=null)fullscreen.Title="Tela cheia — "+title;}
    void OpenFullscreen()
    {
        if(fullscreen!=null){fullscreen.Activate();return;}if(bitmap==null)return;
        var viewer=new Window{Title="Tela cheia — "+caption.Text,Name="StreamFullscreen",WindowState=WindowState.FullScreen,SystemDecorations=SystemDecorations.None,Background=Brushes.Black};
        var picture=new Image{Source=bitmap,Stretch=Stretch.Uniform,Name="FullscreenVideoImage"};fullImage=picture;fullscreen=viewer;
        var close=Ui.Action("Sair da tela cheia  ·  Esc");close.Name="CloseFullscreenVideo";close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(18);close.Click+=(_,_)=>viewer.Close();
        viewer.Content=new Grid{Children={picture,close}};viewer.KeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;viewer.Close();}};picture.DoubleTapped+=(_,e)=>{e.Handled=true;viewer.Close();};
        viewer.Closed+=(_,_)=>{picture.Source=null;fullImage=null;fullscreen=null;};if(TopLevel.GetTopLevel(Root) is Window owner)viewer.Show(owner);else viewer.Show();
    }
    public void SetAutomationId(string id){Root.Name=id;caption.Name=id;}
    public void Update(VideoPixels frame)
    {
        if(bitmap==null||bitmap.PixelSize.Width!=frame.Width||bitmap.PixelSize.Height!=frame.Height){image.Source=null;if(fullImage!=null)fullImage.Source=null;bitmap?.Dispose();bitmap=new(new PixelSize(frame.Width,frame.Height),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque);image.Source=bitmap;if(fullImage!=null)fullImage.Source=bitmap;}
        using var buffer=bitmap.Lock();for(var y=0;y<frame.Height;y++)Marshal.Copy(frame.Bgra,y*frame.Width*4,buffer.Address+y*buffer.RowBytes,frame.Width*4);
        image.InvalidateVisual();fullImage?.InvalidateVisual();
    }
    public void Dispose(){fullscreen?.Close();image.Source=null;bitmap?.Dispose();bitmap=null;}
}
