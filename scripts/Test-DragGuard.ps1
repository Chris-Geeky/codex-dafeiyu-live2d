param([Parameter(Mandatory=$true)][string]$ExecutablePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class GuardTestEvents {[DllImport("user32.dll")]public static extern void NotifyWinEvent(uint evt,IntPtr window,int obj,int child);}
public static class GuardTestVisibility {
 public struct Bounds{public int left,top,right,bottom;}
 [DllImport("user32.dll")]public static extern bool ShowWindowAsync(IntPtr w,int command);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr w);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr w,out Bounds r);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]public static extern IntPtr GetStyle(IntPtr w,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]public static extern IntPtr SetStyle(IntPtr w,int index,IntPtr style);
}
'@
$petAssembly=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExecutablePath))
$petType=$petAssembly.GetType('NativeDragGuard')
$petEvents=[Collections.Generic.List[bool]]::new()
$petCallback=[Action[bool]]{param($moving)$petEvents.Add($moving)}
$petGuard=[Activator]::CreateInstance($petType,[Reflection.BindingFlags]'Instance,NonPublic',$null,@($petCallback),$null)
$petConfigure=$petType.GetMethod('Configure',[Reflection.BindingFlags]'Instance,NonPublic')
$petChecks=[Collections.Generic.List[object]]::new()
function Test-Guard([string]$Name,[bool]$Pass){$petChecks.Add([pscustomobject]@{name=$Name;pass=$Pass});if(-not $Pass){throw $Name}}
function Wait-Guard([int]$Ms=60){$until=[DateTime]::UtcNow.AddMilliseconds($Ms);do{[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 5}while([DateTime]::UtcNow -lt $until)}
$petWindow=[Windows.Forms.Form]::new();$petWindow.ShowInTaskbar=$true;$petHandle=$petWindow.Handle
$petTool=[Windows.Forms.Form]::new();$petTool.FormBorderStyle=[Windows.Forms.FormBorderStyle]::FixedToolWindow;$petToolHandle=$petTool.Handle
try{
 $petConfigure.Invoke($petGuard,@([uint32]$PID,$true))|Out-Null
 Test-Guard 'native_event_hook_registered' ([bool]$petType.GetProperty('Registered',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($petGuard,$null))
 [GuardTestEvents]::NotifyWinEvent(0xA,$petHandle,0,0);Wait-Guard
 Test-Guard 'native_move_start_reaches_the_guard' ($petEvents.Count -eq 1 -and $petEvents[0])
 [GuardTestEvents]::NotifyWinEvent(0xB,$petHandle,0,0);Wait-Guard
 Test-Guard 'native_move_end_restores_the_state' ($petEvents.Count -eq 2 -and -not $petEvents[1])
 [GuardTestEvents]::NotifyWinEvent(0xA,$petToolHandle,0,0);Wait-Guard
 Test-Guard 'pet_tool_windows_are_excluded' ($petEvents.Count -eq 2)
 [GuardTestEvents]::NotifyWinEvent(0xA,$petHandle,0,0);Wait-Guard;Wait-Guard 330
 Test-Guard 'missed_end_notification_recovers_via_gui_state' ($petEvents.Count -eq 4 -and -not $petEvents[3])
 $petConfigure.Invoke($petGuard,@([uint32]0,$false))|Out-Null
 [GuardTestEvents]::NotifyWinEvent(0xA,$petHandle,0,0);Wait-Guard
 Test-Guard 'disabled_guard_unregisters_notifications' ($petEvents.Count -eq 4 -and -not $petType.GetProperty('Registered',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($petGuard,$null))
 $petTool.Opacity=0
 $style=[GuardTestVisibility]::GetStyle($petToolHandle,-20).ToInt64() -bor 0x08000020
 $null=[GuardTestVisibility]::SetStyle($petToolHandle,-20,[IntPtr]$style)
 $null=[GuardTestVisibility]::ShowWindowAsync($petToolHandle,4);Wait-Guard
 $rect=[GuardTestVisibility+Bounds]::new();$null=[GuardTestVisibility]::GetWindowRect($petToolHandle,[ref]$rect)
 $petConfigure.Invoke($petGuard,@([uint32]$PID,$true))|Out-Null
 $petType.GetMethod('TrackPet',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($petGuard,@([uint32]$PID,$rect.left,$rect.top,($rect.right-$rect.left),($rect.bottom-$rect.top)))|Out-Null
 [GuardTestEvents]::NotifyWinEvent(0xA,$petHandle,0,0);Wait-Guard
 Test-Guard 'native_fast_path_hides_verified_overlay' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 [GuardTestEvents]::NotifyWinEvent(0xB,$petHandle,0,0);Wait-Guard
 Test-Guard 'native_fast_path_restores_without_activation' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $null=[GuardTestVisibility]::ShowWindowAsync($petToolHandle,0);Wait-Guard
 [GuardTestEvents]::NotifyWinEvent(0xA,$petHandle,0,0);Wait-Guard
 [GuardTestEvents]::NotifyWinEvent(0xB,$petHandle,0,0);Wait-Guard
 Test-Guard 'native_fast_path_preserves_already_hidden_overlay' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
}finally{$petGuard.Dispose();$petWindow.Dispose();$petTool.Dispose()}
$petChecks|ConvertTo-Json -Depth 3
