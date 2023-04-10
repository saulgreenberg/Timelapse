using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Util
{
    public static class  Numbers
    {
        public static float? ToFloatOrDefault(object value, float defaultValue)
        {
            return (Double.TryParse(value.ToString(), out double parsedValue ))
                ? (float?) parsedValue
                : (float?)defaultValue;
        }
    }
}
