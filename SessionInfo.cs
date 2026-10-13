using System;
using System.Globalization;

namespace PipeSplitter2024
{
    internal sealed class SessionInfo
    {
        internal static string ToolVersion
        {
            get { return typeof(SessionInfo).Assembly.GetName().Version.ToString(); }
        }
        internal readonly string Id=Guid.NewGuid().ToString("N");
        internal readonly DateTime Started=DateTime.Now;
        internal string RevitVersion, WindowsUser, DocumentPath;
        internal string Header()
        {
            return "Session="+Id+"; started="+Started.ToString("o",CultureInfo.InvariantCulture)+"; tool="+ToolVersion+"; Revit="+RevitVersion+"; Windows user="+WindowsUser+"; document path="+DocumentPath;
        }
    }
}
