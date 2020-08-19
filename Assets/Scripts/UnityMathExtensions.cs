using System.Runtime.CompilerServices;

namespace Unity.Mathematics
{
    public static class bool3Extentions
    {
        public static bool or(this bool3 value)
        {
            return value.x || value.y || value.z;
        }

        public static bool and(this bool3 value)
        {
            return value.x && value.y && value.z;
        }
    }

    public static class utility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ceilPow8(int x)
        {
            int ans = 1;
            while (ans < x) ans <<= 3;
            return ans;
        }
    }
}