﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using NUnit.Framework;
using NWheels.Logging;
using NWheels.Logging.Core;
using Shouldly;

namespace NWheels.UnitTests.Logging
{
    [TestFixture]
    public class ThreadLogTests : ThreadLogUnitTestBase
    {
        [Test]
        public void NewInstance_StartedAtUtc_EqualsUtcNow()
        {
            //-- Arrange

            var now = new DateTime(2014, 10, 15, 12, 30, 45);
            Framework.UtcNow = now;

            //-- Act

            var log = new ThreadLog(
                Framework, Clock, Registry, Anchor, ThreadTaskType.Unspecified, new FormattedActivityLogNode("Test"));

            //-- Assert

            Assert.That(log.ThreadStartedAtUtc, Is.EqualTo(now));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void NewInstance_LogId_IsNewGuid()
        {
            //-- Arrange

            var newGuid = new Guid("E690328B-994E-494F-B4F6-B317A0E2668B");
            Framework.PresetGuids.Enqueue(newGuid);

            //-- Act

            var log = new ThreadLog(
                Framework, Clock, Registry, Anchor, ThreadTaskType.Unspecified, new FormattedActivityLogNode("Test"));

            //-- Assert

            Assert.That(log.LogId, Is.EqualTo(newGuid));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void NewInstance_CorrelationId_EqualsLogId()
        {
            //-- Arrange

            var newGuid = new Guid("E690328B-994E-494F-B4F6-B317A0E2668B");
            Framework.PresetGuids.Enqueue(newGuid);

            //-- Act

            var log = new ThreadLog(
                Framework, Clock, Registry, Anchor, ThreadTaskType.Unspecified, new FormattedActivityLogNode("Test"));

            //-- Assert

            Assert.That(log.CorrelationId, Is.EqualTo(newGuid));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void NewInstance_TaskType_AsSpecified()
        {
            //-- Act

            var log = new ThreadLog(
                Framework, Clock, Registry, Anchor, ThreadTaskType.QueuedWorkItem, new FormattedActivityLogNode("Test"));

            //-- Assert

            Assert.That(log.TaskType, Is.EqualTo(ThreadTaskType.QueuedWorkItem));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void NewInstance_CurrentActivity_IsRootActivity()
        {
            //-- Arrange

            var rootActivity = new FormattedActivityLogNode("Root");

            //-- Act

            var log = new ThreadLog(
                Framework, Clock, Registry, Anchor, ThreadTaskType.Unspecified, rootActivity);

            //-- Assert

            Assert.That(log.RootActivity, Is.SameAs(rootActivity));
            Assert.That(log.CurrentActivity, Is.SameAs(rootActivity));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void AppendPlainNode_PlacedUnderCurrentActivity()
        {
            //-- Arrange

            var log = CreateThreadLog();

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void AppendActivityNode_NodeBecomesCurrentActivity()
        {
            //-- Arrange

            var log = CreateThreadLog();

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One;AI:Two{LI:Three}}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CloseActivityNode_ParentBecomesCurrentActivity()
        {
            //-- Arrange

            var log = CreateThreadLog();
            ActivityLogNode activity;

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(activity = new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            activity.Close();
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Four"));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One;AI:Two{LI:Three};LI:Four}"));
            Assert.That(log.CurrentActivity, Is.SameAs(log.RootActivity));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void AppendWarningLog_BubbleUpToParent()
        {
            //-- Arrange

            var log = CreateThreadLog();

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(new FormattedLogNode(LogLevel.Warning, "Four"));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One;AW:Two{LI:Three;LW:Four}}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CloseWarningActivity_BubbleUpToParent()
        {
            //-- Arrange

            var log = CreateThreadLog();
            ActivityLogNode activityTwo;
            ActivityLogNode activityFour;

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(activityTwo = new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(activityFour = new FormattedActivityLogNode("Four"));
            log.AppendNode(new FormattedLogNode(LogLevel.Warning, "Five"));

            activityFour.Close();
            var afterFourClosed = ToTestString(log.RootActivity);

            activityTwo.Close();
            var afterTwoClosed = ToTestString(log.RootActivity);

            //-- Assert

            Assert.That(afterFourClosed, Is.EqualTo("AI:Root{LI:One;AW:Two{LI:Three;AW:Four{LW:Five}}}"));
            Assert.That(afterTwoClosed, Is.EqualTo("AW:Root{LI:One;AW:Two{LI:Three;AW:Four{LW:Five}}}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void AppendErrorNodeWithException_SetParentActivityToFailure()
        {
            //-- Arrange

            var log = CreateThreadLog();

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(new FormattedActivityLogNode("Four"));
            log.AppendNode(new FormattedLogNode(LogLevel.Error, "Five", exception: new Exception()));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One;AI:Two{LI:Three;AEX:Four{LEX:Five}}}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CloseFailedActivity_BubbleFailureToParent()
        {
            //-- Arrange

            var log = CreateThreadLog();
            ActivityLogNode activityFour;

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(activityFour = new FormattedActivityLogNode("Four"));
            log.AppendNode(new FormattedLogNode(LogLevel.Error, "Five", exception: new Exception()));

            activityFour.Close();

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One;AEX:Two{LI:Three;AEX:Four{LEX:Five}}}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void AppendErrorNodeWithException_ClearFailure_FailureBubbledUpAsWarning()
        {
            //-- Arrange

            var log = CreateThreadLog();
            ActivityLogNode activityFour;

            //-- Act

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(activityFour = new FormattedActivityLogNode("Four"));
            log.AppendNode(new FormattedLogNode(LogLevel.Error, "Five", exception: new Exception()), clearFailure: true);

            activityFour.Close();

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AI:Root{LI:One;AW:Two{LI:Three;AW:Four{LEX:Five}}}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void ActivityNotClosed_Duration_FromThreadClock()
        {
            //-- Arrange

            var log = CreateThreadLog();
            ActivityLogNode activity;

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(activity = new FormattedActivityLogNode("Two"));

            //-- Act

            Clock.ElapsedMilliseconds = 123;

            var rootDuration1 = log.RootActivity.MillisecondsDuration;
            var childDuration1 = activity.MillisecondsDuration;

            Clock.ElapsedMilliseconds = 456;

            var rootDuration2 = log.RootActivity.MillisecondsDuration;
            var childDuration2 = activity.MillisecondsDuration;

            //-- Assert

            Assert.That(rootDuration1, Is.EqualTo(123));
            Assert.That(childDuration1, Is.EqualTo(123));

            Assert.That(rootDuration2, Is.EqualTo(456));
            Assert.That(childDuration2, Is.EqualTo(456));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void ActivityClosed_Duration_Fixed()
        {
            //-- Arrange

            var log = CreateThreadLog();
            ActivityLogNode activity;

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(activity = new FormattedActivityLogNode("Two"));

            //-- Act

            Clock.ElapsedMilliseconds = 123;

            activity.Close();

            Clock.ElapsedMilliseconds = 456;

            var rootDuration = log.RootActivity.MillisecondsDuration;
            var childDuration = activity.MillisecondsDuration;

            //-- Assert

            Assert.That(rootDuration, Is.EqualTo(456));
            Assert.That(childDuration, Is.EqualTo(123));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void ThreadLogSnapshot_Serialize_Deserialize()
        {
            //-- Arrange

            var log = CreateThreadLog();

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(new FormattedActivityLogNode("Four"));
            log.AppendNode(new FormattedLogNode(LogLevel.Error, "Five", exception: new Exception()));

            var originalSnapshot = log.TakeSnapshot();
            var serializer = new DataContractSerializer(typeof(ThreadLogSnapshot));
            
            //-- Act

            var output1 = new StringBuilder();
            var writer1 = XmlWriter.Create(output1, new XmlWriterSettings { Indent = true, IndentChars = "\t" });
            serializer.WriteObject(writer1, originalSnapshot);
            writer1.Flush();

            var reader = XmlReader.Create(new StringReader(output1.ToString()));
            var deserializedSnapshot = serializer.ReadObject(reader);

            var output2 = new StringBuilder();
            var writer2 = XmlWriter.Create(output2, new XmlWriterSettings { Indent = true, IndentChars = "\t" });
            serializer.WriteObject(writer2, originalSnapshot);
            writer2.Flush();

            //-- Assert

            Console.WriteLine(output1.ToString());
            Assert.That(output2.ToString(), Is.EqualTo(output1.ToString()));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void NewInstance_AddedToThreadRegistry()
        {
            //-- Act

            var log = CreateThreadLog();

            //-- Assert

            Assert.IsTrue(Registry.GetRunningThreads().Contains(log));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CloseThreadLog_RemovedFromThreadRegistry()
        {
            //-- Arrange

            var log = CreateThreadLog();

            //-- Act

            log.RootActivity.Close();

            //-- Assert

            Assert.IsFalse(Registry.GetRunningThreads().Contains(log));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CompactMode_UnimportantNodesOmitted()
        {
            //-- Arrange

            var log = CreateThreadLog(rootActivityOptions: LogOptions.CompactMode);

            //-- Act

            log.AppendNode(new NameValuePairLogNode("!M1", LogLevel.Debug, LogOptions.None, exception: null));
            log.AppendNode(new NameValuePairLogNode("!M2", LogLevel.Warning, LogOptions.None, exception: null));

            var activity = new NameValuePairActivityLogNode("!A1", LogLevel.Verbose, LogOptions.None);
            log.AppendNode(activity);

            log.AppendNode(new NameValuePairLogNode("!M3", LogLevel.Error, LogOptions.None, new Exception()));
            log.AppendNode(new NameValuePairLogNode("!M4", LogLevel.Verbose, LogOptions.None, exception: null));
            log.AppendNode(new NameValuePairLogNode("!M5", LogLevel.Critical, LogOptions.None, new Exception()));

            activity.Close();

            log.AppendNode(new NameValuePairLogNode("!M6", LogLevel.Audit, LogOptions.None, exception: null));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AAX:Root{LW:M2;ACX:A1{LEX:M3;LCX:M5};LA:M6}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CompactMode_UnimportantNodesAggregated()
        {
            //-- Arrange

            var log = CreateThreadLog(rootActivityOptions: LogOptions.CompactMode);

            //-- Act

            log.AppendNode(new NameValuePairLogNode("!MZ", LogLevel.Debug, LogOptions.Aggregate, exception: null));
            log.AppendNode(new NameValuePairLogNode("!M2", LogLevel.Warning, LogOptions.None, exception: null));

            var activity = new NameValuePairActivityLogNode("!A1", LogLevel.Verbose, LogOptions.None);
            log.AppendNode(activity);

            log.AppendNode(new NameValuePairLogNode("!M3", LogLevel.Error, LogOptions.None, new Exception()));
            log.AppendNode(new NameValuePairLogNode("!MZ", LogLevel.Verbose, LogOptions.Aggregate, exception: null));
            log.AppendNode(new NameValuePairLogNode("!M5", LogLevel.Critical, LogOptions.None, new Exception()));

            activity.Close();

            log.AppendNode(new NameValuePairLogNode("!M6", LogLevel.Audit, LogOptions.None, exception: null));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AAX:Root{LW:M2;ACX:A1{LEX:M3;LCX:M5};LA:M6}"));

            var totals = log.RootActivity.GetTotals(includeBuiltIn: false);

            totals.Length.ShouldBe(1);
            totals[0].MessageId.ShouldBe("!MZ");
            totals[0].Count.ShouldBe(2);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CompactModeAtActivityLevel_UnimportantNodesOmitted()
        {
            //-- Arrange

            var log = CreateThreadLog(rootActivityOptions: LogOptions.None);

            //-- Act

            log.AppendNode(new NameValuePairLogNode("!Z1", LogLevel.Debug, LogOptions.None, exception: null));

            var activity = new NameValuePairActivityLogNode("!A1", LogLevel.Verbose, LogOptions.CompactMode);
            log.AppendNode(activity);

            log.AppendNode(new NameValuePairLogNode("!M1", LogLevel.Debug, LogOptions.None, exception: null));
            log.AppendNode(new NameValuePairLogNode("!M2", LogLevel.Warning, LogOptions.None, exception: null));

            var subActivity = new NameValuePairActivityLogNode("!A2", LogLevel.Verbose, LogOptions.None);
            log.AppendNode(subActivity);

            log.AppendNode(new NameValuePairLogNode("!M3", LogLevel.Error, LogOptions.None, new Exception()));
            log.AppendNode(new NameValuePairLogNode("!M4", LogLevel.Verbose, LogOptions.None, exception: null));
            log.AppendNode(new NameValuePairLogNode("!M5", LogLevel.Critical, LogOptions.None, new Exception()));

            subActivity.Close();

            log.AppendNode(new NameValuePairLogNode("!M6", LogLevel.Audit, LogOptions.None, exception: null));

            activity.Close();

            log.AppendNode(new NameValuePairLogNode("!Z2", LogLevel.Verbose, LogOptions.None, exception: null));

            //-- Assert

            Assert.That(ToTestString(log.RootActivity), Is.EqualTo("AAX:Root{LD:Z1;AAX:A1{LW:M2;ACX:A2{LEX:M3;LCX:M5};LA:M6};LV:Z2}"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        #if INCLUDE_MANUAL_TESTS
        [Category("Manual")]
        [TestCase(ThreadTaskType.StartUp, "Node is starting up", true, true)]
        [TestCase(ThreadTaskType.StartUp, "Node is starting up", false, false)]
        [TestCase(ThreadTaskType.IncomingRequest, "http://myapp/request", false, false)]
        [TestCase(ThreadTaskType.ScheduledJob, "Timer [Cleaner]", false, false)]
        [TestCase(ThreadTaskType.QueuedWorkItem, "Queue [Data] Item [12345]", false, false)]
        [TestCase(ThreadTaskType.ShutDown, "Node is shutting down", false, false)]
        public void GenerateExampleThreadLogs(ThreadTaskType taskType, string rootActivityText, bool includeWarning, bool includeError)
        {
            var realThreadRegistry = new ThreadRegistry(@"D:\ThreadLogExamples");
            var rootActivity = new FormattedActivityLogNode(rootActivityText);
            var log = new ThreadLog(Framework, _clock, realThreadRegistry, taskType, rootActivity);

            log.AppendNode(new FormattedLogNode(LogLevel.Info, "One"));
            log.AppendNode(new FormattedActivityLogNode("Two"));
            log.AppendNode(new FormattedLogNode(LogLevel.Verbose, "Two-1"));
            log.AppendNode(new FormattedLogNode(LogLevel.Verbose, "Two-2"));
            log.AppendNode(new FormattedLogNode(LogLevel.Info, "Three"));
            log.AppendNode(new FormattedActivityLogNode("Four"));

            if ( includeError )
            {
                try
                {
                    throw new Exception("This is a generated exception");
                }
                catch ( Exception e )
                {
                    log.AppendNode(new FormattedLogNode(LogLevel.Error, "Five", exception: e, fullDetailsText: e.ToString()));
                }
            }

            log.CurrentActivity.Close();

            if ( includeWarning )
            {
                log.AppendNode(new FormattedLogNode(LogLevel.Warning, "Six"));
            }

            log.AppendNode(new FormattedLogNode(LogLevel.Debug, "Two-3"));
            log.AppendNode(new FormattedLogNode(LogLevel.Debug, "Two-4"));

            Thread.Sleep(5000);
        }
        #endif

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private ThreadLog CreateThreadLog(
            string rootActivityText = "Root", 
            ThreadTaskType taskType = ThreadTaskType.Unspecified, 
            LogOptions rootActivityOptions = LogOptions.None)
        {
            var rootActivity = new FormattedActivityLogNode(rootActivityText, LogLevel.Info, rootActivityOptions);
            return new ThreadLog(Framework, Clock, Registry, Anchor, ThreadTaskType.Unspecified, rootActivity);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private string ToTestString(ActivityLogNode activity)
        {
            var output = new StringBuilder();
            AppendActivityTestString(activity, output);
            return output.ToString();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void AppendActivityTestString(ActivityLogNode activity, StringBuilder output)
        {
            AppendNodeTestString(activity, output);
            output.Append("{");

            for ( var child = activity.FirstChild ; child != null ; child = child.NextSibling )
            {
                if ( child is ActivityLogNode )
                {
                    AppendActivityTestString((ActivityLogNode)child, output);
                }
                else
                {
                    AppendNodeTestString(child, output);
                }

                if ( child.NextSibling != null )
                {
                    output.Append(";");
                }
            }

            output.Append("}");
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void AppendNodeTestString(LogNode node, StringBuilder output)
        {
            output.Append(node is ActivityLogNode ? "A" : "L");
            output.Append(node.Level.ToString().Substring(0, 1));

            if ( node.Exception != null )
            {
                output.Append("X");
            }

            output.Append(":");
            output.Append(node.SingleLineText);
        }
    }
}
