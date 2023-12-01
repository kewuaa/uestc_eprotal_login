using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using HtmlAgilityPack;


namespace UESTC {
    public enum EportalApp {
        GraduteSystem,
        BBS,
        VPN,
    }

    public class AuthorizeError: Exception {
        public AuthorizeError(): base("Incorrect username or password") {}
    }

    public static class EportalCore {
        private struct SliderData {
            [DllImport("verify.dll", EntryPoint = "calculate_move_length")]
            extern static unsafe double CalculateMoveLength(
                    int fg_width,
                    int bg_width,
                    int height,
                    double* fg_data,
                    double* bg_data
                    );

            static private unsafe void CalculateGray(
                    ref double[] gray,
                    Bitmap image
                    ) {
                var img = image.Clone() as Bitmap;
                var rect = new Rectangle(0, 0, img.Width, img.Height);
                var data = img.LockBits(rect, ImageLockMode.ReadWrite, img.PixelFormat);
                var pixel_size = Image.GetPixelFormatSize(img.PixelFormat) / 8;
                unsafe {
                    var ptr = (byte*)data.Scan0.ToPointer();
                    for (int i = 0; i < img.Height; i++) {
                        var p = ptr + i * data.Stride;
                        for (int j = 0; j < img.Width; j++) {
                            var B = *p++;
                            var G = *p++;
                            var R = *p++;
                            gray[i * img.Width + j] = (byte)((R * 299 + G * 587 + B * 114 + 500) / 1000);
                        }
                    }
                }
                img.UnlockBits(data);
            }

            public string smallImage {get; set;}
            public string bigImage {get; set;}

            public double GetMoveLength() {
                using var fg_ms = new MemoryStream(Convert.FromBase64String(smallImage));
                using var bg_ms = new MemoryStream(Convert.FromBase64String(bigImage));
                var fg = new Bitmap(fg_ms);
                var bg = new Bitmap(bg_ms);
                var fg_array = new double[fg.Width * fg.Height];
                var bg_array = new double[bg.Width * bg.Height];
                CalculateGray(ref fg_array, fg);
                CalculateGray(ref bg_array, bg);
                double move_length;
                unsafe {
                    fixed (double* fg_data = fg_array, bg_data = bg_array) {
                        move_length = CalculateMoveLength(fg.Width, bg.Width, fg.Height, fg_data, bg_data);
                    }
                }
                return move_length;
            }
        }

        private struct VerifyData {
            public string sign {get; set;}
            public string message {get; set;}
            public int code {get; set;}
        }

        static private long GetTimeStamp() => new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

        static public async Task Login(HttpClient client, string username, string password) {
            var url = "https://idas.uestc.edu.cn/authserver/login?"
                + "service=https%3A%2F%2Feportal.uestc.edu.cn%3A443%2Flogin%3Fservice%3Dhttps%3A%2F%2Feportal.uestc.edu.cn%2Fnew%2Findex.html%3Fbrowser%3Dno";
            var page = await client.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(page);
            var inputs = doc.DocumentNode.SelectNodes("//form[@id=\"casLoginForm\"]/div[@class=\"dlk_foot\"]/input");
            var lt = inputs[0].Attributes["value"].Value;
            var dllt = inputs[1].Attributes["value"].Value;
            var execution = inputs[2].Attributes["value"].Value;
            var _eventId = inputs[3].Attributes["value"].Value;
            var rmShown = inputs[4].Attributes["value"].Value;
            var key = inputs[5].Attributes["value"].Value;
            var slider_data = JsonSerializer.Deserialize<SliderData>(
                await client.GetStringAsync($"https://idas.uestc.edu.cn/authserver/sliderCaptcha.do?_={GetTimeStamp()}")
            );
            int move_length = (int)Math.Round(slider_data.GetMoveLength() * 280);
            var verify_data = JsonSerializer.Deserialize<VerifyData>(
                await client.GetStringAsync($"https://idas.uestc.edu.cn/authserver/verifySliderImageCode.do?canvasLength=280&moveLength={move_length}")
            );
            if (verify_data.code != 0) {
                throw new Exception(verify_data.message);
            }
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var data = new[] {
                new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", CryptoLib.Encrypt(password, key)),
                    new KeyValuePair<string, string>("lt", lt),
                    new KeyValuePair<string, string>("dllt", dllt),
                    new KeyValuePair<string, string>("execution", execution),
                    new KeyValuePair<string, string>("_eventId", _eventId),
                    new KeyValuePair<string, string>("rmShown", rmShown),
                    new KeyValuePair<string, string>("sign", verify_data.sign),
            };
            request.Content = new FormUrlEncodedContent(data);
            var res = await client.SendAsync(request);
            res.EnsureSuccessStatusCode();
            page = await res.Content.ReadAsStringAsync();
            if (page.Contains("class=\"auth_error\"")) {
                throw new AuthorizeError();
            }
            // foreach (Cookie c in handle.CookieContainer.GetCookies(new Uri("https://idas.uestc.edu.cn"))) {
            //      Console.WriteLine($"Cookie: {c.Name}={c.Value}");
            // }
            // foreach (Cookie c in handle.CookieContainer.GetCookies(new Uri("https://eportal.uestc.edu.cn"))) {
            //      Console.WriteLine($"Cookie: {c.Name}={c.Value}");
            // }
        }

        static public async Task VisitApp(HttpClient client, EportalApp app) {
            var appid = app switch {
                EportalApp.GraduteSystem => 5609306976424512,
                EportalApp.BBS => 5827229364820279,
                EportalApp.VPN => 5827206405491321,
                _ => throw new Exception("unreachable"),
            };
            var res = await client.GetAsync($"https://eportal.uestc.edu.cn/appShow?appId={appid}");
            while (((int)res.StatusCode) == 302) {
                res = await client.GetAsync(res.Headers.Location);
            }
            var page = await res.Content.ReadAsStringAsync();
        }
    }
}
