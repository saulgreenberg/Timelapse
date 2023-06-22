using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Enums
{
    public enum CreateSubfolderResultEnum
    {
        Success,
        FailAsSourceFolderDoesNotExist,
        FailAsDestinationFolderExists,
        FailDueToSystemCreateException
    }
}
