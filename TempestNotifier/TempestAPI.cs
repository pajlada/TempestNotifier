using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace TempestNotifier
{
    class TempestAPI
    {
        public static async Task<string> get_raw(string endpoint, string version="v1")
        {
            using (var client = new HttpClient()) {
                client.BaseAddress = new Uri("http://poetempest.com/api/"+version+"/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync(endpoint);
                if (response.IsSuccessStatusCode) {
                    return response.Content.ReadAsStringAsync().Result;
                }
            }
            return "";
        }
    }
}
