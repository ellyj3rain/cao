using System.Collections.Generic;

namespace Verse
{
    public interface IExposable { void ExposeData(); }

    public enum LookMode { Undefined, Value, Deep }

    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string label) { }
        public static void Look<T>(ref T value, string label, T defaultValue) { }
    }

    public static class Scribe_Collections
    {
        public static void Look<T>(ref List<T> value, string label,
            LookMode mode) { }
    }

    public static class CAStringExtensions
    {
        public static bool NullOrEmpty(this string value) =>
            string.IsNullOrEmpty(value);
    }
}
