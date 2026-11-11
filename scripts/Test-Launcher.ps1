[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PetCaptionTest {
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr handle,int index);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
    public static int Hit(IntPtr handle,int x,int y) {
        return (int)SendMessage(handle,0x84,IntPtr.Zero,(IntPtr)((x & 0xffff) | ((y & 0xffff) << 16)));
    }
}
'@
$petAssembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExecutablePath))
$petChecks = [Collections.Generic.List[object]]::new()
function Test-PetCondition([string]$Name,[bool]$Pass) {
    $petChecks.Add([pscustomobject]@{name=$Name;pass=$Pass})
    if (-not $Pass) { throw ('Native UI check failed: ' + $Name) }
}
$petConsent = [Activator]::CreateInstance($petAssembly.GetType('CoreConsent'),$true)
try {
    $petAgree = @($petConsent.Controls | Where-Object { $_ -is [Windows.Forms.CheckBox] })[0]
    $petAccept = @($petConsent.Controls | Where-Object { $_ -is [Windows.Forms.Button] -and $_.DialogResult -eq [Windows.Forms.DialogResult]::OK })[0]
    Test-PetCondition 'first_use_requires_explicit_consent' (-not $petAgree.Checked -and -not $petAccept.Enabled)
    $petAgree.Checked = $true
    Test-PetCondition 'agreement_enables_preparation' $petAccept.Enabled
    $petAgree.Checked = $false
    Test-PetCondition 'withdrawn_agreement_disables_preparation' (-not $petAccept.Enabled)
} finally { $petConsent.Dispose() }
$petPanel = [Activator]::CreateInstance($petAssembly.GetType('Dashboard'),$true)
try {
    $petUpdate = $petPanel.GetType().GetMethod('UpdateState',[Reflection.BindingFlags]'Instance,NonPublic')
    $petEnable = $petPanel.Controls.Find('EnablePet',$true)[0]
    $petRestore = $petPanel.Controls.Find('RestorePet',$true)[0]
    $petFollow = $petPanel.Controls.Find('FollowPet',$true)[0]
    $petFps30 = $petPanel.Controls.Find('Fps30',$true)[0]
    $petUpdate.Invoke($petPanel,@('Connected','Connected','Local','active',$false,60,$true)) | Out-Null
    Test-PetCondition 'connected_state_cannot_start_a_duplicate' (-not $petEnable.Enabled)
    Test-PetCondition 'connected_state_can_restore_the_original' $petRestore.Enabled
    Test-PetCondition 'automatic_binding_is_visible' (-not $petFollow.Enabled)
    $petUpdate.Invoke($petPanel,@('Restored','Restored','Local','paused',$false,60,$true)) | Out-Null
    Test-PetCondition 'restored_state_can_enable_again' $petEnable.Enabled
    Test-PetCondition 'restored_state_does_not_offer_redundant_restore' (-not $petRestore.Enabled)
    $petUpdate.Invoke($petPanel,@('Not visible','Retry restoration','Local','restore-warning',$false,60,$true)) | Out-Null
    Test-PetCondition 'failed_restoration_can_retry_without_starting_live2d' $petRestore.Enabled
    Test-PetCondition 'failed_restoration_can_enable_live2d_again' $petEnable.Enabled
    $petUpdate.Invoke($petPanel,@('Working','Working','Local','waiting',$true,60,$false)) | Out-Null
    Test-PetCondition 'busy_state_prevents_conflicting_operations' (-not $petEnable.Enabled -and -not $petRestore.Enabled -and -not $petFps30.Enabled -and -not $petFollow.Enabled)
    $petUpdate.Invoke($petPanel,@('Error','Error','Local','error',$false,30,$false)) | Out-Null
    Test-PetCondition 'failed_connection_can_retry' $petEnable.Enabled
    Test-PetCondition 'fixed_binding_can_return_to_automatic' $petFollow.Enabled
    Test-PetCondition 'settings_title_bar_hides_icon_but_keeps_application_icon' (-not $petPanel.ShowIcon -and $null -ne $petPanel.Icon -and $petPanel.Icon.Width -ge 16)
    Test-PetCondition 'settings_window_has_the_short_assistant_name' ($petPanel.Text -eq '大肥鱼助手')
    $petUpdate.Invoke($petPanel,@('Watching','Watching','Local','watching',$false,60,$true))|Out-Null
    Test-PetCondition 'closed_codex_can_be_opened_from_the_watcher' ($petEnable.Enabled -and $petEnable.Text -eq '打开 Codex')
    $petUpdate.Invoke($petPanel,@('Normal launch','Normal launch','Local','relaunch-needed',$false,60,$true))|Out-Null
    Test-PetCondition 'normal_launch_explains_and_offers_handoff' ($petEnable.Enabled -and $petEnable.Text -eq '安排接入')
    $petUpdate.Invoke($petPanel,@('Handoff','Handoff','Local','handoff-wait',$false,60,$true))|Out-Null
    Test-PetCondition 'queued_handoff_waits_without_a_futile_retry_or_dialog' (-not $petEnable.Enabled -and $petEnable.Text -eq '等待 Codex 退出' -and $petRestore.Enabled -and $petRestore.Text -eq '停止自动接入' -and $petEnable.AccessibleName -eq $petEnable.Text -and $petRestore.AccessibleName -eq $petRestore.Text)
    $petOptions=$petPanel.GetType().GetMethod('UpdateOptions',[Reflection.BindingFlags]'Instance,NonPublic')
    $petOptions.Invoke($petPanel,@('dark',$true,$true,'press',100,$false))|Out-Null
    Test-PetCondition 'compatibility_can_be_enabled' $petPanel.Controls.Find('DragCompatibility',$true)[0].Checked
    Test-PetCondition 'early_avoidance_can_be_enabled' ($petPanel.Controls.Find('EarlyAvoidance',$true)[0].Checked -and $petPanel.Controls.Find('EarlyAvoidance',$true)[0].Enabled)
    Test-PetCondition 'press_trigger_is_the_recommended_choice' ($petPanel.Controls.Find('AvoidPress',$true)[0].AccessibleDescription -eq '已选择' -and $petPanel.Controls.Find('AvoidHover',$true)[0].AccessibleDescription -eq '未选择' -and $petPanel.Controls.Find('AvoidPress',$true)[0].Enabled)
    $petOptions.Invoke($petPanel,@('dark',$true,$true,'hover',117,$false))|Out-Null
    $petSlider=$petPanel.Controls.Find('PetSizeSlider',$true)[0]
    $petSizeLabel=$petPanel.Controls.Find('PetSizeValue',$true)[0]
    $petResetSize=$petPanel.Controls.Find('ResetPetSize',$true)[0]
    Test-PetCondition 'saved_custom_size_is_visible_and_accessible' ($petSizeLabel.Text -eq '117%' -and $petSlider.AccessibilityObject.Value -eq '117%' -and $petSlider.AccessibilityObject.Role -eq 'Slider')
    $petMore=$petPanel.Controls.Find('DashboardMore',$true)[0]
    $petBody=$petPanel.Controls.Find('DashboardContent',$true)[0]
    $petMinimize=$petPanel.Controls.Find('CaptionMinimize',$true)[0]
    $petClose=$petPanel.Controls.Find('CaptionClose',$true)[0]
    $petHandle=$petPanel.Handle
    $smallHandle=[PetCaptionTest]::SendMessage($petHandle,0x7f,[IntPtr]0,[IntPtr]0)
    $bigHandle=[PetCaptionTest]::SendMessage($petHandle,0x7f,[IntPtr]1,[IntPtr]0)
    $smallFallback=[PetCaptionTest]::SendMessage($petHandle,0x7f,[IntPtr]2,[IntPtr]0)
    Test-PetCondition 'the_custom_caption_supplies_both_icons_to_the_taskbar' ($smallHandle -ne [IntPtr]::Zero -and $bigHandle -ne [IntPtr]::Zero -and $smallFallback -eq $smallHandle)
    Test-PetCondition 'the_menu_and_window_buttons_live_in_one_tall_title_bar' (
        (([PetCaptionTest]::GetWindowLong($petHandle,-16) -band 0x00C00000) -eq 0) -and
        $petMore.Parent -eq $petPanel -and $petMore.Bottom -lt $petBody.Top -and
        $petMinimize.Height -eq $petBody.Top -and $petClose.Right -eq $petPanel.ClientSize.Width -and
        $petBody.Top -eq [Math]::Round(48*$petPanel.DeviceDpi/96)
    )
    $petDragPoint=$petPanel.PointToScreen([Drawing.Point]::new([int]($petPanel.ClientSize.Width*0.68),[int]($petBody.Top/2)))
    $petMenuPoint=$petPanel.PointToScreen([Drawing.Point]::new(($petMore.Left+5),($petMore.Top+5)))
    $petBodyPoint=$petPanel.PointToScreen([Drawing.Point]::new(10,($petBody.Top+5)))
    Test-PetCondition 'windows_hit_test_drags_only_the_title_bar_background' (
        [PetCaptionTest]::Hit($petHandle,$petDragPoint.X,$petDragPoint.Y) -eq 2 -and
        [PetCaptionTest]::Hit($petHandle,$petMenuPoint.X,$petMenuPoint.Y) -eq 1 -and
        [PetCaptionTest]::Hit($petHandle,$petBodyPoint.X,$petBodyPoint.Y) -eq 1
    )
    $script:petPositionWrites=0
    $petPanel.GetType().GetField('PositionSettled',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petPanel,[Action]{$script:petPositionWrites++})
    [PetCaptionTest]::SendMessage($petHandle,0x0232,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null
    Test-PetCondition 'drag_completion_requests_one_position_save' ($script:petPositionWrites -eq 1)
    $petConstrain=$petAssembly.GetType('Program').GetMethod('ConstrainPanelPosition',[Reflection.BindingFlags]'Static,NonPublic')
    $petWork=[Drawing.Rectangle]::new(-1200,0,1200,800)
    $petSize=[Drawing.Size]::new(680,730)
    $petInside=[Drawing.Point]::new(-1000,50)
    $petOutside=[Drawing.Point]::new(2100,-400)
    $petClamped=$petConstrain.Invoke($null,@($petOutside,$petSize,$petWork))
    Test-PetCondition 'saved_position_is_kept_on_its_display_or_clamped_when_it_is_missing' (
        $petConstrain.Invoke($null,@($petInside,$petSize,$petWork)).Equals($petInside) -and $petClamped.X -eq -680 -and $petClamped.Y -eq 0
    )
    $petWindowClick=$petMinimize.GetType().GetMethod('OnClick',[Reflection.BindingFlags]'Instance,NonPublic')
    $petWindowClick.Invoke($petMinimize,@([EventArgs]::Empty))|Out-Null
    Test-PetCondition 'title_bar_minimize_uses_the_windows_window_state' ($petPanel.WindowState -eq 'Minimized')
    $petPanel.WindowState='Normal'
    $script:petChromeCloses=0
    $petPanel.GetType().GetField('CloseBehavior',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petPanel,'tray')
    $petPanel.GetType().GetField('CloseActionRequested',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petPanel,
        [Action[string,bool]]{param($action,$remember);if($action -eq 'tray' -and -not $remember){$script:petChromeCloses++}})
    $petWindowClick.Invoke($petClose,@([EventArgs]::Empty))|Out-Null
    [PetCaptionTest]::SendMessage($petHandle,0x112,[IntPtr]0xf060,[IntPtr]::Zero)|Out-Null
    Test-PetCondition 'title_bar_close_and_system_close_honor_the_saved_preference' ($script:petChromeCloses -eq 2 -and -not $petPanel.IsDisposed)
    $script:petSelectedSize=0
    $petPanel.GetType().GetField('PetSizeRequested',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petPanel,[Action[int]]{param($size);$script:petSelectedSize=$size})
    $petSlider.AccessibilityObject.Value='87%'
    Test-PetCondition 'slider_can_apply_an_arbitrary_integer_size' ($script:petSelectedSize -eq 87 -and $petSizeLabel.Text -eq '87%')
    $petSizeKey=$petSlider.GetType().GetMethod('OnKeyDown',[Reflection.BindingFlags]'Instance,NonPublic')
    $petSizeKey.Invoke($petSlider,@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Right)))|Out-Null
    Test-PetCondition 'keyboard_moves_slider_by_one_without_waiting_for_disk' ($script:petSelectedSize -eq 87 -and $petSizeLabel.Text -eq '88%')
    $petUntil=[DateTime]::UtcNow.AddSeconds(1)
    do{[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 15}while($script:petSelectedSize -ne 88 -and [DateTime]::UtcNow -lt $petUntil)
    Test-PetCondition 'keyboard_draft_is_saved_after_keys_settle' ($script:petSelectedSize -eq 88)
    $petSizeClick=$petResetSize.GetType().GetMethod('OnClick',[Reflection.BindingFlags]'Instance,NonPublic')
    $petSizeClick.Invoke($petResetSize,@([EventArgs]::Empty))|Out-Null
    Test-PetCondition 'size_reset_restores_the_original_size' ($script:petSelectedSize -eq 100 -and $petSizeLabel.Text -eq '100%')
    Test-PetCondition 'hover_trigger_remains_selectable' ($petPanel.Controls.Find('AvoidHover',$true)[0].AccessibleDescription -eq '已选择' -and $petPanel.Controls.Find('AvoidPress',$true)[0].AccessibleDescription -eq '未选择')
    $petOptions.Invoke($petPanel,@('light',$false,$true,'press',100,$false))|Out-Null
    Test-PetCondition 'early_avoidance_requires_compatibility' (-not $petPanel.Controls.Find('EarlyAvoidance',$true)[0].Enabled)
    Test-PetCondition 'trigger_choice_requires_compatibility' (-not $petPanel.Controls.Find('AvoidPress',$true)[0].Enabled -and -not $petPanel.Controls.Find('AvoidHover',$true)[0].Enabled)
    $petOptions.Invoke($petPanel,@('light',$false,$true,'press',100,$true))|Out-Null
    Test-PetCondition 'busy_preferences_prevent_conflicting_changes' (-not $petPanel.Controls.Find('ThemeLight',$true)[0].Enabled -and -not $petPanel.Controls.Find('DragCompatibility',$true)[0].Enabled)
    $petLocale=$petAssembly.GetType('Locale').GetMethod('Select',[Reflection.BindingFlags]'Static,NonPublic')
    $petLocalize=$petPanel.GetType().BaseType.GetMethod('RefreshLanguage',[Reflection.BindingFlags]'Instance,NonPublic')
    $petOptions.Invoke($petPanel,@('light',$true,$false,'press',117,$false))|Out-Null
    try{
        $petLocale.Invoke($null,@('en'))|Out-Null
        $petLocalize.Invoke($petPanel,@())|Out-Null
        $petUpdate.Invoke($petPanel,@('已连接 Codex','大肥鱼正在陪你工作。收起到托盘后，助手会继续运行。','Local','active',$false,60,$true))|Out-Null
        $petOptions.Invoke($petPanel,@('light',$true,$false,'press',117,$false))|Out-Null
        Test-PetCondition 'changing_language_refreshes_static_and_live_text_on_the_same_window' ($petPanel.Text -eq 'Dafeiyu Assistant' -and $petMore.Text -match 'More options' -and $petEnable.Text -eq 'Enabled' -and $petPanel.Handle -eq $petHandle)
        Test-PetCondition 'changing_language_keeps_the_custom_size_readout' ($petSizeLabel.Text -eq '117%')
    } finally { $petLocale.Invoke($null,@('zh-CN'))|Out-Null }
} finally { $petPanel.Dispose() }
$script:petHeaderCalls=''
$petHeader=[Activator]::CreateInstance($petAssembly.GetType('DashboardMenu'),[Reflection.BindingFlags]'Instance,NonPublic',$null,
    @(96,[Action]{$script:petHeaderCalls+='V'},[Action]{$script:petHeaderCalls+='P'},[Action]{$script:petHeaderCalls+='L'},[Action[string]]{param($value);$script:petHeaderCalls+=$value}),$null)
try {
    $petHeader.Items['OptionsVersion'].PerformClick()
    $petHeader.Items['OptionsProject'].PerformClick()
    $petHeader.Items['OptionsLogs'].PerformClick()
    $petHeader.Items['OptionsClose'].DropDownItems['OptionsAsk'].PerformClick()
    Test-PetCondition 'header_menu_keeps_version_project_logs_and_close_preferences' ($script:petHeaderCalls -eq 'VPLask')
    $script:petLanguage=''
    $petHeader.GetType().GetField('LanguageRequested',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petHeader,[Action[string]]{param($value);$script:petLanguage=$value})
    $petLanguageItems=$petHeader.Items['OptionsLanguage'].DropDownItems
    $petLanguageItems['LanguageEnglish'].PerformClick()
    Test-PetCondition 'the_language_menu_exposes_system_chinese_and_english' ($petLanguageItems.Count -eq 3 -and $petLanguageItems['LanguageChinese'].Checked -and $script:petLanguage -eq 'en')
} finally { $petHeader.Dispose() }
$petTray = [Activator]::CreateInstance($petAssembly.GetType('PetTrayMenu'),$true)
try {
    $petTrayUpdate=$petTray.GetType().GetMethod('UpdateState',[Reflection.BindingFlags]'Instance,NonPublic')
    $petTrayEnable=$petTray.Items['Trayplay'];$petTrayRestore=$petTray.Items['Trayrestore'];$petTrayFollow=$petTray.Items['Trayfollow']
    $petTrayRates=$petTray.Items['Trayfps'];$petRate60=$petTrayRates.DropDownItems[1]
    $petTrayUpdate.Invoke($petTray,@('Connected','active',$false,60,$true))|Out-Null
    Test-PetCondition 'tray_connected_state_prevents_duplicate_enable' (-not $petTrayEnable.Enabled -and $petTrayRestore.Enabled)
    Test-PetCondition 'tray_follow_and_fps_show_actual_selections' ($petTrayFollow.Checked -and -not $petTrayFollow.Enabled -and $petRate60.Checked -and -not $petTrayRates.DropDownItems[0].Checked)
    $petTrayUpdate.Invoke($petTray,@('Waiting','handoff-wait',$false,30,$false))|Out-Null
    Test-PetCondition 'tray_handoff_matches_panel_and_can_stop_waiting' (-not $petTrayEnable.Enabled -and $petTrayEnable.Text -eq '等待 Codex 退出' -and $petTrayRestore.Enabled -and $petTrayRestore.Text -eq '停止自动接入')
    Test-PetCondition 'tray_fixed_binding_can_resume_following' ($petTrayFollow.Enabled -and -not $petTrayFollow.Checked)
    $petTrayUpdate.Invoke($petTray,@('Ready','relaunch-needed',$false,30,$false))|Out-Null
    $script:petTrayEnableCalls=0
    $petTray.GetType().GetField('EnableRequested',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petTray,[Action]{$script:petTrayEnableCalls++})
    $petTrayEnable.PerformClick()
    Test-PetCondition 'tray_enable_dispatches_once' ($script:petTrayEnableCalls -eq 1)
    $script:petTrayFps=0
    $petTray.GetType().GetField('FpsRequested',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($petTray,[Action[int]]{param($requested);$script:petTrayFps=$requested})
    $petRate60.PerformClick()
    Test-PetCondition 'tray_fps_submenu_dispatches_selected_rate' ($script:petTrayFps -eq 60)
    $petTrayUpdate.Invoke($petTray,@('Busy','starting',$true,60,$false))|Out-Null
    $petTrayEnable.PerformClick()
    Test-PetCondition 'tray_busy_state_blocks_mutating_commands' (-not $petTrayEnable.Enabled -and -not $petTrayRestore.Enabled -and -not $petTrayFollow.Enabled -and -not $petRate60.Enabled -and -not $petTray.Items['Traylooks'].Enabled -and $script:petTrayEnableCalls -eq 1)
    $petTrayUpdate.Invoke($petTray,@('Restored','paused',$false,60,$true))|Out-Null
    Test-PetCondition 'tray_restored_state_can_enable_without_redundant_restore' ($petTrayEnable.Enabled -and -not $petTrayRestore.Enabled)
    $petPalette=$petAssembly.GetType('Palette');$petConfigure=$petPalette.GetMethod('Configure',[Reflection.BindingFlags]'Static,NonPublic')
    $petRefresh=$petTray.GetType().GetMethod('RefreshTheme',[Reflection.BindingFlags]'Instance,NonPublic')
    foreach($petTheme in @('dark','light')) {
        $petConfigure.Invoke($null,@($petTheme))|Out-Null;$petRefresh.Invoke($petTray,@())|Out-Null
        $petSurface=$petPalette.GetField('Surface',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
        Test-PetCondition ('tray_and_submenu_follow_'+$petTheme+'_theme') ($petTray.BackColor -eq $petSurface -and $petTrayRates.DropDown.BackColor -eq $petSurface)
    }
    $petTray.Show([Drawing.Point]::new(30,30));[Windows.Forms.Application]::DoEvents()
    $petWorkingArea=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    Test-PetCondition 'tray_menu_fits_display_and_uses_rounded_region' ($petTray.Height -le $petWorkingArea.Height -and $petTray.Width -le $petWorkingArea.Width -and $null -ne $petTray.Region)
    foreach($petItem in @($petTray.Items|Where-Object{$_ -is [Windows.Forms.ToolStripMenuItem]})) {
        $petText=[Windows.Forms.TextRenderer]::MeasureText($petItem.Text,$petTray.Font)
        Test-PetCondition ('tray_text_fits_'+$petItem.Name) ($petText.Width -lt $petItem.Width-64*[float]($petAssembly.GetType('Program').GetMethod('SystemDpi',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@())/96))
    }
    $petTray.Close()
} finally { $petTray.Dispose() }
$petChoice = [Activator]::CreateInstance($petAssembly.GetType('CloseChoice'),$true)
try {
    Test-PetCondition 'close_defaults_to_hiding_in_tray' ($petChoice.AcceptButton.DialogResult -eq [Windows.Forms.DialogResult]::Yes)
    $petEscape=$petChoice.GetType().GetMethod('ProcessDialogKey',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($petChoice,@([Windows.Forms.Keys]::Escape))
    Test-PetCondition 'close_escape_cancels_the_choice' ($petEscape -and $petChoice.DialogResult -eq [Windows.Forms.DialogResult]::Cancel)
    Test-PetCondition 'close_only_offers_two_actions_and_remember_is_opt_in' (@($petChoice.Controls | Where-Object {$_ -is [Windows.Forms.Button]}).Count -eq 2 -and -not $petChoice.Controls['RememberCloseChoice'].Checked)
    Test-PetCondition 'quit_requires_explicit_choice' (@($petChoice.Controls | Where-Object { $_ -is [Windows.Forms.Button] -and $_.DialogResult -eq [Windows.Forms.DialogResult]::No }).Count -eq 1)
} finally { $petChoice.Dispose() }
$petAppearance = [Activator]::CreateInstance($petAssembly.GetType('AppearanceDialog'),[Reflection.BindingFlags]'Instance,NonPublic',$null,@($null),$null)
try {
    $petSelect = $petAppearance.GetType().GetMethod('Selection',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($petAppearance,@())
    Test-PetCondition 'appearance_defaults_to_automatic_faces' ($petSelect['expression'] -eq 'auto' -and $petSelect['autoExpressions'])
    Test-PetCondition 'appearance_has_ten_independent_outfit_groups' ($petSelect['accessories'].Count -eq 10)
    Test-PetCondition 'appearance_accessories_start_at_model_defaults' (@($petSelect['accessories'].Values | Where-Object { $_ -ne 'none' }).Count -eq 0)
    Test-PetCondition 'appearance_has_apply_and_reset_controls' ($null -ne $petAppearance.Controls.Find('ApplyAppearance',$true)[0] -and $null -ne $petAppearance.Controls.Find('ResetAppearance',$true)[0])
} finally { $petAppearance.Dispose() }
$petProgram = $petAssembly.GetType('Program')
$petMetric = $petProgram.GetMethod('IconPixels',[Reflection.BindingFlags]'Static,NonPublic')
$petLoadIcon = $petProgram.GetMethod('LoadIconSize',[Reflection.BindingFlags]'Static,NonPublic')
foreach ($petDpi in @(96,120,144,168,192,216,240)) {
    foreach ($petSmall in @($true,$false)) {
        $petSize = $petMetric.Invoke($null,@($petDpi,$petSmall))
        $petIcon = $petLoadIcon.Invoke($null,@($petSize))
        try { Test-PetCondition ('native_icon_' + $petDpi + '_' + $petSmall) ($petIcon.Width -eq $petSize -and $petIcon.Height -eq $petSize) } finally { $petIcon.Dispose() }
    }
}
$petChecks | ConvertTo-Json -Depth 3
