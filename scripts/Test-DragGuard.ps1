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
$script:petNearFrame=$false
$script:petButtonDown=$false
$petSampler=[Func[bool]]{return $script:petNearFrame}
$petButtonSampler=[Func[bool]]{return $script:petButtonDown}
$petGuard=[Activator]::CreateInstance($petType,[Reflection.BindingFlags]'Instance,NonPublic',$null,@($petCallback,$petSampler,$petButtonSampler),$null)
$petConfigure=$petType.GetMethod('Configure',[Reflection.BindingFlags]'Instance,NonPublic')
$petEarly=$petType.GetMethod('SetEarlyAvoidance',[Reflection.BindingFlags]'Instance,NonPublic')
$petTrigger=$petType.GetMethod('SetAvoidanceTrigger',[Reflection.BindingFlags]'Instance,NonPublic')
$petArea=$petType.GetMethod('IsFrameArea',[Reflection.BindingFlags]'Static,NonPublic')
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
 $null=[GuardTestVisibility]::ShowWindowAsync($petToolHandle,4);Wait-Guard
 $script:petNearFrame=$true;Wait-Guard 80
 Test-Guard 'ordinary_mode_does_not_hide_on_hover' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $petTrigger.Invoke($petGuard,@('hover'))|Out-Null
 $petEarly.Invoke($petGuard,@($true))|Out-Null;Wait-Guard
 Test-Guard 'early_avoidance_hides_before_a_move_event' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle) -and $petEvents[$petEvents.Count-1])
 $script:petNearFrame=$false;Wait-Guard 65
 Test-Guard 'brief_frame_gaps_do_not_flicker' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 Wait-Guard 260
 Test-Guard 'leaving_the_frame_restores_the_owned_pet' ([GuardTestVisibility]::IsWindowVisible($petToolHandle) -and -not $petEvents[$petEvents.Count-1])
 $script:petNearFrame=$true;Wait-Guard
 [GuardTestEvents]::NotifyWinEvent(0xA,$petHandle,0,0);Wait-Guard
 [GuardTestEvents]::NotifyWinEvent(0xB,$petHandle,0,0);Wait-Guard
 Test-Guard 'release_over_frame_keeps_early_avoidance_active' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $petEarly.Invoke($petGuard,@($false))|Out-Null;Wait-Guard
 Test-Guard 'turning_off_early_avoidance_restores_immediately' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $petEarly.Invoke($petGuard,@($true))|Out-Null;Wait-Guard
 $petConfigure.Invoke($petGuard,@([uint32]0,$false))|Out-Null;Wait-Guard
 Test-Guard 'turning_off_compatibility_cancels_early_avoidance' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $petConfigure.Invoke($petGuard,@([uint32]$PID,$true))|Out-Null
 $petType.GetMethod('TrackPet',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($petGuard,@([uint32]$PID,$rect.left,$rect.top,($rect.right-$rect.left),($rect.bottom-$rect.top)))|Out-Null
 $null=[GuardTestVisibility]::ShowWindowAsync($petToolHandle,0);Wait-Guard
 $script:petNearFrame=$false;Wait-Guard 260
 Test-Guard 'early_avoidance_never_wakes_an_originally_hidden_pet' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $petEarly.Invoke($petGuard,@($false))|Out-Null
 $petTrigger.Invoke($petGuard,@('press'))|Out-Null
 $null=[GuardTestVisibility]::ShowWindowAsync($petToolHandle,4);Wait-Guard
 $petEarly.Invoke($petGuard,@($true))|Out-Null
 $script:petNearFrame=$true;Wait-Guard
 Test-Guard 'press_mode_keeps_pet_visible_when_pointer_only_passes' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petButtonDown=$true;Wait-Guard
 Test-Guard 'press_on_frame_hides_before_move_notification' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petNearFrame=$false;Wait-Guard
 Test-Guard 'a_press_from_the_frame_stays_hidden_across_client_area' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petNearFrame=$true;$script:petButtonDown=$false;Wait-Guard
 Test-Guard 'release_over_frame_restores_in_press_mode' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petNearFrame=$false;$script:petButtonDown=$true;Wait-Guard
 Test-Guard 'pressing_in_content_does_not_hide_pet' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petNearFrame=$true;Wait-Guard
 Test-Guard 'text_selection_dragging_to_header_does_not_hide_pet' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petButtonDown=$false;Wait-Guard
 $script:petButtonDown=$true;Wait-Guard
 $petEarly.Invoke($petGuard,@($false))|Out-Null;Wait-Guard
 Test-Guard 'turning_off_press_mode_restores_while_button_held' ([GuardTestVisibility]::IsWindowVisible($petToolHandle))
 $script:petButtonDown=$false;Wait-Guard
 $null=[GuardTestVisibility]::ShowWindowAsync($petToolHandle,0);Wait-Guard
 $petEarly.Invoke($petGuard,@($true))|Out-Null
 $script:petButtonDown=$true;Wait-Guard
 $script:petButtonDown=$false;Wait-Guard
 Test-Guard 'press_mode_preserves_originally_hidden_pet' (-not [GuardTestVisibility]::IsWindowVisible($petToolHandle))
 Test-Guard 'client_content_is_not_a_frame' (-not $petArea.Invoke($null,@(1,500,500,0,0,1200,900,96)))
 Test-Guard 'custom_header_scales_with_window_dpi' ([bool]$petArea.Invoke($null,@(1,500,80,0,0,1200,900,192)))
 Test-Guard 'small_dpi_header_does_not_cover_content' (-not $petArea.Invoke($null,@(1,500,80,0,0,1200,900,96)))
 Test-Guard 'negative_monitor_coordinates_are_supported' ([bool]$petArea.Invoke($null,@(10,-1998,500,-2000,0,-800,900,144)))
 Test-Guard 'points_outside_window_do_not_trigger' (-not $petArea.Invoke($null,@(2,500,-1,0,0,1200,900,96)))
 Test-Guard 'scrollbars_do_not_trigger_early_avoidance' (-not $petArea.Invoke($null,@(7,1198,500,0,0,1200,900,96)))
}finally{$petGuard.Dispose();$petWindow.Dispose();$petTool.Dispose()}
$petChecks|ConvertTo-Json -Depth 3
