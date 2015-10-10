﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gemini.Framework;
using NWheels.Testing.Controllers;

namespace NWheels.Tools.TestBoard.Modules.LogViewer
{
    [Export(typeof(LogViewerViewModel))]
    public class LogViewerViewModel : Document
    {
        private readonly ControllerBase _controller;
        private readonly List<ILogConnection> _connections;
        private readonly string _explorerItemPath;
        private readonly LogPanelViewModel _logs;
        private bool _captureMode;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public LogViewerViewModel(string explorerItemPath, ControllerBase controller)
        {
            _explorerItemPath = explorerItemPath;
            _controller = controller;

            _connections = new List<ILogConnection>();
            AddLogConnectionsTo(controller);

            _logs = new LogPanelViewModel();
            
            _captureMode = true;
            StartCapture();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override bool ShouldReopenOnStart
        {
            get { return false; }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override string DisplayName
        {
            get { return "Logs - " + _explorerItemPath; }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ControllerBase Controller
        {
            get { return _controller; }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public LogPanelViewModel Logs
        {
            get { return _logs; }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public override void TryClose(bool? dialogResult = null)
        {
            foreach ( var connection in _connections )
            {
                connection.Dispose();
            }

            base.TryClose(dialogResult);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public bool CaptureMode
        {
            get
            {
                return _captureMode;
            }
            set
            {
                if ( value != _captureMode )
                {
                    _captureMode = value;

                    if ( _captureMode )
                    {
                        StartCapture();
                    }
                    else
                    {
                        StopCapture();
                    }

                    base.NotifyOfPropertyChange(() => this.CaptureMode);
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void StartCapture()
        {
            foreach ( var connection in _connections )
            {
                connection.StartCapture();
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void StopCapture()
        {
            foreach ( var connection in _connections )
            {
                connection.StopCapture();
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void AddLogConnectionsTo(ControllerBase controller)
        {
            var nodeInstance = (controller as NodeInstanceController);

            if ( nodeInstance != null )
            {
                var connection = new NodeInstanceLogConnection(nodeInstance);
                connection.ThreadLogsCaptured += OnThreadLogsCaptured;
                _connections.Add(connection);
            }

            foreach ( var subController in controller.GetSubControllers() )
            {
                AddLogConnectionsTo(subController);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void OnThreadLogsCaptured(object sender, ThreadLogsCapturedEventArgs args)
        {
            foreach ( var log in args.ThreadLogs )
            {
                Logs.AddLog(log);
            }
        }
    }
}
