// 학사일정 · 급식 (NEIS 교육정보 개방 포털) / 날씨 (Open-Meteo 또는 기상청 단기예보)
// 화면보호기 실행 중 백그라운드에서 받아 %LOCALAPPDATA%\BaejeongFlip\info-cache.json 에 저장합니다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace BaejeongFlip
{
    class SchoolEvent { public string Name; public string Start; public string End; }   // 날짜는 yyyyMMdd
    class MealDay { public string Date; public string Kind; public List<string> Menu = new List<string>(); }

    class InfoData
    {
        public string WeatherAt, WeatherKey;          // 받아 온 시각(yyyyMMddHHmm), 설정 식별값
        public string SchoolAt, SchoolKey;
        public bool HasWeather;
        public double Temp, TMin, TMax;
        public int Pop = -1;                           // 강수확률 %, 모르면 -1
        public string Sky, Icon, WeatherSource;        // Icon: sun, partly, cloud, rain, snow, thunder, fog
        public List<SchoolEvent> Events = new List<SchoolEvent>();
        public List<MealDay> Meals = new List<MealDay>();
    }

    static class InfoService
    {
        static readonly object lk = new object();
        static InfoData current;
        static int version, running;
        public static int Version { get { lock (lk) return version; } }
        public static InfoData Current { get { lock (lk) return current; } }

        static string CacheDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaejeongFlip"); } }
        static string CachePath { get { return Path.Combine(CacheDir, "info-cache.json"); } }

        public static void SetForTest(InfoData d) { lock (lk) { current = d; version++; } }

        public static void LoadCache()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var d = new JavaScriptSerializer().Deserialize<InfoData>(File.ReadAllText(CachePath, Encoding.UTF8));
                    if (d != null) lock (lk) { current = d; version++; }
                }
            }
            catch { }
        }

        static void Publish(InfoData d)
        {
            lock (lk) { current = d; version++; }
            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(CachePath, new JavaScriptSerializer().Serialize(d), Encoding.UTF8);
            }
            catch { }
        }

        /// 오래된 정보가 있으면 백그라운드에서 새로 받기 (동시에 한 번만)
        public static void RefreshIfStale(Settings cfg)
        {
            if (!(cfg.ShowWeather || cfg.ShowSchedule || cfg.ShowMeal)) return;
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Refresh(cfg, false); } catch { }
                finally { Interlocked.Exchange(ref running, 0); }
            });
        }

        static bool Stale(string at, TimeSpan maxAge, bool sameDayRequired)
        {
            DateTime t;
            if (at == null || !DateTime.TryParseExact(at, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return true;
            if (sameDayRequired && t.Date != DateTime.Today) return true;
            return DateTime.Now - t > maxAge;
        }

        /// 정보 받기. force=true 이면 시간과 관계없이 받음. 결과 요약 문자열을 돌려줌 (설정 창의 "지금 가져와 보기" 용)
        public static string Refresh(Settings c, bool force)
        {
            var old = Current;
            var d = old == null ? new InfoData() : new JavaScriptSerializer().Deserialize<InfoData>(new JavaScriptSerializer().Serialize(old));
            var report = new StringBuilder();
            bool changed = false;
            string stamp = DateTime.Now.ToString("yyyyMMddHHmm");

            if (c.ShowWeather)
            {
                string wkey = c.WeatherSource + "|" + c.Lat + "|" + c.Lon;
                if (force || d.WeatherKey != wkey || Stale(d.WeatherAt, TimeSpan.FromMinutes(30), false))
                {
                    try
                    {
                        FetchWeatherWithFallback(c, d, report);
                        d.HasWeather = true; d.WeatherAt = stamp; d.WeatherKey = wkey; changed = true;
                        report.AppendLine(string.Format("날씨 ({0}): {1:0.#}° {2}, 최저 {3:0}° / 최고 {4:0}°{5}",
                            d.WeatherSource, d.Temp, d.Sky, d.TMin, d.TMax, d.Pop >= 0 ? ", 강수확률 " + d.Pop + "%" : ""));
                    }
                    catch (Exception ex) { report.AppendLine("날씨: 실패 - " + Short(ex)); }
                }
                else report.AppendLine("날씨: 최근에 받은 정보 사용");
            }

            if (c.ShowSchedule || c.ShowMeal)
            {
                string skey = c.AtptCode + "|" + c.SchoolCode + "|" + (c.NeisKey ?? "").Length;
                if (force || d.SchoolKey != skey || Stale(d.SchoolAt, TimeSpan.FromHours(3), true))
                {
                    bool ok = true;
                    if (c.ShowSchedule)
                    {
                        try
                        {
                            d.Events = FetchEvents(c, DateTime.Today);
                            report.AppendLine("학사일정: 앞으로 90일 동안 " + d.Events.Count + "건" +
                                ((c.NeisKey ?? "").Trim().Length == 0 ? " (인증키 없음: 구간마다 최대 5건)" : ""));
                        }
                        catch (Exception ex) { ok = false; report.AppendLine("학사일정: 실패 - " + Short(ex)); }
                    }
                    if (c.ShowMeal)
                    {
                        try
                        {
                            d.Meals = FetchMeals(c, DateTime.Today);
                            report.AppendLine("급식: 앞으로 7일 동안 " + d.Meals.Count + "끼");
                        }
                        catch (Exception ex) { ok = false; report.AppendLine("급식: 실패 - " + Short(ex)); }
                    }
                    if (ok) { d.SchoolAt = stamp; d.SchoolKey = skey; }
                    changed = true;
                }
                else report.AppendLine("학교 정보: 오늘 받은 정보 사용");
            }

            if (changed) Publish(d);
            return report.Length == 0 ? "표시할 정보가 선택되지 않았습니다." : report.ToString().TrimEnd();
        }

        // 고른 날씨 서버가 실패하면 다른 서버로 한 번 더 시도.
        // 학교망에서 해외 서버(Open-Meteo)가 막혀도, 기상청 인증키가 있으면 기상청으로 받음
        static void FetchWeatherWithFallback(Settings c, InfoData d, StringBuilder report)
        {
            bool kmaFirst = c.WeatherSource == 1;
            bool hasKma = !string.IsNullOrEmpty((c.KmaKey ?? "").Trim());
            try
            {
                if (kmaFirst) FetchKma(c, d); else FetchOpenMeteo(c, d);
            }
            catch (Exception first)
            {
                if (!kmaFirst && !hasKma)
                    throw new Exception(Short(first) + " (Open-Meteo 서버에 연결하지 못했습니다. 학교망에서 막혔을 수 있으니 기상청 인증키를 넣으면 기상청으로 자동 전환합니다.)");
                report.AppendLine("날씨: " + (kmaFirst ? "기상청" : "Open-Meteo") + " 실패 (" + Short(first) + "), " +
                                  (kmaFirst ? "Open-Meteo" : "기상청") + "으로 다시 시도");
                if (kmaFirst) FetchOpenMeteo(c, d); else FetchKma(c, d);
            }
        }

        static string Short(Exception ex)
        {
            var we = ex as WebException;
            if (we != null && we.Response is HttpWebResponse)
                return "서버 응답 " + (int)((HttpWebResponse)we.Response).StatusCode + " (" + ex.Message + ")";
            return ex.Message;
        }

        // ───────── HTTP / JSON ─────────
        class TimeoutWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri a)
            {
                var r = base.GetWebRequest(a);
                r.Timeout = 15000;
                return r;
            }
        }

        static string Get(string url)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2
            using (var wc = new TimeoutWebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "BaejeongFlip/1.0";
                return wc.DownloadString(url);
            }
        }

        static object J(object o, params object[] path)
        {
            foreach (var p in path)
            {
                if (o == null) return null;
                if (p is string)
                {
                    var dct = o as Dictionary<string, object>;
                    object v;
                    if (dct == null || !dct.TryGetValue((string)p, out v)) return null;
                    o = v;
                }
                else
                {
                    var arr = o as object[];
                    int i = (int)p;
                    if (arr == null || i >= arr.Length) return null;
                    o = arr[i];
                }
            }
            return o;
        }

        static double Num(object o, double def)
        {
            if (o == null) return def;
            double v;
            if (double.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return def;
        }

        static string Inv(double v) { return v.ToString("0.0000", CultureInfo.InvariantCulture); }

        // ───────── NEIS ─────────
        static object[] NeisRows(string service, string url)
        {
            var root = new JavaScriptSerializer().DeserializeObject(Get(url));
            var block = J(root, service) as object[];
            if (block == null)
            {
                string code = J(root, "RESULT", "CODE") as string, msg = J(root, "RESULT", "MESSAGE") as string;
                if (code == "INFO-200") return new object[0];   // 해당하는 데이터 없음
                throw new Exception((msg ?? "알 수 없는 응답") + (code != null ? " [" + code + "]" : ""));
            }
            return (J(block, 1, "row") as object[]) ?? new object[0];
        }

        static string NeisBase(Settings c, string service, int size)
        {
            string key = (c.NeisKey ?? "").Trim();
            return "https://open.neis.go.kr/hub/" + service + "?Type=json&pIndex=1&pSize=" + size +
                   "&ATPT_OFCDC_SC_CODE=" + Uri.EscapeDataString(c.AtptCode.Trim()) +
                   "&SD_SCHUL_CODE=" + Uri.EscapeDataString(c.SchoolCode.Trim()) +
                   (key.Length > 0 ? "&KEY=" + Uri.EscapeDataString(key) : "");
        }

        static List<SchoolEvent> FetchEvents(Settings c, DateTime today)
        {
            bool hasKey = (c.NeisKey ?? "").Trim().Length > 0;
            // 인증키가 없으면 한 번에 5건만 오므로, 기간을 여러 구간으로 나눠 받습니다.
            int[][] windows = hasKey ? new[] { new[] { 0, 90 } }
                                     : new[] { new[] { 0, 3 }, new[] { 4, 10 }, new[] { 11, 24 }, new[] { 25, 45 }, new[] { 46, 90 } };
            var raw = new List<KeyValuePair<DateTime, string>>();
            foreach (var w in windows)
            {
                string url = NeisBase(c, "SchoolSchedule", hasKey ? 500 : 5) +
                             "&AA_FROM_YMD=" + today.AddDays(w[0]).ToString("yyyyMMdd") +
                             "&AA_TO_YMD=" + today.AddDays(w[1]).ToString("yyyyMMdd");
                foreach (var r in NeisRows("SchoolSchedule", url))
                {
                    string name = ((J(r, "EVENT_NM") as string) ?? "").Trim();
                    DateTime dt;
                    if (name.Length == 0 || name.Contains("토요휴업일")) continue;
                    if (!DateTime.TryParseExact(J(r, "AA_YMD") as string, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) continue;
                    raw.Add(new KeyValuePair<DateTime, string>(dt, name));
                }
            }
            // 같은 이름이 며칠 이어지면 하나로 묶기 (주말 사이 끊김은 3일까지 이어 붙임)
            var result = new List<SchoolEvent>();
            foreach (var grp in raw.Distinct().GroupBy(x => x.Value))
            {
                DateTime? s = null, e = null;
                foreach (var dt in grp.Select(x => x.Key).OrderBy(x => x))
                {
                    if (s == null) { s = e = dt; continue; }
                    if ((dt - e.Value).TotalDays <= 3) e = dt;
                    else { result.Add(Ev(grp.Key, s.Value, e.Value)); s = e = dt; }
                }
                if (s != null) result.Add(Ev(grp.Key, s.Value, e.Value));
            }
            return result.OrderBy(x => x.Start).ThenBy(x => x.Name).ToList();
        }

        static SchoolEvent Ev(string n, DateTime s, DateTime e)
        {
            return new SchoolEvent { Name = n, Start = s.ToString("yyyyMMdd"), End = e.ToString("yyyyMMdd") };
        }

        static readonly Regex Allergy = new Regex(@"\(\s*[\d\.\s,]+\)|(\d+\.)+\d*\s*$|[*#&]", RegexOptions.Compiled);

        static List<MealDay> FetchMeals(Settings c, DateTime today)
        {
            bool hasKey = (c.NeisKey ?? "").Trim().Length > 0;
            int[][] windows = hasKey ? new[] { new[] { 0, 7 } } : new[] { new[] { 0, 1 }, new[] { 2, 7 } };
            var list = new List<MealDay>();
            foreach (var w in windows)
            {
                string url = NeisBase(c, "mealServiceDietInfo", hasKey ? 100 : 5) +
                             "&MLSV_FROM_YMD=" + today.AddDays(w[0]).ToString("yyyyMMdd") +
                             "&MLSV_TO_YMD=" + today.AddDays(w[1]).ToString("yyyyMMdd");
                foreach (var r in NeisRows("mealServiceDietInfo", url))
                {
                    var m = new MealDay { Date = J(r, "MLSV_YMD") as string, Kind = ((J(r, "MMEAL_SC_NM") as string) ?? "").Trim() };
                    foreach (var item in Regex.Split((J(r, "DDISH_NM") as string) ?? "", @"<br\s*/?>", RegexOptions.IgnoreCase))
                    {
                        string t = Allergy.Replace(item, "").Trim();
                        t = Regex.Replace(t, @"\s{2,}", " ");
                        if (t.Length > 0) m.Menu.Add(t);
                    }
                    if (m.Date != null && m.Menu.Count > 0 && !list.Any(x => x.Date == m.Date && x.Kind == m.Kind)) list.Add(m);
                }
            }
            return list.OrderBy(x => x.Date).ThenBy(x => KindOrder(x.Kind)).ToList();
        }

        public static int KindOrder(string k) { return k == "조식" ? 0 : k == "중식" ? 1 : k == "석식" ? 2 : 3; }

        // ───────── 날씨: Open-Meteo (인증키 없음) ─────────
        static void FetchOpenMeteo(Settings c, InfoData d)
        {
            double lat = Num(c.Lat, 35.139), lon = Num(c.Lon, 129.069);
            string url = "https://api.open-meteo.com/v1/forecast?latitude=" + Inv(lat) + "&longitude=" + Inv(lon) +
                         "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                         "&timezone=Asia%2FSeoul&forecast_days=1";
            var root = new JavaScriptSerializer().DeserializeObject(Get(url));
            if (J(root, "error") != null) throw new Exception(Convert.ToString(J(root, "reason")));
            d.Temp = Num(J(root, "current", "temperature_2m"), double.NaN);
            if (double.IsNaN(d.Temp)) throw new Exception("현재 기온이 응답에 없습니다.");
            int code = (int)Num(J(root, "current", "weather_code"), 0);
            d.TMax = Num(J(root, "daily", "temperature_2m_max", 0), d.Temp);
            d.TMin = Num(J(root, "daily", "temperature_2m_min", 0), d.Temp);
            d.Pop = (int)Num(J(root, "daily", "precipitation_probability_max", 0), -1);
            string sky, icon; Wmo(code, out sky, out icon);
            d.Sky = sky; d.Icon = icon; d.WeatherSource = "Open-Meteo";
        }

        static void Wmo(int code, out string sky, out string icon)
        {
            if (code == 0) { sky = "맑음"; icon = "sun"; }
            else if (code == 1) { sky = "대체로 맑음"; icon = "partly"; }
            else if (code == 2) { sky = "구름 조금"; icon = "partly"; }
            else if (code == 3) { sky = "흐림"; icon = "cloud"; }
            else if (code == 45 || code == 48) { sky = "안개"; icon = "fog"; }
            else if (code >= 51 && code <= 57) { sky = "이슬비"; icon = "rain"; }
            else if (code >= 61 && code <= 67) { sky = "비"; icon = "rain"; }
            else if (code >= 71 && code <= 77) { sky = "눈"; icon = "snow"; }
            else if (code >= 80 && code <= 82) { sky = "소나기"; icon = "rain"; }
            else if (code == 85 || code == 86) { sky = "눈"; icon = "snow"; }
            else if (code >= 95) { sky = "뇌우"; icon = "thunder"; }
            else { sky = "흐림"; icon = "cloud"; }
        }

        // ───────── 날씨: 기상청 단기예보 (공공데이터포털 인증키) ─────────
        public static void LatLonToGrid(double lat, double lon, out int nx, out int ny)
        {
            const double RE = 6371.00877, GRID = 5.0, SLAT1 = 30.0, SLAT2 = 60.0, OLON = 126.0, OLAT = 38.0, XO = 43, YO = 136;
            double DEGRAD = Math.PI / 180.0;
            double re = RE / GRID, slat1 = SLAT1 * DEGRAD, slat2 = SLAT2 * DEGRAD, olon = OLON * DEGRAD, olat = OLAT * DEGRAD;
            double sn = Math.Tan(Math.PI * 0.25 + slat2 * 0.5) / Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
            sn = Math.Log(Math.Cos(slat1) / Math.Cos(slat2)) / Math.Log(sn);
            double sf = Math.Tan(Math.PI * 0.25 + slat1 * 0.5);
            sf = Math.Pow(sf, sn) * Math.Cos(slat1) / sn;
            double ro = Math.Tan(Math.PI * 0.25 + olat * 0.5);
            ro = re * sf / Math.Pow(ro, sn);
            double ra = Math.Tan(Math.PI * 0.25 + lat * DEGRAD * 0.5);
            ra = re * sf / Math.Pow(ra, sn);
            double theta = lon * DEGRAD - olon;
            if (theta > Math.PI) theta -= 2.0 * Math.PI;
            if (theta < -Math.PI) theta += 2.0 * Math.PI;
            theta *= sn;
            nx = (int)Math.Floor(ra * Math.Sin(theta) + XO + 0.5);
            ny = (int)Math.Floor(ro - ra * Math.Cos(theta) + YO + 0.5);
        }

        static object[] KmaItems(string url)
        {
            string body = Get(url).Trim();
            if (body.StartsWith("<"))
            {
                var m = Regex.Match(body, @"<(returnAuthMsg|resultMsg|errMsg)>([^<]*)<");
                throw new Exception("기상청: " + (m.Success ? m.Groups[2].Value : "XML 오류 응답") + " (인증키를 확인하세요)");
            }
            var root = new JavaScriptSerializer().DeserializeObject(body);
            string code = J(root, "response", "header", "resultCode") as string;
            if (code != "00") throw new Exception("기상청: " + (J(root, "response", "header", "resultMsg") as string) + " [" + code + "]");
            return (J(root, "response", "body", "items", "item") as object[]) ?? new object[0];
        }

        static void FetchKma(Settings c, InfoData d)
        {
            string key = (c.KmaKey ?? "").Trim();
            if (key.Length == 0) throw new Exception("기상청 인증키가 비어 있습니다. 설정에서 넣어 주세요.");
            string k = key.Contains("%") ? key : Uri.EscapeDataString(key);
            int nx, ny;
            LatLonToGrid(Num(c.Lat, 35.139), Num(c.Lon, 129.069), out nx, out ny);
            const string Base = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/";
            DateTime now = DateTime.Now;
            string today = now.ToString("yyyyMMdd");

            // 1) 단기예보: 하루 최저/최고, 강수확률, 하늘 상태
            DateTime vb = now >= now.Date.AddHours(2).AddMinutes(15) ? now.Date.AddHours(2) : now.Date.AddDays(-1).AddHours(23);
            var fc = KmaItems(Base + "getVilageFcst?serviceKey=" + k + "&pageNo=1&numOfRows=1000&dataType=JSON&base_date=" +
                              vb.ToString("yyyyMMdd") + "&base_time=" + vb.ToString("HHmm") + "&nx=" + nx + "&ny=" + ny);
            double tmn = double.NaN, tmx = double.NaN, tmpMin = double.MaxValue, tmpMax = double.MinValue, tmpNow = double.NaN;
            int pop = -1, sky = 1, pty = 0;
            string curHour = now.ToString("HH00");
            string bestTime = null;
            foreach (var it in fc)
            {
                if ((J(it, "fcstDate") as string) != today) continue;
                string cat = J(it, "category") as string, time = J(it, "fcstTime") as string;
                double v = Num(J(it, "fcstValue"), double.NaN);
                if (cat == "TMN") tmn = v;
                else if (cat == "TMX") tmx = v;
                else if (cat == "TMP" && !double.IsNaN(v)) { tmpMin = Math.Min(tmpMin, v); tmpMax = Math.Max(tmpMax, v); }
                if (string.CompareOrdinal(time, curHour) < 0) continue;     // 지난 시각 제외
                if (cat == "POP" && !double.IsNaN(v)) pop = Math.Max(pop, (int)v);
                if (bestTime == null || string.CompareOrdinal(time, bestTime) < 0) bestTime = time;
            }
            foreach (var it in fc)
            {
                if ((J(it, "fcstDate") as string) != today || (J(it, "fcstTime") as string) != bestTime) continue;
                string cat = J(it, "category") as string;
                double v = Num(J(it, "fcstValue"), double.NaN);
                if (cat == "SKY" && !double.IsNaN(v)) sky = (int)v;
                if (cat == "PTY" && !double.IsNaN(v)) pty = (int)v;
                if (cat == "TMP") tmpNow = v;
            }

            // 2) 초단기실황: 현재 기온, 강수 형태 (매시 40분 이후 발표)
            try
            {
                DateTime nb = now.AddMinutes(-45);
                var nc = KmaItems(Base + "getUltraSrtNcst?serviceKey=" + k + "&pageNo=1&numOfRows=20&dataType=JSON&base_date=" +
                                  nb.ToString("yyyyMMdd") + "&base_time=" + nb.ToString("HH00") + "&nx=" + nx + "&ny=" + ny);
                foreach (var it in nc)
                {
                    string cat = J(it, "category") as string;
                    double v = Num(J(it, "obsrValue"), double.NaN);
                    if (cat == "T1H" && !double.IsNaN(v)) tmpNow = v;
                    if (cat == "PTY" && !double.IsNaN(v)) pty = (int)v;
                }
            }
            catch { /* 실황이 없으면 예보 기온 사용 */ }

            if (double.IsNaN(tmpNow)) throw new Exception("기상청: 기온 정보가 응답에 없습니다.");
            d.Temp = tmpNow;
            d.TMin = !double.IsNaN(tmn) ? tmn : (tmpMin != double.MaxValue ? Math.Min(tmpMin, tmpNow) : tmpNow);
            d.TMax = !double.IsNaN(tmx) ? tmx : (tmpMax != double.MinValue ? Math.Max(tmpMax, tmpNow) : tmpNow);
            d.TMin = Math.Min(d.TMin, d.Temp);            // 예보 최저·최고보다 현재 기온이 벗어나면 현재 기온 반영
            d.TMax = Math.Max(d.TMax, d.Temp);
            d.Pop = pop;
            if (pty == 1 || pty == 5) { d.Sky = "비"; d.Icon = "rain"; }
            else if (pty == 2 || pty == 6) { d.Sky = "비 또는 눈"; d.Icon = "rain"; }
            else if (pty == 3 || pty == 7) { d.Sky = "눈"; d.Icon = "snow"; }
            else if (pty == 4) { d.Sky = "소나기"; d.Icon = "rain"; }
            else if (sky == 4) { d.Sky = "흐림"; d.Icon = "cloud"; }
            else if (sky == 3) { d.Sky = "구름많음"; d.Icon = "partly"; }
            else { d.Sky = "맑음"; d.Icon = "sun"; }
            d.WeatherSource = "기상청";
        }
    }
}
