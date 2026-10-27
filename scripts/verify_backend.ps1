param(
    [string]$GoExecutable = "go"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $projectRoot "backend"
$toolRoot = Join-Path $projectRoot ".tools"
$integrationRoot = Join-Path $toolRoot ("integration-" + [guid]::NewGuid().ToString("N"))
$serverPath = Join-Path $integrationRoot "inspection-server.exe"
$dataPath = Join-Path $integrationRoot "issues.json"
$logPath = Join-Path $integrationRoot "server.log"

New-Item -ItemType Directory -Force -Path $integrationRoot | Out-Null

Push-Location $backendRoot
try {
    & $GoExecutable test ./...
    if ($LASTEXITCODE -ne 0) {
        throw "Go tests failed."
    }

    & $GoExecutable build -o $serverPath ./cmd/server
    if ($LASTEXITCODE -ne 0) {
        throw "Go build failed."
    }
}
finally {
    Pop-Location
}

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$baseUrl = "http://127.0.0.1:$port"

function Start-Server {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $serverPath
    $startInfo.WorkingDirectory = $backendRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment["ADDR"] = "127.0.0.1:$port"
    $startInfo.Environment["DATA_FILE"] = $dataPath
    return [System.Diagnostics.Process]::Start($startInfo)
}

function Wait-ForServer {
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 1
            if ($health.status -eq "ok") {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }
    throw "Backend did not become healthy."
}

function Stop-ServerProcess([System.Diagnostics.Process]$process) {
    if (-not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit()
    }
    Add-Content -LiteralPath $logPath -Value $process.StandardOutput.ReadToEnd()
    Add-Content -LiteralPath $logPath -Value $process.StandardError.ReadToEnd()
}

$server = $null
try {
    $server = Start-Server
    Wait-ForServer

    $createBody = @{
        title = "1 号机柜温度异常"
        description = "机柜右侧温度明显偏高"
        priority = "high"
        position = @{ x = 1.25; y = 0.8; z = 2.4 }
    } | ConvertTo-Json -Depth 5

    $created = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUrl/api/issues" `
        -ContentType "application/json; charset=utf-8" `
        -Body $createBody
    if ([string]::IsNullOrWhiteSpace($created.id) -or $created.status -ne "open") {
        throw "Create response did not contain an ID and open status."
    }

    try {
        Invoke-RestMethod `
            -Method Post `
            -Uri "$baseUrl/api/issues" `
            -ContentType "application/json; charset=utf-8" `
            -Body '{"title":"","priority":"high","position":{"x":0,"y":0,"z":0}}'
        throw "Empty title unexpectedly succeeded."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 400) {
            throw
        }
    }

    $updated = Invoke-RestMethod `
        -Method Patch `
        -Uri "$baseUrl/api/issues/$($created.id)/status" `
        -ContentType "application/json; charset=utf-8" `
        -Body '{"status":"in_progress"}'
    if ($updated.status -ne "in_progress") {
        throw "Status update failed."
    }

    $cors = Invoke-WebRequest -Method Options -Uri "$baseUrl/api/issues"
    if ($cors.Headers["Access-Control-Allow-Origin"] -ne "*") {
        throw "CORS header is missing."
    }

    Stop-ServerProcess $server
    $server = Start-Server
    Wait-ForServer

    $list = Invoke-RestMethod -Uri "$baseUrl/api/issues"
    if ($list.issues.Count -ne 1 -or $list.issues[0].status -ne "in_progress") {
        throw "Issue did not survive a backend restart."
    }

    Write-Output "PASS: health, validation, CORS, create, status update and restart persistence"
    Write-Output "Integration artifacts: $integrationRoot"
}
finally {
    if ($null -ne $server) {
        Stop-ServerProcess $server
    }
}
