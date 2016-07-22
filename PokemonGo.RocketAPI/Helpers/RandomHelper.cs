using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGo.RocketAPI.Helpers
{
    public class RandomHelper
    {
        private static Random _random = new Random();

        public static long GetLongRandom(long min, long max)
        {
            byte[] buf = new byte[8];
            _random.NextBytes(buf);
            var longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }

        public static double GetDoubleRandom(double min, double max)
        {
            return min + (_random.NextDouble() * (max - min));
        }

        public static float GetFloatRandom(float min, float max)
        {
            return (float)GetDoubleRandom(min, max);
        }

        public static double JitterDouble(double value, double jitter)
        {
            return GetDoubleRandom(value - jitter, value + jitter);
        }

        public static Task RandomDelay(int min, int max)
        {
            return Task.Delay((int)GetLongRandom(min, max));
        }
    }
}
