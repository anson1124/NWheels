using System.Collections.Generic;
using NWheels.Testing.Controllers;

namespace NWheels.Tools.TestBoard.Services
{
    public interface IApplicationControllerService
    {
        void Open(string bootConfigFilePath, bool autoRun = false);
        void Close(ApplicationController application);
        void CloseAll();
        IReadOnlyList<ApplicationController> Applications { get; }
    }
}