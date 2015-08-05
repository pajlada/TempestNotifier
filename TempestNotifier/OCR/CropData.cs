using System;
using System.Collections.Generic;

namespace TempestNotifier.OCR
{
    public class CropData
    {
        public Tuple<int, int> map_from;
        public Tuple<int, int> map_to;
        public Tuple<int, int> tempest_from;
        public Tuple<int, int> tempest_to;

        public static Dictionary<Tuple<int, int>, CropData> resolutions = new Dictionary<Tuple<int, int>, CropData>
        {
            {
                new Tuple<int, int>(1920, 1080),
                new CropData {
                    map_from = new Tuple<int, int>(1730, 40),
                    map_to = new Tuple<int, int>(1910, 65),
                    tempest_from = new Tuple<int, int>(1560, 185),
                    tempest_to = new Tuple<int, int>(1920, 206),
                }
            }, {
                new Tuple<int, int>(2560, 1440),
                new CropData {
                    map_from = new Tuple<int, int>(2300, 58),
                    map_to = new Tuple<int, int>(2545, 88),
                    tempest_from = new Tuple<int, int>(2056, 256),
                    tempest_to = new Tuple<int, int>(2560, 283),
                }
            },
        };
    }
}
