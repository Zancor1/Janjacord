using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using JanjaCord.Client.Media;
namespace JanjaCord.Client.Views;
public sealed partial class MainWindow
{
    bool audioSettingsOpen;
    void AddMicrophoneSettings(Button button)
    {
        button.ContextRequested+=async(_,e)=>{e.Handled=true;await Safe(AudioSettings);};
    }
    async Task AudioSettings()
    {
        if(audioSettingsOpen)return;audioSettingsOpen=true;
        try{await ShowAudioSettings();}finally{audioSettingsOpen=false;}
    }
    async Task ShowAudioSettings()
    {
        var inputs=SelectedAudio.Devices(true);var outputs=SelectedAudio.Devices(false);
        ComboBox Choices(AudioDeviceChoice[] list,string? selected,string name)=>new(){ItemsSource=list,SelectedItem=list.FirstOrDefault(d=>d.Id==selected)??list[0],HorizontalAlignment=HorizontalAlignment.Stretch,Name=name};
        var input=Choices(inputs,profile.InputDeviceId,"AudioInputDevice");var output=Choices(outputs,profile.OutputDeviceId,"AudioOutputDevice");
        var dialog=new Window{Title="Dispositivos de áudio",Width=550,Height=700,MinHeight=500,CanResize=true,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Sidebar};Ui.ApplyStyles(dialog);
        var error=Ui.Label("",12,Ui.Red);var suppression=new CheckBox{Content="Supressor de ruído",IsChecked=profile.NoiseSuppressionEnabled,Name="NoiseSuppressionToggle"};
        var initial=float.IsFinite(profile.NoiseSuppressionStrength)?Math.Clamp(profile.NoiseSuppressionStrength,0,1):.9f;
        var model=new ComboBox{ItemsSource=new[]{"Avançado · DeepFilterNet","Leve · RNNoise"},SelectedIndex=profile.NoiseSuppressionAdvanced?0:1,HorizontalAlignment=HorizontalAlignment.Stretch,Name="NoiseSuppressionModel"};
        var strength=new Slider{Minimum=0,Maximum=1,Value=initial,SmallChange=.01,LargeChange=.1,TickFrequency=.01,IsSnapToTickEnabled=true,Name="NoiseSuppressionStrength"};
        var amount=Ui.Label("",15);amount.Name="NoiseSuppressionAmount";
        var state=Ui.Label("",12,Ui.Muted);state.Name="NoiseSuppressionStatus";
        void Refresh(){amount.Text=strength.Value.ToString("0.00");state.Text=suppression.IsChecked!=true?"Desativado":strength.Value==0?"Sem supressão (0)":"Ativado";}
        Refresh();
        MicrophonePreview? preview=null;bool closing=false,testing=false,wasInCall=false;
        var testStatus=Ui.Label("Teste parado",12,Ui.Muted);testStatus.Name="MicrophoneTestStatus";
        var meter=new ProgressBar{Minimum=0,Maximum=1,Value=0,Height=8,Name="MicrophoneTestLevel"};
        var test=Ui.Action("Testar meu microfone");test.Name="TestMicrophone";
        var updateGate=new SemaphoreSlim(1,1);
        async Task ApplyNoise()
        {
            await updateGate.WaitAsync();try{
                var enabled=suppression.IsChecked==true;var level=(float)strength.Value;
                var advanced=model.SelectedIndex==0;
                await call!.SetNoiseSuppression(enabled,level,advanced);preview?.SetNoiseSuppression(enabled,level,advanced);
                profile.NoiseSuppressionAdvanced=advanced;profile.NoiseSuppressionEnabled=enabled;profile.NoiseSuppressionStrength=level;profile.Save();Refresh();
            }finally{updateGate.Release();}
        }
        var debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
        model.SelectionChanged+=(_,_)=>{if(closing)return;debounce.Stop();debounce.Start();};
        debounce.Tick+=async(_,_)=>{debounce.Stop();try{await ApplyNoise();error.Text="";}catch(Exception ex){error.Text=ex.Message;}};
        strength.PropertyChanged+=(_,e)=>{if(e.Property!=Slider.ValueProperty||closing)return;Refresh();debounce.Stop();debounce.Start();};
        suppression.IsCheckedChanged+=async(_,_)=>{if(closing)return;suppression.IsEnabled=false;try{await ApplyNoise();error.Text="";}catch(Exception ex){error.Text=ex.Message;}finally{suppression.IsEnabled=true;}};
        async Task StopTest()
        {
            var active=preview;preview=null;
            try{active?.Dispose();}finally{testing=false;meter.Value=0;test.Content="Testar meu microfone";testStatus.Text="Teste parado";input.IsEnabled=output.IsEnabled=true;await call!.PauseMicrophoneForTest(false);}
        }
        test.Click+=async(_,_)=>{
            test.IsEnabled=false;try{
                if(testing){await StopTest();return;}
                await ApplyNoise();if(closing)return;await call!.PauseMicrophoneForTest(true);if(closing){await call.PauseMicrophoneForTest(false);return;}
                preview=new MicrophonePreview();preview.Error+=message=>Dispatcher.UIThread.Post(()=>{if(closing)return;error.Text=message;_ = Safe(StopTest);});
                preview.Start(((AudioDeviceChoice)input.SelectedItem!).Id,((AudioDeviceChoice)output.SelectedItem!).Id,suppression.IsChecked==true,(float)strength.Value,model.SelectedIndex==0);
                testing=true;wasInCall=call.InCall;input.IsEnabled=output.IsEnabled=false;test.Content="Parar teste";testStatus.Text="Teste local ativo · ouvindo seu microfone";error.Text="";
            }catch(Exception ex){await StopTest();error.Text=ex.Message;}finally{test.IsEnabled=true;}
        };
        var meterTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
        meterTimer.Tick+=(_,_)=>{
            meter.Value=Math.Clamp((preview?.Level??0)*3,0,1);
            if(!testing)return;if(call!.InCall)wasInCall=true;else if(wasInCall)_ = Safe(StopTest);
        };meterTimer.Start();
        void Interrupted()=>Dispatcher.UIThread.Post(()=>{if(!closing)_ = Safe(StopTest);});connection.Interrupted+=Interrupted;
        var save=Ui.Action("Aplicar",true);save.Name="ApplyAudioDevices";
        save.Click+=async(_,_)=>{save.IsEnabled=false;try{await StopTest();await ApplyNoise();var capture=((AudioDeviceChoice)input.SelectedItem!).Id;var sink=((AudioDeviceChoice)output.SelectedItem!).Id;await call!.SelectAudio(capture,sink);profile.InputDeviceId=capture;profile.OutputDeviceId=sink;profile.Save();dialog.Close();}catch(Exception ex){error.Text=ex.Message;}finally{save.IsEnabled=true;}};
        var close=Ui.Action("Fechar");close.Name="CloseAudioSettings";close.Click+=(_,_)=>dialog.Close();
        var deafen=Button(call!.Deafened?"Ativar áudio recebido":"Silenciar áudio recebido",async()=>{await StopTest();await call.ToggleDeafen();dialog.Close();});deafen.IsEnabled=call.InCall;
        var devicesRow=Ui.Columns("*,*",new Border{Margin=new Thickness(0,0,8,0),Child=Ui.Stack(6,Ui.Label("MICROFONE",11,Ui.Muted),input)},new Border{Margin=new Thickness(8,0,0,0),Child=Ui.Stack(6,Ui.Label("FONES / ALTO-FALANTES",11,Ui.Muted),output)});
        var content=Ui.Stack(12,Ui.Label("Seu áudio",25),devicesRow,new Separator(),Ui.Columns("*,Auto",suppression,state),model,Ui.Label("Avan\u00e7ado: melhor separa\u00e7\u00e3o de voz e ru\u00eddo. Leve: menor uso de CPU.",12,Ui.Muted),Ui.Columns("*,Auto",Ui.Label("Intensidade",13),amount),strength,Ui.Columns("*,Auto",Ui.Label("0 · sem supressão",11,Ui.Muted),Ui.Label("1 · máximo",11,Ui.Muted)),Ui.Label("Aumente para remover mais ruído. Diminua se partes da voz sumirem. A intensidade é aplicada na hora e fica salva.",12,Ui.Muted),new Separator(),test,testStatus,meter,Ui.Label("Use fones para evitar eco. Enquanto testa, o áudio da chamada fica pausado para você e sua voz não é enviada aos outros. Nada é gravado em arquivo.",12,Ui.Muted),error,deafen);
        var layout=new Grid{RowDefinitions=new RowDefinitions("*,Auto")};layout.Children.Add(new ScrollViewer{Content=content});var footer=Ui.Columns("*,*",close,save);footer.Margin=new Thickness(0,16,0,0);Grid.SetRow(footer,1);layout.Children.Add(footer);dialog.Content=new Border{Padding=new Thickness(24),Child=layout};
        dialog.Closed+=(_,_)=>{closing=true;debounce.Stop();meterTimer.Stop();try{preview?.Dispose();}catch(Exception ex){Report(ex.Message);}finally{preview=null;}};
        try{await dialog.ShowDialog(this);}finally{closing=true;debounce.Stop();meterTimer.Stop();connection.Interrupted-=Interrupted;await StopTest();try{await ApplyNoise();}catch(Exception ex){Report(ex.Message);}}
    }
}
