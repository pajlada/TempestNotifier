using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TempestNotifier;

namespace TestSuite
{
    class Program
    {
        public static Dictionary<string, MapTempest> test_data = new Dictionary<string, MapTempest>();
        static List<Map> map_data = new List<Map>();

        public static void add_test_data(string filename, string map, string prefix = "none", string suffix = "none")
        {
            test_data.Add(filename, new MapTempest
            {
                map = map,
                tempest = new Tempest
                {
                    prefix = prefix,
                    suffix = suffix
                }
            });
        }

        /* Contains hard-coded data for all potential images we're testing. */
        static async Task load_test_data()
        {
            add_test_data("55bcc98d351a3.png", "spider_forest");
            add_test_data("55bcd20dcaaec.png", "museum", "scathing");
            add_test_data("55bcd093449e5.png", "dungeon");
            add_test_data("55bce107efd8f.png", "overgrown_shrine");
            add_test_data("55bd001ec06c7.png", "bog", "electrocuting");
            add_test_data("55bd332ba2344.png", "reef", "consuming");
            add_test_data("55bd444a3771f.png", "shore", "shining", "turmoil");
            add_test_data("55bd6760d349e.png", "labyrinth", "shielding", "animation");
            add_test_data("55bd15871fccd.png", "shipyard", "crushing", "intensity");
            add_test_data("55bd19491ff8b.png", "dunes");
            add_test_data("55bd34946e6f5.png", "shore", "shining", "turmoil");
            add_test_data("55bd2883393c2.png", "crypt");
            add_test_data("55bdff18c7dec.png", "spider_forest", "corrupting");
            add_test_data("55be2a0ec2236.png", "bog", "restorative", "revelation");
            add_test_data("55be2a406f561.png", "bazaar", "arctic", "precision");
            add_test_data("55be05a2dc75e.png", "gorge", "ethereal", "destiny");
            add_test_data("55be18be6ffef.png", "volcano", "shining", "fire");
            add_test_data("55be25d8a5e33.png", "temple", "brisk", "aberrance");
            add_test_data("55be25df34741.png", "temple", "brisk", "aberrance");
            add_test_data("55be28a101e62.png", "temple", "brisk", "aberrance");
            add_test_data("55be28a63951b.png", "temple", "brisk", "aberrance");
            add_test_data("55be28ab610a4.png", "temple", "brisk", "aberrance");
            add_test_data("55be28afdd1e0.png", "temple", "brisk", "aberrance");
            add_test_data("55be28b60e249.png", "temple", "brisk", "aberrance");
            add_test_data("55be28ba8631e.png", "temple", "brisk", "aberrance");
            add_test_data("55be28bf23af6.png", "temple", "brisk", "aberrance");
            add_test_data("55be29db8db6f.png", "temple", "brisk", "aberrance");
            add_test_data("55be32bc55e22.png", "precinct", "ghastly");
            add_test_data("55be280d1144f.png", "temple", "brisk", "aberrance");
            add_test_data("55be285daf3c8.png", "temple", "brisk", "aberrance");
            add_test_data("55be286caf3d6.png", "temple", "brisk", "aberrance");
            add_test_data("55be287e08016.png", "temple", "brisk", "aberrance");
            add_test_data("55be289b84905.png", "temple", "brisk", "aberrance");
            add_test_data("55be2878babbc.png", "temple", "brisk", "aberrance");
            add_test_data("55be2889f0728.png", "temple", "brisk", "aberrance");
            add_test_data("55be15651e76c.png", "volcano", "shining", "fire");
            add_test_data("55be28678ba21.png", "temple", "brisk", "aberrance");
            add_test_data("55be28902c077.png", "temple", "brisk", "aberrance");
            add_test_data("55be28960e1f2.png", "temple", "brisk", "aberrance");
            add_test_data("55be2883690e0.png", "temple", "brisk", "aberrance");
            add_test_data("55be287297343.png", "temple", "brisk", "aberrance");
            add_test_data("55be64443f6fc.png", "bog", "restorative", "revelation");
            add_test_data("55be19212153c.png", "necropolis", "shrinking", "desperation");
            add_test_data("55bfb176ba8db.png", "???", "electrocuting", "animation");
            add_test_data("55bfc0290ced9.png", "???", "quickening", "lightning");
            add_test_data("55bfc9710cc69.png", "???", "infernal", "animation");
            add_test_data("55bfdf9552fee.png", "???", "divine", "lightning");
            add_test_data("55c0bf3b634fe.png", "???", "morbid", "the_ancestors");
            add_test_data("55c0c87363c76.png", "???", "static", "lightning");
            add_test_data("55c1e91bb28f6.png", "dunes", "enlarging", "combustion");
            add_test_data("55c1ec7e0b2b8.png", "overgrown_shrine", "static", "influence");
            add_test_data("55c09a82c858f.png", "???", "brisk");
            add_test_data("55c09bb83078c.png", "???", "consuming");
            add_test_data("55c22f0e846c6.png", "strand", "static", "winter");
            add_test_data("55c27bc0bfbeb.png", "strand", "morbid");
            add_test_data("55c28d1110f4b.png", "strand", "galvanizing", "combustion");
            add_test_data("55c35c7237eb2.png", "mine", "seething", "fire");
            add_test_data("55c108c6d292a.png", "dunes", "crushing", "contamination");
            add_test_data("55c112ac233d5.png", "channel", "glacial");
            add_test_data("55c354af12388.png", "labyrinth", "shimmering");
            add_test_data("55c1348c3213c.png", "ghetto", "shivering");
            add_test_data("55c2359dd0057.png", "arachnid_nest", "shivering", "influence");
            add_test_data("55c2392bbf38e.png", "colonnade", "quickening", "ice");
            add_test_data("55c21975c48ff.png", "pier", "infernal");
            add_test_data("55c23258b4e0a.png", "catacomb", "enlarging", "intensity");
            add_test_data("55c122861e672.png", "arcade", "galvanizing", "fire");
            add_test_data("55c141600388e.png", "shore", "arctic", "desperation");
        }

        static async Task load_maps()
        {
            var raw_data = await TempestAPI.get_raw("maps", "v0");
            Dictionary<string, int> map_levels = JsonConvert.DeserializeObject<Dictionary<string, int>>(raw_data);

            foreach (KeyValuePair<string, int> kv in map_levels) {
                map_data.Add(new Map
                {
                    name = kv.Key,
                    level = kv.Value
                });
            }
        }

        static void Main(string[] args)
        {
            string exe_dir = (new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location)).Directory.FullName;
            string images_dir = Path.Combine(exe_dir, "images");

            Console.Write("Loading... ");
            Task[] tasks = new Task[4]
            {
                load_test_data(),
                TempestNotifier.OCR.TesseractWrapper.init(),
                TempestAffixData.init(),
                load_maps()
            };
            Task.WaitAll(tasks);
            Console.Write("Done\n");

            foreach (KeyValuePair<string, MapTempest> kv in test_data) {
                Console.Write("Parsing {0} ", kv.Key);

                string image_path = Path.Combine(images_dir, kv.Key);
                MapTempest mt = TempestNotifier.OCR.Image.read_map_tempest(image_path, map_data);

                if (mt.map != kv.Value.map) {
                    Console.Write(" [ERROR]\n");
                    Console.WriteLine("MAP {0} does not match expected {1}", mt.map, kv.Value.map);
                    continue;
                }

                if (mt.tempest.prefix != kv.Value.tempest.prefix) {
                    Console.Write(" [ERROR]\n");
                    Console.WriteLine("PREFIX {0} does not match expected {1}", mt.tempest.prefix, kv.Value.tempest.prefix);
                    continue;
                }

                if (mt.tempest.suffix != kv.Value.tempest.suffix) {
                    Console.Write(" [ERROR]\n");
                    Console.WriteLine("SUFFIX {0} does not match expected {1}", mt.tempest.suffix, kv.Value.tempest.suffix);
                    continue;
                }

                Console.Write(" [DONE]\n");
            }

            Console.ReadKey();
        }
    }
}
