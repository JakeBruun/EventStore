// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext.IO;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace EventStore.Core.Services.Archive.Storage;

public class FileSystemReader : IArchiveStorageReader {
	protected static readonly ILogger Log = Serilog.Log.ForContext<FileSystemReader>();

	private readonly string _archivePath;
	private readonly Func<int?, int?, string> _getChunkPrefix;
	private readonly string _archiveCheckpointFile;
	private readonly FileStreamOptions _fileStreamOptions;

	public FileSystemReader(FileSystemOptions options, Func<int?, int?, string> getChunkPrefix, string archiveCheckpointFile) {
		_archivePath = options.Path;
		_getChunkPrefix = getChunkPrefix;
		_archiveCheckpointFile = archiveCheckpointFile;
		_fileStreamOptions = new FileStreamOptions {
			Access = FileAccess.Read,
			Mode = FileMode.Open,
			Options = FileOptions.Asynchronous,
		};
	}

	public ValueTask<long> GetCheckpoint(CancellationToken ct) {
		ValueTask<long> task;
		var handle = default(SafeFileHandle);
		try {
			var checkpointPath = Path.Combine(_archivePath, _archiveCheckpointFile);

			// Perf: we don't need buffered read for simple one-shot read of 8 bytes
			handle = File.OpenHandle(checkpointPath);

			Span<byte> buffer = stackalloc byte[sizeof(long)];
			if (RandomAccess.Read(handle, buffer, fileOffset: 0L) != buffer.Length)
				throw new EndOfStreamException();

			var checkpoint = BinaryPrimitives.ReadInt64LittleEndian(buffer);
			task = ValueTask.FromResult(checkpoint);
		} catch (FileNotFoundException) {
			task = ValueTask.FromResult(0L);
		} catch (Exception e) {
			task = ValueTask.FromException<long>(e);
		} finally {
			handle?.Dispose();
		}

		return task;
	}

	public ValueTask<Stream> GetChunk(string chunkFile, CancellationToken ct) {
		ValueTask<Stream> task;
		try {
			var chunkPath = Path.Combine(_archivePath, chunkFile);
			task = ValueTask.FromResult<Stream>(File.Open(chunkPath, _fileStreamOptions));
		} catch (FileNotFoundException) {
			task = ValueTask.FromException<Stream>(new ChunkDeletedException());
		} catch (Exception e) {
			task = ValueTask.FromException<Stream>(e);
		}

		return task;
	}

	public ValueTask<Stream> GetChunk(string chunkFile, long start, long end, CancellationToken ct) {
		var length = end - start;

		ValueTask<Stream> task;
		if (length < 0) {
			task = ValueTask.FromException<Stream>(new InvalidOperationException(
				$"Attempted to read negative amount from chunk {chunkFile}. Start: {start}. End {end}"));
		} else {
			try {
				var chunkPath = Path.Combine(_archivePath, chunkFile);
				var fileStream = File.Open(chunkPath, _fileStreamOptions);
				var segment = new StreamSegment(fileStream, leaveOpen: false);
				segment.Adjust(start, length);
				task = ValueTask.FromResult<Stream>(segment);

			} catch (FileNotFoundException) {
				task = ValueTask.FromException<Stream>(new ChunkDeletedException());
			} catch (Exception e) {
				task = ValueTask.FromException<Stream>(e);
			}
		}

		return task;
	}

	public IAsyncEnumerable<string> ListChunks(CancellationToken ct) {
		return new DirectoryInfo(_archivePath)
			.EnumerateFiles($"{_getChunkPrefix(null, null)}*")
			.Select(chunk => chunk.Name)
			.Order()
			.ToAsyncEnumerable();
	}
}
