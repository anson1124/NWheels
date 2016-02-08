﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Hapil;
using Hapil.Testing.NUnit;
using NUnit.Framework;
using NWheels.Core;
using NWheels.Logging;
using NWheels.Logging.Core;
using NWheels.Testing;

namespace NWheels.UnitTests.Logging
{
    [TestFixture]
    public class ApplicationEventLoggerConventionTests : UnitTestBase
    {
        private ConventionObjectFactory _factory;
        private TestThreadLogAppender _logAppender;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            _factory = new ConventionObjectFactory(
                base.CreateDynamicModule(),
                new ApplicationEventLoggerConvention());

            _logAppender = new TestThreadLogAppender(base.Framework);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanEmitLogger()
        {
            //-- Act

            var logger = CreateTestLogger();

            //-- Assert

            Assert.That(logger, Is.Not.Null);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanAppendEmptyMessage()
        {
            //-- Arrange

            var logger = CreateTestLogger();

            //-- Act

            logger.ThisIsMyEmptyDebugMessage();

            //-- Assert

            var log = _logAppender.TakeLog();

            Assert.That(log.Length, Is.EqualTo(1));
            Assert.That(log[0].Level, Is.EqualTo(LogLevel.Debug));
            Assert.That(log[0].SingleLineText, Is.EqualTo("This is my empty debug message"));
            Assert.That(log[0].NameValuePairs.All(pair => pair.IsBaseValue()));
            Assert.That(log[0].Exception, Is.Null);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanAppendMessageWithParameters()
        {
            //-- Arrange

            var logger = CreateTestLogger();

            //-- Act

            logger.ThisIsMyDebugMessage(num: 123, str: "ABC");

            //-- Assert

            var log = _logAppender.TakeLog();

            Assert.That(log.Length, Is.EqualTo(1));
            Assert.That(log[0].Level, Is.EqualTo(LogLevel.Debug));
            Assert.That(log[0].SingleLineText, Is.EqualTo("This is my debug message: num=123, str=ABC"));
            Assert.That(log[0].NameValuePairs.Where(p => !p.IsIncludedInSingleLineText()).All(p => p.IsBaseValue()));
            Assert.That(log[0].Exception, Is.Null);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanAppendMessageWithException()
        {
            //-- Arrange

            var logger = CreateTestLogger();
            var exception = new DivideByZeroException();

            //-- Act

            logger.ThisIsMyErrorMessageWithExceptionParameter(num: 123, str: "ABC", e: exception);

            //-- Assert

            var log = _logAppender.TakeLog();

            Assert.That(log.Length, Is.EqualTo(1));
            Assert.That(log[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(log[0].SingleLineText, Is.EqualTo("This is my error message with exception parameter: num=123, str=ABC"));
            Assert.That(log[0].FullDetailsText.Contains(exception.ToString()));
            Assert.That(log[0].Exception, Is.SameAs(exception));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanAppendErrorMessageAndThrow()
        {
            //-- Arrange

            var logger = CreateTestLogger();

            //-- Act

            try
            {
                throw logger.ThisIsMyErrorMessageThatCreatesException();
            }
            catch ( TestErrorException e )
            {
                //-- Assert

                var log = _logAppender.TakeLog();

                Assert.That(e.Message, Is.EqualTo("This is my error message that creates exception"));
                Assert.That(log.Length, Is.EqualTo(1));
                Assert.That(log[0].Level, Is.EqualTo(LogLevel.Error));
                Assert.That(log[0].SingleLineText, Is.EqualTo("This is my error message that creates exception"));
                Assert.That(log[0].FullDetailsText.Contains(e.ToString()));
                Assert.That(log[0].Exception, Is.SameAs(e));
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanAppendErrorMessageWithValuesAndThrow()
        {
            //-- Arrange

            var logger = CreateTestLogger();

            //-- Act

            try
            {
                throw logger.ThisIsMyCriticalMessageThatCreatesException("ABC", 123);
            }
            catch ( TestErrorException e )
            {
                //-- Assert

                var log = _logAppender.TakeLog();

                Assert.That(e.Message, Is.EqualTo("This is my critical message that creates exception: str=ABC, num=123"));
                Assert.That(log.Length, Is.EqualTo(1));
                Assert.That(log[0].Level, Is.EqualTo(LogLevel.Critical));
                Assert.That(log[0].SingleLineText, Is.EqualTo("This is my critical message that creates exception: str=ABC, num=123"));
                Assert.That(log[0].FullDetailsText.Contains(e.ToString()));
                Assert.That(log[0].Exception, Is.SameAs(e));
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void RegularLogsAndActivitiesDoNotStartThreadLog()
        {
            //-- Arrange

            var logger = CreateTestLogger();

            //-- Act

            logger.ThisIsMyCriticalMessage();
            logger.ThisIsMyActivity();

            //-- Assert

            Assert.IsFalse(_logAppender.StartedThreadTaskType.HasValue);
            Assert.IsFalse(_logAppender.StartedThreadLogIndex.HasValue);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void ActivityMarkedAsLogThreadWillStartThreadLog()
        {
            //-- Arrange

            var logger = CreateTestLogger();

            //-- Act

            logger.ThisIsMyCriticalMessage();
            logger.ThisIsMyThread(123, "ABC");
            logger.ThisIsMyActivity();

            //-- Assert

            Assert.That(_logAppender.StartedThreadTaskType, Is.EqualTo(ThreadTaskType.ScheduledJob));
            Assert.That(_logAppender.StartedThreadLogIndex, Is.EqualTo(1));
            
            Assert.That(_logAppender.GetLog()[1], Is.InstanceOf<ActivityLogNode>());
            Assert.That(_logAppender.GetLog()[1].SingleLineText, Is.EqualTo("This is my thread: num=123, str=ABC"));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanUseParametersOfNullableValueTypes()
        {
            //-- Arrange

            var logger = CreateTestLogger();
            var date1 = new DateTime(2015, 1, 1);
            var date2 = new DateTimeOffset(new DateTime(2015, 2, 2), TimeSpan.FromHours(-7));

            //-- Act

            logger.AnActivityWithNullableParameterTypes(date1, date2);
            logger.AnActivityWithNullableParameterTypes(null, null);

            //-- Assert

            Assert.That(_logAppender.GetLogStrings(), Is.EqualTo(new[] {
                string.Format("An activity with nullable parameter types: date1=\"{0}\", date2=\"{1}\"", date1, date2),
                "An activity with nullable parameter types: date1=, date2="
            }));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanSpecifyParameterFormat()
        {
            //-- Arrange

            var logger = CreateTestLogger();
            var date1 = new DateTime(2015, 1, 1);
            var date2 = new DateTimeOffset(new DateTime(2015, 2, 2), TimeSpan.FromHours(-7));

            //-- Act

            logger.LogMessageWithFormattedParameters(date1, date2);

            //-- Assert

            Assert.That(_logAppender.GetLogStrings(), Is.EqualTo(new[] {
                "Log message with formatted parameters: date1=2015-01-01, date2=\"" + date2.ToString("MMM dd yyyy") + "\"",
            }));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanMoveParametersIntoFullDetailsText()
        {
            //-- Arrange

            var logger = CreateTestLogger();
            var dateTime = new DateTime(2015, 1, 1);
            var dateTimeOffset = new DateTimeOffset(new DateTime(2015, 2, 2), TimeSpan.FromHours(-7));

            //-- Act

            logger.LogMessageWithFormattedAndDetailParameters(123, "ABC", dateTime, dateTimeOffset);

            //-- Assert

            Assert.That(_logAppender.GetLog()[0].SingleLineText, Is.EqualTo(
                "Log message with formatted and detail parameters: num=123, dateTime=2015-01-01"
            ));
            Assert.That(_logAppender.GetLog()[0].FullDetailsText.Contains(
                Environment.NewLine + "str=ABC"
            ));
            Assert.That(_logAppender.GetLog()[0].FullDetailsText.Contains(
                Environment.NewLine + "dateTimeOffset=\"" + dateTimeOffset.ToString("MMM dd yyyy") + "\""
            ));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void CanSpecifySpecialContentTypes()
        {
            //-- Arrange

            var logger = CreateTestLogger();
            var messageXml = XElement.Parse("<Envelope><Header from='foo' to='bar' /><Body><Data value='12345' /></Body></Envelope>");

            //-- Act

            logger.ThisIsMyCommunicationMessageDump(123, "ABC", messageXml);

            //-- Assert

            Assert.That(_logAppender.GetLog()[0].SingleLineText, Is.EqualTo(
                "This is my communication message dump: num=123"
            ));
            Assert.That(_logAppender.GetLog()[0].FullDetailsText.Contains(
                Environment.NewLine + "str=ABC"
            ));
            Assert.That(_logAppender.GetLog()[0].FullDetailsText.Contains(
                Environment.NewLine + "messageContents=\"" + messageXml.ToString().Replace("\"", "'") + "\""
            ));
            Assert.That(_logAppender.GetLog()[0].ContentTypes, Is.EqualTo(LogContentTypes.Text | LogContentTypes.CommunicationMessage));
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        //[Test, Ignore("LogMethod is not yet supported")]
        //public void CanLogVoidMethodCallAsActivity()
        //{
        //    //-- Arrange

        //    var logger = CreateTestLogger();
        //    var callCount = 0;

        //    //-- Act

        //    logger.ThisIsMyVoidMethod(() => callCount++);

        //    //-- Assert

        //    var log = _logAppender.TakeLog();

        //    Assert.That(callCount, Is.EqualTo(1));
        //    Assert.That(log.Length, Is.EqualTo(1));
        //    Assert.That(log[0].SingleLineText, Is.EqualTo("This is my void method"));
        //    Assert.That(log[0].FullDetailsText, Is.Null);
        //}

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private ITestLogger CreateTestLogger()
        {
            var threadLogAppenderPipeline = new Pipeline<IThreadLogAppender>(new IThreadLogAppender[] { _logAppender }, Resolve<PipelineObjectFactory>());
            return _factory.CreateInstanceOf<ITestLogger>().UsingConstructor<Pipeline<IThreadLogAppender>>(threadLogAppenderPipeline);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public interface ITestLogger : IApplicationEventLogger
        {
            [LogDebug]
            void ThisIsMyEmptyDebugMessage();
            
            [LogDebug]
            void ThisIsMyDebugMessage(int num, string str);
            
            [LogVerbose]
            void ThisIsMyVerboseMessageWithCollection(DayOfWeek day, IEnumerable<DateTime> dates);
            
            [LogInfo]
            void ThisIsMyInfoMessageWithDefaultParameters(string first, string second = null, int third = 12345);
            
            [LogError]
            void ThisIsMyEmptyErrorMessage();
            
            [LogError]
            void ThisIsMyErrorMessageWithExceptionParameter(int num, string str, Exception e);
            
            [LogError]
            TestErrorException ThisIsMyErrorMessageThatCreatesException();
            
            [LogCritical]
            void ThisIsMyCriticalMessage();
            
            [LogCritical]
            TestErrorException ThisIsMyCriticalMessageThatCreatesException(string str, int num);
            
            [LogActivity]
            ILogActivity ThisIsMyActivity();
            
            [LogActivity]
            ILogActivity ThisIsMyActivityWithParameters(int num, string str);
            
            [LogThread(ThreadTaskType.ScheduledJob)]
            ILogActivity ThisIsMyThread(int num, string str);

            [LogActivity]
            ILogActivity AnActivityWithNullableParameterTypes(DateTime? date1, DateTimeOffset? date2);

            [LogDebug]
            void LogMessageWithFormattedParameters(
                [Format("yyyy-MM-dd")] DateTime? date1, 
                [Format("MMM dd yyyy")] DateTimeOffset? date2);

            [LogVerbose]
            void LogMessageWithFormattedAndDetailParameters(
                int num,
                [Detail] string str,
                [Format("yyyy-MM-dd")] DateTime dateTime,
                [Format("MMM dd yyyy"), Detail] DateTimeOffset? dateTimeOffset);

            [LogVerbose]
            void ThisIsMyCommunicationMessageDump(
                int num,
                [Detail] string str,
                [CommunicationMessageDetail] XElement messageContents);

            [LogVerbose]
            void ThisIsMyDataEntityDump(
                int num,
                [Detail] string str,
                [DataEntityDetail] XElement entityContents);

            //[LogMethod]
            //void ThisIsMyVoidMethod(Action method);

            //[LogMethod]
            //void ThisIsMyVoidMethodWithParameters(
            //    Action<string, int, decimal> method, 
            //    string str, 
            //    int num, 
            //    [Detail, Format("#,##0.00")] decimal value);

            //[LogMethod]
            //bool ThisIsMyFunction(Func<bool> method);

            //[LogMethod]
            //string ThisIsMyFunctionWithParameters(
            //    Func<int, decimal, string> method, 
            //    int num,
            //    [Detail, Format("#,##0.00")] decimal value);
        }
    
        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public class TestErrorException : Exception
        {
            public TestErrorException(string message)
                : base(message)
            {
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private class CompiledExample : ITestLogger
        {
            private IThreadLogAppender _threadLogAppender;

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public CompiledExample(IThreadLogAppender appender)
            {
                this._threadLogAppender = appender;
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            #region ITestLogger Members

            public void ThisIsMyEmptyDebugMessage()
            {
                var node = new NameValuePairLogNode("ThisIsMyEmptyDebugMessage", LogLevel.Debug, LogOptions.None, exception: null);
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyDebugMessage(int num, string str)
            {
                LogNameValuePair<int> pair1;
                pair1 = new LogNameValuePair<int> {
                    Name = "num",
                    Value = num
                };
                LogNameValuePair<string> pair2;
                pair2 = new LogNameValuePair<string> {
                    Name = "str",
                    Value = str
                };
                var node = new NameValuePairLogNode<int, string>(
                    "ApplicationEventLoggerConventionTests.Test.ThisIsMyDebugMessage", LogLevel.Debug, LogOptions.None, null, pair1, pair2);
                
                this._threadLogAppender.AppendLogNode(node);
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyVerboseMessageWithCollection(DayOfWeek day, IEnumerable<DateTime> dates)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyInfoMessageWithDefaultParameters(string first, string second = null, int third = 12345)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyEmptyErrorMessage()
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyErrorMessageWithExceptionParameter(int num, string str, Exception e)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public TestErrorException ThisIsMyErrorMessageThatCreatesException()
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyCriticalMessage()
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public TestErrorException ThisIsMyCriticalMessageThatCreatesException(string str, int num)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public ILogActivity ThisIsMyActivity()
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public ILogActivity ThisIsMyActivityWithParameters(int num, string str)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public ILogActivity ThisIsMyThread(int num, string str)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public ILogActivity AnActivityWithNullableParameterTypes(DateTime? date1, DateTimeOffset? date2)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void LogMessageWithFormattedParameters(DateTime? date1, DateTimeOffset? date2)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void LogMessageWithFormattedAndDetailParameters(int num, string str, DateTime dateTime, DateTimeOffset? dateTimeOffset)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyCommunicationMessageDump(int num, string str, XElement messageContents)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyDataEntityDump(int num, string str, XElement entityContents)
            {
                throw new NotImplementedException();
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public void ThisIsMyVoidMethod(Action method)
            {
                using ( ILogActivity activity = new NameValuePairActivityLogNode("Test.ThisIsMyVoidMethod", LogLevel.Debug, LogOptions.None, userStory: null) )
                {
                    try
                    {
                        method();
                    }
                    catch ( Exception e )
                    {
                        activity.Fail(e);
                        throw;
                    }
                }
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------
            
            public void ThisIsMyVoidMethodWithParameters(Action<string, int, decimal> method, string str, int num, decimal value)
            {
                var pair1 = new LogNameValuePair<string> { Name = "str", Value = str };
                var pair2 = new LogNameValuePair<int> { Name = "num", Value = num };
                var pair3 = new LogNameValuePair<decimal> { Name = "value", Value = value };

                using ( ILogActivity activity = new NameValuePairActivityLogNode<string, int, decimal>(
                    "Test.ThisIsMyVoidMethodWithParameters", LogLevel.Debug, LogOptions.None, null, pair1, pair2, pair3) )
                {
                    try
                    {
                        method(str, num, value);
                    }
                    catch ( Exception e )
                    {
                        activity.Fail(e);
                        throw;
                    }
                }
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public bool ThisIsMyFunction(Func<bool> method)
            {
                using ( ILogActivity activity = new NameValuePairActivityLogNode("Test.ThisIsMyFunction", LogLevel.Debug, LogOptions.None, userStory: null) )
                {
                    try
                    {
                        return method();
                    }
                    catch ( Exception e )
                    {
                        activity.Fail(e);
                        throw;
                    }
                }
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------
            
            public string ThisIsMyFunctionWithParameters(Func<int, decimal, string> method, int num, decimal value)
            {
                var pair1 = new LogNameValuePair<int> { Name = "num", Value = num };
                var pair2 = new LogNameValuePair<decimal> { Name = "value", Value = value };

                using ( ILogActivity activity =
                    new NameValuePairActivityLogNode<int, decimal>("Test.ThisIsMyFunctionWithParameters", LogLevel.Debug, LogOptions.None, null, pair1, pair2) )
                {
                    try
                    {
                        return method(num, value);
                    }
                    catch ( Exception e )
                    {
                        activity.Fail(e);
                        throw;
                    }
                }
            }

            #endregion
        }
    }
}
