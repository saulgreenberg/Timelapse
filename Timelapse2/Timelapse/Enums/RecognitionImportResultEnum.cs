using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Enums
{
    public enum RecognitionImportResultEnum
    {
        IncompatableDetectionCategories,
        IncompatableClassificationCategories,
        JsonFileCouldNotBeRead,
        Success,
        Failure
    }
}
