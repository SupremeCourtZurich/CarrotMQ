# Changelog

All notable changes to the CarrotMQ packages are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- `PublishFailedException` with a `Reason` (`PublishFailureReason.Unknown`, `PublishFailureReason.Unreachable`), so a
  caller can tell an unreachable broker from a failed message.
- `RetryLimitExceededException` derives from `PublishFailedException`; its `Reason` is the cause of the last attempt.

### Fixed
- The message TTL and the caller's `CancellationToken` are honoured while waiting for the first connection, the
  channel lock and a publisher-confirm slot.
- No connection within `InitialConnectionTimeout` throws `PublishFailedException` with `Reason = Unreachable`.
- A response not published within the request's TTL, counted from its arrival (or, without a positive TTL, within 5 s
  after the handler ended), is dropped with a warning.

### ⚠ Breaking changes
- A publish cancelled by its TTL or the caller's token throws `OperationCanceledException` with a
  `PublishFailedException` (and its `Reason`) as `InnerException`, from `ICarrotClient`, `ITransport` and
  `IPublisherChannel` alike. It no longer throws `TaskCanceledException`.
- A publish without publisher confirms on a closed channel throws `PublishFailedException` (no longer RabbitMQ's
  `AlreadyClosedException`, which is now the `InnerException`).
- A request with a TTL ≤ 0 and no cancellable `CancellationToken` stops waiting for its reply after 5 s.
- `IResponseSender.TrySendResponseAsync(MiddlewareContext)` is now
  `TrySendResponseAsync(MiddlewareContext, CancellationToken)`; the token bounds the publish of the response.
- A handler cancelled by the request's TTL no longer sends a `504` or `500` response. A handler cancelled by
  `MessageProcessingTimeout` still does.

**Action:** replace `catch (TaskCanceledException)` with `catch (OperationCanceledException)` and
`catch (AlreadyClosedException)` with `catch (PublishFailedException)`; implementations of `IResponseSender` add the
`CancellationToken` parameter and publish within it.
