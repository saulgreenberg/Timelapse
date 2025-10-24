using System.Text.Json;
namespace Timelapse.Util
{
    public static class ObjectUtillities
    {
        // Return a deep clone of the object using JSON serialization
        public static T DeepClone<T>(this T obj)
        {
            if (obj == null)
                return default(T);

            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
