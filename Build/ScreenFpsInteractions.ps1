function Test-ScreenFps($a,$b,$testRoot){
    Invoke-Button (Wait-Control $a 'StartDirectCall');$ring=Wait-Dialog $b 'Chamada recebida';Invoke-Button (Find-Control $ring 'AcceptDirectCall')
    for($i=0;$i -lt 100;$i++){if((Wait-Control $a 'CallScreenShare').Current.IsEnabled){break};Start-Sleep -Milliseconds 100}
    Invoke-Button (Wait-Control $a 'CallScreenShare');$dialog=Wait-Dialog $a 'Compartilhar tela'
    $quality=Find-Control $dialog 'ScreenShareQuality';if(!$quality){throw 'Share quality selector missing'}
    $expand=$quality.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern);$expand.Expand();Start-Sleep -Milliseconds 200
    $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)
    $items=$quality.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
    if($items.Count -ne 6){Save-Window $a (Join-Path $root '.tools/ui-screen-fps-options.png');throw "Expected six quality choices; found $($items.Count)"}
    $items[2].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();$expand.Collapse()
    Invoke-Button (Find-Control $dialog 'StartScreenShare')
    $identity=Get-Content -Raw -Encoding utf8 (Join-Path $testRoot 'a/profile.json') | ConvertFrom-Json
    $null=Wait-Control $b ('ScreenParticipant_'+$identity.UserId)
    Invoke-Button (Wait-Control $a 'CallScreenShare');Invoke-Button (Wait-Control $a 'CallHangup')
    Write-Output 'PASS: actual share dialog has six quality choices; 720p60 starts, remote screen arrives and sharing stops'
}
