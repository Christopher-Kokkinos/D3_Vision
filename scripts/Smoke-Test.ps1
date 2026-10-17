$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class D3SmokeNative {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(IntPtr cls,string title);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetStyle(IntPtr window,int index);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
}
"@
function Press-Key([byte]$key) {
 [D3SmokeNative]::keybd_event($key,0,0,[UIntPtr]::Zero)
 [D3SmokeNative]::keybd_event($key,0,2,[UIntPtr]::Zero)
 Start-Sleep -Milliseconds 250
}
$workspace = Split-Path -Parent $PSScriptRoot
$appPath=Join-Path $workspace 'src\D3Vision.UI\bin\Release\net8.0-windows10.0.19041.0\D3Vision.UI.exe'
$testProcess=Start-Process -FilePath $appPath -WindowStyle Hidden -PassThru
try {
 $deadline=[DateTime]::UtcNow.AddSeconds(15)
 $exitControl=$null
 while([DateTime]::UtcNow -lt $deadline -and !$testProcess.HasExited) {
  $condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$testProcess.Id)
  $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
  foreach($window in $windows) {
   $buttonCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'Exit D3Vision')
   $exitControl=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$buttonCondition)
   if($exitControl){break}
  }
  if($exitControl){break}
  Start-Sleep -Milliseconds 250
 }
 if(!$exitControl){throw 'Settings window did not appear.'}
 $overlay=[D3SmokeNative]::FindWindow([IntPtr]::Zero,'D3Vision Overlay')
 if($overlay -eq [IntPtr]::Zero){throw 'Overlay HWND missing.'}
 $visibleBefore=[D3SmokeNative]::IsWindowVisible($overlay)
 Press-Key 0x79
 if([D3SmokeNative]::IsWindowVisible($overlay) -eq $visibleBefore){throw 'F10 did not toggle visibility.'}
 Press-Key 0x79
 $styleBefore=[D3SmokeNative]::GetStyle($overlay,-20).ToInt64()
 Press-Key 0x7A
 $styleAfter=[D3SmokeNative]::GetStyle($overlay,-20).ToInt64()
 if(($styleBefore -band 0x20) -eq ($styleAfter -band 0x20)){throw 'F11 did not toggle click-through.'}
 Press-Key 0x7A
 $applyCondition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'Apply and save')
 $apply=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$applyCondition)
 $apply.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 250
 Press-Key 0x7B
 $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
 $exitControl=$null
 foreach($window in $windows){
  $exitControl=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$buttonCondition)
  if($exitControl){break}
 }
 if(!$exitControl){throw 'F12 did not reopen settings.'}
 Write-Output 'F10 / F11 / F12 hotkey checks passed.'
 $testProcess.Refresh()
 Write-Output ('Settings UI found. Working set: {0:N1} MB' -f ($testProcess.WorkingSet64/1MB))
 $invoke=$exitControl.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
 $invoke.Invoke()
 if(!$testProcess.WaitForExit(15000)){throw 'Exit command did not terminate the app.'}
 if($testProcess.ExitCode -ne 0){throw "Application exit code $($testProcess.ExitCode)"}
 Write-Output 'UI startup/shutdown passed.'
}finally{
 if(!$testProcess.HasExited){Stop-Process -Id $testProcess.Id}
}
