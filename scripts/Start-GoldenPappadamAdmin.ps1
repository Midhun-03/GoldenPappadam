<#
  Starts the Golden Pappadam admin panel for local use and opens it in the browser.

  The admin panel is two processes, the same two CLAUDE.md section 11 starts by hand:
    1. the API      - dotnet run --project src/GoldenPappadam.Api --launch-profile http   (port 5207)
    2. the client   - npm run dev --prefix client, i.e. vite                             (port 5173,
                      which proxies /api to 5207 so the session cookie stays same-origin)

  Each runs in its own minimized window, so closing the browser or this window stops nothing.
  A service that is already running is reused, but only after checking that the process on the
  port really is this application. Nothing here changes configuration, ports or data.

  To stop: open each "Golden Pappadam ..." window from the taskbar and press Ctrl+C.
#>
param(
    # Start and check everything, but do not open a browser.
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ApiProject = Join-Path $RepoRoot 'src\GoldenPappadam.Api\GoldenPappadam.Api.csproj'
$ClientDir  = Join-Path $RepoRoot 'client'

$ApiPort    = 5207    # launchSettings.json, "http" profile
$ClientPort = 5173    # client/vite.config.ts, server.port
$ApiUrl     = "http://localhost:$ApiPort"
$AdminUrl   = "http://localhost:$ClientPort/"

$ApiTimeoutSeconds    = 240   # the first run after a change also builds the solution
$ClientTimeoutSeconds = 90

function Write-Step([string]$Message) { Write-Host "  $Message" }
function Write-Ok([string]$Message)   { Write-Host "  OK   $Message" -ForegroundColor Green }
function Write-Wait([string]$Message) { Write-Host "  ...  $Message" -ForegroundColor Yellow }

function Stop-WithError([string]$Message) {
    Write-Host ''
    Write-Host "  PROBLEM: $Message" -ForegroundColor Red
    Write-Host ''
    Read-Host '  Press Enter to close this window'
    exit 1
}

# An HTTP answer of any status, or $null when nothing answered.
function Get-Http([string]$Url) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5 -MaximumRedirection 0
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = [string]$response.Content }
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) {
            return [pscustomobject]@{ Status = [int]$_.Exception.Response.StatusCode; Body = '' }
        }
        return $null
    }
    catch {
        return $null
    }
}

# The process listening on a port, or $null when the port is free.
function Get-PortOwner([int]$Port) {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $listener) { return $null }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)" -ErrorAction SilentlyContinue
    if ($process) { return $process }

    return [pscustomobject]@{ ProcessId = $listener.OwningProcess; Name = 'unknown'; CommandLine = '' }
}

# This repository's API: the apphost Visual Studio and "dotnet run" both start, or dotnet itself.
function Test-IsOurApi($Process) {
    $Process -and $Process.Name -in @('GoldenPappadam.Api.exe', 'dotnet.exe') -and
        [string]$Process.CommandLine -match 'GoldenPappadam\.Api'
}

# This repository's Vite dev server: node running vite from this client folder.
function Test-IsOurClient($Process) {
    $Process -and $Process.Name -eq 'node.exe' -and
        [string]$Process.CommandLine -match [regex]::Escape($ClientDir) -and
        [string]$Process.CommandLine -match 'vite'
}

function Get-OurProcesses([scriptblock]$IsOurs) {
    @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe' OR Name = 'GoldenPappadam.Api.exe' OR Name = 'node.exe'" -ErrorAction SilentlyContinue |
        Where-Object { & $IsOurs $_ })
}

# The API is ready when its OpenAPI document answers and names this application.
function Test-ApiReady {
    $answer = Get-Http "$ApiUrl/openapi/v1.json"
    $answer -and $answer.Status -eq 200 -and $answer.Body -match 'GoldenPappadam'
}

# The client is ready when Vite serves the Golden Pappadam page.
function Test-ClientReady {
    $answer = Get-Http $AdminUrl
    $answer -and $answer.Status -eq 200 -and $answer.Body -match '<title>Golden Pappadam</title>'
}

function Start-ServiceWindow([string]$Title, [string]$WorkingDirectory, [string]$Command) {
    # -NoExit keeps the window open if the service stops, so its error stays readable.
    # Passed encoded, because Windows PowerShell 5.1 does not quote -ArgumentList items.
    $script = "`$Host.UI.RawUI.WindowTitle = '$Title  (Ctrl+C to stop)'; $Command"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    Start-Process -FilePath 'powershell.exe' `
        -ArgumentList "-NoExit -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded" `
        -WorkingDirectory $WorkingDirectory -WindowStyle Minimized | Out-Null
}

function Wait-Until([scriptblock]$IsReady, [scriptblock]$IsOurs, [int]$TimeoutSeconds, [string]$Name, [string]$WindowTitle) {
    $started = Get-Date
    while (((Get-Date) - $started).TotalSeconds -lt $TimeoutSeconds) {
        if (& $IsReady) { return }

        # Give it time to appear; after that, a vanished process means it stopped with an error.
        if (((Get-Date) - $started).TotalSeconds -gt 15 -and (Get-OurProcesses $IsOurs).Count -eq 0) {
            Stop-WithError "The $Name stopped while starting. Open the '$WindowTitle' window from the taskbar to read the error."
        }

        Start-Sleep -Seconds 2
    }

    Stop-WithError "The $Name did not become ready within $TimeoutSeconds seconds. Open the '$WindowTitle' window from the taskbar to see what it is doing."
}

# Reuses a running service, or starts one, then waits for it to answer.
function Ensure-Service {
    param(
        [string]$Name, [int]$Port, [string]$WindowTitle, [string]$WorkingDirectory, [string]$Command,
        [scriptblock]$IsOurs, [scriptblock]$IsReady, [int]$TimeoutSeconds, [bool]$MayStart
    )

    $owner = Get-PortOwner $Port

    if ($owner) {
        # The command line is unreadable for a process running as administrator when this launcher is
        # not, so a service that answers like this application is recognised by its answer too.
        $answersLikeUs = $false
        if (-not (& $IsOurs $owner)) {
            for ($i = 0; $i -lt 5 -and -not $answersLikeUs; $i++) {
                $answersLikeUs = [bool](& $IsReady)
                if (-not $answersLikeUs) { Start-Sleep -Seconds 2 }
            }
        }

        if (-not (& $IsOurs $owner) -and -not $answersLikeUs) {
            Stop-WithError ("Port $Port is in use by another program ($($owner.Name), process $($owner.ProcessId)), " +
                "not Golden Pappadam. Nothing was started. Close that program, then try again.")
        }

        Write-Ok "$Name is already running on port $Port - reusing it."
    }
    elseif ((Get-OurProcesses $IsOurs).Count -gt 0) {
        Write-Wait "$Name is already starting - waiting for it."
    }
    elseif ($MayStart) {
        Write-Wait "Starting the $Name (window '$WindowTitle', minimized)..."
        Start-ServiceWindow $WindowTitle $WorkingDirectory $Command
    }
    else {
        Write-Wait "Another launcher is starting the $Name - waiting for it."
    }

    Wait-Until $IsReady $IsOurs $TimeoutSeconds $Name $WindowTitle
    Write-Ok "$Name is ready."
}

# ---------- start ----------

$Host.UI.RawUI.WindowTitle = 'Golden Pappadam Admin - starting'
Write-Host ''
Write-Host '  Golden Pappadam Admin' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $ApiProject))                       { Stop-WithError "The API project is missing: $ApiProject" }
if (-not (Test-Path (Join-Path $ClientDir 'package.json'))) { Stop-WithError "The admin client is missing: $ClientDir" }
if (-not (Test-Path (Join-Path $ClientDir 'node_modules'))) {
    Stop-WithError "The admin client's packages are not installed. Run once in a terminal: npm install --prefix client"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue))  { Stop-WithError 'The .NET SDK (dotnet) was not found.' }
if (-not (Get-Command node -ErrorAction SilentlyContinue))    { Stop-WithError 'Node.js (node) was not found.' }
if (-not (Test-Path (Join-Path $ClientDir 'node_modules\vite\bin\vite.js'))) {
    Stop-WithError "Vite is missing from the admin client's packages. Run once in a terminal: npm install --prefix client"
}

$sql = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
if (-not $sql) {
    Stop-WithError 'SQL Server Express (MSSQL$SQLEXPRESS) is not installed on this computer.'
}
if ($sql.Status -ne 'Running') {
    Stop-WithError ("SQL Server Express is not running (status: $($sql.Status)). Start 'SQL Server (SQLEXPRESS)' " +
        "from the Services app, then try again.")
}
Write-Ok 'SQL Server Express is running.'

# Two quick double-clicks must not start everything twice: only one launcher starts services,
# any other one just waits for them.
$mutex = New-Object System.Threading.Mutex($false, 'Local\GoldenPappadamAdminLauncher')
$mayStart = $false
try { $mayStart = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $mayStart = $true }

try {
    Ensure-Service -Name 'API' -Port $ApiPort -WindowTitle 'Golden Pappadam API' `
        -WorkingDirectory $RepoRoot `
        -Command 'dotnet run --project src/GoldenPappadam.Api --launch-profile http' `
        -IsOurs ${function:Test-IsOurApi} -IsReady ${function:Test-ApiReady} `
        -TimeoutSeconds $ApiTimeoutSeconds -MayStart $mayStart

    # The same program "npm run dev" runs (package.json: "dev": "vite"), started with node directly so
    # npm and cmd do not rename the window. --strictPort: if 5173 is taken, fail instead of quietly
    # moving to another port.
    Ensure-Service -Name 'admin client' -Port $ClientPort -WindowTitle 'Golden Pappadam Admin client' `
        -WorkingDirectory $ClientDir `
        -Command "node '$ClientDir\node_modules\vite\bin\vite.js' --strictPort" `
        -IsOurs ${function:Test-IsOurClient} -IsReady ${function:Test-ClientReady} `
        -TimeoutSeconds $ClientTimeoutSeconds -MayStart $mayStart
}
finally {
    if ($mayStart) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

# The client reaches the API through its proxy: an answer from the API (401 when signed out)
# proves the whole path works, not just the two halves.
$proxied = Get-Http "http://localhost:$ClientPort/api/auth/me"
if (-not $proxied -or $proxied.Status -notin @(200, 401)) {
    $status = if ($proxied) { $proxied.Status } else { 'no answer' }
    Stop-WithError "The admin client cannot reach the API through its proxy ($status). Check both service windows."
}
Write-Ok 'The admin client reaches the API.'

if (-not $NoBrowser) {
    $chrome = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if ($chrome) {
        Start-Process -FilePath $chrome -ArgumentList $AdminUrl
    } else {
        Start-Process $AdminUrl
    }
    Write-Ok "Opened $AdminUrl"
}

Write-Host ''
Write-Host '  Golden Pappadam Admin is running. Closing the browser or this window stops nothing.' -ForegroundColor Cyan
Write-Host "  To stop: open the 'Golden Pappadam API' and 'Golden Pappadam Admin client' windows from"
Write-Host '  the taskbar and press Ctrl+C in each.'
Write-Host ''
Write-Host '  This window closes by itself in 10 seconds.'
Start-Sleep -Seconds 10
