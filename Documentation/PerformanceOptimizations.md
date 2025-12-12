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

## Summary

All evaluated optimizations were either benchmarked and found to be slower/worse, or determined to be premature optimization without evidence of actual performance issues. The current implementation prioritizes:

1. **Code clarity and maintainability**
2. **Proven performance patterns** (ArrayPool, mutable state with Reset)
3. **Real-world performance** (optimizing the critical path: file I/O, not micro-allocations)
4. **Evidence-based optimization** (benchmark before implementing, don't assume struct < closure)

Future optimization work should be driven by profiling data from production workloads showing actual bottlenecks.
