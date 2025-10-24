using System;

//
// This code was imported and slightly modified from a Github project see  http://brain2cpu.com/devtools.html
//
namespace Timelapse.ExifTool
{
    [Serializable]
    //#pragma warning disable CA2229 // Implement serialization constructors Reason: not sure what it does, and it generates other CAs.
    //#pragma warning disable CA1032 //  Reason: not sure what it does, and it generates other CAs.
    public class ExifToolException(string msg) : Exception(msg);
        //#pragma warning restore CA1032 
    //#pragma warning restore CA2229 
}
