using Avalonia.Controls;
using JanjaCord.Client.Services;
using JanjaCord.Shared;
using Microsoft.AspNetCore.SignalR;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    bool flushingOutbox;
    Avalonia.Threading.DispatcherTimer? outboxRetry;
    void StartOutboxRetry(){outboxRetry?.Stop();outboxRetry=new(){Interval=TimeSpan.FromSeconds(5)};outboxRetry.Tick+=(_,_)=>_ = Safe(FlushOutbox);outboxRetry.Start();}
    void AppendPending(PendingDirect pending)=>Append(new(pending.LocalId,0,connection.Session!.UserId,profile.Name,pending.Text,pending.SentAt));
    void RemovePendingView(long id){messageIds.Remove(id);var old=messages.FirstOrDefault(m=>m.Id==id);if(old!=null)messages.Remove(old);}
    async Task FlushOutbox()
    {
        if(flushingOutbox||!connection.Connected||shuttingDown)return;flushingOutbox=true;
        try{foreach(var pending in cache.Outbox.Where(p=>p.Error==null).ToArray()){
            if(!connection.Connected||shuttingDown)break;
            try{var delivered=await connection.SendDirect(pending.PeerId,pending.Text,pending.ClientId);cache.Put(delivered,connection.Session!.UserId);if(activeDirect?.Id==pending.PeerId){RemovePendingView(pending.LocalId);AppendDirect(delivered);}}
            catch(HubException ex){var index=cache.Outbox.FindIndex(p=>p.ClientId==pending.ClientId);if(index>=0)cache.Outbox[index]=pending with{Error=ex.Message};cache.Save();if(activeDirect?.Id==pending.PeerId){RemovePendingView(pending.LocalId);AppendPending(cache.Outbox.First(p=>p.ClientId==pending.ClientId));}Report("Não foi possível enviar uma mensagem: "+ex.Message);}
            catch(Exception ex){Report("Mensagem guardada para enviar quando a rede voltar. "+ex.Message);break;}
        }}finally{flushingOutbox=false;}
    }
    Control PendingStatus(PendingDirect pending)
    {
        var label=Ui.Label(pending.Error==null?"◷ Pendente · será enviada quando a rede voltar":"Falha no envio · clique direito para tentar novamente",11,pending.Error==null?Ui.Muted:Ui.Red);
        label.Name="PendingMessage_"+pending.ClientId;
        var retry=new MenuItem{Header="Tentar novamente"};retry.Click+=async(_,_)=>{var index=cache.Outbox.FindIndex(p=>p.ClientId==pending.ClientId);if(index>=0){cache.Outbox[index]=pending with{Error=null};cache.Save();RemovePendingView(pending.LocalId);AppendPending(cache.Outbox[index]);await Safe(FlushOutbox);}};
        var cancel=new MenuItem{Header="Cancelar envio"};cancel.Click+=(_,_)=>{cache.Outbox.RemoveAll(p=>p.ClientId==pending.ClientId);cache.Save();RemovePendingView(pending.LocalId);};
        label.ContextMenu=new ContextMenu{ItemsSource=new[]{retry,cancel}};return label;
    }
}
