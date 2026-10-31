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
    Test-PetCondition 'window_has_a_real_application_icon' ($null -ne $petPanel.Icon -and $petPanel.Icon.Width -ge 16)
    $petOptions=$petPanel.GetType().GetMethod('UpdateOptions',[Reflection.BindingFlags]'Instance,NonPublic')
    $petOptions.Invoke($petPanel,@('dark',$true,$true,'press',$false))|Out-Null
    Test-PetCondition 'compatibility_can_be_enabled' $petPanel.Controls.Find('DragCompatibility',$true)[0].Checked
    Test-PetCondition 'early_avoidance_can_be_enabled' ($petPanel.Controls.Find('EarlyAvoidance',$true)[0].Checked -and $petPanel.Controls.Find('EarlyAvoidance',$true)[0].Enabled)
    Test-PetCondition 'press_trigger_is_the_recommended_choice' ($petPanel.Controls.Find('AvoidanceTrigger',$true)[0].SelectedIndex -eq 0 -and $petPanel.Controls.Find('AvoidanceTrigger',$true)[0].Enabled)
    $petOptions.Invoke($petPanel,@('dark',$true,$true,'hover',$false))|Out-Null
    Test-PetCondition 'hover_trigger_remains_selectable' ($petPanel.Controls.Find('AvoidanceTrigger',$true)[0].SelectedIndex -eq 1)
    $petOptions.Invoke($petPanel,@('light',$false,$true,'press',$false))|Out-Null
    Test-PetCondition 'early_avoidance_requires_compatibility' (-not $petPanel.Controls.Find('EarlyAvoidance',$true)[0].Enabled)
    Test-PetCondition 'trigger_choice_requires_compatibility' (-not $petPanel.Controls.Find('AvoidanceTrigger',$true)[0].Enabled)
    $petOptions.Invoke($petPanel,@('light',$false,$true,'press',$true))|Out-Null
    Test-PetCondition 'busy_preferences_prevent_conflicting_changes' (-not $petPanel.Controls.Find('ThemeLight',$true)[0].Enabled -and -not $petPanel.Controls.Find('DragCompatibility',$true)[0].Enabled)
} finally { $petPanel.Dispose() }
$petChoice = [Activator]::CreateInstance($petAssembly.GetType('CloseChoice'),$true)
try {
    Test-PetCondition 'close_defaults_to_hiding_in_tray' ($petChoice.AcceptButton.DialogResult -eq [Windows.Forms.DialogResult]::Yes)
    Test-PetCondition 'close_escape_cancels_the_choice' ($petChoice.CancelButton.DialogResult -eq [Windows.Forms.DialogResult]::Cancel)
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
