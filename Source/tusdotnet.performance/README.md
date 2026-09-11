# tusdotnet.performance

A load-testing and counter-analysis toolkit for tusdotnet. Use it to measure memory usage, allocation rates, and GC behaviour under realistic upload load, and to compare two branches head-to-head.

---

## Contents

| File | Purpose |
|---|---|
| `Program.cs` | Load test client — fires concurrent uploads against a running tusdotnet server |
| `collect-counters.ps1` | Collects `dotnet-counters` metrics for a running server process into a CSV file |
| `analyze-counters.ps1` | Compares two CSV files and produces a Markdown analysis report |

---

## Prerequisites

```powershell
dotnet tool install --global dotnet-counters
```

---

## Workflow

The typical workflow when comparing two branches:

```
1. Start tusdotnet server (branch A)
2. `collect-counters.ps1 main`  ->  main.csv
3. Run tusdotnet.performance (load test)
4. Ctrl+C to stop collection once the load test is done

5. Switch to branch B, restart server
6. `collect-counters.ps1 feature`  ->  feature.csv
7. Run tusdotnet.performance again
8. Ctrl+C to stop collection

9. analyze-counters.ps1 -BaselineFile main.csv -ComparisonFile feature.csv
```

---

## Step 1 — Start a tusdotnet server

The load test points to `https://localhost:5011` by default (configurable in `Program.cs`). You can use any tusdotnet test site. A typical setup is one of the ASP.NET Core test sites in the solution.

Example using the ASP.NET Core test site:

```powershell
# From the repo root
dotnet run --project Source/TestSites/AspNetCore_netcoreapp10.0 -c Release
```

> Run with `-c Release` to avoid JIT overhead skewing the measurements.

If your server uses a different URL or port, update `SERVER_URL` in `Program.cs` before running the load test.

---

## Step 2 — Collect counters

Run `collect-counters.ps1` and pass the name you want for the output file (no extension):

```powershell
cd Source/tusdotnet.performance
.\collect-counters.ps1 -OutputFile main
```

The script will:

1. List all running .NET processes via `dotnet-counters ps`
2. Ask you to enter the PID of your tusdotnet server
3. Start collecting `System.Runtime` counters into `main.csv`

Leave this running while you execute the load test (Step 3).

---

## Step 3 — Run the load test

In a **separate terminal**, run the performance project:

```powershell
dotnet run --project Source/tusdotnet.performance -c Release
```

The default configuration uploads **10 MB files** in two halves (simulating a reconnect) using **50 concurrent clients** and **20 files per client**. You can adjust these constants at the top of `Program.cs`:

Wait until the load test prints `Test completed.`

---

## Step 4 — Stop collection

Switch back to the `collect-counters.ps1` terminal and press **Ctrl+C**. The CSV file is written to the current directory.

---

## Step 5 — Switch branch and repeat

Checkout the other branch, restart the server, and repeat Steps 2–4 with a different output file name:

```powershell
git checkout main           # or feature branch
dotnet run --project Source/TestSites/AspNetCore_netcoreapp10.0 -c Release

.\collect-counters.ps1 -OutputFile feature
# run load test again...
```

---

## Step 6 — Analyze and compare

```powershell
.\analyze-counters.ps1 -BaselineFile main.csv -ComparisonFile feature.csv
```

The script prints a quick summary to the console and writes a full Markdown report to:

```
$baselineName-vs-$comparisonName-analysis.md
```