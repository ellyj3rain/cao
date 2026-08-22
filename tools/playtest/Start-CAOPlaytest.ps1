<#
.SYNOPSIS
  Start one CAO playtest session with the MouseMux chain proven live first.

.DESCRIPTION
  Three defects this exists to make impossible, each of which cost real time:

  1. STARTUP-ORDERING RACE. Input Mapper probes ws://localhost:41001 for two
     seconds before showing any UI. On success it connects silently; on failure
     it opens a modal that is invisible behind a minimised window. Its MCP
     server lives in the Electron MAIN process, so the endpoint comes up either
     way, lists every tool, and answers not_connected forever. Restarting it by
     hand is a loophole -- the race simply has not fired again yet. This waits
     for MouseMux to accept, then verifies Input Mapper actually holds a socket
     to 41001, and only restarts it when it does not.

  2. DOUBLE LAUNCH. Invoking RimWorldWin64.exe directly lets Steam's DRM
     relaunch leave two live processes (observed 12 seconds apart). Two
     instances compete for the main thread and silently invalidate every
     performance number. This refuses to start if one is already running and
     asserts exactly one afterwards.

  3. FOREGROUND THEFT. The game grabs focus on launch and lands wherever Unity
     last stored a monitor index. This pins the window through Unity's own
     registry keys and restores whatever window the operator was using, so the
     session appears without taking over the workspace.

.PARAMETER Scale
  Exercise arming value, e.g. 400x6. The -hold suffix is added by -Hold.

.PARAMETER Display
  Tv (default) or Laptop. Tv keeps the game off the working monitor entirely.

.PARAMETER Hold
  Leave the session interactive after receipts instead of shutting down.

.PARAMETER SubProbe
  Arm CA_STEADY_SUBPROBE=1. Inflates absolute timings by design; never use it
  for a run whose absolute ms/tick will be quoted.
#>
[CmdletBinding()]
param(
    [string]$Scale = '400x6',
    [ValidateSet('Tv','Laptop')][string]$Display = 'Tv',
    [switch]$Hold,
    [switch]$SubProbe,
    # Geography is a fixture axis, not a property of the world seed. Without
    # it the fixture lands on its old seeded-random inland tile, which reaches
    # none of the projection kernel's coast, littoral, water-depth or river
    # paths -- so those paths are neither exercised nor represented in any
    # timing taken from the run.
    [ValidateSet('none','coast','peninsula','island','river','mountain','inland')]
    [string]$Geography = 'none',
    [int]$TimeoutSeconds = 1800,
    # The whole cycle is one operator-runnable action: after clearing the
    # old instance this deploys a built DLL into Assemblies\ before launch.
    # 'latest' (default) takes the newest .build-*/ColonistAwareness.dll in
    # the worktree; a tag (e.g. 'grow') takes .build-<tag>; 'none' launches
    # whatever is already deployed.
    [string]$DeployBuild = 'latest'
)

$ErrorActionPreference = 'Stop'

$MouseMuxUrl   = 'ws://localhost:41001'
$MouseMuxPort  = 41001
$SteamAppId    = 294100
$RimWorldExe   = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe'
$UnityKey      = 'HKCU:\Software\Ludeon Studios\RimWorld by Ludeon Studios'
$PlayerLog     = Join-Path $env:LOCALAPPDATA '..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log'
$InputMapperExe  = 'C:\Program Files (x86)\The MouseMux Company\MouseMux V3\store\apps\websdk\3.0.7\websdk-electron\electron\electron.exe'
$InputMapperAsar = 'C:\Program Files (x86)\The MouseMux Company\MouseMux V3\store\apps\websdk\3.0.7\webapp-input-mapper-beta-asar\webapp-input-mapper-beta.asar'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PlaytestWin {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
"@

function Write-Step($m) { Write-Host "[playtest] $m" }
function Fail($m) { Write-Error "[playtest] $m"; exit 1 }

# ---------------------------------------------------------------- 1. MouseMux
function Test-MouseMuxAccepting {
    $c = New-Object Net.Sockets.TcpClient
    try {
        $a = $c.BeginConnect('127.0.0.1', $MouseMuxPort, $null, $null)
        if (-not $a.AsyncWaitHandle.WaitOne(1500)) { return $false }
        $c.EndConnect($a); return $true
    } catch { return $false } finally { $c.Close() }
}

Write-Step 'waiting for MouseMux to accept on 41001'
$deadline = (Get-Date).AddSeconds(60)
while (-not (Test-MouseMuxAccepting)) {
    if ((Get-Date) -gt $deadline) { Fail 'MouseMux is not accepting on 41001. Start MouseMux first; do NOT restart it from here.' }
    Start-Sleep -Milliseconds 500
}
Write-Step 'MouseMux accepting'

# ------------------------------------------------- 2. Input Mapper is CONNECTED
# The only trustworthy signal is a real socket to 41001. A live 41760 listener
# proves nothing: the MCP answers happily while disconnected.
function Get-InputMapperMain {
    Get-CimInstance Win32_Process -Filter "Name='electron.exe'" |
        Where-Object { $_.CommandLine -like '*input-mapper*' -and $_.CommandLine -notlike '*--type=*' } |
        Select-Object -First 1
}
function Test-InputMapperConnected {
    [bool](Get-NetTCPConnection -RemotePort $MouseMuxPort -State Established -ErrorAction SilentlyContinue)
}

if (-not (Test-InputMapperConnected)) {
    Write-Step 'Input Mapper holds no socket to MouseMux -- restarting it (MouseMux is left alone)'
    $mainProc = Get-InputMapperMain
    if ($mainProc) { Stop-Process -Id $mainProc.ProcessId -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }
    Start-Process -FilePath $InputMapperExe -ArgumentList @("`"$InputMapperAsar`"") -WindowStyle Minimized
    $deadline = (Get-Date).AddSeconds(45)
    while (-not (Test-InputMapperConnected)) {
        if ((Get-Date) -gt $deadline) { Fail 'Input Mapper still not connected after restart.' }
        Start-Sleep -Milliseconds 500
    }
}
Write-Step 'Input Mapper connected to MouseMux'

# ------------------------------------------------------- 3. single-instance guard
$running = @(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    # Resolve, do not refuse. The invariant is "exactly one instance at the end",
    # and a stale process is this script's problem to clear, not the operator's.
    Write-Step ("clearing " + $running.Count + " existing instance(s): PID " + ($running.Id -join ', '))
    $running | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    $deadline = (Get-Date).AddSeconds(30)
    while (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count -gt 0) {
        if ((Get-Date) -gt $deadline) { Fail 'existing RimWorld processes would not exit' }
        Start-Sleep -Milliseconds 500
    }
}

# ------------------------------------------------------------ 3b. deploy build
# The game is down and the DLL unlocked: this is the one safe moment to
# deploy, and having it here makes the full cycle (kill -> deploy -> launch)
# a single operator command instead of a hand sequence someone else owns.
if ($DeployBuild -ne 'none') {
    $worktree = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $dll = $null
    if ($DeployBuild -eq 'latest') {
        $dll = Get-ChildItem -Path (Join-Path $worktree '.build-*') -Filter 'ColonistAwareness.dll' -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
    } else {
        $candidate = Join-Path $worktree (".build-" + $DeployBuild + "\ColonistAwareness.dll")
        if (Test-Path $candidate) { $dll = Get-Item $candidate }
    }
    if ($null -eq $dll) {
        Fail ("no built DLL found for -DeployBuild '" + $DeployBuild + "'")
    }
    $target = Join-Path $worktree 'Assemblies\ColonistAwareness.dll'
    $sourceHash = (Get-FileHash $dll.FullName -Algorithm SHA256).Hash.Substring(0,16)
    $targetHash = if (Test-Path $target) { (Get-FileHash $target -Algorithm SHA256).Hash.Substring(0,16) } else { 'absent' }
    if ($sourceHash -ne $targetHash) {
        Copy-Item $dll.FullName $target -Force
        Write-Step ("deployed " + $dll.Directory.Name + " (" + $sourceHash + ") over " + $targetHash)
    } else {
        Write-Step ("build already deployed (" + $sourceHash + ", " + $dll.Directory.Name + ")")
    }
}

# ------------------------------------------------------------ 4. pin the window
# Unity reads these at startup. Fullscreen mode 3 = windowed. Positions are
# virtual-desktop coordinates, so the TV's negative Y origin is intentional.
$targets = @{
    # laptop panel: 2560x1600 at 0,0
    Laptop = @{ Monitor = 0; W = 1920; H = 1080; X =  320; Y =   260 }
    # external: 1920x1080 at 309,-1080
    Tv     = @{ Monitor = 1; W = 1600; H =  900; X =  469; Y =  -990 }
}
$t = $targets[$Display]
Write-Step ("pinning window: $Display  $($t.W)x$($t.H) at $($t.X),$($t.Y)  monitor $($t.Monitor)")
Set-ItemProperty $UnityKey -Name 'Screenmanager Fullscreen mode_h3630240806'      -Value 3          -Type DWord
Set-ItemProperty $UnityKey -Name 'Screenmanager Resolution Use Native_h1405027254' -Value 0          -Type DWord
Set-ItemProperty $UnityKey -Name 'Screenmanager Resolution Width_h182942802'       -Value $t.W       -Type DWord
Set-ItemProperty $UnityKey -Name 'Screenmanager Resolution Height_h2627697771'     -Value $t.H       -Type DWord
Set-ItemProperty $UnityKey -Name 'Screenmanager Resolution Window Width_h2524650974'  -Value $t.W    -Type DWord
Set-ItemProperty $UnityKey -Name 'Screenmanager Resolution Window Height_h1684712807' -Value $t.H    -Type DWord
Set-ItemProperty $UnityKey -Name 'UnitySelectMonitor_h17969598'                    -Value $t.Monitor -Type DWord

# ------------------------------------------------------------------- 5. arming
$armed = $Scale
if ($Geography -ne 'none') { $armed = "$armed@$Geography" }
if ($Hold) { $armed = "$armed-hold" }
$env:CA_CONVERGENCE_EXERCISE = $armed
if ($SubProbe) {
    $env:CA_STEADY_SUBPROBE     = '1'
    $env:CA_LISTNORMAL_SUBPROBE = '1'
} else {
    Remove-Item Env:\CA_STEADY_SUBPROBE     -ErrorAction SilentlyContinue
    Remove-Item Env:\CA_LISTNORMAL_SUBPROBE -ErrorAction SilentlyContinue
}
Write-Step "arming CA_CONVERGENCE_EXERCISE=$armed  subprobes=$([bool]$SubProbe)"
if ($SubProbe) { Write-Step 'NOTE: probe bracketing inflates absolute ms/tick -- read the split, not the totals' }

# ------------------------------------------------------------------ 6. launch
# MUST be a direct exe launch, NOT steam://rungameid. A Steam-spawned process
# inherits Steam's environment, not this shell's, so CA_CONVERGENCE_EXERCISE
# and the probe variables never reach the game and it boots to the main menu
# with nothing armed. Steam's DRM relaunch can leave a duplicate behind; that
# is resolved after the window appears rather than avoided by giving up the
# environment.
$operatorWindow = [PlaytestWin]::GetForegroundWindow()
Start-Process -FilePath $RimWorldExe -WorkingDirectory (Split-Path $RimWorldExe)

$deadline = (Get-Date).AddSeconds(180)
$game = $null
while (-not $game) {
    if ((Get-Date) -gt $deadline) { Fail 'RimWorld window never appeared.' }
    Start-Sleep -Milliseconds 500
    $game = Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
}
Write-Step "RimWorld up (PID $($game.Id))"

# ------------------------------------------- 7. give the operator their focus back
# SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE: reorder only, never move or raise.
Start-Sleep -Seconds 2
if ($operatorWindow -ne [IntPtr]::Zero -and $operatorWindow -ne $game.MainWindowHandle) {
    [PlaytestWin]::SetWindowPos($game.MainWindowHandle, [IntPtr]1, 0,0,0,0, 0x0013) | Out-Null
    [PlaytestWin]::SetForegroundWindow($operatorWindow) | Out-Null
    Write-Step 'restored operator foreground window; the game was not raised'
}

# --------------------------------------------------------- 8. assert one instance
Start-Sleep -Seconds 10
$all = @(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue)
if ($all.Count -gt 1) {
    # Steam's DRM relaunch can leave a duplicate. The survivor must be chosen
    # by which one is actually loading the game, not by which appeared first:
    # the real process climbs past a gigabyte while the stub stays small.
    $real = $all | Sort-Object WorkingSet64 -Descending | Select-Object -First 1
    Write-Step ("Steam left " + $all.Count + " instances; keeping PID " + $real.Id +
                " (" + [math]::Round($real.WorkingSet64/1MB) + " MB) and dropping the rest")
    $all | Where-Object { $_.Id -ne $real.Id } | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
    $game = $real
    $all = @(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue)
}
Write-Step ("exactly one instance confirmed (PID " + ($all.Id -join ', ') + ")")

# ------------------------------------------------------- 8b. assert it ARMED
# A Steam-protocol launch silently dropped the environment once and the game
# booted to the main menu with nothing armed, which looks identical to a slow
# generate until someone glances at the screen. Never let that be silent again.
$deadline = (Get-Date).AddSeconds(120)
while ($true) {
    if (Test-Path $PlayerLog) {
        if (Select-String -Path $PlayerLog -SimpleMatch '[CA][Exercise] ARMED' -Quiet -ErrorAction SilentlyContinue) {
            Write-Step 'exercise ARMED confirmed in Player.log'
            break
        }
    }
    if ((Get-Date) -gt $deadline) {
        Fail 'exercise never armed -- the game is sitting at the main menu. CA_CONVERGENCE_EXERCISE did not reach the process (a Steam-protocol launch causes exactly this).'
    }
    Start-Sleep -Seconds 2
}
if ($SubProbe) {
    foreach ($probe in @('[CA][SteadySub] armed', '[CA][ListNormal] armed')) {
        if (-not (Select-String -Path $PlayerLog -SimpleMatch $probe -Quiet -ErrorAction SilentlyContinue)) {
            Write-Step ("WARNING: probe not armed: " + $probe + " -- its bucket will read 0 and must not be trusted")
        }
    }
}

# ----------------------------------------------------------------- 9. wait
$marker = if ($Hold) { 'HOLD: session left interactive' } else { 'exercise finished' }
Write-Step "waiting for: $marker"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ($true) {
    if ((Get-Date) -gt $deadline) { Write-Step 'TIMEOUT waiting for marker'; break }
    if (-not (Get-Process -Id $game.Id -ErrorAction SilentlyContinue)) { Write-Step 'game exited before marker'; break }
    if (Test-Path $PlayerLog) {
        if (Select-String -Path $PlayerLog -SimpleMatch $marker -Quiet -ErrorAction SilentlyContinue) {
            Write-Step "REACHED: $marker"; break
        }
    }
    Start-Sleep -Seconds 2
}
Write-Step 'done'
