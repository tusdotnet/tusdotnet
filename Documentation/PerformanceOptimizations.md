# Performance Optimizations - Analysis & Decisions

This document tracks performance optimization ideas that were evaluated but not implemented, along with the rationale for each decision.

## 1. InMemoryFileLock: ConcurrentDictionary vs Lock + HashSet

**Proposed Change:**
Replace the current `lock + HashSet<string>` implementation with `ConcurrentDictionary<string, byte>` for lock-free operations.

**Benchmark Results:**
- Current approach (lock + HashSet): ~300-400 microseconds faster
- ConcurrentDictionary approach: Slightly slower due to allocation overhead

**Decision: NOT IMPLEMENTED**

**Rationale:**
- The performance difference (~300-400 microseconds) is negligible in the context of file upload operations
- File uploads involve disk I/O, network operations, and data processing measured in milliseconds to seconds
- The current implementation is simpler and easier to understand
- Lock contention is minimal as file locks are held briefly

**Benchmark Code:** Created and deleted during analysis

---

## 2. Validator/Requirement Allocation: Stateless Struct-Based Approach

**Proposed Change:**
Replace the current mutable `Requirement` base class pattern with a stateless approach:
- Return `ValidationResult` struct instead of mutating properties
- Cache static `Requirement` instances and arrays to avoid allocations per request

**Current Implementation:**
- Creates new `Requirement[]` arrays per request
- Creates new `Requirement` instances per request
- Uses mutable `StatusCode` and `ErrorMessage` properties
- Calls `Reset()` before each validation

**Benchmark Results:**

| Method | Mean | Allocated |
|--------|------|-----------|
| Current_GetFileInfo | 75.50 µs | 141 KB |
| Stateless_GetFileInfo | 73.31 µs | **234 KB (+66%)** |
| Current_CreateFile | 85.57 µs | 141 KB |
| Stateless_CreateFile | 70.78 µs | **234 KB (+66%)** |
| Current_WriteFile | 211.94 µs | 414 KB |
| Stateless_WriteFile | 240.85 µs | **781 KB (+89%)** |
| Current_Parallel (4 threads) | 130.97 µs | 142 KB |
| Stateless_Parallel (4 threads) | 146.35 µs | **235 KB (+65%)** |
| Current_Parallel (8 threads) | 109.08 µs | 142 KB |
| Stateless_Parallel (8 threads) | 128.56 µs | **236 KB (+66%)** |

**Decision: NOT IMPLEMENTED**

**Rationale:**
- **Worse Performance:** Stateless approach was 15-29 µs slower for most operations
- **Higher Memory Usage:** Allocated 66-89% more memory due to `ValidationResult` struct copying
- **Implementation Complexity:** Many requirements need constructor parameters (e.g., `UploadChecksum` takes `ChecksumHelper`, `UploadMetadata` takes `Action<>` callback), so arrays would still need to be created per request in many cases
- **Current Approach is Efficient:** Mutable property pattern with `Reset()` is actually quite performant

**Benchmark Code:** Created and deleted during analysis

---

## 3. Pre-allocated Byte Arrays for Error Messages

**Proposed Change:**
Pre-allocate static `byte[]` arrays for common error messages to avoid encoding on every error response.

**Current Implementation:**
```csharp
var bytes = ArrayPool<byte>.Shared.Rent(_utf8Encoding.GetByteCount(response.Message));
var byteCount = _utf8Encoding.GetBytes(response.Message, 0, response.Message.Length, bytes, 0);
await clientResponseStream.WriteAsync(bytes, 0, byteCount);
ArrayPool<byte>.Shared.Return(bytes);
```

**Decision: NOT IMPLEMENTED**

**Rationale:**
- **Already Optimized:** Current code uses `ArrayPool<byte>.Shared`, which minimizes allocations
- **Infrequent Errors:** Error responses are rare compared to successful upload operations
- **String Allocation Remains:** The `response.Message` string itself is allocated upstream regardless
- **Minimal Savings:** Would only save `GetByteCount()` + `GetBytes()` calls and ArrayPool overhead
- **Complexity vs Benefit:** Pre-allocating for all possible error messages adds complexity for negligible gain
- **No Telemetry Evidence:** No concrete data showing error responses cause GC pressure

**What Would Be Saved:**
- Skip UTF-8 encoding on each error
- Avoid ArrayPool rent/return overhead

**What Would NOT Be Saved:**
- The error message string allocation (happens in calling code)
- HTTP write operation overhead

**Conclusion:** Only worth implementing if telemetry shows error responses are causing measurable GC issues at scale.

---

## 4. Array Pool for 1-Byte Buffer in Creation-With-Upload

**Proposed Change:**
Use `ArrayPool<byte>.Shared` for the 1-byte buffer used to peek at the request stream.

**Current Implementation:**
```csharp
var buffer = new byte[1];
var hasData = await creationContext.Request.Body.ReadAsync(buffer, 0, 1, CancellationToken.None) > 0;
```

**Decision: NOT IMPLEMENTED**

**Rationale:**
- **Trivial Allocation:** It's literally 1 byte
- **Gen0 Collection:** Single-byte arrays are collected almost immediately in Gen0
- **ArrayPool Overhead:** Rent/return overhead likely exceeds the cost of allocating 1 byte
- **Code Clarity:** Current code is simple and obvious; ArrayPool would add noise

**Conclusion:** The optimization overhead would likely exceed the benefit. Not worth the complexity.

---

## 5. WriteMessageToStream Encoding Optimizations

**Proposed Changes:**
Three potential optimizations for the `WriteMessageToStream` method in `ResponseAdapterExtensions.cs`:

1. **stackalloc for small messages:** Use `stackalloc` for messages ≤256 bytes to avoid heap allocation
2. **Span-based encoding:** Use `Encoding.GetBytes(ReadOnlySpan<char>)` instead of array-based API (.NET Core 3.1+)
3. **Remove MemoryStream wrapper:** Call `WriteAsync` directly instead of `MemoryStream.CopyToAsync` in .NET Framework path

**Current Implementation:**
```csharp
var bytes = ArrayPool<byte>.Shared.Rent(_utf8Encoding.GetByteCount(response.Message));
var byteCount = _utf8Encoding.GetBytes(response.Message, 0, response.Message.Length, bytes, 0);
await clientResponseStream.WriteAsync(bytes, 0, byteCount);
ArrayPool<byte>.Shared.Return(bytes);
```

**Benchmark Results:**

| Method | Message Size | Mean | Ratio | Allocated |
|--------|-------------|------|-------|-----------|
| **Current** | 445 bytes | 183.69 ns | 1.00 | 0 B |
| WithStackalloc | 445 bytes | 154.93 ns | 0.90 | 0 B |
| WithSpanEncoding | 445 bytes | 143.90 ns | 0.83 | 0 B |
| WithoutMemoryStreamWrapper | 445 bytes | 148.95 ns | 0.82 | 0 B |
| Combined | 445 bytes | **139.59 ns** | **0.77** | 0 B |
|  |  |  |  |  |
| **Current** | 172 bytes | 133.79 ns | 1.00 | 0 B |
| WithStackalloc | 172 bytes | 122.57 ns | 0.92 | 192 B |
| WithSpanEncoding | 172 bytes | 124.33 ns | 0.94 | 0 B |
| WithoutMemoryStreamWrapper | 172 bytes | 146.55 ns | 1.11 | 0 B |
| Combined | 172 bytes | 132.43 ns | 1.00 | 192 B |
|  |  |  |  |  |
| **Current** | 56 bytes | 78.34 ns | 1.00 | 0 B |
| WithStackalloc | 56 bytes | 62.34 ns | 0.80 | 80 B |
| WithSpanEncoding | 56 bytes | 90.31 ns | **1.16** | 0 B |
| WithoutMemoryStreamWrapper | 56 bytes | 93.89 ns | **1.19** | 0 B |
| Combined | 56 bytes | 71.86 ns | 0.92 | 80 B |
|  |  |  |  |  |
| **Current** | 14 bytes | 74.33 ns | 1.00 | 0 B |
| WithStackalloc | 14 bytes | 59.61 ns | 0.80 | 40 B |
| WithSpanEncoding | 14 bytes | 99.53 ns | **1.35** | 0 B |
| WithoutMemoryStreamWrapper | 14 bytes | 111.39 ns | **1.51** | 0 B |
| Combined | 14 bytes | 76.22 ns | 1.03 | 40 B |

**Decision: NOT IMPLEMENTED**

**Rationale:**

1. **Error Path, Not Hot Path:** Error messages are written infrequently compared to successful upload operations
2. **Negligible Real-World Impact:** Savings of 10-60 nanoseconds are meaningless in HTTP response time context
3. **Mixed Results:** 
   - Span-based encoding is **slower** on small messages (15-51% slower on 14-56 byte messages)
   - stackalloc approach allocates heap memory for messages >256 bytes, defeating the purpose
   - Combined approach only wins on very large messages (>400 bytes), which are rare for error responses
4. **Current Implementation Already Good:** ArrayPool avoids allocations and is simple
5. **Typical Error Messages Are Small:** Most error messages (14-56 bytes) see no benefit or perform **worse** with these optimizations
6. **Complexity Cost:** Additional preprocessor directives and code paths for negligible gains

**Key Findings:**
- ✅ Large messages (445 bytes): Combined approach 24% faster
- ❌ Medium messages (56 bytes): Span encoding 15% **slower**, wrapper removal 20% **slower**
- ❌ Small messages (14 bytes): Span encoding 34% **slower**, wrapper removal 51% **slower**
- ❌ stackalloc allocates heap memory when message exceeds 256 bytes

**Conclusion:** The current ArrayPool approach strikes the right balance between performance, simplicity, and allocation avoidance. These micro-optimizations aren't worth the added complexity for an infrequent error path.

**Benchmark Code:** Created and deleted during analysis

---

## 6. ClientDisconnectGuard: Stateful Approach to Avoid Closure Allocations

**Proposed Change:**
Replace closure-based `Execute()` calls with stateful overloads that accept a state struct, avoiding closure allocations by passing captured variables explicitly through a struct parameter.

**Current Implementation:**
```csharp
// Closure captures: backingReader, cancellationToken, _emptySequence
return await _clientDisconnectGuard.Execute(
    guardFromClientDisconnect: async () => await _backingReader.ReadAsync(cancellationToken),
    getDefaultValue: () => new ReadResult(_emptySequence, isCanceled: true, isCompleted: false),
    cancellationToken
);
```

**Proposed Implementation:**
```csharp
// State struct explicitly passes captured variables
var state = new ReadState { BackingReader = _backingReader, CancellationToken = cancellationToken, EmptySequence = _emptySequence };
return await _clientDisconnectGuard.Execute<ReadState, ReadResult>(
    s => s.BackingReader.ReadAsync(s.CancellationToken),
    state,
    () => new ReadResult(state.EmptySequence, isCanceled: true, isCompleted: false),
    cancellationToken
);
```

**Benchmark Results:**

| Method | Mean | Ratio | Allocated | Ratio |
|--------|------|-------|-----------|-------|
| **Stream_ReadAsync_Closure** | 633.0 ns | 1.00 | 424 B | 1.00 |
| Stream_ReadAsync_Stateful | 583.5 ns | 0.92 | 360 B | 0.85 |
| **PipeReader_ReadAsync_Closure** | 225.1 ns | 1.00 | 256 B | 1.00 |
| PipeReader_ReadAsync_Stateful | 249.8 ns | **1.11** | 312 B | **1.22** |

**Decision: NOT IMPLEMENTED**

**Rationale:**

1. **Mixed Results:**
   - Stream: 8% faster, 15% less allocation (good improvement)
   - PipeReader: **11% SLOWER**, **22% MORE allocation** (worse in both metrics!)

2. **PipeReader Performance Degradation:**
   - The state struct overhead (creation, copying, passing) exceeds the closure allocation cost
   - Struct copying through async state machines adds more overhead than simple closure capture
   - ValueTask-based APIs are already highly optimized; struct passing disrupts that optimization

3. **Complexity vs Benefit:**
   - Requires defining state structs for every call site
   - Mixed results mean you have to benchmark each case individually
   - Stream improvements don't justify the complexity when PipeReader regresses

4. **Real-World Context:**
   - These nanosecond-level differences are negligible in actual upload operations
   - Network I/O, disk writes, and request processing dominate the time budget
   - Memory allocation patterns during uploads involve much larger buffers (KB-MB range)

5. **Code Maintainability:**
   - State struct approach is harder to read and understand
   - More code to maintain for negligible or negative real-world impact
   - Increases barrier to future changes

**Key Finding:**
Struct-based state passing is **not universally better** than closures. In fact, for lightweight async operations (like PipeReader), it's measurably worse. The JIT and runtime are already highly optimized for closure scenarios.

**Conclusion:**
Keep the simpler closure-based approach. The marginal gains on Stream operations don't justify introducing a pattern that makes PipeReader operations slower and allocates more memory.

---

## 7. FileStream Buffer Sizing: Matched vs Fixed 84KB Buffer

**Proposed Change:**
Dynamically size the FileStream buffer to match the application write buffer size (`Math.Min(_maxWriteBufferSize, 84KB)`) instead of using a fixed 84KB buffer, hypothesizing that aligned buffer sizes would reduce flush overhead.

**Current Implementation:**
```csharp
private static async Task FlushToDisk(this FileStream stream, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
{
    foreach (var segment in buffer)
    {
        await stream.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
    }
    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
}

// FileStream created with fixed 84KB buffer:
new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 84 * 1024, useAsync: true)
```

**Benchmark Results:**

| Write Buffer Size | FileStream Buffer | Mean (100MB) | Ratio | Allocated |
|------------------|-------------------|--------------|-------|-----------|
| 51,200 (50KB) | **84KB (Fixed)** | 174.57 ms | 1.00 | 40 KB |
| 51,200 (50KB) | 50KB (Matched) | 204.97 ms | **1.17** | 40 KB |
| 86,016 (84KB) | **84KB (Fixed)** | 158.47 ms | 1.00 | 40 KB |
| 86,016 (84KB) | 84KB (Matched) | 158.53 ms | 1.00 | 40 KB |
| 102,400 (100KB) | **84KB (Fixed)** | 187.17 ms | 1.00 | 40 KB |
| 102,400 (100KB) | 84KB (Matched) | 172.64 ms | 0.92 | 40 KB |

**Decision: NOT IMPLEMENTED**

**Rationale:**

1. **Mixed Results with High Variance:**
   - 51KB write buffer: Matched is **17% SLOWER** (significant regression)
   - 86KB write buffer: Essentially identical (0.04% difference is noise)
   - 102KB write buffer: Matched is 8% faster, but write buffer exceeds LOH threshold anyway

2. **No Consistent Improvement:**
   - Only shows benefit when write buffer exceeds FileStream buffer (102KB case)
   - But 102KB write buffer is already problematic (exceeds 85KB LOH threshold)
   - Default configuration (51KB) shows significant performance degradation

3. **FileStream Internals:**
   - FileStream auto-flushes when its internal buffer fills regardless of application buffer size
   - Matching buffer sizes doesn't eliminate flush operations, just changes their timing
   - Fixed 84KB buffer performs well across all tested write buffer sizes

4. **LOH Considerations:**
   - 84KB buffer stays under 85KB LOH threshold (critical for avoiding Gen2 allocations)
   - Matched buffer approach would vary buffer size, potentially crossing LOH boundary
   - Fixed buffer guarantees predictable allocation behavior

5. **Code Simplicity:**
   - Fixed buffer size is simpler and more predictable
   - No runtime calculation needed
   - Easier to reason about memory usage

**Key Finding:**
Application-level batching (via `_maxWriteBufferSize`) is the primary performance driver, not FileStream buffer size. The fixed 84KB buffer provides consistent, good performance across all write buffer configurations.

**Conclusion:**
Keep the fixed 84KB FileStream buffer. It's simple, stays under the LOH threshold, and performs consistently well. Matching buffer sizes adds complexity without meaningful benefit.

---

## 8. RandomAccess.WriteAsync Scatter-Gather I/O for PipeReader Segments

**Proposed Change:**
Use `RandomAccess.WriteAsync(SafeFileHandle, IReadOnlyList<ReadOnlyMemory<byte>>)` to write all PipeReader segments in a single vectored I/O operation instead of iterating segments and calling `FileStream.WriteAsync` for each one.

**Hypothesis:**
Scatter-gather I/O would reduce syscall overhead by writing all segments in one operation, potentially improving throughput for fragmented PipeReader buffers.

**Implementation Tested:**
```csharp
private static async Task FlushToDisk(this FileStream stream, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
{
    if (buffer.IsSingleSegment)
    {
        await stream.WriteAsync(buffer.First, cancellationToken).ConfigureAwait(false);
    }
    else
    {
        var position = stream.Position;
        var segments = buffer.ToArray();
        await RandomAccess.WriteAsync(stream.SafeFileHandle, segments, position, cancellationToken).ConfigureAwait(false);
        stream.Seek(position + buffer.Length, SeekOrigin.Begin);
    }
    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
}
```

**Benchmark Results:**

| Method | Write Buffer | Mean (25MB) | Ratio | Allocated | Ratio |
|--------|-------------|-------------|-------|-----------|-------|
| **Buffered Flush** | 51,200 | 59.22 ms | 1.00 | 10 KB | 1.00 |
| RandomAccess | 51,200 | 423.64 ms | **7.15** | 20 KB | **2.05** |
| **Buffered Flush** | 86,016 | 53.12 ms | 1.00 | 10 KB | 1.00 |
| RandomAccess | 86,016 | 277.51 ms | **5.22** | 50 KB | **5.13** |
| **Buffered Flush** | 102,400 | 68.52 ms | 1.00 | 10 KB | 1.00 |
| RandomAccess | 102,400 | 344.82 ms | **5.03** | 40 KB | **4.41** |

**Decision: NOT IMPLEMENTED (CATASTROPHIC FAILURE)**

**Rationale:**

1. **Devastating Performance Regression:**
   - **5-7x SLOWER** across all configurations
   - 51KB buffer: 715% slower (7.15x)
   - 86KB buffer: 522% slower (5.22x)
   - 102KB buffer: 503% slower (5.03x)

2. **Increased Memory Allocation:**
   - **2-5x MORE memory allocated**
   - `buffer.ToArray()` creates array of Memory<byte> for each segment
   - Additional allocations in RandomAccess implementation
   - Completely defeats optimization purpose

3. **Why It Failed So Badly:**
   - **Overhead Dominates:** RandomAccess vectored I/O has massive per-call overhead
   - **Fast Path Bypassed:** FileStream optimizations for sequential writes are highly tuned
   - **Segment Materialization:** Converting ReadOnlySequence to array is expensive
   - **Position Management:** Manual seek adds overhead vs FileStream's internal tracking
   - **No Benefit on Windows:** Modern async I/O is already efficient for sequential writes

4. **Fundamental Misunderstanding:**
   - Scatter-gather I/O is designed for scenarios with many small, scattered writes
   - File uploads are sequential, streaming workloads - not scattered
   - FileStream's segment iteration is negligible compared to I/O operation itself
   - The "problem" we were trying to solve (segment iteration overhead) doesn't actually exist

**Key Learning:**
The frequency of `WriteAsync` calls matters for application-level batching (which is why we buffer in PipeReader), but once you're calling FileStream, segment iteration overhead is completely negligible. RandomAccess.WriteAsync is optimized for different use cases (random access to large files, not streaming uploads).

**Conclusion:**
A spectacular failure. Keep the simple `foreach (var segment in buffer) await stream.WriteAsync(segment)` pattern. It's fast, allocates minimally, and leverages FileStream's optimizations. Sometimes the obvious solution is obvious because it's correct.

---

## Summary

All evaluated optimizations were either benchmarked and found to be slower/worse, or determined to be premature optimization without evidence of actual performance issues. The current implementation prioritizes:

1. **Code clarity and maintainability**
2. **Proven performance patterns** (ArrayPool, mutable state with Reset)
3. **Real-world performance** (optimizing the critical path: file I/O, not micro-allocations)
4. **Evidence-based optimization** (benchmark before implementing, don't assume struct < closure)

Notable catastrophic failures:
- **RandomAccess.WriteAsync scatter-gather I/O:** 5-7x slower with 2-5x more allocations
- **Matched FileStream buffers:** 17% slower for default configuration, no consistent benefit

Future optimization work should be driven by profiling data from production workloads showing actual bottlenecks. And maybe don't assume vectored I/O is a silver bullet without benchmarking first.
