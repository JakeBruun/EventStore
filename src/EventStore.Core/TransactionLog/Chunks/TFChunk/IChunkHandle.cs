// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.Buffers;
using DotNext.IO;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

/// <summary>
/// Represents a handle to access the underlying chunk physical storage.
/// </summary>
public interface IChunkHandle : IFlushable, IDisposable {

	ValueTask WriteAsync(ReadOnlyMemory<byte> data, long offset, CancellationToken token);

	ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token);

	/// <summary>
	/// Gets or sets the length of the data represented by this handle, in bytes.
	/// </summary>
	long Length {
		get;
		set;
	}

	/// <summary>
	/// Gets access mode for this handle.
	/// </summary>
	FileAccess Access { get; }

	ValueTask SetReadOnlyAsync(bool value, CancellationToken token);

	/// <summary>
	/// Creates an unbuffered stream for this handle.
	/// </summary>
	/// <returns>The unbuffered stream for this handle.</returns>
	Stream CreateStream() => CreateStream(this, 60_000);

	protected static Stream CreateStream(IChunkHandle handle, int synchronousTimeout)
		=> new UnbufferedStream(handle) { ReadTimeout = synchronousTimeout, WriteTimeout = synchronousTimeout };

	private sealed class UnbufferedStream(IChunkHandle handle) : RandomAccessStream {
		private int _readTimeout, _writeTimeout;
		private CancellationTokenSource _timeoutSource;

		public override void Flush() => handle.Flush();

		public override void SetLength(long value) => handle.Length = value;

		public override bool CanRead => handle.Access.HasFlag(FileAccess.Read);

		public override bool CanSeek => true;

		public override bool CanWrite => handle.Access.HasFlag(FileAccess.Write);

		public override bool CanTimeout => true;

		public override int WriteTimeout {
			get => _writeTimeout;
			set => _writeTimeout =
				value >= Timeout.Infinite ? value : throw new ArgumentOutOfRangeException(nameof(value));
		}

		public override int ReadTimeout {
			get => _readTimeout;
			set => _readTimeout =
				value >= Timeout.Infinite ? value : throw new ArgumentOutOfRangeException(nameof(value));
		}

		public override long Length => handle.Length;

		protected override void Write(ReadOnlySpan<byte> buffer, long offset) {
			// leave fast without sync over async
			if (buffer.IsEmpty)
				return;

			// Do sync over async without any optimizations to make it just works.
			// In practice, no one should call synchronous write
			var bufferCopy = buffer.Copy();
			var timeoutToken = GetTimeoutToken(WriteTimeout);
			var task = WriteAsync(bufferCopy.Memory, offset, timeoutToken).AsTask();
			try {
				task.Wait();
			} catch (AggregateException e) when (e.InnerExceptions is [OperationCanceledException canceledEx] &&
			                                     canceledEx.CancellationToken == timeoutToken) {
				throw new TimeoutException(e.Message, canceledEx);
			}
			finally {
				task.Dispose();
				ResetTimeout();
				bufferCopy.Dispose();
			}
		}


		protected override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken token)
			=> handle.WriteAsync(buffer, offset, token);

		protected override int Read(Span<byte> buffer, long offset) {
			// leave fast without sync over async
			int bytesRead;
			if (buffer.IsEmpty) {
				bytesRead = 0;
			} else {
				// Do sync over async without any optimizations to make it just works.
				// In practice, no one should call synchronous write
				var bufferCopy = Memory.AllocateExactly<byte>(buffer.Length);
				var timeoutToken = GetTimeoutToken(ReadTimeout);
				var task = ReadAsync(bufferCopy.Memory, offset, timeoutToken).AsTask();
				try {
					task.Wait();
					bytesRead = task.Result;
				} catch (AggregateException e) when (e.InnerExceptions is [OperationCanceledException canceledEx] &&
				                                     canceledEx.CancellationToken == timeoutToken) {
					throw new TimeoutException(e.Message, canceledEx);
				} finally {
					task.Dispose();
					ResetTimeout();
					bufferCopy.Dispose();
				}

				bufferCopy.Span.Slice(0, bytesRead).CopyTo(buffer);
			}

			return bytesRead;
		}

		protected override ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
			=> handle.ReadAsync(buffer, offset, token);

		private CancellationToken GetTimeoutToken(int timeout) {
			_timeoutSource ??= new();
			_timeoutSource.CancelAfter(timeout);
			return _timeoutSource.Token;
		}

		private void ResetTimeout() {
			// attempt to reuse the token source to avoid extra memory allocation
			if (!_timeoutSource.TryReset()) {
				_timeoutSource.Dispose();
				_timeoutSource = null;
			}
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				_timeoutSource?.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}
