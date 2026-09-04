# analyze-counters.ps1
# Compares two dotnet-counters CSV files and produces a Markdown analysis report.
#
# Usage:
#   .\analyze-counters.ps1 -BaselineFile main.csv -ComparisonFile feature.csv
#
# Output: <baseline>-vs-<comparison>-analysis.md in the current directory.

param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineFile,

    [Parameter(Mandatory = $true)]
    [string]$ComparisonFile
)

function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Error "File not found: $path"
        exit 1
    }
}

Assert-FileExists $BaselineFile
Assert-FileExists $ComparisonFile

function Read-Counters([string]$path) {
    Import-Csv $path -Header "Timestamp","Provider","CounterName","CounterType","Value" |
        Select-Object -Skip 1
}

function Get-Stats([object[]]$rows, [string]$counterName, [double]$minValue = 0) {
    $vals = $rows |
        Where-Object { $_.CounterName -eq $counterName -and $_.Value -match '^\d' -and [double]$_.Value -gt $minValue } |
        ForEach-Object { [double]$_.Value }

    if (-not $vals -or $vals.Count -eq 0) {
        return @{ Max = 0; Avg = 0; Sum = 0; Count = 0 }
    }

    $m = $vals | Measure-Object -Maximum -Minimum -Average -Sum
    return @{
        Max   = $m.Maximum
        Avg   = $m.Average
        Sum   = $m.Sum
        Count = $m.Count
    }
}

function Format-Bytes([double]$bytes) {
    if ($bytes -ge 1GB) { return "$([math]::Round($bytes / 1GB, 2)) GB" }
    if ($bytes -ge 1MB) { return "$([math]::Round($bytes / 1MB, 2)) MB" }
    if ($bytes -ge 1KB) { return "$([math]::Round($bytes / 1KB, 2)) KB" }
    return "$([math]::Round($bytes, 0)) B"
}

function Format-Round([double]$val, [int]$decimals = 2) {
    return "$([math]::Round($val, $decimals))"
}

function Format-Cpu([double]$cpuSeconds, [double]$cpuCores) {
    if ($cpuCores -eq 0) { return "N/A" }
    $pct = [math]::Round(($cpuSeconds / $cpuCores) * 100, 1)
    return "$pct%"
}

function Format-Diff([double]$baseline, [double]$comparison) {
    if ($baseline -eq 0) { return "N/A" }
    $pct = [math]::Round((($comparison - $baseline) / $baseline) * 100, 1)
    if ($pct -lt 0) { return "$pct% **better**" }
    if ($pct -gt 0) { return "+$pct% **worse**" }
    return "0% (equal)"
}

function Format-DiffCount([double]$baseline, [double]$comparison) {
    if ($baseline -eq 0) { return "N/A" }
    $pct = [math]::Round((($comparison - $baseline) / $baseline) * 100, 1)
    if ($pct -lt 0) { return "$pct%" }
    if ($pct -gt 0) { return "+$pct%" }
    return "0%"
}

Write-Host "Reading $BaselineFile..." -ForegroundColor Cyan
$base = Read-Counters $BaselineFile

Write-Host "Reading $ComparisonFile..." -ForegroundColor Cyan
$comp = Read-Counters $ComparisonFile

# ── Gather all stats ─────────────────────────────────────────────────────────

$counters = @{
    # Memory
    CommittedSize       = "dotnet.gc.last_collection.memory.committed_size (By)"
    WorkingSet          = "dotnet.process.memory.working_set (By)"
    PohSize             = "dotnet.gc.last_collection.heap.size (By)[gc.heap.generation=poh]"
    # Allocations
    AllocRate           = "dotnet.gc.heap.total_allocated (By / 1 sec)"
    # GC collections
    Gen0                = "dotnet.gc.collections ({collection} / 1 sec)[gc.heap.generation=gen0]"
    Gen1                = "dotnet.gc.collections ({collection} / 1 sec)[gc.heap.generation=gen1]"
    Gen2                = "dotnet.gc.collections ({collection} / 1 sec)[gc.heap.generation=gen2]"
    # GC pause
    PauseTime           = "dotnet.gc.pause.time (s / 1 sec)"
    # Heap size by generation
    Gen0HeapSize        = "dotnet.gc.last_collection.heap.size (By)[gc.heap.generation=gen0]"
    Gen1HeapSize        = "dotnet.gc.last_collection.heap.size (By)[gc.heap.generation=gen1]"
    Gen2HeapSize        = "dotnet.gc.last_collection.heap.size (By)[gc.heap.generation=gen2]"
    LohHeapSize         = "dotnet.gc.last_collection.heap.size (By)[gc.heap.generation=loh]"
    # Heap fragmentation
    Gen0Frag            = "dotnet.gc.last_collection.heap.fragmentation.size (By)[gc.heap.generation=gen0]"
    Gen1Frag            = "dotnet.gc.last_collection.heap.fragmentation.size (By)[gc.heap.generation=gen1]"
    Gen2Frag            = "dotnet.gc.last_collection.heap.fragmentation.size (By)[gc.heap.generation=gen2]"
    LohFrag             = "dotnet.gc.last_collection.heap.fragmentation.size (By)[gc.heap.generation=loh]"
    # CPU
    CpuUser             = "dotnet.process.cpu.time (s / 1 sec)[cpu.mode=user]"
    CpuSystem           = "dotnet.process.cpu.time (s / 1 sec)[cpu.mode=system]"
    CpuCount            = "dotnet.process.cpu.count ({cpu})"
    # Thread pool
    ThreadPoolThreads   = "dotnet.thread_pool.thread.count ({thread} / 1 sec)"
    ThreadPoolWorkItems = "dotnet.thread_pool.work_item.count ({work_item} / 1 sec)"
    ThreadPoolQueue     = "dotnet.thread_pool.queue.length ({work_item} / 1 sec)"
    # Contention
    LockContentions     = "dotnet.monitor.lock_contentions ({contention} / 1 sec)"
    # Exceptions
    ExFileNotFound      = "dotnet.exceptions ({exception} / 1 sec)[error.type=FileNotFoundException]"
    ExConnReset         = "dotnet.exceptions ({exception} / 1 sec)[error.type=ConnectionResetException]"
}

$b = @{}
$c = @{}
foreach ($key in $counters.Keys) {
    $minVal = if ($key -eq "AllocRate") { 1MB } else { 0 }
    $b[$key] = Get-Stats $base $counters[$key] $minVal
    $c[$key] = Get-Stats $comp $counters[$key] $minVal
}

# ── Build report ──────────────────────────────────────────────────────────────

$baselineName   = [System.IO.Path]::GetFileNameWithoutExtension($BaselineFile)
$comparisonName = [System.IO.Path]::GetFileNameWithoutExtension($ComparisonFile)
$outputFile     = "$baselineName-vs-$comparisonName-analysis.md"
$timestamp      = (Get-Date).ToString("yyyy-MM-dd HH:mm")

$lines = [System.Collections.Generic.List[string]]::new()

$lines.Add("# Performance Analysis: $baselineName vs $comparisonName")
$lines.Add("")
$lines.Add("_Generated: ${timestamp}_")
$lines.Add("")
$lines.Add("---")
$lines.Add("")

# ── Summary table ─────────────────────────────────────────────────────────────

$lines.Add("## Summary")
$lines.Add("")
$lines.Add("| Metric | $baselineName | $comparisonName | Diff |")
$lines.Add("|--------|$(("-" * ($baselineName.Length + 2)))|$(("-" * ($comparisonName.Length + 2)))|------|")
$lines.Add("| **Peak committed heap** | $(Format-Bytes $b.CommittedSize.Max) | $(Format-Bytes $c.CommittedSize.Max) | $(Format-Diff $b.CommittedSize.Max $c.CommittedSize.Max) |")
$lines.Add("| **Peak working set** | $(Format-Bytes $b.WorkingSet.Max) | $(Format-Bytes $c.WorkingSet.Max) | $(Format-Diff $b.WorkingSet.Max $c.WorkingSet.Max) |")
$lines.Add("| **Alloc rate avg (under load)** | $(Format-Bytes $b.AllocRate.Avg)/s | $(Format-Bytes $c.AllocRate.Avg)/s | $(Format-Diff $b.AllocRate.Avg $c.AllocRate.Avg) |")
$lines.Add("| **Alloc rate peak** | $(Format-Bytes $b.AllocRate.Max)/s | $(Format-Bytes $c.AllocRate.Max)/s | $(Format-Diff $b.AllocRate.Max $c.AllocRate.Max) |")
$lines.Add("| **Gen0 collections (total)** | $($b.Gen0.Sum) | $($c.Gen0.Sum) | $(Format-Diff $b.Gen0.Sum $c.Gen0.Sum) |")
$lines.Add("| **Gen1 collections (total)** | $($b.Gen1.Sum) | $($c.Gen1.Sum) | $(Format-Diff $b.Gen1.Sum $c.Gen1.Sum) |")
$lines.Add("| **Gen2 collections (total)** | $($b.Gen2.Sum) | $($c.Gen2.Sum) | $(Format-Diff $b.Gen2.Sum $c.Gen2.Sum) |")
$lines.Add("| **GC pause time avg** | $(Format-Round $b.PauseTime.Avg 4) s/s | $(Format-Round $c.PauseTime.Avg 4) s/s | $(Format-Diff $b.PauseTime.Avg $c.PauseTime.Avg) |")
$lines.Add("| **CPU user avg** | $(Format-Cpu $b.CpuUser.Avg $b.CpuCount.Max) | $(Format-Cpu $c.CpuUser.Avg $c.CpuCount.Max) | $(Format-Diff $b.CpuUser.Avg $c.CpuUser.Avg) |")
$lines.Add("| **Lock contentions avg** | $(Format-Round $b.LockContentions.Avg 1)/s | $(Format-Round $c.LockContentions.Avg 1)/s | $(Format-Diff $b.LockContentions.Avg $c.LockContentions.Avg) |")
$lines.Add("| **Thread pool work items avg** | $(Format-Round $b.ThreadPoolWorkItems.Avg 0)/s | $(Format-Round $c.ThreadPoolWorkItems.Avg 0)/s | $(Format-Diff $b.ThreadPoolWorkItems.Avg $c.ThreadPoolWorkItems.Avg) |")
$lines.Add("| **Peak POH size** | $(Format-Bytes $b.PohSize.Max) | $(Format-Bytes $c.PohSize.Max) | $(Format-Diff $b.PohSize.Max $c.PohSize.Max) |")
$lines.Add("")

# ── Highlights ────────────────────────────────────────────────────────────────

$lines.Add("## Highlights")
$lines.Add("")

$allocDiff  = if ($b.AllocRate.Avg -gt 0)      { [math]::Round((($c.AllocRate.Avg - $b.AllocRate.Avg) / $b.AllocRate.Avg) * 100, 1) } else { 0 }
$gen0Diff   = if ($b.Gen0.Sum -gt 0)            { [math]::Round((($c.Gen0.Sum - $b.Gen0.Sum) / $b.Gen0.Sum) * 100, 1) } else { 0 }
$heapDiff   = if ($b.CommittedSize.Max -gt 0)   { [math]::Round((($c.CommittedSize.Max - $b.CommittedSize.Max) / $b.CommittedSize.Max) * 100, 1) } else { 0 }
$cpuDiff    = if ($b.CpuUser.Avg -gt 0)         { [math]::Round((($c.CpuUser.Avg - $b.CpuUser.Avg) / $b.CpuUser.Avg) * 100, 1) } else { 0 }
$pauseDiff  = if ($b.PauseTime.Avg -gt 0)       { [math]::Round((($c.PauseTime.Avg - $b.PauseTime.Avg) / $b.PauseTime.Avg) * 100, 1) } else { 0 }

function Bullet([double]$pct, [string]$metric, [string]$baseline, [string]$comparison) {
    $sign  = if ($pct -lt 0) { "" } else { "+" }
    $emoji = if ($pct -lt 0) { "✅" } else { "⚠️" }
    return "- $emoji **$metric**: $sign$pct% ($baseline -> $comparison)"
}

$lines.Add($(Bullet $allocDiff "Allocation rate (avg under load)" (Format-Bytes $b.AllocRate.Avg) (Format-Bytes $c.AllocRate.Avg)))
$lines.Add($(Bullet $gen0Diff  "Gen0 GC collections" $b.Gen0.Sum $c.Gen0.Sum))
$lines.Add($(Bullet $heapDiff  "Peak committed heap" (Format-Bytes $b.CommittedSize.Max) (Format-Bytes $c.CommittedSize.Max)))
$lines.Add($(Bullet $cpuDiff   "CPU user time (avg)" (Format-Cpu $b.CpuUser.Avg $b.CpuCount.Max) (Format-Cpu $c.CpuUser.Avg $c.CpuCount.Max)))
$lines.Add($(Bullet $pauseDiff "GC pause time (avg)" (Format-Round $b.PauseTime.Avg 4) (Format-Round $c.PauseTime.Avg 4)))
$lines.Add("")

# ── Memory ────────────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Memory")
$lines.Add("")
$lines.Add("> **Committed heap vs working set:** Committed heap is purely managed memory owned by the")
$lines.Add("> .NET GC. Working set (what Task Manager shows) is larger and includes JIT-compiled code,")
$lines.Add("> Kestrel internals, ASP.NET Core middleware, thread stacks, and pinned buffers on top.")
$lines.Add(">")
$lines.Add("> A branch with fewer GC collections lets the heap grow longer between collections,")
$lines.Add("> which can make the *average* working set appear slightly higher even when *peak* is lower")
$lines.Add("> (a 1-second sampling artefact). Peak committed heap is the most reliable indicator.")
$lines.Add("")
$lines.Add("### Committed Heap Size")
$lines.Add("")
$lines.Add("The total memory committed by the .NET GC for managed objects. This is purely managed")
$lines.Add("memory and is independent of native allocations, JIT code, and thread stacks.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName |")
$lines.Add("|---|---|---|")
$lines.Add("| Peak | $(Format-Bytes $b.CommittedSize.Max) | $(Format-Bytes $c.CommittedSize.Max) |")
$lines.Add("| Average | $(Format-Bytes $b.CommittedSize.Avg) | $(Format-Bytes $c.CommittedSize.Avg) |")
$lines.Add("")
$lines.Add("### Process Working Set")
$lines.Add("")
$lines.Add("Total process memory as seen by the OS — equivalent to what Task Manager shows. This")
$lines.Add("includes committed heap plus native memory: JIT-compiled code, Kestrel internals,")
$lines.Add("ASP.NET Core middleware, thread stacks, and pinned buffers.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName |")
$lines.Add("|---|---|---|")
$lines.Add("| Peak | $(Format-Bytes $b.WorkingSet.Max) | $(Format-Bytes $c.WorkingSet.Max) |")
$lines.Add("| Average | $(Format-Bytes $b.WorkingSet.Avg) | $(Format-Bytes $c.WorkingSet.Avg) |")
$lines.Add("")
$lines.Add("### Pinned Object Heap (POH)")
$lines.Add("")
$lines.Add("Request body buffers pinned by Kestrel. Expected to be similar between branches.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName |")
$lines.Add("|---|---|---|")
$lines.Add("| Peak | $(Format-Bytes $b.PohSize.Max) | $(Format-Bytes $c.PohSize.Max) |")
$lines.Add("| Average | $(Format-Bytes $b.PohSize.Avg) | $(Format-Bytes $c.PohSize.Avg) |")
$lines.Add("")

# ── Allocations ───────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Allocations")
$lines.Add("")
$lines.Add("Allocation rate measured during active load only (rows > 1 MB/s).")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| Peak | $(Format-Bytes $b.AllocRate.Max)/s | $(Format-Bytes $c.AllocRate.Max)/s | $(Format-DiffCount $b.AllocRate.Max $c.AllocRate.Max) |")
$lines.Add("| Average | $(Format-Bytes $b.AllocRate.Avg)/s | $(Format-Bytes $c.AllocRate.Avg)/s | $(Format-DiffCount $b.AllocRate.Avg $c.AllocRate.Avg) |")
$lines.Add("| Samples | $($b.AllocRate.Count) | $($c.AllocRate.Count) | |")
$lines.Add("")

# ── GC ────────────────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Garbage Collection")
$lines.Add("")
$lines.Add("### Collection Counts")
$lines.Add("")
$lines.Add("Total number of GC collections recorded during the entire run.")
$lines.Add("")
$lines.Add("| Generation | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| Gen0 | $($b.Gen0.Sum) | $($c.Gen0.Sum) | $(Format-DiffCount $b.Gen0.Sum $c.Gen0.Sum) |")
$lines.Add("| Gen1 | $($b.Gen1.Sum) | $($c.Gen1.Sum) | $(Format-DiffCount $b.Gen1.Sum $c.Gen1.Sum) |")
$lines.Add("| Gen2 | $($b.Gen2.Sum) | $($c.Gen2.Sum) | $(Format-DiffCount $b.Gen2.Sum $c.Gen2.Sum) |")
$lines.Add("")
$lines.Add("> Fewer Gen0 collections indicates less short-lived object churn (closures, delegates).")
$lines.Add("> Gen2 collections are driven by long-lived infrastructure objects and are expected to be similar.")
$lines.Add("")
$lines.Add("### Pause Time")
$lines.Add("")
$lines.Add("Time the GC suspended all threads to collect. High values indicate GC pressure impacting latency.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| Peak | $(Format-Round $b.PauseTime.Max 4) s/s | $(Format-Round $c.PauseTime.Max 4) s/s | $(Format-DiffCount $b.PauseTime.Max $c.PauseTime.Max) |")
$lines.Add("| Average | $(Format-Round $b.PauseTime.Avg 4) s/s | $(Format-Round $c.PauseTime.Avg 4) s/s | $(Format-DiffCount $b.PauseTime.Avg $c.PauseTime.Avg) |")
$lines.Add("")
$lines.Add("### Heap Size by Generation (last collection)")
$lines.Add("")
$lines.Add("| Generation | $baselineName peak | $comparisonName peak |")
$lines.Add("|---|---|---|")
$lines.Add("| Gen0 | $(Format-Bytes $b.Gen0HeapSize.Max) | $(Format-Bytes $c.Gen0HeapSize.Max) |")
$lines.Add("| Gen1 | $(Format-Bytes $b.Gen1HeapSize.Max) | $(Format-Bytes $c.Gen1HeapSize.Max) |")
$lines.Add("| Gen2 | $(Format-Bytes $b.Gen2HeapSize.Max) | $(Format-Bytes $c.Gen2HeapSize.Max) |")
$lines.Add("| LOH  | $(Format-Bytes $b.LohHeapSize.Max) | $(Format-Bytes $c.LohHeapSize.Max) |")
$lines.Add("")
$lines.Add("### Heap Fragmentation (last collection)")
$lines.Add("")
$lines.Add("Fragmentation is unused space within heap segments. High fragmentation in LOH can indicate")
$lines.Add("large short-lived allocations (e.g. byte arrays > 85 KB) that are not being pooled.")
$lines.Add("")
$lines.Add("| Generation | $baselineName peak | $comparisonName peak |")
$lines.Add("|---|---|---|")
$lines.Add("| Gen0 | $(Format-Bytes $b.Gen0Frag.Max) | $(Format-Bytes $c.Gen0Frag.Max) |")
$lines.Add("| Gen1 | $(Format-Bytes $b.Gen1Frag.Max) | $(Format-Bytes $c.Gen1Frag.Max) |")
$lines.Add("| Gen2 | $(Format-Bytes $b.Gen2Frag.Max) | $(Format-Bytes $c.Gen2Frag.Max) |")
$lines.Add("| LOH  | $(Format-Bytes $b.LohFrag.Max) | $(Format-Bytes $c.LohFrag.Max) |")
$lines.Add("")

# ── CPU ───────────────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## CPU")
$lines.Add("")
$lines.Add("Shown as percentage of total machine capacity ($($b.CpuCount.Max.ToString('0')) logical cores).")
$lines.Add("User time is time spent executing managed code and GC. System time is OS-level work")
$lines.Add("(I/O syscalls, Kestrel network). Reducing allocations directly lowers user time by")
$lines.Add("reducing the amount of work the GC needs to do.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| User peak | $(Format-Cpu $b.CpuUser.Max $b.CpuCount.Max) | $(Format-Cpu $c.CpuUser.Max $c.CpuCount.Max) | $(Format-DiffCount $b.CpuUser.Max $c.CpuUser.Max) |")
$lines.Add("| User avg | $(Format-Cpu $b.CpuUser.Avg $b.CpuCount.Max) | $(Format-Cpu $c.CpuUser.Avg $c.CpuCount.Max) | $(Format-DiffCount $b.CpuUser.Avg $c.CpuUser.Avg) |")
$lines.Add("| System peak | $(Format-Cpu $b.CpuSystem.Max $b.CpuCount.Max) | $(Format-Cpu $c.CpuSystem.Max $c.CpuCount.Max) | $(Format-DiffCount $b.CpuSystem.Max $c.CpuSystem.Max) |")
$lines.Add("| System avg | $(Format-Cpu $b.CpuSystem.Avg $b.CpuCount.Max) | $(Format-Cpu $c.CpuSystem.Avg $c.CpuCount.Max) | $(Format-DiffCount $b.CpuSystem.Avg $c.CpuSystem.Avg) |")
$lines.Add("")

# ── Thread pool ───────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Thread Pool")
$lines.Add("")
$lines.Add("Work item throughput shows how many request-related tasks complete per second.")
$lines.Add("A longer queue means requests are waiting for a thread. Both are influenced by")
$lines.Add("how quickly individual requests complete — faster completion frees threads sooner.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| Work items peak | $(Format-Round $b.ThreadPoolWorkItems.Max 0)/s | $(Format-Round $c.ThreadPoolWorkItems.Max 0)/s | $(Format-DiffCount $b.ThreadPoolWorkItems.Max $c.ThreadPoolWorkItems.Max) |")
$lines.Add("| Work items avg | $(Format-Round $b.ThreadPoolWorkItems.Avg 0)/s | $(Format-Round $c.ThreadPoolWorkItems.Avg 0)/s | $(Format-DiffCount $b.ThreadPoolWorkItems.Avg $c.ThreadPoolWorkItems.Avg) |")
$lines.Add("| Queue length peak | $(Format-Round $b.ThreadPoolQueue.Max 0) | $(Format-Round $c.ThreadPoolQueue.Max 0) | $(Format-DiffCount $b.ThreadPoolQueue.Max $c.ThreadPoolQueue.Max) |")
$lines.Add("| Queue length avg | $(Format-Round $b.ThreadPoolQueue.Avg 0) | $(Format-Round $c.ThreadPoolQueue.Avg 0) | $(Format-DiffCount $b.ThreadPoolQueue.Avg $c.ThreadPoolQueue.Avg) |")
$lines.Add("| Thread count peak | $(Format-Round $b.ThreadPoolThreads.Max 0) | $(Format-Round $c.ThreadPoolThreads.Max 0) | $(Format-DiffCount $b.ThreadPoolThreads.Max $c.ThreadPoolThreads.Max) |")
$lines.Add("")

# ── Contention ────────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Lock Contentions")
$lines.Add("")
$lines.Add("Number of times a thread failed to acquire a monitor lock and had to wait.")
$lines.Add("Fewer allocations can reduce contention on GC-internal locks.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| Peak | $(Format-Round $b.LockContentions.Max 0)/s | $(Format-Round $c.LockContentions.Max 0)/s | $(Format-DiffCount $b.LockContentions.Max $c.LockContentions.Max) |")
$lines.Add("| Average | $(Format-Round $b.LockContentions.Avg 1)/s | $(Format-Round $c.LockContentions.Avg 1)/s | $(Format-DiffCount $b.LockContentions.Avg $c.LockContentions.Avg) |")
$lines.Add("| Total | $(Format-Round $b.LockContentions.Sum 0) | $(Format-Round $c.LockContentions.Sum 0) | $(Format-DiffCount $b.LockContentions.Sum $c.LockContentions.Sum) |")
$lines.Add("")

# ── Exceptions ────────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Exceptions")
$lines.Add("")
$lines.Add("ConnectionResetException is expected during disconnect tests. FileNotFoundException")
$lines.Add("is thrown by TusDiskStore when checking for partial upload files. Both should be")
$lines.Add("similar between branches — differences may indicate changed code paths.")
$lines.Add("")
$lines.Add("| | $baselineName | $comparisonName | Diff |")
$lines.Add("|---|---|---|---|")
$lines.Add("| ConnectionResetException total | $(Format-Round $b.ExConnReset.Sum 0) | $(Format-Round $c.ExConnReset.Sum 0) | $(Format-DiffCount $b.ExConnReset.Sum $c.ExConnReset.Sum) |")
$lines.Add("| FileNotFoundException total | $(Format-Round $b.ExFileNotFound.Sum 0) | $(Format-Round $c.ExFileNotFound.Sum 0) | $(Format-DiffCount $b.ExFileNotFound.Sum $c.ExFileNotFound.Sum) |")
$lines.Add("")

# ── Raw files ─────────────────────────────────────────────────────────────────

$lines.Add("---")
$lines.Add("")
$lines.Add("## Raw files")
$lines.Add("")
$lines.Add("| File | Description |")
$lines.Add("|---|---|")
$lines.Add("| ``$BaselineFile`` | Baseline measurements |")
$lines.Add("| ``$ComparisonFile`` | Comparison measurements |")
$lines.Add("")

# ── Write output ──────────────────────────────────────────────────────────────

$lines | Set-Content -LiteralPath $outputFile -Encoding UTF8

Write-Host ""
Write-Host "Report written to: $outputFile" -ForegroundColor Green
Write-Host ""

$allocDiffDisplay = if ($b.AllocRate.Avg -gt 0) { [math]::Round((($c.AllocRate.Avg - $b.AllocRate.Avg) / $b.AllocRate.Avg) * 100, 1) } else { 0 }
$gen0DiffDisplay  = if ($b.Gen0.Sum -gt 0) { [math]::Round((($c.Gen0.Sum - $b.Gen0.Sum) / $b.Gen0.Sum) * 100, 1) } else { 0 }
$heapDiffDisplay  = if ($b.CommittedSize.Max -gt 0) { [math]::Round((($c.CommittedSize.Max - $b.CommittedSize.Max) / $b.CommittedSize.Max) * 100, 1) } else { 0 }

Write-Host "=== Quick summary ===" -ForegroundColor Cyan
Write-Host "  Alloc rate avg:      $(Format-Bytes $b.AllocRate.Avg)/s -> $(Format-Bytes $c.AllocRate.Avg)/s  ($allocDiffDisplay%)"
Write-Host "  Gen0 collections:    $($b.Gen0.Sum) -> $($c.Gen0.Sum)  ($gen0DiffDisplay%)"
Write-Host "  Peak committed heap: $(Format-Bytes $b.CommittedSize.Max) -> $(Format-Bytes $c.CommittedSize.Max)  ($heapDiffDisplay%)"
Write-Host "  CPU user avg:        $(Format-Cpu $b.CpuUser.Avg $b.CpuCount.Max) -> $(Format-Cpu $c.CpuUser.Avg $c.CpuCount.Max)"
Write-Host ""
