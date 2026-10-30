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
    $petUpdate.Invoke($petPanel,@('Working','Working','Local','waiting',$true,60,$false)) | Out-Null
    Test-PetCondition 'busy_state_prevents_conflicting_operations' (-not $petEnable.Enabled -and -not $petRestore.Enabled -and -not $petFps30.Enabled -and -not $petFollow.Enabled)
    $petUpdate.Invoke($petPanel,@('Error','Error','Local','error',$false,30,$false)) | Out-Null
    Test-PetCondition 'failed_connection_can_retry' $petEnable.Enabled
    Test-PetCondition 'fixed_binding_can_return_to_automatic' $petFollow.Enabled
    Test-PetCondition 'window_has_a_real_application_icon' ($null -ne $petPanel.Icon -and $petPanel.Icon.Width -ge 16)
} finally { $petPanel.Dispose() }
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
