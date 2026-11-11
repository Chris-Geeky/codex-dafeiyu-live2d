[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()
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
    Test-PetCondition 'header_has_a_menu_button' ($petPanel.Controls.Find('DashboardMore',$true)[0].AccessibleName -eq '更多选项')
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
