﻿namespace NWheels.Logging.Core
{
    internal class ThreadLogAppender : IThreadLogAppender
    {
        public static readonly string UnknownThreadMessageId = "ThreadLog.UnknownThread";

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private readonly IFramework _framework;
        private readonly IThreadLogAnchor _anchor;
        private readonly IThreadRegistry _registry;
        private readonly IPlainLog _plainLog;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ThreadLogAppender(IFramework framework, IThreadLogAnchor anchor, IThreadRegistry registry, IPlainLog plainLog)
        {
            _framework = framework;
            _anchor = anchor;
            _registry = registry;
            _plainLog = plainLog;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void AppendLogNode(LogNode node)
        {
            var currentLog = _anchor.CurrentThreadLog;

            if ( currentLog != null )
            {
                currentLog.AppendNode(node);
            }
            else
            {
                using ( var unknownThreadActivity = new FormattedActivityLogNode(UnknownThreadMessageId, "???") )
                { 
                    StartThreadLogNoCheck(ThreadTaskType.Unspecified, unknownThreadActivity);
                    _anchor.CurrentThreadLog.AppendNode(node);
                }
            }

            if ((node.Options & LogOptions.PlainLog) != 0)
            {
                _plainLog.LogNode(node);
            }

            //if ( node.Level >= LogLevel.Warning )
            //{
            //    PlainLog.LogNode(node);
            //}
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void AppendActivityNode(ActivityLogNode activity)
        {
            var currentLog = _anchor.CurrentThreadLog;

            if ( currentLog != null )
            {
                currentLog.AppendNode(activity);
            }
            else
            {
                StartThreadLogNoCheck(ThreadTaskType.Unspecified, activity);
            }

            if ((activity.Options & LogOptions.PlainLog) != 0)
            {
                _plainLog.LogActivity(activity);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void StartThreadLog(ThreadTaskType taskType, ActivityLogNode rootActivity)
        {
            var currentLog = _anchor.CurrentThreadLog;

            if ( currentLog != null )
            {
                currentLog.AppendNode(rootActivity);

                if ((rootActivity.Options & LogOptions.PlainLog) != 0)
                {
                    _plainLog.LogActivity(rootActivity);
                }
            }
            else
            {
                StartThreadLogNoCheck(taskType, rootActivity);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void StartThreadLogNoCheck(ThreadTaskType taskType, ActivityLogNode rootActivity)
        {
            _anchor.CurrentThreadLog = new ThreadLog(_framework, new StopwatchClock(), _registry, _anchor, taskType, rootActivity);
            
            if ((rootActivity.Options & LogOptions.PlainLog) != 0)
            {
                _plainLog.LogActivity(rootActivity);
            }
        }
    }
}
