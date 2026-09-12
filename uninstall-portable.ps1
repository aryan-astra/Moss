[CmdletBinding(SupportsShouldProcess=$true)]
param([switch]$DeleteLocalData)
$ErrorActionPreference='Stop'
Write-Host 'Exit Moss using its tray menu before removing the application folder.'
if ($PSCmdlet.ShouldProcess('Moss startup entry','Remove')) {
    Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Moss -ErrorAction SilentlyContinue
}
$sid=[System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$tasks=Get-ScheduledTask -TaskName "Moss-$sid-*" -ErrorAction SilentlyContinue
foreach ($task in $tasks) {
    if ($PSCmdlet.ShouldProcess($task.TaskName,'Remove Moss reminder task')) {
        Unregister-ScheduledTask -TaskName $task.TaskName -Confirm:$false
    }
}
if ($DeleteLocalData -and $PSCmdlet.ShouldProcess("$env:LOCALAPPDATA\Moss",'Delete ALL notes, reminder records, settings, imported characters and logs')) {
    Remove-Item "$env:LOCALAPPDATA\Moss" -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'You may now delete the folder containing Moss.exe. Local notes are retained unless -DeleteLocalData was explicitly supplied.'
