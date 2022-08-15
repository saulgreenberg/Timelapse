using System;
/// <summary>
/// This code was imported and slightly modified from a Github project see  http://brain2cpu.com/devtools.html
/// </summary>
namespace Timelapse.ExifTool
{
    [Serializable]
    //#pragma warning disable CA2229 // Implement serialization constructors Reason: not sure what it does, and it generates other CAs.
    //#pragma warning disable CA1032 //  Reason: not sure what it does, and it generates other CAs.
    public class ExifToolException : Exception
    //#pragma warning restore CA1032 
    //#pragma warning restore CA2229 
    {
        public ExifToolException(string msg) : base(msg)
        { }
    }
}
