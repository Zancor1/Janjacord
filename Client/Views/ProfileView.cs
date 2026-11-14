using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using JanjaCord.Shared;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    void ShowReturningProfile()
    {
        var content=Ui.Stack(20,Brand(),Ui.Avatar(profile.Avatar,profile.Name,84),Ui.Label("Bom te ver novamente,",22),Ui.Label(profile.Name,30),Ui.Label("Seu perfil está salvo. Conectando à sua rede…",14,Ui.Muted),new ProgressBar{IsIndeterminate=true,Height=4},NewStatus());
        Content=new Border{MaxWidth=480,Margin=new Thickness(32),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=Ui.Box(content,Ui.Sidebar,30,20)};
    }
    async Task ChoosePhoto(){if(await PickPhoto(this) is string photo){profile.Avatar=photo;profile.Save();}}
    static async Task<string?> PickPhoto(Window owner)
    {
        var files=await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Escolha sua foto de perfil",AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("Fotos"){Patterns=["*.png","*.jpg","*.jpeg","*.bmp"]}]});
        if(files.Count==0)return null;var file=files[0];var properties=await file.GetBasicPropertiesAsync();if(properties.Size>10*1024*1024)throw new Exception("Escolha uma imagem de até 10 MB.");
        await using var input=await file.OpenReadAsync();using var image=System.Drawing.Image.FromStream(input);using var square=new System.Drawing.Bitmap(160,160);
        using(var graphics=System.Drawing.Graphics.FromImage(square)){graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;var side=Math.Min(image.Width,image.Height);graphics.DrawImage(image,new System.Drawing.Rectangle(0,0,160,160),new System.Drawing.Rectangle((image.Width-side)/2,(image.Height-side)/2,side,side),System.Drawing.GraphicsUnit.Pixel);}
        using var output=new MemoryStream();square.Save(output,System.Drawing.Imaging.ImageFormat.Png);return Convert.ToBase64String(output.ToArray());
    }
    async Task EditProfile()
    {
        var dialog=new Window{Title="Meu perfil",Width=480,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var name=Ui.Field("Seu nome",profile.Name);name.Name="EditProfileName";name.MaxLength=32;var pendingAvatar=profile.Avatar;
        var preview=new ContentControl{Content=Ui.Avatar(pendingAvatar,name.Text??"J",88),HorizontalAlignment=HorizontalAlignment.Center};name.TextChanged+=(_,_)=>preview.Content=Ui.Avatar(pendingAvatar,name.Text??"J",88);
        var notice=Ui.Label(connection.Connected?"Seu nome deve ser único na sua rede. Seus amigos e conversas serão mantidos.":"Sua foto pode ser salva neste computador e será sincronizada quando a rede voltar. Para alterar o nome, conecte-se à rede.",12,Ui.Muted);notice.Name="ProfileEditStatus";
        name.IsEnabled=connection.Connected;editingProfile=true;
        var loadingPhoto=false;
        var save=Ui.Action("Salvar alterações",true);save.Name="SaveProfileChanges";
        var remove=Ui.Action("Remover foto");remove.Name="RemoveProfilePhoto";remove.Click+=(_,_)=>{pendingAvatar=null;preview.Content=Ui.Avatar(null,name.Text??"J",88);};
        var choose=Ui.Action("Alterar foto");choose.Name="ChangeProfilePhoto";choose.Click+=async(_,_)=>{
            loadingPhoto=true;choose.IsEnabled=remove.IsEnabled=save.IsEnabled=false;notice.Text="Preparando sua foto…";
            try{if(await PickPhoto(dialog) is string photo){pendingAvatar=photo;preview.Content=Ui.Avatar(pendingAvatar,name.Text??"J",88);notice.Text="Foto pronta. Clique em Salvar alterações para confirmar.";}else notice.Text="Escolha outra foto ou salve seu nome.";}
            catch(Exception ex){notice.Text=ex.Message;}finally{loadingPhoto=false;choose.IsEnabled=remove.IsEnabled=save.IsEnabled=true;}
        };
        save.Click+=async(_,_)=>{
            if(loadingPhoto)return;
            if(!NameRules.Valid(name.Text)){notice.Text="Use de 2 a 32 caracteres: letras, números, espaço, ponto, hífen ou sublinhado.";return;}
            save.IsEnabled=false;
            try{
                if(!connection.Connected){
                    if(NameRules.Normalize(name.Text!)!=profile.Name){notice.Text="Sua rede caiu. Reconecte para confirmar o novo nome, ou mantenha o nome atual para salvar a foto.";return;}
                    profile.Avatar=pendingAvatar;profile.PendingAvatarSync=true;profile.Save();ApplyProfileChange(new(profile.UserId,profile.Name,pendingAvatar));dialog.Close();status.Text="Foto salva neste computador. Ela será sincronizada quando a rede voltar.";
                }else{var updated=await connection.UpdateProfile(new(NameRules.Normalize(name.Text!),pendingAvatar));profile.PendingAvatarSync=false;ApplyProfileChange(updated);dialog.Close();status.Text="Seu nome e sua foto foram salvos.";}
            }
            catch(Exception ex){notice.Text=ex.Message;}
            finally{save.IsEnabled=true;}
        };
        var cancel=Ui.Action("Cancelar");cancel.Name="CancelProfileEdit";cancel.Click+=(_,_)=>dialog.Close();
        dialog.Content=new Border{Padding=new Thickness(28),Child=Ui.Stack(18,Ui.Label("Seu perfil",27),preview,Ui.Columns("*,*",choose,remove),Ui.Label("NOME",11,Ui.Muted),name,notice,Ui.Columns("*,*",cancel,save))};try{await dialog.ShowDialog(this);}finally{editingProfile=false;}
    }
    void ApplyProfileChange(MemberProfile updated)
    {
        if(shuttingDown)return;
        if(updated.Id==connection.Session?.UserId){profile.Name=updated.Name;profile.Avatar=updated.Avatar;profile.Save();personalName.Text=updated.Name;personalAvatar.Content=Ui.Avatar(updated.Avatar,updated.Name,33);}
        for(var i=0;i<friends.Count;i++)if(friends[i].Id==updated.Id)friends[i]=friends[i] with{Name=updated.Name,Avatar=updated.Avatar};
        cache.Friends=friends.ToList();foreach(var list in cache.Messages.Values)for(var i=0;i<list.Count;i++)if(list[i].SenderId==updated.Id)list[i]=list[i] with{Author=updated.Name};cache.Save();
        for(var g=0;g<directGroups.Count;g++)directGroups[g]=directGroups[g] with{Members=directGroups[g].Members.Select(m=>m.Id==updated.Id?updated:m).ToArray()};
        if(activeGroup!=null){activeGroup=directGroups.FirstOrDefault(g=>g.Id==activeGroup.Id)??activeGroup;RenderGroupInfo();}
        UpdateTrayLabel();
        if(members.ContainsKey(updated.Id))members[updated.Id]=updated;
        for(var i=0;i<messages.Count;i++)if(messages[i].UserId==updated.Id)messages[i]=messages[i] with{Author=updated.Name};
        if(activeDirect?.Id==updated.Id){activeDirect=activeDirect with{Name=updated.Name,Avatar=updated.Avatar};title.Text=updated.Name;headerSymbol.Content=Ui.Avatar(updated.Avatar,updated.Name,28);draft.Watermark="Mensagem para @"+updated.Name;SetDirectState();}
        if(communityList.SelectedItem is Community community){memberList.Children.Clear();foreach(var member in members.Values.OrderByDescending(m=>m.Id==community.OwnerId).ThenBy(m=>m.Name))memberList.Children.Add(Ui.Columns("Auto,*",Ui.Avatar(member.Avatar,member.Name,34),new Border{Margin=new Thickness(9,0),Child=Ui.Stack(2,Ui.Label(member.Name,13),Ui.Label(member.Id==community.OwnerId?"Dono do servidor":"Membro",10,Ui.Muted))}));}
        ApplySearch();RenderFriends();
    }
}
