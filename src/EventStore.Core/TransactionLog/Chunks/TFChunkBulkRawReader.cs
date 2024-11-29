// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Chunks;

public sealed class TFChunkBulkRawReader(TFChunk.TFChunk chunk, Stream streamToUse, bool isMemory)
	: TFChunkBulkReader(chunk, streamToUse, isMemory) {

	public override void SetPosition(long rawPosition) {
		if (rawPosition >= Stream.Length)
			throw new ArgumentOutOfRangeException("rawPosition",
				string.Format("Raw position {0} is out of bounds.", rawPosition));
		Stream.Position = rawPosition;

	}

	public override async ValueTask<BulkReadResult> ReadNextBytes(Memory<byte> buffer, CancellationToken token) {
		var oldPos = (int)Stream.Position;
		int bytesRead = await Stream.ReadAsync(buffer, token);
		return new(oldPos, bytesRead, isEof: Stream.Length == Stream.Position);
	}
}
