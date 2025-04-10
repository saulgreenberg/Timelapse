using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
namespace Timelapse.Util
{
    public static class ObjectUtillities
    {
        // Return a deep clone of the object
        public static T DeepClone<T>(this T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }
    }
}
