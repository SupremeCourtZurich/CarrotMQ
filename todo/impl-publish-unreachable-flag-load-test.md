---
title: publish-unreachable-flag-load-test
created: 2026-11-16T17:18:35+01:00
depends-on: [publish-failure-cause]
---

# Unreachable at the TTL, refusal isolation behind a flag, load test

## Goal
A publish ends the same way whether or not a connection exists: `PublishFailedException(Unreachable/Unknown)` when the TTL
expires, `OperationCanceledException` when the caller cancels. The recovery channel moves behind a flag (default off), and a
load test reports flag-on / flag-off ratios.

## Plan
### 1. Unreachable at the end of the TTL (test-first)
- [x] Red: channel tests — token fires during an outage → `PublishFailedException(Unreachable)`, inner OCE; while waiting for the confirm on an open connection → `Unknown`; after a lost-connection republish → `Unreachable`
- [x] Red: `PublisherChannelTests` outage test → `Unreachable`
- [x] Red: new `RabbitTransportTests` — channel not available + token → `Unreachable`; `BrokerUnreachableException` → `PublishFailedException(Unreachable)`; same for `SendReceiveAsync`
- [x] Red: `CarrotClient` — caller token cancelled → OCE (inner `PublishFailedException`); TTL → `PublishFailedException`
- [x] Red: integration `BrokerUnreachableTest` — no connection: TTL → Unreachable, caller cancels → OCE, `InitialConnectionTimeout` → Unreachable, query TTL → Unreachable
- [x] Implement: `PublishFailure.Cancelled`, channels wrap token cancellation, `RabbitTransport` waits for the channel with the token, `CarrotClient` maps by token source
- [x] Docs: `ICarrotClient` exceptions, publisher_confirms.md outage + sample, error_handling.md, connection_lifecycle.md
### 2. Flag `PublisherConfirmOptions.IsolateRefusedPublishes` (default false)
- [x] Red: flag off → refused message retried on the main channel, fails after `RetryLimit` with `Unknown`, no recovery channel
- [x] Implement the switch in `FlagAllMessagesForRepublishAsync`; recovery tests and `PublishRefusalTest` opt in
- [x] Docs: options table, appsettings.md, Refused Publishes section (design artifact: ask first, it is shared live)
### 3. Load test `CarrotMQ.RabbitMQ.Test.Load`
- [x] Console/MSTest project, net10.0, `[TestCategory("Load")]`, excluded in CI
- [x] Two producer hosts (flag off/on), ABBA rounds after warm-up, scenarios 0 / 0.1 / 1 / 10 % refusals + requests 1 %
- [x] Report median and min–max ratios (throughput, p50, p99 of good messages) + absolute counts (good failed, channel closes)
### 4. Verify
- [x] /verify: Release build, inspectcode, all tests, fresh review

### 5. Remaining (2026-11-17)
- [x] Load run with default retries: `CARROTMQ_LOAD_TEST=1 CARROTMQ_LOAD_RETRY_LIMIT=100 CARROTMQ_LOAD_RETRY_INTERVAL_MS=1000` (~1 h; only when no other task is load testing in the sandbox)
- [x] Update design artifact https://claude.ai/artifact/ErrRQcsFMNcMskLcofyxmE (user approved): only the new change (exception interface, Unreachable at TTL, `IsolateRefusedPublishes` fail-fast), drop the quarantine approach, add the run results as charts relative to the baseline (retried = 1.0; ratios with min–max bars per run; lost messages as counts side by side; query losses by cause)
- [ ] Optional: rerun the query scenario against RabbitMQ.Client 7.3.0-beta.0; if the 541 crash persists, file an upstream issue with the three EventSource traces

## Notes
- Reason at cancellation: connection not open → `Unreachable`, otherwise the message's `LastFailureReason` (default `Unknown`).
- The channel cannot tell the TTL from the caller's token; `CarrotClient` can. Channels throw `PublishFailedException` with
  the OCE as inner; `CarrotClient` turns it back into OCE when the caller's token is cancelled.
- A request's reply wait after a confirmed publish stays OCE — publishing succeeded.
- Duplicate implementations were rejected: they need a test matrix. The flag has a single branch point.
- A connect timeout surfaces as `BrokerUnreachableException` or as `OperationCanceledException` from RabbitMQ.Client
  (seen at 10 s); `RabbitTransport` maps both to `Unreachable`.
- Load test runs only with `CARROTMQ_LOAD_TEST=1`; CI and plain `dotnet test` report it as skipped (Inconclusive).
- Load findings: without isolation, refusals fail most good events by RetryLimit (amplified by RetryLimit 5). With
  isolation, queries confirmed before a refusal close lose their reply (direct reply-to) and time out at the TTL.
- One good event was lost with isolation in the first smoke run (10 %, 200 events) before failures were recorded;
  not reproduced in 10 further rounds. Open.
- Load run 1 (RetryLimit 5, 100 ms; 2000 events / 500 queries, 4 rounds each), isolated ÷ retried, goodput / p50 / p99,
  good lost retried/isolated: none refused 0.91/1.10/1.16, 0/0; 0.1 % 3.05/0.75/12.31, 369/0; 1 % 65.7/0.31/16.81, 5923/0;
  10 % 249/0.32/21.83, 6997/0; queries 1 % 2.14/0.78/2.23, 1051/742. Query losses by cause (retried/isolated):
  RetryLimit 232/0, reply lost on close 480/325, TTL before confirm in the serial recovery queue 297/292, client crash 42/125.
- RabbitMQ.Client 7.2.2 closes the connection itself (541, Library) under query load with refusals: KeyNotFoundException in
  Connection.ProcessFrameAsync, NRE in Session.HandleFrameAsync. No upstream issue with that trace; 7.3.0-beta.0 notes
  related fixes (#1827, #2024, delivery to disposed channel no longer closes the connection). Unconfirmed.
- Review (author mode): fixed test-first — reason counts a closed channel while the connection is open; Try* completions on
  cancelled publishes (multiple ack); non-confirm publish on a closed channel → PublishFailedException (refusal close of
  another message → Unknown). Docs fixed. For the user: TTL expiry now throws PublishFailedException instead of OCE —
  needs a major version or a ⚠ release note; caller cancellation is now OperationCanceledException, not TaskCanceledException.
- Final state 2026-11-16: build ✓, inspectcode warnings only, Core 140 / unit 132 / integration 148 on net472/8/9/10. Nothing committed.
- Load run 2 (defaults: RetryLimit 100, 1 s), isolated ÷ retried, goodput / p50 / p99, good lost retried/isolated: none 0.92/1.11/1.15, 0/0;
  0.1 % 42.0/0.82/16.0, 396/0; 1 % 577/0.31/7.85, 5224/0; 10 % 3273/0.49/29.1, 7014/0; queries 1 % 16.3/0.78/13.9, 1482/270.
  A higher RetryLimit does NOT reduce baseline losses (more closes instead: 5880 vs 400 at 1 %). New: broker closed the
  connection once with 504 CHANNEL_ERROR expected 'channel.open' (baseline). Isolated queries: 0 TTL-in-queue losses this time,
  unlike run 1 (292); unexplained. Artifact version 6 published 2026-11-17 with both runs.
- 2026-11-17: proved test-first that CarrotMQ handed publishes to a channel it knew was closed (both publisher channels).
  Fix: confirm publishes on a closed channel go to _publishedOnClosedChannel without calling the client; non-confirm publishes
  wait for the reopen (CarrotChannel.NextReopenAsync, signalled by CreateChannelAsync and IConnection.RecoverySucceededAsync),
  honouring the token. Covers publishes started after the close is known; an in-flight publish and the client's own gap remain.
  ChannelSpy now orders by 'accepted' (sent or queued). Tests: Core 140, unit 136, integration 148, all frameworks.
- [ ] Optional: rerun the query load scenario to see whether 504/541 connection closes drop.
