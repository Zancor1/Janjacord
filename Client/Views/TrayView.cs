using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    TrayIcon? tray;
    bool exiting;
    void RestoreFromTray(){if(exiting||shuttingDown)return;Show();WindowState=WindowState.Normal;Activate();}
    void UpdateTrayLabel(){if(tray!=null)tray.ToolTipText="JanjaCord - "+(string.IsNullOrWhiteSpace(profile.Name)?"Seu grupo":profile.Name);}
    void ConfigureTray()
    {
        if(!OperatingSystem.IsWindows()||tray!=null)return;
        using var bitmap=new System.Drawing.Bitmap(32,32);
        using(var graphics=System.Drawing.Graphics.FromImage(bitmap)){
            graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(System.Drawing.Color.Transparent);
            using var background=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(105,80,225));graphics.FillEllipse(background,1,1,30,30);
            using var font=new System.Drawing.Font("Segoe UI",18,System.Drawing.FontStyle.Bold,System.Drawing.GraphicsUnit.Pixel);
            using var ink=new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var format=new System.Drawing.StringFormat{Alignment=System.Drawing.StringAlignment.Center,LineAlignment=System.Drawing.StringAlignment.Center};
            graphics.DrawString("J",font,ink,new System.Drawing.RectangleF(0,0,32,31),format);
        }
        using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);stream.Position=0;
        var menu=new NativeMenu();var open=new NativeMenuItem("Abrir JanjaCord");open.Click+=(_,_)=>RestoreFromTray();menu.Items.Add(open);menu.Items.Add(new NativeMenuItemSeparator());
        var exit=new NativeMenuItem("Sair do JanjaCord");exit.Click+=(_,_)=>ExitFromTray();menu.Items.Add(exit);
        tray=new TrayIcon{Icon=new WindowIcon(stream),ToolTipText="JanjaCord · clique para abrir",Menu=menu,IsVisible=true};tray.Clicked+=(_,_)=>RestoreFromTray();TrayIcon.SetIcons(Avalonia.Application.Current!,new TrayIcons{tray});UpdateTrayLabel();
        Closing+=(_,e)=>{if(exiting||e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown or WindowCloseReason.OwnerWindowClosing)return;e.Cancel=true;Hide();};
    }
    void ExitFromTray(){if(exiting)return;exiting=true;Close();}
    void FinishShutdown(){tray?.Dispose();tray=null;if(Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)desktop.Shutdown();}
}
