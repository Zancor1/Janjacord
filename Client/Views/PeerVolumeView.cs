using Avalonia;
using Avalonia.Controls;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    string VolumeKey(long user)=>profile.Fingerprint+":"+user;
    int PeerVolume(long user)=>Math.Clamp(profile.PeerVolumes.GetValueOrDefault(VolumeKey(user),100),0,200);
    Border CallAvatar(MemberProfile peer,double size)
    {
        var avatar=Ui.Avatar(peer.Avatar,peer.Name,size);if(peer.Id==connection.Session?.UserId)return avatar;
        var value=Ui.Label(PeerVolume(peer.Id)+"%",13);value.Name="PeerVolumeValue";
        var slider=new Slider{Minimum=0,Maximum=200,Value=PeerVolume(peer.Id),TickFrequency=1,IsSnapToTickEnabled=true,Width=220,Name="PeerVolumeSlider_"+peer.Id};
        slider.PropertyChanged+=(_,e)=>{if(e.Property!=Slider.ValueProperty)return;var percent=(int)Math.Round(slider.Value);value.Text=percent+"%";profile.PeerVolumes[VolumeKey(peer.Id)]=percent;_ = Safe(()=>call!.SetPeerVolume(peer.Id,percent));};
        Button Preset(int percent){var b=Ui.Action(percent+"%");b.Padding=new Thickness(8,5);b.Click+=(_,_)=>slider.Value=percent;return b;}
        var content=Ui.Stack(10,Ui.Label("Volume de "+peer.Name,14),Ui.Columns("*,Auto",Ui.Label("Só para você",11,Ui.Muted),value),slider,Ui.Columns("*,*,*",Preset(0),Preset(100),Preset(200)));
        var item=new MenuItem{Header=content,StaysOpenOnClick=true,Name="PeerVolumeMenu_"+peer.Id};var menu=new ContextMenu{ItemsSource=new[]{item}};
        menu.Opened+=(_,_)=>{slider.Value=PeerVolume(peer.Id);value.Text=PeerVolume(peer.Id)+"%";};menu.Closed+=(_,_)=>{try{profile.Save();}catch(Exception ex){Report(ex.Message);}};
        avatar.ContextMenu=menu;ToolTip.SetTip(avatar,peer.Name+" · clique direito para ajustar o volume");return avatar;
    }
}
