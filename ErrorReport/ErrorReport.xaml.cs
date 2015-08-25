using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ErrorReport
{
    public partial class MainWindow : Window
    {
        private static string post_uri = "http://api.pajlada.se/v1/report/";
        private static string application_name = "TempestNotifier";
        private string log = "";
        private bool first_time = true;
        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length > 1) {
                try {
                    using (StreamReader reader = File.OpenText(new FileInfo(args[1]).FullName)) {
                        log = reader.ReadToEnd();
                    }
                } catch (Exception) {
                    // do nothing
                }
            }
        }

        private void btn_close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void btn_send_Click(object sender, RoutedEventArgs e)
        {
            await Dispatcher.BeginInvoke(new Action(() =>
            {

                btn_send.IsEnabled = false;
                lbl_status.Content = "Uploading error data...";
            }));
                string description = "";
                if (tb_description.Text != "Describe what you were doing at the time of the crash here (optional).") {
                    description = tb_description.Text;
                }
                using (var client = new HttpClient()) {
                    client.BaseAddress = new Uri(post_uri);
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("application", application_name),
                        new KeyValuePair<string, string>("desc", description),
                        new KeyValuePair<string, string>("log", log),
                    });
                    var result = client.PostAsync("error/", content).Result;
                    string result_content = result.Content.ReadAsStringAsync().Result;

                    // The result doesn't matter to us, once the log has been sent we will just quit.
                    Application.Current.Shutdown();
                }
        }

        private void tb_description_GotFocus(object sender, RoutedEventArgs e)
        {
            if (first_time) {
                first_time = false;
                tb_description.Foreground = Brushes.Black;
                if (tb_description.Text == "Describe what you were doing at the time of the crash here (optional).") {
                    tb_description.Text = "";
                }
            }
        }
    }
}
