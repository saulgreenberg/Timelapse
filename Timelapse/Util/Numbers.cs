using System;

namespace Timelapse.Util
{
    public static class  Numbers
    {
        public static float? ToFloatOrDefault(object value, float defaultValue)
        {
            return (Double.TryParse(value.ToString(), out double parsedValue ))
                ? (float?) parsedValue
                : defaultValue;
        }
    }
}
