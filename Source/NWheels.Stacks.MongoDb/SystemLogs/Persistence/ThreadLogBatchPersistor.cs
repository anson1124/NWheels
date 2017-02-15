﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NWheels.Extensions;
using NWheels.Logging;

namespace NWheels.Stacks.MongoDb.SystemLogs.Persistence
{
    internal class ThreadLogBatchPersistor
    {
        private readonly MongoDbThreadLogPersistor _owner;
        private readonly IReadOnlyThreadLog[] _threadLogs;
        private readonly Dictionary<string, DailySummaryRecord> _dailySummaryRecordById;
        private readonly List<LogMessageRecord> _logMessageBatch;
        private readonly List<ThreadLogRecord> _threadLogBatch;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ThreadLogBatchPersistor(MongoDbThreadLogPersistor owner, IReadOnlyThreadLog[] threadLogs)
        {
            _owner = owner;
            _threadLogs = threadLogs;
            _dailySummaryRecordById = new Dictionary<string, DailySummaryRecord>(capacity: 64);
            _logMessageBatch = new List<LogMessageRecord>(capacity: 200 * threadLogs.Length);
            _threadLogBatch = new List<ThreadLogRecord>(capacity: threadLogs.Length);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void PersistBatch(out TimeSpan dbTime)
        {
            var dbClock = new Stopwatch();

            foreach ( var log in _threadLogs )
            {
                VisitLogActivity(log.RootActivity);

                if ( ShouldPersistThreadLog(log) )
                {
                    _threadLogBatch.Add(new ThreadLogRecord(log));
                }
            }

            if ( _dailySummaryRecordById.Count > 0 )
            {
                var dailySummaryBulkWrite = BuildDailySummaryBulkWriteOperation();

                dbClock.Start();
                dailySummaryBulkWrite.Execute(WriteConcern.Acknowledged);
                dbClock.Stop();
            }

            if ( _logMessageBatch.Count > 0 )
            {
                dbClock.Start();
                _owner.LogMessageCollection.InsertBatch(_logMessageBatch, WriteConcern.Acknowledged);
                dbClock.Stop();
            }

            if ( _threadLogBatch.Count > 0 )
            {
                dbClock.Start();
                _owner.ThreadLogCollection.InsertBatch(_threadLogBatch, WriteConcern.Acknowledged);
                dbClock.Stop();

                foreach (var threadLog in _threadLogBatch)
                {
                    dbClock.Start();
                    _owner.ThreadLogGridfs.Upload(SerializeThreadLogSnapshot(threadLog.VolatileSnapshot), threadLog.LogId);
                    dbClock.Stop();
                }
            }

            dbTime = dbClock.Elapsed;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private Stream SerializeThreadLogSnapshot(ThreadLogSnapshot snapshot)
        {
            var stream = new MemoryStream();

            using (var writer = BsonWriter.Create(stream, new BsonBinaryWriterSettings() { CloseOutput = false }))
            {
                BsonSerializer.Serialize(writer, typeof(ThreadLogSnapshot), snapshot);
                writer.Flush();
            }

            stream.Position = 0;
            return stream;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private bool ShouldPersistThreadLog(IReadOnlyThreadLog log)
        {
            if ((log.RootActivity.Options & LogOptions.RetainThreadLog) != 0)
            {
                return true;
            }

            switch ( _owner.LoggingConfig.ThreadLogPersistenceLevel )
            {
                case ThreadLogPersistenceLevel.All:
                    return true;
                case ThreadLogPersistenceLevel.StartupShutdown:
                    return (
                        log.TaskType == ThreadTaskType.StartUp || 
                        log.TaskType == ThreadTaskType.ShutDown);
                case ThreadLogPersistenceLevel.StartupShutdownErrors:
                    return (
                        log.TaskType == ThreadTaskType.StartUp || 
                        log.TaskType == ThreadTaskType.ShutDown ||
                        log.RootActivity.Level >= LogLevel.Error);
                case ThreadLogPersistenceLevel.StartupShutdownErrorsWarnings:
                    return (
                        log.TaskType == ThreadTaskType.StartUp ||
                        log.TaskType == ThreadTaskType.ShutDown ||
                        log.RootActivity.Level >= LogLevel.Warning);
                case ThreadLogPersistenceLevel.StartupShutdownErrorsWarningsDuration:
                    return (
                        log.TaskType == ThreadTaskType.StartUp ||
                        log.TaskType == ThreadTaskType.ShutDown ||
                        log.RootActivity.Level >= LogLevel.Warning ||
                        log.RootActivity.MillisecondsDuration >= 1000);
                default:
                    return false;
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private BulkWriteOperation BuildDailySummaryBulkWriteOperation()
        {
            var bulkWrite = _owner.DailySummaryCollection.InitializeUnorderedBulkOperation();

            foreach ( var record in _dailySummaryRecordById.Values )
            {
                record.BuildIncrementUpsert(bulkWrite);
            }

            return bulkWrite;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void VisitLogActivity(ActivityLogNode activity)
        {
            VisitLogNode(activity);

            for ( var child = activity.FirstChild ; child != null ; child = child.NextSibling )
            {
                var childActivity = child as ActivityLogNode;

                if ( childActivity != null )
                {
                    VisitLogActivity(childActivity);
                }
                else
                {
                    VisitLogNode(child);
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void VisitLogNode(LogNode node)
        {
            if (ShouldCountMessageInSummary(node))
            {
                var summaryRecord = _dailySummaryRecordById.GetOrAdd(
                    DailySummaryRecord.GetRecordId(node), 
                    id => new DailySummaryRecord(node));

                summaryRecord.Increment(node);
            }

            if (ShouldPersistMessageDetails(node))
            {
                var messageRecord = new LogMessageRecord(node);
                _logMessageBatch.Add(messageRecord);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private static bool ShouldCountMessageInSummary(LogNode node)
        {
            return (
                node.IsImportant ||
                (node.Options & (LogOptions.CollectCount | LogOptions.CollectStats)) != 0 || 
                node.Options.HasAggregation());
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private static bool ShouldPersistMessageDetails(LogNode node)
        {
            return (
                node.IsImportant || 
                (node.Options & LogOptions.RetainDetails) != 0);
        }
    }
}