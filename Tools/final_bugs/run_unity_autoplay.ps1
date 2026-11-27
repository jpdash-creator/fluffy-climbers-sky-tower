param([int[]]$Regions = @(1, 2, 3, 4, 5), [int]$RegionTimeoutMin = 90, [string]$Out = "E:/Unity/shots/final_bugs2", [switch]$Fresh)
# Overnight screen test: one Unity run per region (FinalBugsAutoplayTests.RegionN). A hung or crashed Unity is killed and the region is started again;
# the test skips finished runs and records the one that was in progress as hang_or_crash. Needs graphics (no -nographics) and no other Unity on the project.
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$unity = "E:\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
$logs = "E:\Unity\logs"
New-Item -ItemType Directory -Force $logs, $Out | Out-Null
$env:FB2_OUT = $Out
foreach ($r in $Regions) {
    if ($Fresh) { Remove-Item "$Out/report_region$r.csv", "$Out/inprogress_region$r.txt", "$Out/layout_region$r.txt", "$Out/errors_region$r.txt" -ErrorAction SilentlyContinue }
    Remove-Item "$Out/region$r.done" -ErrorAction SilentlyContinue
    for ($try = 1; $try -le 4 -and -not (Test-Path "$Out/region$r.done"); $try++) {
        $log = "$logs\final_bugs2_r$($r)_$try.log"
        $a = @('-batchmode', '-projectPath', $root, '-runTests', '-testPlatform', 'PlayMode', '-testFilter', "FluffyClimbers.Tests.FinalBugsAutoplayTests.Region$r",
               '-testResults', "$logs\final_bugs2_r$r.xml", '-logFile', $log)
        "region $r try $try $(Get-Date -Format s)"
        $p = Start-Process -FilePath $unity -ArgumentList $a -PassThru
        if (-not $p.WaitForExit($RegionTimeoutMin * 60 * 1000)) { $p.Kill(); $p.WaitForExit(); "  TIMEOUT, killed" } else { "  exit " + $p.ExitCode }
        Select-String -Path $log -Pattern 'error CS' | Select-Object -First 10 | ForEach-Object { $_.Line }
        # no report file at all after a run: compile error, license or a filter that matched nothing; retrying cannot help
        if (-not (Test-Path "$Out/report_region$r.csv")) { "  no report written, see $log"; break }
    }
    if (Test-Path "$Out/report_region$r.csv") {
        $rows = @(Import-Csv "$Out/report_region$r.csv")
        "  region $r rows $($rows.Count), done=$(Test-Path "$Out/region$r.done")"
        $rows | Group-Object result | ForEach-Object { "    $($_.Name): $($_.Count)" }
    }
}
