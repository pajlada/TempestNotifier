using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace TempestNotifier
{
    public class TempestAffix
    {
        public string name = "";
        public string description = "";

        public override string ToString()
        {
            CultureInfo culture_info = Thread.CurrentThread.CurrentCulture;
            TextInfo text_info = culture_info.TextInfo;
            return text_info.ToTitleCase(name) + " (" + description + ")";
        }
    }

    public class TempestAffixes
    {
        [JsonProperty(PropertyName = "bases")]
        public Dictionary<string, string> prefixes { get; set; }

        public Dictionary<string, string> suffixes { get; set; }
    }

    public class TempestAffixData
    {
        public static TempestAffixes _data = null;
        public static Dictionary<string, string> prefixes
        {
            get
            {
                return _data.prefixes;
            }
        }

        public static Dictionary<string, string> suffixes
        {
            get
            {
                return _data.suffixes;
            }
        }

        public static async Task init()
        {
            _data = JsonConvert.DeserializeObject<TempestAffixes>(await TempestAPI.get_raw("tempests"));
        }
    }

    public class Tempest
    {
        public string name { get; set; }

        [JsonProperty(PropertyName = "base")]
        public string prefix { get; set; }

        public string suffix { get; set; }

        public int votes { get; set; }
    }

    public class Map
    {
        public string name { get; set; }

        public int level { get; set; }

        public string tempest_description { get; set; }

        public Tempest tempest_data { get; set; }

        public int state { get; set; }

        public int votes { get; set; }
    }

    public class TempestDescription
    {
        public string short_description { get; set; }

        public bool good { get; set; }
    }

    public class MapTempest
    {
        public string map { get; set; }

        public Tempest tempest { get; set; }
    }
}
