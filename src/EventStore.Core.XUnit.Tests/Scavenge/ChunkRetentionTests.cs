// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.XUnit.Tests.Scavenge.Infrastructure;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.Infrastructure.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class ChunkRetentionTests : SqliteDbPerTest<MaxAgeTests> {
	[Fact]
	public async Task chunks_get_deleted() {
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithArchive(chunksInArchive: 1, retainBytes: 20)
			.WithDb(x => x
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"),
					ScavengePointRec(t++, timeStamp: DateTime.UtcNow)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.AssertTrace(
				Tracer.Line("Accumulating from start to SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 0"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 0"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 1"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 1"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Calculating SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Calculating SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Executing chunks for SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Deleted Chunk 0-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 0"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Retained Chunk 1-1"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 1"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Merging chunks for SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Merging chunks for SP-0"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Executing index for SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing index for SP-0"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Cleaning for SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Cleaning for SP-0"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Cleaning for SP-0"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Begin"),
				Tracer.Line("    Checkpoint: Done SP-0"),
				Tracer.Line("Commit"))

			// keep all records and indexes
			.RunAsync(x => new[] {
				x.Recs[0],
				x.Recs[1],
			});
	}
}