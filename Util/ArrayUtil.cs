namespace FancyLighting.Util;

internal static class ArrayUtil
{
    public static void MakeAtLeastSize<T>(ref T[] array, int length)
    {
        if (array is null || array.Length < length)
        {
            array = new T[length];
        }
    }
}
