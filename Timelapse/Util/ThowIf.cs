using System;
namespace Timelapse.Util
{
    /// <summary>
    /// If an argument is null, throw an exception.
    /// The method call gives a compact one-line wasy to check parameters in methods
    /// </summary>
    public static class ThrowIf
    {
        /// <summary>
        /// Throw an exception if the argument is null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="name"></param>
        public static void IsNullArgument<T>(T value, string name) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
