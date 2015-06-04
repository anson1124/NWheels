﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using NWheels.UI;
using NWheels.UI.Core;

namespace NWheels.Stacks.NancyFx
{
    public class WebApplicationModule : NancyModule
    {
        private readonly ApplicationBase _application;
        private readonly string _contentRootPath;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public WebApplicationModule(ApplicationBase application)
        {
            _application = application;

            base.Get["/"] = parameters => View[GetSkinViewPath(_application.InitialScreen.ViewName)];
            base.Get["/init.json"] = parameters => Response.AsJson<ScreenBase>(_application.InitialScreen);

            _contentRootPath = Path.Combine(Path.GetDirectoryName(_application.GetType().Assembly.Location), _application.IdName);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public string GetSkinViewPath(string viewName)
        {
            return ("Content/Skin.Default/" + viewName + ".html");
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public string ContentRootPath
        {
            get { return _contentRootPath; }
        }
    }
}
