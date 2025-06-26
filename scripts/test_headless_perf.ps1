# Performance Testing Script for ML-Agents Training
# Systematically tests different num-envs and timescale combinations

param(
    [string]$configPath = "results/CommanderCurriculum_v2/configuration.yaml",
    [string]$runId = "PerfTest",
    [int]$maxEnvs = 16,
    [int]$maxTimeScale = 30,
    [int]$testDurationMinutes = 5
)

Write-Host "Starting ML-Agents Performance Testing..."
Write-Host "Max Environments: $maxEnvs"
Write-Host "Max TimeScale: $maxTimeScale"
Write-Host "Test Duration: $testDurationMinutes minutes each"

# Test configurations to try
$testConfigs = @(
    @{envs=1; timescale=20},
    @{envs=4; timescale=25},
    @{envs=8; timescale=25},
    @{envs=12; timescale=25},
    @{envs=16; timescale=25},
    @{envs=8; timescale=30},
    @{envs=8; timescale=35}
)

$results = @()

foreach ($config in $testConfigs) {
    $envCount = $config.envs
    $timeScale = $config.timescale
    $testRunId = "${runId}_E${envCount}_T${timeScale}"
    
    Write-Host "`n=== Testing $envCount environments at ${timeScale}x timescale ===" -ForegroundColor Cyan
    
    # Backup original config
    $originalConfig = Get-Content $configPath
    
    # Update config file
    $updatedConfig = $originalConfig -replace "num_envs: \d+", "num_envs: $envCount"
    $updatedConfig = $updatedConfig -replace "time_scale: \d+", "time_scale: $timeScale"
    $updatedConfig | Set-Content $configPath
    
    # Start performance monitoring
    $perfJob = Start-Job -ScriptBlock {
        param($duration)
        $endTime = (Get-Date).AddMinutes($duration)
        $samples = @()
        
        while ((Get-Date) -lt $endTime) {
            $cpu = (Get-Counter "\Processor(_Total)\% Processor Time").CounterSamples.CookedValue
            $memory = (Get-Counter "\Memory\Available MBytes").CounterSamples.CookedValue
            $samples += @{
                Time = Get-Date
                CPU = [math]::Round($cpu, 2)
                AvailableMemoryMB = [math]::Round($memory, 2)
            }
            Start-Sleep 10
        }
        return $samples
    } -ArgumentList $testDurationMinutes
    
    # Start training
    $trainingStart = Get-Date
    Write-Host "Starting training process..." -ForegroundColor Yellow
    
    $trainingProcess = Start-Process -FilePath "mlagents-learn" -ArgumentList @(
        $configPath,
        "--run-id=$testRunId",
        "--no-graphics",
        "--time-scale=$timeScale"
    ) -PassThru -NoNewWindow
    
    # Wait for test duration
    Start-Sleep ($testDurationMinutes * 60)
    
    # Stop training
    if (!$trainingProcess.HasExited) {
        $trainingProcess.Kill()
        Write-Host "Training process terminated." -ForegroundColor Red
    }
    
    # Get performance results
    $perfData = Receive-Job $perfJob
    Remove-Job $perfJob
    
    # Calculate averages
    $avgCPU = ($perfData | Measure-Object -Property CPU -Average).Average
    $minMemory = ($perfData | Measure-Object -Property AvailableMemoryMB -Minimum).Minimum
    $maxMemoryUsed = 16384 - $minMemory  # Assuming 16GB total RAM
    
    $result = @{
        Environments = $envCount
        TimeScale = $timeScale
        AvgCPUPercent = [math]::Round($avgCPU, 2)
        MaxMemoryUsedMB = [math]::Round($maxMemoryUsed, 2)
        Stable = $avgCPU -lt 90 -and $minMemory -gt 1000  # CPU < 90% and >1GB free RAM
        TestDuration = $testDurationMinutes
    }
    
    $results += $result
    
    Write-Host "Results: CPU: $($result.AvgCPUPercent)%, Memory Used: $($result.MaxMemoryUsedMB)MB, Stable: $($result.Stable)" -ForegroundColor Green
    
    # Restore original config
    $originalConfig | Set-Content $configPath
}

# Display final results
Write-Host "`n=== PERFORMANCE TEST RESULTS ===" -ForegroundColor Magenta
Write-Host "Environments | TimeScale | Avg CPU% | Max Memory MB | Stable?" -ForegroundColor White
Write-Host "-------------|-----------|----------|---------------|--------" -ForegroundColor White

foreach ($result in $results) {
    $status = if ($result.Stable) { "✓" } else { "✗" }
    Write-Host ("{0,11} | {1,9} | {2,8} | {3,13} | {4,6}" -f 
        $result.Environments, 
        $result.TimeScale, 
        $result.AvgCPUPercent, 
        $result.MaxMemoryUsedMB, 
        $status)
}

# Find optimal configuration
$stableConfigs = $results | Where-Object { $_.Stable }
if ($stableConfigs) {
    $optimal = $stableConfigs | Sort-Object { $_.Environments * $_.TimeScale } -Descending | Select-Object -First 1
    Write-Host "`nOPTIMAL CONFIGURATION:" -ForegroundColor Green
    Write-Host "  Environments: $($optimal.Environments)" -ForegroundColor Green
    Write-Host "  TimeScale: $($optimal.TimeScale)" -ForegroundColor Green
    Write-Host "  Expected training speedup: $($optimal.Environments * $optimal.TimeScale / 20)x faster than your current setup" -ForegroundColor Green
}

Write-Host "`nTest completed. Check results/PerfTest_* folders for detailed logs." -ForegroundColor Cyan 