using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace TempestNotifier.OCR
{
    public class TesseractWrapper
    {
        public static TesseractEngine engine = null;

        public static async Task init()
        {
            try {
                string exe_dir = (new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location)).Directory.FullName;
                string tessdata_dir = Path.Combine(exe_dir, "tessdata");
                engine = new TesseractEngine(tessdata_dir, "eng", EngineMode.TesseractAndCube);
            } catch (Exception e) {
                Trace.TraceError(e.ToString());
                Console.WriteLine("Unexpected Error: " + e.Message);
                Console.WriteLine("Details: ");
                Console.WriteLine(e.ToString());
            }
        }

        public static string get_text(string image_path)
        {
            try {
                using (var img = Pix.LoadFromFile(image_path)) {
                    using (var page = engine.Process(img)) {
                        var text = page.GetText();
                        return text;
                    }
                }
            } catch (Exception e) {
                Trace.TraceError(e.ToString());
                Console.WriteLine("Unexpected Error: " + e.Message);
                Console.WriteLine("Details: ");
                Console.WriteLine(e.ToString());
            }

            return "";
        }
    }
}
