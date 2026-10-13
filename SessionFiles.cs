using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PipeSplitter2024
{
    internal sealed class AuditSessionFile
    {
        private readonly string folder;
        private readonly string stem;
        internal string PathName {get;private set;}
        internal AuditSessionFile(string directory, DateTime started)
        {
            folder=directory;stem="audit_"+started.ToString("yyyyMMdd_HHmmss",CultureInfo.InvariantCulture);
        }
        internal void Append(string header,IEnumerable<string> lines)
        {
            Directory.CreateDirectory(folder);
            FileStream stream=null;bool created=false;
            if(PathName==null)
            {
                for(int suffix=0;stream==null;suffix++)
                {
                    string candidate=Path.Combine(folder,stem+(suffix==0?"":"_"+suffix.ToString("00",CultureInfo.InvariantCulture))+".csv");
                    try {stream=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write,FileShare.Read);PathName=candidate;created=true;}
                    catch(IOException) {if(!File.Exists(candidate)) throw;}
                }
            }
            else stream=new FileStream(PathName,FileMode.Open,FileAccess.Write,FileShare.Read);
            using(stream)
            {
                long length=stream.Length;stream.Position=length;
                try
                {
                    var text=new StringBuilder();if(created) text.AppendLine(header);
                    foreach(string line in lines) text.AppendLine(line);
                    byte[] bytes=new UTF8Encoding(false).GetBytes(text.ToString());stream.Write(bytes,0,bytes.Length);stream.Flush();
                }
                catch
                {
                    stream.SetLength(length);
                    if(created) {stream.Dispose();File.Delete(PathName);PathName=null;}
                    throw;
                }
            }
        }
    }
    internal static class SessionFiles
    {
        internal static bool RotateLog(string path,long limit)
        {
            if(!File.Exists(path) || new FileInfo(path).Length<limit) return false;
            string backup=path+".bak";if(File.Exists(backup)) File.Delete(backup);File.Move(path,backup);return true;
        }
    }
}
