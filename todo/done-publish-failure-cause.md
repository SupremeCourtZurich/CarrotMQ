---
title: publish-failure-cause
created: 2026-11-11T16:37:49+01:00
depends-on: []
---

# Transport-neutral cause for a failed confirmed publish

## Goal

A failed confirmed publish tells the caller why it failed, through a transport-neutral base exception:
`RetryLimitExceededException` derives from a new `PublishFailedException` that carries a `PublishFailureReason`
(`Unreachable`, `DestinationNotFound`, `AccessRefused`, `MessageRejected`, `Unknown`), mapped from AMQP reply codes
only. The broker's reply text appears in the exception message for humans; the transport's own exception (RabbitMQ:
`AlreadyClosedException`) is the `InnerException`. Purely additive: same exception type thrown, same retries, same
timing. Deciding which messages to retry, count or give up is the caller's job (e.g. APG's outbox).

## Constraints (from the maintainer)

1. No compatibility break of any kind: binary, source, or observable behaviour for existing callers.
2. Open-source contribution: surgical, small diff.
3. No bulky doc comments: one-line `<summary>` per public member, nothing more.
4. The reason is on the thrown exception itself; transport details at most one `InnerException` down.
5. Rely only on protocol-defined data (AMQP reply codes, close initiator). Reply text is informational: put it in the
   exception message, never parse or match it.

## Plan

Decided 2026-11-16 (user): (1) a publish honours the caller's token while the channel recovers; (2) a refused
message fails as soon as the recovery channel identifies it, as the default, replacing the quarantine. (2) is a
behaviour change: a message whose exchange is declared later no longer succeeds on a retry. State it in docs and PR.

### Core (`CarrotMQ.Core/`) — tests first

- [x] `Protocol/PublishFailureReason.cs`: `public enum PublishFailureReason { Unknown, Unreachable, DestinationNotFound, AccessRefused, MessageRejected }`.
- [x] `Protocol/PublishFailedException.cs`: `public class PublishFailedException : Exception` (not sealed) with
      `Reason` (get-only, default `Unknown`) and ctors `()`, `(message)`, `(message, inner)`, `(message, reason, inner?)`.
- [x] `Protocol/RetryLimitExceededException.cs`: base `PublishFailedException`; existing ctors unchanged (`Unknown`);
      add `(message, reason, inner?)`. Stays `sealed`.
- [x] `Common/AsyncLock.cs`: add `LockAsync(CancellationToken)` overload (additive).
- [x] `CarrotMQ.Core.Test`: hierarchy and legacy ctors give `Unknown`; `LockAsync(token)` cancels while held.

### RabbitMQ (`CarrotMQ.RabbitMQ/Connectivity/`) — tests first

- [x] Internal mapping `ShutdownEventArgs` → reason by protocol data only: refused publish (Peer, 60/40) by reply code
      404 → `DestinationNotFound`, 403 → `AccessRefused`, 406 → `MessageRejected`, other code → `Unknown`; a close not
      initiated by the broker, or 320 / 541 → `Unreachable`; anything else → `Unknown`.
- [x] `PublishRecoveryChannel`: remove the quarantine `SerialPublisher`; a refused message fails at once with
      `RetryLimitExceededException(text, reason, AlreadyClosedException)`. `text` names exchange, routing key, reply
      code and reply text. Nack / timeout / other close keep counting toward `RetryLimit`; on exhaustion throw with
      the last recorded reason.
- [x] `PublisherConfirmMessage`: internal `LastReason` / `LastError`, set on every failed attempt (close mapped as
      above, nack → `Unknown`, publish on a closed channel → mapped from the exception's shutdown reason).
- [x] `PublisherConfirmChannel.PublishWithRetryCheckAsync`: throw with `LastReason` / `LastError` and a text with the
      retry limit and, if a close was recorded, its reply code and text.
- [x] `PublisherConfirmChannel.PublishAsync`: wait for `ChannelLock` with the caller's token.

### Tests

- [x] Update the unit and integration tests that assume quarantine retries (recovery tests, `PublisherConfirmChannelTests`
      refusal tests, `PublishRefusalTest` exchange-declared-while-retrying and quarantined-query tests).
- [x] Unit: refusal 404 / 403 / 406 (DataRow) → fails on first identification with matching `Reason`, message text
      and inner `AlreadyClosedException`; the other messages are confirmed.
- [x] Unit: nack until the limit → `Unknown`; connection-level close then limit → `Unreachable`.
- [x] Unit: outage (recovery loop holds `ChannelLock`) → publish with a cancelled token completes with
      `OperationCanceledException` on time.
- [x] Integration: refused query/event → `RetryLimitExceededException` with `DestinationNotFound` / `AccessRefused`.

### Docs and release

- [x] `publisher_confirms.md`: refused publishes fail fast; `catch (PublishFailedException ex)` / `ex.Reason`;
      outage → cancellation on the caller's TTL/token; behaviour change note.
- [x] `Documentation/docs/guides/error_handling.md`: one row for `PublishFailedException`.
- [x] Update the design page artifact (quarantine removed, reason on the exception).
- [x] Build Release, run unit + integration tests (`verify` skill).

## Notes

### How the confirm flow correlates messages and acks (v1.3.0)

All of it is in `CarrotMQ.RabbitMQ`; Core only defines `ITransport.SendAsync` (a `Task`) and the exception.

1. `RabbitTransport.SendAsync` → one shared confirm channel per `BrokerConnection` (`BrokerConnection.cs:201-221`) →
   `PublisherConfirmChannel.PublishAsync` (`PublisherConfirmChannel.cs:106-137`): waits on the
   `MaxConcurrentConfirms` semaphore, wraps the message with a `TaskCompletionSource` in a `PublisherConfirmMessage`,
   publishes under `ChannelLock`, then awaits the TCS. One `SendAsync` = one TCS.
2. `PublishInternallyAsync` (`:139-166`): takes the channel's next publish sequence number, stores the message in
   `_outstandingConfirms[seqNo]`, calls `BasicPublishAsync`. Client-side confirm tracking of RabbitMQ.Client 7 is off
   (`CreateChannelOptions(true, false, …)`, `:61`); CarrotMQ tracks confirms itself.
3. The broker acks with `deliveryTag == seqNo` (per channel, from 1), `multiple` = everything up to the tag.
   `BasicAcksAsync` (`:192-221`) removes the entries and completes their TCS. `BasicNacksAsync` (`:223-253`) moves them
   to `_republishQueue`.
4. A timer (`RepublishEvaluationIntervalInMs`, 100 ms) drains `_republishQueue` and republishes the oldest
   outstanding messages older than `RetryIntervalInMs` (`:288-350`) with a new seqNo; each republish increments
   `RepublishCount`, beyond `RetryLimit` the TCS fails with `RetryLimitExceededException` (`:303-311`).
5. A channel close (≥400) recreates the channel (`CarrotChannel.cs:263-305`); `FlagAllMessagesForRepublishAsync`
   (`:272-286`) resets the seqNo to 1 and moves every outstanding message to `_republishQueue`.

So the message ↔ ack correlation is exact (seqNo), but a channel close carries no seqNo: the only protocol-level
grouping is "outstanding on the channel when it closed". That set is what this plan tags with the reason.

### Verified claims (v1.3.0)

| Claim from the task | Verdict | Where |
|---|---|---|
| `SendAsync` picks the confirm channel when `PublisherConfirm` | true | `RabbitTransport.cs:45` |
| `AlreadyClosedException` is only logged, message stays outstanding | true | `PublisherConfirmChannel.cs:162-165` |
| Republished up to `RetryLimit`, then cause-less exception | true | `PublisherConfirmChannel.cs:303-311`, `PublisherConfirmOptions.cs:17` (100), `:24` (1000 ms) |
| `RetryLimit` is connection-wide | true | `BrokerConnection.cs:213` |
| Channel close ≥400 → recovery → every outstanding message requeued | true | `CarrotChannel.cs:263-305`, `PublisherConfirmChannel.cs:181`, `:272-286` |
| Reply text only logged | true | `CarrotChannel.cs:267` |
| `CheckExchangeAsync` exists but is unreachable | true | `CarrotChannel.cs:188` (class internal) |
| One bad message burns the others' retries | **true, worse: they fail** | each close requeues every outstanding message in seqNo order (`:272-286`) and the timer republishes them in one round (`:292-295`); the broker discards everything after the failing publish, so valid messages behind the bad one fail with `RetryLimitExceededException`. Proven by `PublisherConfirmStarvationTest` (RabbitMQ 4.2, passes 4/4 runs on net10.0). |
| A subclass of `RetryLimitExceededException` can carry the cause | **false** | it is `sealed` (`RetryLimitExceededException.cs:9`); a new *base* class is used instead |
| No way to bound a single publish | **false** | caller's `CancellationToken`; `CarrotClient.SendAsync` applies `MessageProperties.Ttl` via `CancelAfter` (`CarrotClient.cs:183-187`) |
| Non-confirm channel: next publish may get `AlreadyClosedException` with the earlier 404 | plausible, not changed | `PublisherChannel.cs:25-40` has no catch |

`RabbitTransport` is the only production `ITransport` (`ITransport.cs:14`). Core has no `InternalsVisibleTo` for
`CarrotMQ.RabbitMQ`, hence the public `(message, reason, inner?)` ctor. `RabbitMQ.Client` 7.2.2 has
`AlreadyClosedException(ShutdownEventArgs)`.

### Decisions on the proposed improvements

1. **Public `ExchangeExistsAsync` on `IBrokerConnection` — rejected.** A new interface member breaks implementers
   (netstandard2.0, no default interface members). `IBrokerConnection.CurrentConnection` already allows a passive
   declare on the caller's own channel.
2. **Fail fast on 404/403/406 — accepted 2026-11-16 as the default** (was rejected: behaviour change, no
   attribution). The recovery channel now attributes the refusal to one message; the user accepts the behaviour change.
3. **Cause on the exception — accepted, as a base class** with a reason mapped from reply codes.
4. **Per-publish bound — rejected, already exists** (cancellation token / `MessageProperties.Ttl`). Known gap: a
   cancelled publish surfaces `TaskCanceledException` without the cause.
5. **Attributing a close to one message — rejected.** `channel.close` has no delivery tag; matching the exchange in
   the reply text is not protocol. The reason applies to every message outstanding at the close; the reply text is in
   the message for humans. Sequence-number order (confirms are asynchronous), a passive declare per publish
   (round trip) and isolating suspects on republish (invasive) were also rejected.

### Why inserting a base class is compatible

Per the .NET library breaking-change rules, introducing a new base class is allowed as long as the type keeps every
type it had in its hierarchy: `RetryLimitExceededException` is still an `Exception`, keeps its name, namespace,
`sealed` modifier and all ctors. `catch (RetryLimitExceededException)` and `catch (Exception)` behave as before;
binaries compiled against 1.3.0 run unchanged. Only reflection on `BaseType` would notice.

### Design rules for the generic type

- **Causes, not retry policy.** `DestinationNotFound`, not `Permanent`: an exchange may be declared a minute later.
- **`Unknown` is a valid answer** (nack, no confirm and no close, legacy ctors).
- **Minimal enum.** Add values only when a transport needs them.
- **Scope: confirmed publishes only.** `BrokerUnreachableException` from the initial connect propagates unchanged
  (RabbitMQ.Client type, cannot derive). Serialization failures stay `CarrotSerializerException`.

### Compatibility

- Thrown type, timing, retries, delivery: unchanged.
- Observable differences: `ex.Message` becomes descriptive text instead of the default; `InnerException` can be
  non-`null`; `typeof(RetryLimitExceededException).BaseType` is `PublishFailedException`.
- Mixed versions: new `CarrotMQ.Core` under binaries built against 1.3.0 (old `CarrotMQ.RabbitMQ`, app code) runs
  unchanged: same type name and ctors, `catch` and `newobj` resolve by name, the old transport throws with
  `Reason = Unknown`. The reverse (new `CarrotMQ.RabbitMQ`, old Core) would fail with `TypeLoadException`, but the
  `ProjectReference` makes the RabbitMQ package depend on Core ≥ its own version, so NuGet prevents it. Assemblies are
  not strong-named, so no binding-redirect concern on .NET Framework.

### Public-API diff

```csharp
namespace CarrotMQ.Core.Protocol;

+ public enum PublishFailureReason { Unknown, Unreachable, DestinationNotFound, AccessRefused, MessageRejected }

+ public class PublishFailedException : Exception
+ {
+     public PublishFailedException();
+     public PublishFailedException(string message);
+     public PublishFailedException(string message, Exception inner);
+     public PublishFailedException(string message, PublishFailureReason reason, Exception? inner = null);
+     public PublishFailureReason Reason { get; }
+ }

- public sealed class RetryLimitExceededException : Exception
+ public sealed class RetryLimitExceededException : PublishFailedException
  {
      // existing ctors unchanged
+     public RetryLimitExceededException(string message, PublishFailureReason reason, Exception? inner = null);
  }
```

### Release notes (draft)

> **Publisher confirms:** `RetryLimitExceededException` now derives from the new transport-neutral
> `PublishFailedException`, whose `Reason` (`Unreachable`, `DestinationNotFound`, `AccessRefused`, `MessageRejected`,
> `Unknown`) tells why the broker last refused or dropped the channel the message was waiting on. The broker's reply
> text is part of the exception message; the RabbitMQ exception is the `InnerException`. Additive only: same exception
> type thrown, no retry-behaviour or signature change.

### What APG could simplify once this ships (do not edit APG)

- `IsKnownTransient`: `catch (PublishFailedException ex)` → `Unreachable` / `Unknown` transient; `DestinationNotFound`
  / `AccessRefused` / `MessageRejected` mean "the broker refused a publish on this channel".
- `OutboxExchangeProbe`: still needed if the outbox must know that the 404 belongs to *this* message (CarrotMQ does
  not attribute). It only has to run on `DestinationNotFound`, never on an outage. The ~100 s latency remains unless
  APG lowers `RetryLimit` or bounds the send with its token.

### Found on the way: `CarrotHeader.MessageProperties` default contradicts its doc

`CarrotHeader.cs:89` says the default is `MessageProperties.Default`, but the property has no initializer, so a
`new CarrotHeader()` holds `default(MessageProperties)` with `PublisherConfirm = false` (the struct's initializers only
run through its constructor). `CarrotClient` is unaffected (`CarrotMessageBuilder.cs:186` sets `MessageProperties.Default`);
code that builds a `CarrotHeader` for `ITransport` itself silently publishes without confirms. Not fixed here: adding the
initializer changes behaviour for those callers. Report to the maintainer as a separate issue (doc fix or opt-in).

### Recovery channel

Implemented in commit 7f81bb2 ("Isolate refused publishes so they no longer fail the rest of the batch"): refused
publishes are isolated on a recovery channel and retried on a quarantine channel, which gives exact, protocol-only
attribution (`PublishRecoveryChannel.PublishAsync` sees the refusing close per message). With it, the reason here
can be the identified message's own instead of "last close observed"; revise the semantics above when implementing.
The 403 integration case is now possible: `RabbitApi.DeclareExchangeAsync(..., isInternal: true)`.

### Outage behaviour today (verified 2026-11-16, also on main)

- Connection lost (channel close ≥400, e.g. 541): `CarrotChannel.ChannelShutdownAsync` runs the recovery loop inside
  `ChannelLock` (`CarrotChannel.cs:290–307`) until the broker is reachable. `PublishAsync` waits for that lock without
  a token (`PublisherConfirmChannel.cs:130`; `AsyncLock.LockAsync()` has no token overload, `AsyncLock.cs:18`).
  Probe: publish with a token cancelled after 300 ms still pending after 4 s. `DisposeAsync` blocks the same way.
  The republish timer also needs `ChannelLock`, so no retries are counted: an outage produces no exception at all.
- Close below 400 (recovered by RabbitMQ.Client itself): publishes hit the closed channel and wait in
  `_publishedOnClosedChannel` until it is open (commit 9a86f76), without counting retries.
- First connect fails: `BrokerUnreachableException` (RabbitMQ.Client type) from `BrokerConnection.ConnectAsync`.
- So `Reason = Unreachable` on `RetryLimitExceededException` is not reached in an outage unless the lock/retry
  behaviour changes. Decision needed (see Plan).

### Worktree

Existing worktree `C:\workspace\CarrotMQ\improved-error-handling` (branch `xulrflo/improved-error-handling`). Do not
commit.
