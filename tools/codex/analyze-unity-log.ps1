[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Path,

    [string] $OutputPath,

    [double] $SlowTickMs = 20,

    [double] $SlowUiMs = 10,

    [double] $SlowFrameMs = 20,

    [datetime] $Since,

    [int] $LastMinutes = 0,

    [switch] $LatestSession,

    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'

function Resolve-LogPath {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $candidates = @()
    # Prefer the project-owned perf log. Unity's global Editor.log may exist
    # but be locked or unreadable by the current user, which must not prevent
    # diagnosis of the explicitly instrumented project session.
    $candidates += (Join-Path (Get-Location) 'Kingdom\Temp\KingdomPerf.log')
    $candidates += (Join-Path (Get-Location) 'Temp\KingdomPerf.log')
    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR_LOG)) {
        $candidates += $env:UNITY_EDITOR_LOG
    }
    $candidates += (Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log')
    $candidates += (Join-Path $env:APPDATA 'Unity\Editor\Editor.log')

    foreach ($candidate in $candidates) {
        try {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        } catch [System.UnauthorizedAccessException] {
            continue
        } catch [System.IO.IOException] {
            continue
        }
    }

    throw "Unity log not found. Pass -Path <Editor.log> or set UNITY_EDITOR_LOG."
}

function Get-NumberStats {
    param([double[]] $Values)

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [pscustomobject]@{ Count = 0; Average = $null; P95 = $null; Maximum = $null }
    }

    $sorted = @($Values | Sort-Object)
    $p95Index = [Math]::Min($sorted.Count - 1, [Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.95) - 1))
    return [pscustomobject]@{
        Count = $sorted.Count
        Average = [Math]::Round((($sorted | Measure-Object -Average).Average), 2)
        P95 = [Math]::Round($sorted[$p95Index], 2)
        Maximum = [Math]::Round($sorted[-1], 2)
    }
}

$logPath = Resolve-LogPath $Path
$lines = @(Get-Content -LiteralPath $logPath -ErrorAction Stop)
$startLine = 0
if ($LatestSession) {
    for ($candidate = $lines.Count - 1; $candidate -ge 0; $candidate--) {
        if ($lines[$candidate] -match '\[KingdomPerf\] SessionStart') {
            $startLine = $candidate
            break
        }
    }
}
if ($LastMinutes -gt 0) {
    $Since = (Get-Date).AddMinutes(-$LastMinutes)
}

$tickSamples = New-Object System.Collections.Generic.List[double]
$uiSamples = New-Object System.Collections.Generic.List[double]
$tickStats = New-Object System.Collections.Generic.List[psobject]
$uiStats = New-Object System.Collections.Generic.List[psobject]
$uiAllocStats = New-Object System.Collections.Generic.List[psobject]
$uiBranchStats = New-Object System.Collections.Generic.List[psobject]
$frameStats = New-Object System.Collections.Generic.List[psobject]
$touchSlowWindows = New-Object System.Collections.Generic.List[psobject]
$memorySamples = New-Object System.Collections.Generic.List[double]
$touchGestures = New-Object System.Collections.Generic.List[psobject]
$dragInputEvidence = New-Object System.Collections.Generic.List[psobject]
$simulatedDragFrameWindows = 0
$queueLogCount = 0
$pageDragCount = 0
$researchDragCount = 0
$detailDragCount = 0
$canvasIsolationCount = 0
$queueLayoutLogCount = 0
$queueEventLines = New-Object System.Collections.Generic.List[psobject]
$queueVisualLines = New-Object System.Collections.Generic.List[psobject]
$researchBuildPhases = New-Object System.Collections.Generic.List[psobject]
$errors = New-Object System.Collections.Generic.List[string]
$slowEvents = New-Object System.Collections.Generic.List[psobject]
$longFrames = New-Object System.Collections.Generic.List[psobject]
$testLines = New-Object System.Collections.Generic.List[string]

for ($i = $startLine; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]

    if ($Since) {
        $stampMatch = [regex]::Match($line, '^(?<stamp>\d{4}-\d{2}-\d{2}T[^ ]+)\s')
        if ($stampMatch.Success) {
            $lineTime = [datetime]::Parse($stampMatch.Groups['stamp'].Value)
            if ($lineTime -lt $Since) { continue }
        }
    }

    if ($line -match '\[KingdomPerf\] Test(?:InjectResources|ResearchAcceleration|QueuePrepared|FastResearch)') {
        [void]$testLines.Add($line.Trim())
    }

    $longFrameMatch = [regex]::Match($line, 'LongFrame\s+frame=(?<frame>[0-9]+(?:\.[0-9]+)?)ms\s+touchCount=(?<touch>[0-9]+)\s+scrolling=(?<scrolling>\w+)\s+page=(?<page>\S+)\s+gc=\((?<gc0>[0-9]+),(?<gc1>[0-9]+),(?<gc2>[0-9]+)\)')
    if ($longFrameMatch.Success) {
        [void]$longFrames.Add([pscustomobject]@{
            Line = $i + 1
            Milliseconds = [double]$longFrameMatch.Groups['frame'].Value
            TouchCount = [int]$longFrameMatch.Groups['touch'].Value
            Scrolling = [bool]::Parse($longFrameMatch.Groups['scrolling'].Value)
            Page = $longFrameMatch.Groups['page'].Value
            Gc0 = [int]$longFrameMatch.Groups['gc0'].Value
            Gc1 = [int]$longFrameMatch.Groups['gc1'].Value
            Gc2 = [int]$longFrameMatch.Groups['gc2'].Value
            Text = $line.Trim()
        })
    }

    $tickMatch = [regex]::Match($line, 'ManualTick\s+(?<ms>[0-9]+(?:\.[0-9]+)?)ms')
    if ($tickMatch.Success) {
        $ms = [double]$tickMatch.Groups['ms'].Value
        [void]$tickSamples.Add($ms)
        if ($ms -ge $SlowTickMs) {
            [void]$slowEvents.Add([pscustomobject]@{ Line = $i + 1; Kind = 'ManualTick'; Milliseconds = $ms; Text = $line.Trim() })
        }
    }

    $tickStatsMatch = [regex]::Match($line, 'ManualTickStats\s+samples=(?<samples>[0-9]+)\s+avg=(?<avg>[0-9]+(?:\.[0-9]+)?)ms\s+max=(?<max>[0-9]+(?:\.[0-9]+)?)ms(?:\s+buildingAvg=(?<buildingAvg>[0-9]+(?:\.[0-9]+)?)ms\s+buildingMax=(?<buildingMax>[0-9]+(?:\.[0-9]+)?)ms\s+gameAvg=(?<gameAvg>[0-9]+(?:\.[0-9]+)?)ms\s+gameMax=(?<gameMax>[0-9]+(?:\.[0-9]+)?)ms\s+resourceAvg=(?<resourceAvg>[0-9]+(?:\.[0-9]+)?)ms\s+resourceMax=(?<resourceMax>[0-9]+(?:\.[0-9]+)?)ms\s+sectorsAvg=(?<sectorsAvg>[0-9]+(?:\.[0-9]+)?)ms\s+sectorsMax=(?<sectorsMax>[0-9]+(?:\.[0-9]+)?)ms\s+researchAvg=(?<researchAvg>[0-9]+(?:\.[0-9]+)?)ms\s+researchMax=(?<researchMax>[0-9]+(?:\.[0-9]+)?)ms)?(?:\s+allocKB=(?<allocKB>NA|[0-9]+))?')
    if ($tickStatsMatch.Success) {
        [void]$tickStats.Add([pscustomobject]@{
            Samples = [int]$tickStatsMatch.Groups['samples'].Value
            Average = [double]$tickStatsMatch.Groups['avg'].Value
            Maximum = [double]$tickStatsMatch.Groups['max'].Value
            BuildingAverage = if ($tickStatsMatch.Groups['buildingAvg'].Success) { [double]$tickStatsMatch.Groups['buildingAvg'].Value } else { $null }
            BuildingMaximum = if ($tickStatsMatch.Groups['buildingMax'].Success) { [double]$tickStatsMatch.Groups['buildingMax'].Value } else { $null }
            GameAverage = if ($tickStatsMatch.Groups['gameAvg'].Success) { [double]$tickStatsMatch.Groups['gameAvg'].Value } else { $null }
            GameMaximum = if ($tickStatsMatch.Groups['gameMax'].Success) { [double]$tickStatsMatch.Groups['gameMax'].Value } else { $null }
            ResourceAverage = if ($tickStatsMatch.Groups['resourceAvg'].Success) { [double]$tickStatsMatch.Groups['resourceAvg'].Value } else { $null }
            ResourceMaximum = if ($tickStatsMatch.Groups['resourceMax'].Success) { [double]$tickStatsMatch.Groups['resourceMax'].Value } else { $null }
            SectorsAverage = if ($tickStatsMatch.Groups['sectorsAvg'].Success) { [double]$tickStatsMatch.Groups['sectorsAvg'].Value } else { $null }
            SectorsMaximum = if ($tickStatsMatch.Groups['sectorsMax'].Success) { [double]$tickStatsMatch.Groups['sectorsMax'].Value } else { $null }
            ResearchAverage = if ($tickStatsMatch.Groups['researchAvg'].Success) { [double]$tickStatsMatch.Groups['researchAvg'].Value } else { $null }
            ResearchMaximum = if ($tickStatsMatch.Groups['researchMax'].Success) { [double]$tickStatsMatch.Groups['researchMax'].Value } else { $null }
            AllocatedKB = if (!$tickStatsMatch.Groups['allocKB'].Success -or $tickStatsMatch.Groups['allocKB'].Value -eq 'NA') { $null } else { [long]$tickStatsMatch.Groups['allocKB'].Value }
            Text = $line.Trim()
        })
    }

    $uiMatch = [regex]::Match($line, 'RefreshUI\s+(?<ms>[0-9]+(?:\.[0-9]+)?)ms(?:\s+page=(?<page>\S+))?')
    if ($uiMatch.Success) {
        $ms = [double]$uiMatch.Groups['ms'].Value
        [void]$uiSamples.Add($ms)
        if ($ms -ge $SlowUiMs) {
            [void]$slowEvents.Add([pscustomobject]@{ Line = $i + 1; Kind = 'RefreshUI'; Milliseconds = $ms; Page = $uiMatch.Groups['page'].Value; Text = $line.Trim() })
        }
    }

    $uiStatsMatch = [regex]::Match($line, 'RefreshUIStats\s+samples=(?<samples>[0-9]+)\s+avg=(?<avg>[0-9]+(?:\.[0-9]+)?)ms\s+max=(?<max>[0-9]+(?:\.[0-9]+)?)ms(?:\s+page=(?<page>\S+))?')
    if ($uiStatsMatch.Success) {
        [void]$uiStats.Add([pscustomobject]@{
            Samples = [int]$uiStatsMatch.Groups['samples'].Value
            Average = [double]$uiStatsMatch.Groups['avg'].Value
            Maximum = [double]$uiStatsMatch.Groups['max'].Value
            Page = $uiStatsMatch.Groups['page'].Value
            Text = $line.Trim()
        })
    }

    $uiAllocMatch = [regex]::Match($line, 'RefreshUIAlloc\s+samples=(?<samples>[0-9]+)\s+totalKB=(?<total>[0-9]+)\s+maxKB=(?<max>[0-9]+)\s+page=(?<page>\S+)')
    if ($uiAllocMatch.Success) {
        [void]$uiAllocStats.Add([pscustomobject]@{
            Samples = [int]$uiAllocMatch.Groups['samples'].Value
            TotalKB = [long]$uiAllocMatch.Groups['total'].Value
            MaximumKB = [long]$uiAllocMatch.Groups['max'].Value
            Page = $uiAllocMatch.Groups['page'].Value
            Text = $line.Trim()
        })
    }

    $uiBranchMatch = [regex]::Match($line, 'RefreshUIBranches\s+top=(?<topCount>[0-9]+):(?<topAvg>[0-9]+(?:\.[0-9]+)?)\/(?<topMax>[0-9]+(?:\.[0-9]+)?)ms\s+detail=(?<detailCount>[0-9]+):(?<detailAvg>[0-9]+(?:\.[0-9]+)?)\/(?<detailMax>[0-9]+(?:\.[0-9]+)?)ms\s+dataflow=(?<flowCount>[0-9]+):(?<flowAvg>[0-9]+(?:\.[0-9]+)?)\/(?<flowMax>[0-9]+(?:\.[0-9]+)?)ms\s+liveCards=(?<cardsCount>[0-9]+):(?<cardsAvg>[0-9]+(?:\.[0-9]+)?)\/(?<cardsMax>[0-9]+(?:\.[0-9]+)?)ms\s+queue=(?<queueCount>[0-9]+):(?<queueAvg>[0-9]+(?:\.[0-9]+)?)\/(?<queueMax>[0-9]+(?:\.[0-9]+)?)ms\s+page=(?<page>\S+)')
    if ($uiBranchMatch.Success) {
        [void]$uiBranchStats.Add([pscustomobject]@{
            TopCount = [int]$uiBranchMatch.Groups['topCount'].Value
            TopAverage = [double]$uiBranchMatch.Groups['topAvg'].Value
            TopMaximum = [double]$uiBranchMatch.Groups['topMax'].Value
            DetailCount = [int]$uiBranchMatch.Groups['detailCount'].Value
            DetailAverage = [double]$uiBranchMatch.Groups['detailAvg'].Value
            DetailMaximum = [double]$uiBranchMatch.Groups['detailMax'].Value
            DataFlowCount = [int]$uiBranchMatch.Groups['flowCount'].Value
            DataFlowAverage = [double]$uiBranchMatch.Groups['flowAvg'].Value
            DataFlowMaximum = [double]$uiBranchMatch.Groups['flowMax'].Value
            LiveCardsCount = [int]$uiBranchMatch.Groups['cardsCount'].Value
            LiveCardsAverage = [double]$uiBranchMatch.Groups['cardsAvg'].Value
            LiveCardsMaximum = [double]$uiBranchMatch.Groups['cardsMax'].Value
            QueueCount = [int]$uiBranchMatch.Groups['queueCount'].Value
            QueueAverage = [double]$uiBranchMatch.Groups['queueAvg'].Value
            QueueMaximum = [double]$uiBranchMatch.Groups['queueMax'].Value
            Page = $uiBranchMatch.Groups['page'].Value
            Text = $line.Trim()
        })
    }

    $frameStatsMatch = [regex]::Match($line, 'FrameStats\s+samples=(?<samples>[0-9]+)\s+avg=(?<avg>[0-9]+(?:\.[0-9]+)?)ms\s+max=(?<max>[0-9]+(?:\.[0-9]+)?)ms\s+slow20=(?<slow20>[0-9]+)\s+touchFrames=(?<touchFrames>[0-9]+)\s+maxTouch=(?<maxTouch>[0-9]+)\s+dragFrames=(?<dragFrames>[0-9]+)(?:\s+gc0=(?<gc0>[0-9]+)\s+gc1=(?<gc1>[0-9]+)\s+gc2=(?<gc2>[0-9]+))?(?:\s+allocKB=(?<allocKB>NA|[0-9]+))?(?:\s+queueEvents=(?<queueEvents>[0-9]+))?\s+page=(?<page>\S+)')
    if ($frameStatsMatch.Success) {
        $frameEntry = [pscustomobject]@{
            Samples = [int]$frameStatsMatch.Groups['samples'].Value
            Average = [double]$frameStatsMatch.Groups['avg'].Value
            Maximum = [double]$frameStatsMatch.Groups['max'].Value
            Slow20 = [int]$frameStatsMatch.Groups['slow20'].Value
            TouchFrames = [int]$frameStatsMatch.Groups['touchFrames'].Value
            MaxTouch = [int]$frameStatsMatch.Groups['maxTouch'].Value
            DragFrames = [int]$frameStatsMatch.Groups['dragFrames'].Value
            Gc0 = [int]$frameStatsMatch.Groups['gc0'].Value
            Gc1 = [int]$frameStatsMatch.Groups['gc1'].Value
            Gc2 = [int]$frameStatsMatch.Groups['gc2'].Value
            AllocatedKB = if (!$frameStatsMatch.Groups['allocKB'].Success -or $frameStatsMatch.Groups['allocKB'].Value -eq 'NA') { $null } else { [long]$frameStatsMatch.Groups['allocKB'].Value }
            QueueEvents = [int]$frameStatsMatch.Groups['queueEvents'].Value
            Page = $frameStatsMatch.Groups['page'].Value
            Text = $line.Trim()
        }
        [void]$frameStats.Add($frameEntry)
        if ($frameEntry.DragFrames -gt 0 -and $frameEntry.TouchFrames -eq 0) {
            $simulatedDragFrameWindows++
        }
        if ($frameEntry.TouchFrames -gt 0 -and $frameEntry.Maximum -ge $SlowFrameMs) {
            [void]$touchSlowWindows.Add($frameEntry)
        }
    }

    $memoryMatch = [regex]::Match($line, '(?i)(?:WorkingSet|Memory|memory)[^0-9]{0,32}(?<mb>[0-9]+(?:\.[0-9]+)?)\s*MB')
    if ($memoryMatch.Success) {
        [void]$memorySamples.Add([double]$memoryMatch.Groups['mb'].Value)
    }

    $touchGestureMatch = [regex]::Match($line, 'TouchGesture\s+duration=(?<duration>[0-9]+(?:\.[0-9]+)?)ms\s+samples=(?<samples>[0-9]+)\s+distance=(?<distance>[0-9]+(?:\.[0-9]+)?)px\s+maxFrame=(?<maxFrame>[0-9]+(?:\.[0-9]+)?)ms\s+slow20=(?<slow20>[0-9]+)\s+page=(?<page>\S+)')
    if ($touchGestureMatch.Success) {
        [void]$touchGestures.Add([pscustomobject]@{
            DurationMilliseconds = [double]$touchGestureMatch.Groups['duration'].Value
            Samples = [int]$touchGestureMatch.Groups['samples'].Value
            DistancePixels = [double]$touchGestureMatch.Groups['distance'].Value
            MaximumFrameMilliseconds = [double]$touchGestureMatch.Groups['maxFrame'].Value
            Slow20 = [int]$touchGestureMatch.Groups['slow20'].Value
            Page = $touchGestureMatch.Groups['page'].Value
            Text = $line.Trim()
        })
    }

    if ($line -match 'Research queue toolbar|ResearchQueueChanged') {
        $queueLogCount++
    }
    $queueEventMatch = [regex]::Match($line, 'ResearchQueueEvent\s+subscribed=(?<subscribed>\w+)\s+queueCount=(?<count>-?[0-9]+)')
    if ($queueEventMatch.Success) {
        [void]$queueEventLines.Add([pscustomobject]@{
            Subscribed = [bool]::Parse($queueEventMatch.Groups['subscribed'].Value)
            QueueCount = [int]$queueEventMatch.Groups['count'].Value
            Text = $line.Trim()
        })
    }
    $queueVisualMatch = [regex]::Match($line, 'ResearchQueueVisual\s+entries=(?<entries>[0-9]+)\s+lines=(?<lines>[0-9]+)\s+preferredHeight=(?<preferred>[0-9]+(?:\.[0-9]+)?)\s+rectHeight=(?<rect>[0-9]+(?:\.[0-9]+)?)\s+wrap=(?<wrap>\w+)\s+width=(?<width>[0-9]+(?:\.[0-9]+)?)')
    if ($queueVisualMatch.Success) {
        [void]$queueVisualLines.Add([pscustomobject]@{
            Entries = [int]$queueVisualMatch.Groups['entries'].Value
            Lines = [int]$queueVisualMatch.Groups['lines'].Value
            PreferredHeight = [double]$queueVisualMatch.Groups['preferred'].Value
            RectHeight = [double]$queueVisualMatch.Groups['rect'].Value
            Wrap = [bool]::Parse($queueVisualMatch.Groups['wrap'].Value)
            Width = [double]$queueVisualMatch.Groups['width'].Value
            Text = $line.Trim()
        })
    }
    $researchBuildMatch = [regex]::Match($line, 'ResearchBuildPhase\s+phase=(?<phase>\w+)\s+elapsedMs=(?<elapsed>[0-9]+(?:\.[0-9]+)?)')
    if ($researchBuildMatch.Success) {
        [void]$researchBuildPhases.Add([pscustomobject]@{
            Phase = $researchBuildMatch.Groups['phase'].Value
            ElapsedMilliseconds = [double]$researchBuildMatch.Groups['elapsed'].Value
            Text = $line.Trim()
        })
    }
    if ($line -match '\[KingdomPerf\] PageDrag begin') { $pageDragCount++ }
    if ($line -match '\[KingdomPerf\] ResearchDrag (?:begin|manualBegin)') { $researchDragCount++ }
    if ($line -match '\[KingdomPerf\] DetailDrag begin') { $detailDragCount++ }
    if ($line -match '\[KingdomPerf\] CanvasIsolation ') { $canvasIsolationCount++ }
    if ($line -match '\[KingdomPerf\] ResearchQueueLayout ') { $queueLayoutLogCount++ }

    $dragMatch = [regex]::Match($line, '\[KingdomPerf\] (?<kind>PageDrag|ResearchDrag|DetailDrag) (?<phase>begin|manualBegin|end)(?:[^\r\n]*?)touchCount=(?<touchCount>[0-9]+)')
    if ($dragMatch.Success) {
        [void]$dragInputEvidence.Add([pscustomobject]@{
            Kind = $dragMatch.Groups['kind'].Value
            Phase = $dragMatch.Groups['phase'].Value
            TouchCount = [int]$dragMatch.Groups['touchCount'].Value
            Source = if ([int]$dragMatch.Groups['touchCount'].Value -gt 0) { 'touch' } else { 'mouse-or-simulator'
            }
            Text = $line.Trim()
        })
    }

    # ResourceManager failures predate and are explicitly outside this task.
    # Keep them visible in Unity, but do not let them change this UI/perf report.
    if ($line -match '(?i)\b(error|exception|fatal|crash)\b' -and
        $line -notmatch '(?i)ResourceManager|resourceManager' -and
        $line -notmatch '0 errors') {
        [void]$errors.Add(($line.Trim() -replace '\s+', ' '))
    }
}

$report = [pscustomobject]@{
    LogPath = $logPath
    LineCount = $lines.Count
    LastWriteTime = (Get-Item -LiteralPath $logPath).LastWriteTime
    Tick = Get-NumberStats $tickSamples.ToArray()
    Ui = Get-NumberStats $uiSamples.ToArray()
    TickStats = @($tickStats)
    UiStats = @($uiStats)
    UiAllocStats = @($uiAllocStats)
    UiBranchStats = @($uiBranchStats)
    FrameStats = @($frameStats)
    TouchSlowWindows = @($touchSlowWindows)
    MemoryMB = Get-NumberStats $memorySamples.ToArray()
    TouchGestures = @($touchGestures)
    QueueLogCount = $queueLogCount
    PageDragCount = $pageDragCount
    ResearchDragCount = $researchDragCount
    DetailDragCount = $detailDragCount
    CanvasIsolationCount = $canvasIsolationCount
    QueueLayoutLogCount = $queueLayoutLogCount
    QueueEventLines = @($queueEventLines)
    QueueVisualLines = @($queueVisualLines)
    ResearchBuildPhases = @($researchBuildPhases)
    DragInputEvidence = @($dragInputEvidence)
    SimulatedDragFrameWindows = $simulatedDragFrameWindows
    SlowEventCount = $slowEvents.Count
    SlowEvents = @($slowEvents | Select-Object -First 20)
    LongFrames = @($longFrames)
    TestLines = @($testLines)
    ErrorCount = $errors.Count
    Errors = @($errors | Select-Object -Unique -First 20)
}

$trendPage = if ($frameStats.Count -gt 0) { $frameStats[-1].Page } else { $null }
# Startup/domain-reload windows can contain only a handful of samples and
# thousands of milliseconds. Compare steady-state windows on the same page so
# the report answers whether gameplay degraded, not whether Unity finished
# loading the scene.
$idleFrames = @($frameStats | Where-Object { $_.TouchFrames -eq 0 -and $_.Samples -ge 100 -and $_.Page -eq $trendPage })
$earlyIdleFrames = @($idleFrames | Select-Object -First ([Math]::Min(3, $idleFrames.Count)))
$lateIdleFrames = @($idleFrames | Select-Object -Last ([Math]::Min(3, $idleFrames.Count)))
if ($earlyIdleFrames.Count -gt 0 -and $lateIdleFrames.Count -gt 0) {
    $earlyIdleAverage = [Math]::Round((($earlyIdleFrames | Measure-Object -Property Average -Average).Average), 2)
    $lateIdleAverage = [Math]::Round((($lateIdleFrames | Measure-Object -Property Average -Average).Average), 2)
    $memoryDelta = $null
    if ($memorySamples.Count -ge 2) {
        $memoryDelta = [Math]::Round($memorySamples[-1] - $memorySamples[0], 1)
    }
    $report | Add-Member -NotePropertyName IdleTrend -NotePropertyValue ([pscustomobject]@{
        EarlyAverageMilliseconds = $earlyIdleAverage
        LateAverageMilliseconds = $lateIdleAverage
        Ratio = if ($earlyIdleAverage -gt 0) { [Math]::Round($lateIdleAverage / $earlyIdleAverage, 2) } else { $null }
        MemoryDeltaMB = $memoryDelta
        MemoryGrowthPossible = $null -ne $memoryDelta -and $memoryDelta -ge 50
    })
}

$pageFrameSummaries = @($frameStats | Group-Object Page | ForEach-Object {
    $groupFrames = @($_.Group)
    [pscustomobject]@{
        Page = $_.Name
        Windows = $groupFrames.Count
        AverageMilliseconds = [Math]::Round((($groupFrames | Measure-Object -Property Average -Average).Average), 2)
        MaximumMilliseconds = [Math]::Round((($groupFrames | Measure-Object -Property Maximum -Maximum).Maximum), 2)
        Slow20 = ($groupFrames | Measure-Object -Property Slow20 -Sum).Sum
        TouchWindows = @($groupFrames | Where-Object { $_.TouchFrames -gt 0 }).Count
    }
})
$report | Add-Member -NotePropertyName PageFrameSummaries -NotePropertyValue $pageFrameSummaries

if ($AsJson) {
    $rendered = $report | ConvertTo-Json -Depth 6
} else {
    $renderedLines = New-Object System.Collections.Generic.List[string]
    [void]$renderedLines.Add("Unity log diagnosis: $($report.LogPath)")
    [void]$renderedLines.Add("lines=$($report.LineCount), lastWrite=$($report.LastWriteTime)")
    [void]$renderedLines.Add(("ManualTick: samples={0}, avg={1}ms, p95={2}ms, max={3}ms" -f $report.Tick.Count, $report.Tick.Average, $report.Tick.P95, $report.Tick.Maximum))
    [void]$renderedLines.Add(("RefreshUI: samples={0}, avg={1}ms, p95={2}ms, max={3}ms" -f $report.Ui.Count, $report.Ui.Average, $report.Ui.P95, $report.Ui.Maximum))
    if ($report.TickStats.Count -gt 0) {
        $latestTickStats = $report.TickStats[-1]
        $tickAllocText = if ($null -eq $latestTickStats.AllocatedKB) { 'NA' } else { [string]$latestTickStats.AllocatedKB }
        $stageText = if ($null -ne $latestTickStats.BuildingAverage) {
            ", stages(building={0}/{1}ms game={2}/{3}ms resource={4}/{5}ms sectors={6}/{7}ms research={8}/{9}ms)" -f `
                $latestTickStats.BuildingAverage, $latestTickStats.BuildingMaximum,
                $latestTickStats.GameAverage, $latestTickStats.GameMaximum,
                $latestTickStats.ResourceAverage, $latestTickStats.ResourceMaximum,
                $latestTickStats.SectorsAverage, $latestTickStats.SectorsMaximum,
                $latestTickStats.ResearchAverage, $latestTickStats.ResearchMaximum
        } else { '' }
        [void]$renderedLines.Add(("ManualTickStats latest: samples={0}, avg={1}ms, max={2}ms{3}, allocKB={4}" -f $latestTickStats.Samples, $latestTickStats.Average, $latestTickStats.Maximum, $stageText, $tickAllocText))
    }
    if ($report.UiStats.Count -gt 0) {
        $latestUiStats = $report.UiStats[-1]
        [void]$renderedLines.Add(("RefreshUIStats latest: samples={0}, avg={1}ms, max={2}ms, page={3}" -f $latestUiStats.Samples, $latestUiStats.Average, $latestUiStats.Maximum, $latestUiStats.Page))
    }
    if ($report.UiAllocStats.Count -gt 0) {
        $latestUiAlloc = $report.UiAllocStats[-1]
        [void]$renderedLines.Add("RefreshUIAlloc latest: samples=$($latestUiAlloc.Samples), total=$($latestUiAlloc.TotalKB)KB, max=$($latestUiAlloc.MaximumKB)KB, page=$($latestUiAlloc.Page)")
    }
    if ($report.UiBranchStats.Count -gt 0) {
        $latestBranches = $report.UiBranchStats[-1]
        [void]$renderedLines.Add(
            ("RefreshUIBranches latest: top={0}:{1}/{2}ms detail={3}:{4}/{5}ms dataflow={6}:{7}/{8}ms liveCards={9}:{10}/{11}ms queue={12}:{13}/{14}ms page={15}" -f
                $latestBranches.TopCount, $latestBranches.TopAverage, $latestBranches.TopMaximum,
                $latestBranches.DetailCount, $latestBranches.DetailAverage, $latestBranches.DetailMaximum,
                $latestBranches.DataFlowCount, $latestBranches.DataFlowAverage, $latestBranches.DataFlowMaximum,
                $latestBranches.LiveCardsCount, $latestBranches.LiveCardsAverage, $latestBranches.LiveCardsMaximum,
                $latestBranches.QueueCount, $latestBranches.QueueAverage, $latestBranches.QueueMaximum,
                $latestBranches.Page))
    }
    if ($report.FrameStats.Count -gt 0) {
        $latestFrame = $report.FrameStats[-1]
        $frameAllocText = if ($null -eq $latestFrame.AllocatedKB) { 'NA' } else { [string]$latestFrame.AllocatedKB }
        [void]$renderedLines.Add(("Frame latest: samples={0}, avg={1}ms, max={2}ms, slow20={3}, touchFrames={4}, maxTouch={5}, dragFrames={6}, gc=({7},{8},{9}), allocKB={10}, queueEvents={11}, page={12}" -f $latestFrame.Samples, $latestFrame.Average, $latestFrame.Maximum, $latestFrame.Slow20, $latestFrame.TouchFrames, $latestFrame.MaxTouch, $latestFrame.DragFrames, $latestFrame.Gc0, $latestFrame.Gc1, $latestFrame.Gc2, $frameAllocText, $latestFrame.QueueEvents, $latestFrame.Page))
    }
    if ($report.LongFrames.Count -gt 0) {
        [void]$renderedLines.Add('long frames:')
        foreach ($frame in @($report.LongFrames | Select-Object -Last 12)) {
            # LongFrame stores absolute collection counters. A non-zero value
            # is not itself a collection caused by this frame; use the
            # following FrameStats window's gc delta for that claim.
            # A half-second+ frame with no input is already outside normal
            # gameplay cadence. Keep it separate from input/logic hitches;
            # the runtime counters below determine whether game code also
            # reported work during the same window.
            $cause = if ($frame.Milliseconds -ge 500 -and $frame.TouchCount -eq 0 -and -not $frame.Scrolling) {
                'editor-pause-candidate'
            } elseif ($frame.TouchCount -gt 0 -or $frame.Scrolling) {
                'input-correlated'
            } else {
                'unattributed'
            }
            [void]$renderedLines.Add("  frame=$($frame.Milliseconds)ms cause=$cause touch=$($frame.TouchCount) scrolling=$($frame.Scrolling) page=$($frame.Page) gc=($($frame.Gc0),$($frame.Gc1),$($frame.Gc2)) line=$($frame.Line)")
        }
    }
    if ($report.PageFrameSummaries.Count -gt 0) {
        [void]$renderedLines.Add('Frame by page:')
        foreach ($pageSummary in $report.PageFrameSummaries) {
            [void]$renderedLines.Add("  page=$($pageSummary.Page) windows=$($pageSummary.Windows) avg=$($pageSummary.AverageMilliseconds)ms max=$($pageSummary.MaximumMilliseconds)ms slow20=$($pageSummary.Slow20) touchWindows=$($pageSummary.TouchWindows)")
        }
    }
    if ($report.TouchSlowWindows.Count -gt 0) {
        [void]$renderedLines.Add('touch slow windows (up to 10):')
        foreach ($window in @($report.TouchSlowWindows | Select-Object -Last 10)) {
            [void]$renderedLines.Add("  max=$($window.Maximum)ms avg=$($window.Average)ms slow20=$($window.Slow20) touchFrames=$($window.TouchFrames) dragFrames=$($window.DragFrames) gc=($($window.Gc0),$($window.Gc1),$($window.Gc2)) queueEvents=$($window.QueueEvents) page=$($window.Page)")
        }
    }
    [void]$renderedLines.Add(("Memory: samples={0}, avg={1}MB, p95={2}MB, max={3}MB" -f $report.MemoryMB.Count, $report.MemoryMB.Average, $report.MemoryMB.P95, $report.MemoryMB.Maximum))
    if ($null -ne $report.IdleTrend) {
        $trend = $report.IdleTrend
        $trendVerdict = if ($trend.Ratio -ge 2.0 -and $trend.MemoryGrowthPossible) { 'late-idle-degradation-and-memory-growth-possible' } elseif ($trend.Ratio -ge 2.0) { 'late-idle-degradation-possible' } elseif ($trend.MemoryGrowthPossible) { 'memory-growth-possible' } else { 'no-clear-late-idle-degradation' }
        [void]$renderedLines.Add(("Idle trend: earlyAvg={0}ms lateAvg={1}ms ratio={2} memoryDelta={3}MB verdict={4}" -f $trend.EarlyAverageMilliseconds, $trend.LateAverageMilliseconds, $trend.Ratio, $trend.MemoryDeltaMB, $trendVerdict))
    }
    if ($report.TouchGestures.Count -gt 0) {
        $gestureMax = ($report.TouchGestures | Measure-Object -Property MaximumFrameMilliseconds -Maximum).Maximum
        $gestureSlow = @($report.TouchGestures | Where-Object { $_.Slow20 -gt 0 }).Count
        [void]$renderedLines.Add(("Touch gestures: count={0}, maxGestureFrame={1}ms, gesturesWithSlow20={2}" -f $report.TouchGestures.Count, ([Math]::Round($gestureMax, 2)), $gestureSlow))
        foreach ($gesture in @($report.TouchGestures | Select-Object -Last 10)) {
            [void]$renderedLines.Add("  duration=$($gesture.DurationMilliseconds)ms samples=$($gesture.Samples) distance=$($gesture.DistancePixels)px maxFrame=$($gesture.MaximumFrameMilliseconds)ms slow20=$($gesture.Slow20) page=$($gesture.Page)")
        }
    } else {
        [void]$renderedLines.Add('Touch gestures: none (no real Input.touchCount gesture was captured in the selected log range)')
    }
    [void]$renderedLines.Add("researchQueueLogCount=$($report.QueueLogCount), queueLayoutLogs=$($report.QueueLayoutLogCount), canvasIsolation=$($report.CanvasIsolationCount), pageDrags=$($report.PageDragCount), researchDrags=$($report.ResearchDragCount), detailDrags=$($report.DetailDragCount), slowEventCount=$($report.SlowEventCount), errorCount=$($report.ErrorCount)")
    [void]$renderedLines.Add("testToolEvents=$($report.TestLines.Count)")
    if ($report.TestLines.Count -gt 0) {
        [void]$renderedLines.Add("  $($report.TestLines[-1])")
    }
    if ($report.DragInputEvidence.Count -gt 0) {
        $touchDragEvidence = @($report.DragInputEvidence | Where-Object { $_.Source -eq 'touch' }).Count
        $simulatedDragEvidence = @($report.DragInputEvidence | Where-Object { $_.Source -eq 'mouse-or-simulator' }).Count
        [void]$renderedLines.Add("drag input evidence: touchEvents=$touchDragEvidence mouseOrSimulatorEvents=$simulatedDragEvidence simulatedDragFrameWindows=$($report.SimulatedDragFrameWindows)")
    } else {
        [void]$renderedLines.Add("drag input evidence: none (no instrumented drag lifecycle with touchCount was captured); simulatedDragFrameWindows=$($report.SimulatedDragFrameWindows)")
    }
    [void]$renderedLines.Add("queueEventLines=$($report.QueueEventLines.Count), queueVisualLines=$($report.QueueVisualLines.Count)")
    if ($report.ResearchBuildPhases.Count -gt 0) {
        [void]$renderedLines.Add('research build phases:')
        foreach ($phase in $report.ResearchBuildPhases) {
            [void]$renderedLines.Add("  phase=$($phase.Phase) elapsed=$($phase.ElapsedMilliseconds)ms")
        }
    } else {
        [void]$renderedLines.Add('research build phases: none (latest selected session has not loaded the phase-instrumented assembly)')
    }
    if ($report.QueueVisualLines.Count -gt 0) {
        $latestQueueVisual = $report.QueueVisualLines[-1]
        [void]$renderedLines.Add("queue visual latest: entries=$($latestQueueVisual.Entries) lines=$($latestQueueVisual.Lines) preferredHeight=$($latestQueueVisual.PreferredHeight) rectHeight=$($latestQueueVisual.RectHeight) wrap=$($latestQueueVisual.Wrap) width=$($latestQueueVisual.Width)")
    }
    if ($report.SlowEvents.Count -gt 0) {
        [void]$renderedLines.Add('slow events (up to 20):')
        foreach ($event in $report.SlowEvents) { [void]$renderedLines.Add("  [$($event.Kind)] line=$($event.Line) $($event.Milliseconds)ms $($event.Text)") }
    }
    if ($report.Errors.Count -gt 0) {
        [void]$renderedLines.Add('errors/exceptions (unique, up to 20):')
        foreach ($errorLine in $report.Errors) { [void]$renderedLines.Add("  $errorLine") }
    }
    $rendered = $renderedLines -join [Environment]::NewLine
}

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    Set-Content -LiteralPath $OutputPath -Value $rendered -Encoding UTF8
}

$rendered
