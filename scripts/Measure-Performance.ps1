param([Parameter(Mandatory=$true)][int]$ProcessId, [int]$Seconds=30)
$ErrorActionPreference='Stop'
$sampleProcess=Get-Process -Id $ProcessId
$startCpu=$sampleProcess.TotalProcessorTime.TotalSeconds
$clock=[Diagnostics.Stopwatch]::StartNew()
$peak=0L
for($i=0;$i -lt $Seconds;$i++) {
 Start-Sleep -Seconds 1
 $sampleProcess.Refresh()
 if($sampleProcess.HasExited){throw 'Process exited during profiling.'}
 $peak=[Math]::Max($peak,$sampleProcess.WorkingSet64)
}
$clock.Stop()
$cpu=100*($sampleProcess.TotalProcessorTime.TotalSeconds-$startCpu)/$clock.Elapsed.TotalSeconds/[Environment]::ProcessorCount
[pscustomobject]@{CpuPercentOfMachine=[Math]::Round($cpu,2);PeakWorkingSetMB=[Math]::Round($peak/1MB,1);CpuTargetMet=($cpu -lt 2);MemoryTargetMet=($peak -lt 200MB);Seconds=$Seconds}
