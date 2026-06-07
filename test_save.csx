using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

var client = new HttpClient();
var json = "{\"id\": \"Test\", \"category\": \"Financial\", \"name\": \"Test Widget\", \"type\": \"echarts_bar\", \"query\": {\"table\": \"GLRX0310\", \"dimensions\": [\"period\"], \"measures\": [{\"field\": \"ptd_amount\", \"agg\": \"Sum\"}], \"filters\": []}}";
var content = new StringContent(json, Encoding.UTF8, "application/json");

var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8001/api/dynamic-widgets/save") { Content = content };
request.Headers.Add("Origin", "http://127.0.0.1:8003");

var response = await client.SendAsync(request);
Console.WriteLine($"Status: {response.StatusCode}");
Console.WriteLine(await response.Content.ReadAsStringAsync());
