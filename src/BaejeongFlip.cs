// 배정플립 — 배정고등학교 플립시계 화면보호기 (Windows .scr)
// 빌드: build.bat (Windows) 또는 build.sh (Linux/Mono) 참고
// 인자:  /s  전체화면 실행   /c  설정 창   /p <hwnd>  미리보기 창
//        /install <초>  화면보호기로 지정   /uninstall  지정 해제
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("배정플립")]
[assembly: AssemblyDescription("배정고등학교 플립시계 화면보호기")]
[assembly: AssemblyProduct("배정플립")]
[assembly: AssemblyCompany("배정고등학교 교육연구부")]
[assembly: AssemblyVersion("1.5.3.0")]
[assembly: AssemblyFileVersion("1.5.3.0")]
[assembly: AssemblyInformationalVersion("1.5.3")]

namespace BaejeongFlip
{
    // ───────────────────────── 설정 ─────────────────────────
    class Settings
    {
        public const string AppName = "배정플립", Version = "1.5.3";
        const string Key = @"Software\BaejeongFlip";
        const string OldKey = @"Software\FlipClockSaver";     // 이름을 바꾸기 전(플립시계) 설정 위치
        public bool H24 = true, ShowSeconds = false, ShowDate = true;
        public string LeftText = "배정고등학교", RightText = "교육연구부";
        // 학교 정보 (NEIS)
        public bool ShowSchedule = true, ShowMeal = true;
        public int RotateSec = 8;                     // 일정이 여러 건이면 이 간격(초)으로 넘김
        public int FlipSpeedMs = 600;                 // 일정 카드가 접혔다 펴지는 데 걸리는 시간(ms)
        public string NeisKey = "", AtptCode = "C10", SchoolCode = "7150136";
        // 날씨
        public bool ShowWeather = true;
        public int WeatherSource = 0;                 // 0: Open-Meteo, 1: 기상청 단기예보
        public string KmaKey = "", Lat = "35.1390", Lon = "129.0690";   // 기본: 배정고등학교 (부산 남구 문현동)
        // 배경: 0 검은 바탕, 1 사진 배경 + 작은 시계 판
        public int BgMode = 0;
        public string PhotoDir = "";
        public bool PhotoSub = true;
        public int PhotoSec = 30;
        public int PhotoFit = 0;                      // 0 화면 채우기(가장자리 잘림), 1 사진 전체 보이기
        public int PanelPos = 2;                      // 0 오른쪽 아래, 1 왼쪽 아래, 2 10분마다 번갈아
        public int PanelSize = 1;                     // 0 작게, 1 보통, 2 크게
        public int DisplaySize = 0;                   // 0 기본 화면, 1 시계 중심 화면 (시계가 화면 높이의 약 70%)

        static int I(RegistryKey k, string n, int def) { try { var v = k.GetValue(n); return v == null ? def : Convert.ToInt32(v); } catch { return def; } }

        static bool B(RegistryKey k, string n, bool def) { try { var v = k.GetValue(n); return v == null ? def : Convert.ToInt32(v) != 0; } catch { return def; } }
        static string S(RegistryKey k, string n, string def) { var v = k.GetValue(n) as string; return v ?? def; }

        public static Settings Load()
        {
            var s = new Settings();
            MigrateOldSettings();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    if (k != null)
                    {
                        s.H24 = B(k, "H24", s.H24);
                        s.ShowSeconds = B(k, "ShowSeconds", s.ShowSeconds);
                        s.ShowDate = B(k, "ShowDate", s.ShowDate);
                        s.LeftText = S(k, "LeftText", s.LeftText);
                        s.RightText = S(k, "RightText", s.RightText);
                        s.ShowSchedule = B(k, "ShowSchedule", s.ShowSchedule);
                        s.ShowMeal = B(k, "ShowMeal", s.ShowMeal);
                        // 설정 버전 2부터 급식을 기본으로 켬: 이전 버전에서 저장된 "꺼짐"은 한 번만 켜 줌
                        int ver = Convert.ToInt32(k.GetValue("SettingsVersion", 1));
                        if (ver < 2) s.ShowMeal = true;
                        // 설정 버전 3부터 초 표시를 기본으로 끔: 이전 버전에서 저장된 값은 한 번만 끔
                        if (ver < 3) s.ShowSeconds = false;
                        try { s.RotateSec = Convert.ToInt32(k.GetValue("RotateSec", s.RotateSec)); } catch { }
                        try { s.FlipSpeedMs = Convert.ToInt32(k.GetValue("FlipSpeedMs", s.FlipSpeedMs)); } catch { }
                        s.NeisKey = ReadSecret(k, "NeisKeyEnc", "NeisKey");
                        s.AtptCode = S(k, "AtptCode", s.AtptCode);
                        s.SchoolCode = S(k, "SchoolCode", s.SchoolCode);
                        s.ShowWeather = B(k, "ShowWeather", s.ShowWeather);
                        try { s.WeatherSource = Convert.ToInt32(k.GetValue("WeatherSource", 0)); } catch { }
                        s.KmaKey = ReadSecret(k, "KmaKeyEnc", "KmaKey");
                        s.Lat = S(k, "Lat", s.Lat);
                        s.Lon = S(k, "Lon", s.Lon);
                        s.BgMode = I(k, "BgMode", s.BgMode);
                        s.PhotoDir = S(k, "PhotoDir", s.PhotoDir);
                        s.PhotoSub = B(k, "PhotoSub", s.PhotoSub);
                        s.PhotoSec = I(k, "PhotoSec", s.PhotoSec);
                        s.PhotoFit = I(k, "PhotoFit", s.PhotoFit);
                        s.PanelPos = I(k, "PanelPos", s.PanelPos);
                        s.PanelSize = I(k, "PanelSize", s.PanelSize);
                        s.DisplaySize = I(k, "DisplaySize", s.DisplaySize);
                    }
                }
            }
            catch { }
            // 인증키가 비어 있으면 설치 폴더의 키 파일을 한 번 읽어 암호화 저장하고, 파일은 지움
            //   neis-key.txt : NEIS 교육정보 개방 포털 인증키
            //   kma-key.txt  : 공공데이터포털 기상청 단기예보 인증키 (함께 설치되면 날씨 서버를 기상청으로)
            string nk = ReadKeyFile("neis-key.txt");
            if (string.IsNullOrEmpty(s.NeisKey) && nk.Length > 0) { s.NeisKey = nk; StoreSecret("NeisKeyEnc", "NeisKey", nk); }
            string kk = ReadKeyFile("kma-key.txt");
            if (string.IsNullOrEmpty(s.KmaKey) && kk.Length > 0)
            {
                s.KmaKey = kk; StoreSecret("KmaKeyEnc", "KmaKey", kk);
                s.WeatherSource = 1;
                try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("WeatherSource", 1, RegistryValueKind.DWord); } catch { }
            }
            return s;
        }

        // ── 인증키 보호: Windows 사용자 계정 암호화(DPAPI). 같은 PC의 같은 사용자만 풀 수 있음 ──
        static readonly byte[] Entropy = System.Text.Encoding.UTF8.GetBytes("BaejeongFlip.v1");

        static string Protect(string plain)
        {
            var data = System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(plain), Entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        static string Unprotect(string b64)
        {
            try
            {
                var data = System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(b64), Entropy,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return System.Text.Encoding.UTF8.GetString(data);
            }
            catch { return ""; }
        }

        // 암호화 값을 읽고, 예전 버전의 평문 값이 남아 있으면 암호화해서 옮긴 뒤 평문은 지움
        static string ReadSecret(RegistryKey k, string encName, string plainName)
        {
            var enc = k.GetValue(encName) as string;
            if (!string.IsNullOrEmpty(enc)) return Unprotect(enc);
            var plain = k.GetValue(plainName) as string;
            if (!string.IsNullOrEmpty(plain)) { StoreSecret(encName, plainName, plain); return plain; }
            return "";
        }

        static void StoreSecret(string encName, string plainName, string value)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (string.IsNullOrEmpty(value)) k.DeleteValue(encName, false);
                    else k.SetValue(encName, Protect(value), RegistryValueKind.String);
                    k.DeleteValue(plainName, false);
                }
            }
            catch { }
        }

        // 설치 폴더의 키 파일을 읽고 바로 지움 (이미 키가 저장돼 있어도 남은 파일은 정리)
        static string ReadKeyFile(string name)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string f = System.IO.Path.Combine(dir, name);
                if (System.IO.File.Exists(f))
                {
                    string key = System.IO.File.ReadAllText(f).Trim();
                    try { System.IO.File.Delete(f); } catch { }
                    return key;
                }
            }
            catch { }
            return "";
        }

        // 예전 이름(플립시계)으로 저장된 설정을 한 번만 옮겨 옴
        static void MigrateOldSettings()
        {
            try
            {
                using (var nk = Registry.CurrentUser.OpenSubKey(Key))
                    if (nk != null) return;
                using (var ok = Registry.CurrentUser.OpenSubKey(OldKey))
                {
                    if (ok == null) return;
                    using (var nk = Registry.CurrentUser.CreateSubKey(Key))
                        foreach (var name in ok.GetValueNames())
                            nk.SetValue(name, ok.GetValue(name), ok.GetValueKind(name));
                }
                Registry.CurrentUser.DeleteSubKeyTree(OldKey, false);
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Key))
                {
                    k.SetValue("SettingsVersion", 3, RegistryValueKind.DWord);
                    k.SetValue("H24", H24 ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("ShowSeconds", ShowSeconds ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("ShowDate", ShowDate ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("LeftText", LeftText ?? "", RegistryValueKind.String);
                    k.SetValue("RightText", RightText ?? "", RegistryValueKind.String);
                    k.SetValue("ShowSchedule", ShowSchedule ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("ShowMeal", ShowMeal ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("RotateSec", RotateSec, RegistryValueKind.DWord);
                    k.SetValue("FlipSpeedMs", FlipSpeedMs, RegistryValueKind.DWord);
                    k.SetValue("AtptCode", AtptCode ?? "", RegistryValueKind.String);
                    k.SetValue("SchoolCode", SchoolCode ?? "", RegistryValueKind.String);
                    k.SetValue("ShowWeather", ShowWeather ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("WeatherSource", WeatherSource, RegistryValueKind.DWord);
                    k.SetValue("Lat", Lat ?? "", RegistryValueKind.String);
                    k.SetValue("Lon", Lon ?? "", RegistryValueKind.String);
                    k.SetValue("BgMode", BgMode, RegistryValueKind.DWord);
                    k.SetValue("PhotoDir", PhotoDir ?? "", RegistryValueKind.String);
                    k.SetValue("PhotoSub", PhotoSub ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("PhotoSec", PhotoSec, RegistryValueKind.DWord);
                    k.SetValue("PhotoFit", PhotoFit, RegistryValueKind.DWord);
                    k.SetValue("PanelPos", PanelPos, RegistryValueKind.DWord);
                    k.SetValue("PanelSize", PanelSize, RegistryValueKind.DWord);
                    k.SetValue("DisplaySize", DisplaySize, RegistryValueKind.DWord);
                    // 인증키는 암호화해서 저장 (평문 값은 남기지 않음)
                    if (string.IsNullOrEmpty(NeisKey)) k.DeleteValue("NeisKeyEnc", false); else k.SetValue("NeisKeyEnc", Protect(NeisKey), RegistryValueKind.String);
                    if (string.IsNullOrEmpty(KmaKey)) k.DeleteValue("KmaKeyEnc", false); else k.SetValue("KmaKeyEnc", Protect(KmaKey), RegistryValueKind.String);
                    k.DeleteValue("NeisKey", false);
                    k.DeleteValue("KmaKey", false);
                }
            }
            catch { }
        }
    }

    // ───────────────────────── 카드 하나 ─────────────────────────
    class FlipCard
    {
        public string Cur, Prev;
        public long Start = -100000;
        public const int FlipMs = 600;

        public bool Set(string v, long now)
        {
            if (v == Cur) return false;
            Prev = Cur ?? v;
            Cur = v;
            Start = (Cur == Prev) ? -100000 : now;
            return true;
        }
        public double Progress(long now) { return Math.Min(1.0, (now - Start) / (double)FlipMs); }
        public bool Animating(long now) { return Progress(now) < 1.0; }
    }

    // ───────────────────────── 그리기 (WinForms와 분리) ─────────────────────────
    class ClockRenderer
    {
        static readonly Color Bg = Color.FromArgb(11, 12, 14);
        static readonly Color CardTop = Color.FromArgb(35, 36, 40);
        static readonly Color CardBot = Color.FromArgb(27, 28, 31);
        static readonly Color Digit = Color.FromArgb(236, 235, 230);
        static readonly Color Muted = Color.FromArgb(125, 127, 134);
        static readonly Color Seam = Color.FromArgb(5, 5, 6);
        static readonly Color Accent = Color.FromArgb(232, 163, 61);
        static readonly string[] Days = { "일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일" };

        public Settings Cfg;
        readonly FlipCard[] hh = { new FlipCard(), new FlipCard() };
        readonly FlipCard[] mm = { new FlipCard(), new FlipCard() };
        readonly FlipCard[] ss = { new FlipCard(), new FlipCard() };
        readonly FlipCard[] yy = { new FlipCard(), new FlipCard(), new FlipCard(), new FlipCard() };
        readonly FlipCard[] mo = { new FlipCard(), new FlipCard() };
        readonly FlipCard[] dd = { new FlipCard(), new FlipCard() };
        readonly FlipCard wd = new FlipCard();
        string ampm = "AM";

        readonly FontFamily digitFamily, koFamily;
        readonly FontStyle digitStyle;
        readonly Dictionary<string, GraphicsPath> pathCache = new Dictionary<string, GraphicsPath>();
        readonly Dictionary<string, float> emCache = new Dictionary<string, float>();

        // 번인 방지용 이동
        PointF drift, driftFrom, driftTo;
        long driftStart = -100000;
        long lastDriftPick;
        const int DriftMs = 8000;
        readonly Random rng = new Random();

        PhotoShow photos;                     // 사진 배경 (BgMode == 1)

        public ClockRenderer(Settings cfg)
        {
            Cfg = cfg;
            if (cfg.BgMode == 1 && !string.IsNullOrEmpty(cfg.PhotoDir))
                photos = new PhotoShow(cfg.PhotoDir, cfg.PhotoSub, cfg.PhotoFit == 1, cfg.PhotoSec);
            digitFamily = PickFamily(out digitStyle,
                "Bahnschrift SemiBold Condensed", "Bahnschrift SemiBold SemiConden", "Bahnschrift Condensed",
                "Bahnschrift", "Arial Narrow", "DejaVu Sans Condensed", "Arial");
            FontStyle dummy;
            koFamily = PickFamily(out dummy, "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Gulim", "Arial");
        }

        static FontFamily PickFamily(out FontStyle style, params string[] names)
        {
            foreach (var n in names)
            {
                try
                {
                    var f = new FontFamily(n);
                    if (!f.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) { f.Dispose(); continue; }
                    style = f.IsStyleAvailable(FontStyle.Bold) && (n == "Arial Narrow" || n == "Arial" || n.StartsWith("DejaVu"))
                        ? FontStyle.Bold : FontStyle.Regular;
                    if (!f.IsStyleAvailable(style)) style = FontStyle.Regular;
                    return f;
                }
                catch { }
            }
            style = FontStyle.Bold;
            return FontFamily.GenericSansSerif;
        }

        // 시간 갱신: 바뀐 것이 있으면 true
        DateTime shown = DateTime.Now;   // 화면에 보이는 시각 (일정·급식 기준일)

        public bool Update(DateTime t, long now)
        {
            shown = t;
            bool changed = false;
            int h = t.Hour;
            if (!Cfg.H24) { ampm = h < 12 ? "AM" : "PM"; h = h % 12; if (h == 0) h = 12; }
            changed |= SetGroup(hh, h.ToString("00"), now);
            changed |= SetGroup(mm, t.Minute.ToString("00"), now);
            changed |= SetGroup(ss, t.Second.ToString("00"), now);
            changed |= SetGroup(yy, t.Year.ToString("0000"), now);
            changed |= SetGroup(mo, t.Month.ToString("00"), now);
            changed |= SetGroup(dd, t.Day.ToString("00"), now);
            changed |= wd.Set(Days[(int)t.DayOfWeek], now);
            return changed;
        }

        static bool SetGroup(FlipCard[] g, string s, long now)
        {
            bool c = false;
            for (int i = 0; i < g.Length; i++) c |= g[i].Set(s[i].ToString(), now);
            return c;
        }

        /// 사진 준비·전환. 다시 그려야 하면 true
        public bool TickPhotos(long now, Size size)
        {
            return photos != null && photos.Tick(now, size);
        }

        public void LoadPhotoNowForTest(Size size) { if (photos != null) photos.LoadNowForTest(size); }

        /// 사진 배경일 때 ← / → 로 사진 넘기기. 처리했으면 true (화면보호기를 끄지 않음)
        public bool NavigatePhoto(int dir, long now) { return photos != null && photos.HasImage && photos.Navigate(dir, now); }

        public void DisposePhotos() { if (photos != null) { photos.Dispose(); photos = null; } }

        public bool NeedsFrames(long now)
        {
            foreach (var g in new[] { hh, mm, ss, yy, mo, dd })
                foreach (var c in g) if (c.Animating(now)) return true;
            return wd.Animating(now) || (now - driftStart) < DriftMs || CornerFading(now) || RotFlipping(now) || SwapFlipping(now) || PanelAltFlipping(now) || (photos != null && photos.Fading);
        }

        public void TickDrift(long now, bool enabled)
        {
            if (!enabled) return;
            if (now - lastDriftPick >= 60000)
            {
                lastDriftPick = now;
                driftFrom = drift;
                driftTo = new PointF((float)(rng.NextDouble() - 0.5) * 0.06f, (float)(rng.NextDouble() - 0.5) * 0.06f);
                driftStart = now;
            }
            double p = Math.Min(1.0, (now - driftStart) / (double)DriftMs);
            p = p < 0.5 ? 2 * p * p : 1 - Math.Pow(-2 * p + 2, 2) / 2;
            drift = new PointF(driftFrom.X + (float)((driftTo.X - driftFrom.X) * p),
                               driftFrom.Y + (float)((driftTo.Y - driftFrom.Y) * p));
        }

        // ── 텍스트 경로 (원점 중심) ──
        GraphicsPath TextPath(string kind, string text, float targetH)
        {
            int key = (int)Math.Round(targetH);
            string ck = kind + "|" + text + "|" + key;
            GraphicsPath p;
            if (pathCache.TryGetValue(ck, out p)) return p;

            bool dig = kind == "digit" || kind == "temp";
            FontFamily fam = dig ? digitFamily : koFamily;
            FontStyle st = dig ? digitStyle : FontStyle.Bold;
            if (kind == "label" || kind == "corner" || kind == "info") st = FontStyle.Regular;
            if (!fam.IsStyleAvailable(st)) st = FontStyle.Regular;
            string refText = dig ? "0" : kind == "word" ? "요" : kind == "info" || kind == "infob" ? "가" : "년";

            float em;
            string ek = kind + "|" + key;
            RectangleF refB;
            if (!emCache.TryGetValue(ek, out em))
            {
                using (var rp = new GraphicsPath())
                {
                    rp.AddString(refText, fam, (int)st, 100f, PointF.Empty, StringFormat.GenericTypographic);
                    refB = rp.GetBounds();
                }
                em = refB.Height > 0 ? 100f * targetH / refB.Height : targetH;
                emCache[ek] = em;
            }
            using (var rp = new GraphicsPath())
            {
                rp.AddString(refText, fam, (int)st, em, PointF.Empty, StringFormat.GenericTypographic);
                refB = rp.GetBounds();
            }
            p = new GraphicsPath();
            p.AddString(text, fam, (int)st, em, PointF.Empty, StringFormat.GenericTypographic);
            var b = p.GetBounds();
            using (var m = new Matrix())
            {
                // 가로는 자기 글자 기준, 세로는 기준 글자 기준으로 가운데 정렬 (숫자 높이가 흔들리지 않게)
                m.Translate(-(b.X + b.Width / 2f), -(refB.Y + refB.Height / 2f));
                p.Transform(m);
            }
            if (pathCache.Count > 1500) pathCache.Clear();   // 그리는 중인 경로가 있을 수 있어 Dispose 하지 않음
            pathCache[ck] = p;
            return p;
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Max(0.5f, rad * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 카드 반쪽: 위(upper) 또는 아래, 선택적으로 힌지 기준 세로 스케일
        void DrawHalf(Graphics g, RectangleF card, bool upper, string text, string kind, Color fg, float textH,
                      float scaleY, float shade)
        {
            if (scaleY < 0.01f) return;
            var saved = g.Save();
            float hinge = card.Y + card.Height / 2f;
            // 잘라낼 영역은 변형 전에 화면 좌표로 직접 계산 (GDI+ 구현마다 변형된 클립 처리가 달라서)
            float hh = card.Height / 2f * Math.Min(1f, scaleY);
            var visible = upper ? new RectangleF(card.X - 1, hinge - hh, card.Width + 2, hh)
                                : new RectangleF(card.X - 1, hinge, card.Width + 2, hh);
            g.SetClip(visible);
            if (scaleY < 0.999f)
            {
                g.TranslateTransform(0, hinge);
                g.ScaleTransform(1, scaleY);
                g.TranslateTransform(0, -hinge);
            }
            var half = upper ? new RectangleF(card.X, card.Y, card.Width, card.Height / 2f)
                             : new RectangleF(card.X, hinge, card.Width, card.Height / 2f);
            using (var rr = Rounded(card, card.Height * 0.06f))
            using (var br = new LinearGradientBrush(new PointF(0, card.Y - 1), new PointF(0, card.Bottom + 1), CardTop, CardBot))
                g.FillPath(br, rr);
            var tp = TextPath(kind, text, textH);
            var st2 = g.Save();
            g.TranslateTransform(card.X + card.Width / 2f, hinge);
            float tw = tp.GetBounds().Width, maxTw = card.Width * 0.88f;     // 카드가 좁으면 글자를 가로로만 눌러 맞춤
            if (tw > maxTw) g.ScaleTransform(maxTw / tw, 1f);
            using (var b = new SolidBrush(fg)) g.FillPath(b, tp);
            g.Restore(st2);
            if (shade > 0.003f)
                using (var sb = new SolidBrush(Color.FromArgb((int)(Math.Min(1f, shade) * 255), 0, 0, 0)))
                    g.FillRectangle(sb, half);
            g.Restore(saved);
        }

        static double EaseIn(double t) { return t * t; }
        static double EaseOut(double t) { return 1 - (1 - t) * (1 - t); }

        void DrawCard(Graphics g, RectangleF r, FlipCard c, string kind, Color fg, long now)
        {
            if (c.Cur == null) return;
            float textH = kind == "digit" ? r.Height * 0.62f : r.Height * 0.40f;

            // 그림자
            using (var sh = Rounded(new RectangleF(r.X, r.Y + r.Height * 0.03f, r.Width, r.Height), r.Height * 0.06f))
            using (var sb = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                g.FillPath(sb, sh);

            double t = c.Progress(now);
            if (t >= 1.0)
            {
                DrawHalf(g, r, true, c.Cur, kind, fg, textH, 1, 0);
                DrawHalf(g, r, false, c.Cur, kind, fg, textH, 1, 0);
            }
            else
            {
                DrawHalf(g, r, true, c.Cur, kind, fg, textH, 1, 0);          // 뒤에서 기다리는 새 윗장
                double lowerShade = t < 0.5 ? 0 : 0.35 * (1 - (t - 0.5) * 2);
                DrawHalf(g, r, false, c.Prev, kind, fg, textH, 1, (float)lowerShade); // 아직 남아있는 이전 아랫장
                if (t < 0.5)
                {
                    double u = EaseIn(t / 0.5);
                    float s = (float)Math.Cos(u * Math.PI / 2);
                    DrawHalf(g, r, true, c.Prev, kind, fg, textH, s, (float)(u * 0.55)); // 접혀 내려가는 이전 윗장
                }
                else
                {
                    double u = EaseOut((t - 0.5) / 0.5);
                    float s = (float)Math.Sin(u * Math.PI / 2);
                    DrawHalf(g, r, false, c.Cur, kind, fg, textH, s, (float)((1 - u) * 0.45)); // 펼쳐지는 새 아랫장
                }
            }

            // 힌지 줄과 핀
            float hinge = r.Y + r.Height / 2f;
            float seamH = Math.Max(1.5f, r.Height * 0.012f);
            using (var sb = new SolidBrush(Seam))
            {
                g.FillRectangle(sb, r.X, hinge - seamH / 2f, r.Width, seamH);
                float pinH = r.Height * 0.08f, pinW = Math.Max(2f, r.Height * 0.015f);
                g.FillRectangle(sb, r.X - pinW / 2f, hinge - pinH / 2f, pinW, pinH);
                g.FillRectangle(sb, r.Right - pinW / 2f, hinge - pinH / 2f, pinW, pinH);
            }
        }

        // 모서리 문구 자리 바꾸기
        public const int CornerMs = 10 * 60 * 1000;   // 10분마다 자리 이동
        const int CornerFadeMs = 1500;                // 사라지고 나타나는 시간
        public long CornerOffsetMs;                   // 테스트용

        void CornerState(long now, out int layout, out float alpha)
        {
            long e = now + CornerOffsetMs + CornerFadeMs;   // 시작할 때는 이미 다 보이는 상태
            if (e < 0) e = 0;
            layout = (int)((e / CornerMs) % 4);
            long pos = e % CornerMs;
            if (pos < CornerFadeMs) alpha = pos / (float)CornerFadeMs;
            else if (pos > CornerMs - CornerFadeMs) alpha = (CornerMs - pos) / (float)CornerFadeMs;
            else alpha = 1f;
            alpha = alpha * alpha * (3 - 2 * alpha); // 부드럽게
        }

        bool CornerFading(long now)
        {
            long e = now + CornerOffsetMs + CornerFadeMs;
            long pos = e % CornerMs;
            return pos < CornerFadeMs + 50 || pos > CornerMs - CornerFadeMs - 50;
        }

        void DrawCorner(Graphics g, Brush b, string text, int corner, Rectangle bounds,
                        float capH, float margin, float cdx, float cdy)
        {
            if (string.IsNullOrEmpty(text)) return;
            var p = TextPath("corner", text, capH);
            float bw = p.GetBounds().Width;
            bool right = corner == 1 || corner == 3, bottom = corner >= 2;
            float x = right ? bounds.Right - margin - cdx - bw / 2f : bounds.X + margin + cdx + bw / 2f;
            float y = bottom ? bounds.Bottom - margin - cdy - capH / 2f : bounds.Y + margin + cdy + capH / 2f;
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.FillPath(b, p);
            g.Restore(st);
        }

        // ───────── 추가 정보 (날씨 · 학사일정 · 급식) ─────────
        static readonly Color InfoText = Color.FromArgb(206, 205, 200);
        static readonly Color Rain = Color.FromArgb(111, 168, 220);
        static readonly Color CloudC = Color.FromArgb(178, 181, 188);
        const string DowShort = "일월화수목금토";

        class InfoView
        {
            public InfoData Weather;
            public string TempText, WeatherLine1, WeatherLine2;
            public string SchedChip, SchedLine1, SchedLine2;           // 1, 2 는 첫 항목 (있는지 확인용)
            public List<string> Sched1 = new List<string>(), Sched2 = new List<string>();   // 돌아가며 보여 줄 항목
            public string MealLabel;
            public List<string> MealMenu;                              // 첫 끼 (있는지 확인용)
            public List<string> MealLabels = new List<string>();       // 번갈아 보여 줄 끼니들 (점심 ↔ 저녁)
            public List<List<string>> MealMenus = new List<List<string>>();
        }

        static DateTime Ymd(string s)
        {
            DateTime d;
            return DateTime.TryParseExact(s, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d) ? d : DateTime.MinValue;
        }
        static string Md(DateTime d) { return d.Month + "/" + d.Day + "(" + DowShort[(int)d.DayOfWeek] + ")"; }
        // 기간 끝 날짜: 같은 달이면 일만 (예: 12/14(월)~17(목))
        static string MdEnd(DateTime s0, DateTime e0) { return s0.Month == e0.Month ? e0.Day + "(" + DowShort[(int)e0.DayOfWeek] + ")" : Md(e0); }

        const double DinnerOnlyHour = 13.0;       // 이 시각부터 자정까지는 오늘 석식만 표시

        InfoView BuildInfoView()
        {
            var v = new InfoView();
            var d = InfoService.Current;
            if (d == null) return v;
            DateTime today = shown.Date;

            if (Cfg.ShowWeather && d.HasWeather)
            {
                DateTime at;
                bool fresh = DateTime.TryParseExact(d.WeatherAt, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out at) && (shown - at).TotalHours < 6;
                if (fresh)
                {
                    v.Weather = d;
                    v.TempText = Math.Round(d.Temp).ToString("0") + "°";
                    v.WeatherLine1 = d.Sky;
                    v.WeatherLine2 = "최저 " + Math.Round(d.TMin) + "° · 최고 " + Math.Round(d.TMax) + "°" + (d.Pop >= 0 ? " · 강수 " + d.Pop + "%" : "");
                }
            }

            if (Cfg.ShowSchedule && d.Events != null)
            {
                var todays = new List<SchoolEvent>();
                var ups = new List<SchoolEvent>();
                foreach (var e in d.Events)
                {
                    DateTime s0 = Ymd(e.Start), e0 = Ymd(e.End);
                    if (e0 < today) continue;
                    if (s0 <= today) todays.Add(e); else ups.Add(e);
                }
                // 같은 날 시작하는 일정은 하나로 합침 (예: 11/19 대수능 · 대학수학능력시험)
                var merged = new List<SchoolEvent>();
                foreach (var e in ups)
                {
                    var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                    if (last != null && last.Start == e.Start)
                        merged[merged.Count - 1] = new SchoolEvent { Name = last.Name + " · " + e.Name, Start = last.Start,
                            End = string.CompareOrdinal(e.End, last.End) > 0 ? e.End : last.End };
                    else merged.Add(e);
                }
                // 일정을 한 건씩 넘겨 보여 주므로 같은 날 일정도 따로 표시 (합치면 길어져서 잘림)
                // ups = merged;
                if (todays.Count > 0)
                {
                    v.SchedChip = "오늘";
                    foreach (var e in todays)                        // 오늘 일정이 여러 건이면 첫 줄도 돌아가며 표시
                        v.Sched1.Add(Ymd(e.End) > today ? e.Name + "  (~" + Md(Ymd(e.End)) + ")" : e.Name);
                }
                else if (ups.Count > 0)
                {
                    var f = ups[0]; ups.RemoveAt(0);
                    v.SchedChip = "D-" + (int)(Ymd(f.Start) - today).TotalDays;
                    v.Sched1.Add(f.Name + "  " + Md(Ymd(f.Start)) + (f.End != f.Start ? "~" + MdEnd(Ymd(f.Start), Ymd(f.End)) : ""));
                }
                if (v.Sched1.Count > 0)                              // 둘째 줄: 다가오는 일정 (60일 이내, 최대 8건)
                    foreach (var e in ups)
                    {
                        int dd = (int)(Ymd(e.Start) - today).TotalDays;
                        if (dd > 60 || v.Sched2.Count >= 8) break;
                        v.Sched2.Add(Md(Ymd(e.Start)) + (e.End != e.Start ? "~" + MdEnd(Ymd(e.Start), Ymd(e.End)) : "") + "  " + e.Name + "  D-" + dd);
                    }
                if (v.Sched1.Count > 0) v.SchedLine1 = v.Sched1[0];
                if (v.Sched2.Count > 0) v.SchedLine2 = v.Sched2[0];
            }

            if (Cfg.ShowMeal && d.Meals != null && d.Meals.Count > 0)
            {
                // 시간대별 급식 표시 (항상 오늘 기준)
                //  - 13시 전: 오늘 중식·석식을 번갈아 (없는 끼니는 "오늘 중식 없음" 처럼 표시)
                //  - 13시부터 자정까지: 오늘 석식만 (없으면 "오늘 석식 없음"), 날짜가 바뀌면 다시 중식·석식
                //  - 오늘 급식이 아예 없으면(주말·휴일): "오늘 급식 없음"
                string todayS = today.ToString("yyyyMMdd");
                double hour = shown.Hour + shown.Minute / 60.0;
                MealDay lunch = d.Meals.Find(m => m.Date == todayS && m.Kind == "중식");
                MealDay dinner = d.Meals.Find(m => m.Date == todayS && m.Kind == "석식");
                Action<string, MealDay> add = (kind, m) =>
                {
                    v.MealLabels.Add("오늘 " + kind);
                    v.MealMenus.Add(m != null && m.Menu != null && m.Menu.Count > 0 ? m.Menu : new List<string> { "없음" });
                };
                if (lunch == null && dinner == null)
                {
                    v.MealLabels.Add("오늘 급식"); v.MealMenus.Add(new List<string> { "없음" });
                }
                else
                {
                    if (hour < DinnerOnlyHour) add("중식", lunch);
                    add("석식", dinner);
                }
                v.MealLabel = v.MealLabels[0]; v.MealMenu = v.MealMenus[0];
            }
            return v;
        }

        float Measure(string kind, string text, float size)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return TextPath(kind, text, size).GetBounds().Width;
        }

        string Fit(string kind, string text, float size, float maxW)
        {
            if (string.IsNullOrEmpty(text) || Measure(kind, text, size) <= maxW) return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Measure(kind, text.Substring(0, mid).TrimEnd() + "…", size) <= maxW) lo = mid; else hi = mid - 1;
            }
            return lo == 0 ? "" : text.Substring(0, lo).TrimEnd() + "…";
        }

        float DrawText(Graphics g, string kind, string text, float size, Color c, float x, float yc)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var p = TextPath(kind, text, size);
            float bw = p.GetBounds().Width;
            var st = g.Save();
            g.TranslateTransform(x + bw / 2f, yc);
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
            g.Restore(st);
            return bw;
        }

        float ChipWidth(string text, float s) { return Measure("infob", text, s * 0.86f) + s * 1.1f; }

        float DrawChip(Graphics g, string text, float s, float x, float yc)
        {
            float cw = ChipWidth(text, s), ch = s * 1.85f;
            using (var rr = Rounded(new RectangleF(x, yc - ch / 2f, cw, ch), ch * 0.3f))
            using (var b = new SolidBrush(Color.FromArgb(46, Accent)))
                g.FillPath(b, rr);
            DrawText(g, "infob", text, s * 0.86f, Accent, x + s * 0.55f, yc);
            return cw;
        }

        // ── 일정 넘기기 (시계 카드처럼 위아래로) ──
        public long RotOffsetMs;            // 테스트용
        bool rotating;
        int RotFlipMs { get { return Math.Max(150, Math.Min(3000, Cfg.FlipSpeedMs)); } }

        long RotInterval { get { return Math.Max(3, Cfg.RotateSec) * 1000L; } }

        // 지금 보여 줄 항목, 직전 항목, 넘김 진행도(0~1, 1이면 끝)
        void RotState(long now, int count, long offset, out int idx, out int prev, out float t)
        {
            RotState(now, count, offset, RotInterval, out idx, out prev, out t);
        }

        void RotState(long now, int count, long offset, long iv, out int idx, out int prev, out float t)
        {
            long e = now + RotOffsetMs + offset;
            if (e < 0) e = 0;
            long slot = e / iv;
            idx = (int)(slot % count);
            prev = (int)((slot + count - 1) % count);
            t = slot == 0 ? 1f : Math.Min(1f, (e % iv) / (float)RotFlipMs);
        }

        // 급식은 메뉴를 읽을 시간이 필요해서 최소 10초 간격, 일정과 겹치지 않게 1/4 박자 어긋나게
        long MealInterval { get { return Math.Max(10000L, RotInterval); } }
        bool mealRotating;

        bool RotFlipping(long now)
        {
            if (mealRotating && (now + RotOffsetMs + MealInterval / 4) % MealInterval < RotFlipMs + 50) return true;
            if (!rotating) return false;
            long iv = RotInterval;
            long a = (now + RotOffsetMs) % iv, b = (now + RotOffsetMs + iv / 2) % iv;
            return a < RotFlipMs + 50 || b < RotFlipMs + 50;
        }

        // 한 줄 글자를 가운데 가로축 기준으로 접었다 펴기: 앞 절반은 이전 글자가 접히고, 뒤 절반은 새 글자가 펴짐
        void DrawFlipText(Graphics g, string kind, string oldT, string newT, float size, Color c, float x, float yc, float t)
        {
            if (t >= 1f || oldT == newT) { DrawText(g, kind, newT, size, c, x, yc); return; }
            string txt = t < 0.5f ? oldT : newT;
            float sY = (float)Math.Abs(Math.Cos(t * Math.PI));
            if (sY < 0.03f) return;
            var st = g.Save();
            g.TranslateTransform(0, yc);
            g.ScaleTransform(1, sY);
            g.TranslateTransform(0, -yc);
            DrawText(g, kind, txt, size, Color.FromArgb((int)(c.A * (0.3f + 0.7f * sY)), c), x, yc);
            g.Restore(st);
        }

        // ── 날씨 ↔ 일정 자리 바꾸기 (번인 방지): 10분마다, 모서리 문구와 5분 어긋나게 ──
        public const int SwapMs = 10 * 60 * 1000;
        public long SwapOffsetMs;            // 테스트용
        int SwapFlipMs { get { return Math.Max(400, RotFlipMs + 200); } }
        bool swapping;

        void SwapState(long now, out bool swapped, out bool prevSwapped, out float t)
        {
            long e = now + SwapOffsetMs + SwapMs / 2;     // 처음 5분은 날씨 왼쪽, 그 뒤 10분마다 교대
            long slot = e / SwapMs;
            swapped = slot % 2 == 0;
            swapped = !swapped;                           // slot 0 → 날씨 왼쪽
            prevSwapped = !swapped;
            t = slot == 0 ? 1f : Math.Min(1f, (e % SwapMs) / (float)SwapFlipMs);
        }

        // 시계 판 안의 날씨 ↔ 일정 교대 간격 (일정 넘김 간격의 2배, 최소 12초)
        long PanelAltInterval { get { return Math.Max(12000L, RotInterval * 2); } }
        bool panelAlt;

        bool PanelAltFlipping(long now)
        {
            if (!panelAlt) return false;
            long iv = PanelAltInterval;
            return (now + RotOffsetMs + iv / 3) % iv < SwapFlipMs + 50;
        }

        bool SwapFlipping(long now)
        {
            if (!swapping) return false;
            long e = now + SwapOffsetMs + SwapMs / 2;
            return e / SwapMs > 0 && e % SwapMs < SwapFlipMs + 50;
        }

        void DrawTopBand(Graphics g, InfoView v, float cx, float cy, float dh, float maxTotal, long now)
        {
            bool both = v.Weather != null && v.SchedLine1 != null;
            if (both && driftScale == 0f)
            {
                // 작은 시계 판 안: 폭이 좁아서 날씨와 일정을 한 자리에서 번갈아 넘김
                panelAlt = true;
                long iv = PanelAltInterval, e = now + RotOffsetMs + iv / 3;
                long slot = e / iv;
                int cur = slot % 2 == 0 ? 1 : 2, prev = cur == 1 ? 2 : 1;
                float pt = slot == 0 ? 1f : Math.Min(1f, (e % iv) / (float)SwapFlipMs);
                if (pt >= 1f) { DrawTopBandContent(g, v, cx, cy, dh, maxTotal, now, false, cur); return; }
                float pY = (float)Math.Abs(Math.Cos(pt * Math.PI));
                if (pY < 0.03f) return;
                var ps = g.Save();
                g.TranslateTransform(0, cy);
                g.ScaleTransform(1, pY);
                g.TranslateTransform(0, -cy);
                DrawTopBandContent(g, v, cx, cy, dh, maxTotal, now, false, pt < 0.5f ? prev : cur);
                g.Restore(ps);
                return;
            }
            swapping = both;
            bool sw = false, prevSw = false; float t = 1f;
            if (both) SwapState(now, out sw, out prevSw, out t);
            if (t >= 1f) { DrawTopBandContent(g, v, cx, cy, dh, maxTotal, now, sw); return; }
            // 시계 카드처럼 가운데 가로축 기준으로 접었다 펴기
            float sY = (float)Math.Abs(Math.Cos(t * Math.PI));
            if (sY < 0.03f) return;
            var st = g.Save();
            g.TranslateTransform(0, cy);
            g.ScaleTransform(1, sY);
            g.TranslateTransform(0, -cy);
            DrawTopBandContent(g, v, cx, cy, dh, maxTotal, now, t < 0.5f ? prevSw : sw);
            g.Restore(st);
        }

        void DrawTopBandContent(Graphics g, InfoView v, float cx, float cy, float dh, float maxTotal, long now, bool schedFirst, int only = 0)
        {
            float s1 = dh * 0.27f, s2 = dh * 0.21f, tempS = dh * 0.50f, iconS = dh * 0.82f;
            float y1 = cy - dh * 0.19f, y2 = cy + dh * 0.21f;

            bool showW = v.Weather != null && only != 2, showS = v.SchedLine1 != null && only != 1;
            float wW = 0, tempW = 0, wTextW = 0;
            if (showW)
            {
                tempW = Measure("temp", v.TempText, tempS);
                wTextW = Math.Max(Measure("infob", v.WeatherLine1, s1), Measure("info", v.WeatherLine2, s2));
                wW = iconS + dh * 0.16f + tempW + dh * 0.24f + wTextW;
            }
            float sW = 0, chipW = 0;
            if (showS)
            {
                chipW = ChipWidth(v.SchedChip, s1);
                float w1 = 0, w2 = 0;                               // 돌아가는 항목 중 가장 긴 것 기준 (넘길 때 폭이 흔들리지 않게)
                foreach (var t1 in v.Sched1) w1 = Math.Max(w1, Measure("infob", t1, s1));
                foreach (var t2 in v.Sched2) w2 = Math.Max(w2, Measure("info", t2, s2));
                sW = Math.Max(chipW + s1 * 0.6f + w1, w2);
            }
            float sep = (wW > 0 && sW > 0) ? dh * 0.9f : 0f;
            if (wW + sep + sW > maxTotal) sW = Math.Max(dh * 2f, maxTotal - wW - sep);
            float x0 = cx - (wW + sep + sW) / 2f;
            // 자리: 기본은 [날씨 | 일정], 바꾼 상태는 [일정 | 날씨]
            float xWeather = schedFirst ? x0 + sW + sep : x0;
            float xSep = schedFirst ? x0 + sW : x0 + wW;
            float xSched = schedFirst ? x0 : x0 + wW + sep;

            if (showW)
            {
                float x = xWeather;
                DrawWeatherIcon(g, v.Weather.Icon, x + iconS / 2f, cy, iconS);
                float tx = x + iconS + dh * 0.16f;
                DrawText(g, "temp", v.TempText, tempS, Digit, tx, cy);
                float lx = tx + tempW + dh * 0.24f;
                DrawText(g, "infob", v.WeatherLine1, s1, InfoText, lx, y1);
                DrawText(g, "info", v.WeatherLine2, s2, Muted, lx, y2);
            }
            if (sep > 0)
            {
                using (var pen = new Pen(Color.FromArgb(70, Muted), Math.Max(1f, dh * 0.015f)))
                    g.DrawLine(pen, xSep + sep / 2f, cy - dh * 0.36f, xSep + sep / 2f, cy + dh * 0.36f);
            }
            if (showS)
            {
                float x = xSched;
                float ly1 = v.SchedLine2 == null ? cy : y1;
                float cw = DrawChip(g, v.SchedChip, s1, x, ly1);
                float rest = sW - cw - s1 * 0.6f;
                int i1, p1, i2, p2; float t1f, t2f;
                RotState(now, v.Sched1.Count, 0, out i1, out p1, out t1f);
                DrawFlipText(g, "infob", Fit("infob", v.Sched1[p1], s1, rest), Fit("infob", v.Sched1[i1], s1, rest),
                             s1, InfoText, x + cw + s1 * 0.6f, ly1, v.Sched1.Count > 1 ? t1f : 1f);
                if (v.Sched2.Count > 0)
                {
                    // 둘째 줄은 반 박자 늦게 넘겨서 두 줄이 동시에 움직이지 않게 함
                    RotState(now, v.Sched2.Count, RotInterval / 2, out i2, out p2, out t2f);
                    DrawFlipText(g, "info", Fit("info", v.Sched2[p2], s2, sW), Fit("info", v.Sched2[i2], s2, sW),
                                 s2, Muted, x, y2, v.Sched2.Count > 1 ? t2f : 1f);
                }
                rotating = v.Sched1.Count > 1 || v.Sched2.Count > 1;
            }
        }

        int mealLinesForced;                  // 시계 중심 화면에서 정한 급식 줄 수 (0이면 자동)
        float mealLineK = 2.2f;               // 급식 줄 간격 (글자 크기의 배수)

        // 메뉴를 n줄로 고르게 나누기 (줄마다 폭이 비슷하도록)
        List<string>[] SplitMealN(List<string> menu, float s, int n)
        {
            if (n <= 1 || menu.Count < 2) return new[] { menu };
            n = Math.Min(n, menu.Count);
            string sepT = " · ";
            float total = Measure("info", string.Join(sepT, menu.ToArray()), s);
            float target = total / n;
            var res = new List<List<string>>();
            var curLine = new List<string>();
            for (int i = 0; i < menu.Count; i++)
            {
                var trial = new List<string>(curLine) { menu[i] };
                int left = menu.Count - i;                     // 남은 메뉴 수
                int linesLeft = n - res.Count;                 // 남은 줄 수 (지금 줄 포함)
                bool mustBreak = curLine.Count > 0 && left < linesLeft;   // 남은 줄마다 하나씩은 있어야 함
                if (curLine.Count > 0 && res.Count < n - 1 &&
                    (mustBreak || Measure("info", string.Join(sepT, trial.ToArray()), s) > target * 1.08f))
                {
                    res.Add(curLine);
                    curLine = new List<string> { menu[i] };
                }
                else curLine = trial;
            }
            if (curLine.Count > 0) res.Add(curLine);
            return res.ToArray();
        }

        // 급식 띠를 lines 줄로 그릴 때의 폭 (점심·저녁 중 넓은 쪽)
        float MealWidth(InfoView v, float dh, int lines)
        {
            float s = dh * 0.27f, widest = 0, chipMax = 0;
            for (int k = 0; k < v.MealMenus.Count; k++) chipMax = Math.Max(chipMax, ChipWidth(v.MealLabels[k], s));
            foreach (var menu in v.MealMenus)
                foreach (var line in SplitMealN(menu, s, lines))
                    widest = Math.Max(widest, Measure("info", string.Join(" · ", line.ToArray()), s));
            return chipMax + s * 0.7f + widest;
        }

        // 위 띠의 자연스러운 폭: 날씨 블록, 일정 블록 (잘림 없이)
        void TopBandWidths(InfoView v, float dh, out float wW, out float sW)
        {
            float s1 = dh * 0.27f, s2 = dh * 0.21f, tempS = dh * 0.50f, iconS = dh * 0.82f;
            wW = 0; sW = 0;
            if (v.Weather != null)
                wW = iconS + dh * 0.16f + Measure("temp", v.TempText, tempS) + dh * 0.24f +
                     Math.Max(Measure("infob", v.WeatherLine1, s1), Measure("info", v.WeatherLine2, s2));
            if (v.SchedLine1 != null)
            {
                float w1 = 0, w2 = 0;
                foreach (var t1 in v.Sched1) w1 = Math.Max(w1, Measure("infob", t1, s1));
                foreach (var t2 in v.Sched2) w2 = Math.Max(w2, Measure("info", t2, s2));
                sW = Math.Max(ChipWidth(v.SchedChip, s1) + s1 * 0.6f + w1, w2);
            }
        }

        // ── 시계 중심 화면: 플립시계가 화면 높이의 약 70% ──
        // 가로: 맨 위 모서리 문구 / 위 띠 [날짜 | 날씨 | 일정] / 시계 한 줄 / 급식(시계 아래)
        // 세로: 모서리 문구 / 날짜 / 날씨 / 일정 / 시계 두 줄(시 위, 분 아래) / 급식
        // 카드 비율은 화면 비율에 맞춰 자동: 시계가 높이 70%가 되는 선에서 카드를 가능한 한 넓게 하고,
        // 간격을 좁혀 16:9 에서는 가로:세로 약 0.56:1 이 됨. 4:3 처럼 너무 좁아지면 시계를 조금 줄임.
        const float FocusRatioMin = 0.45f, FocusRatioMax = 0.62f;

        // 한 줄 시계(시:분[:초])의 가로 = 높이 × 이 값 (카드 비율 r 일 때)
        float ClockFactor(int groups, float r)
        {
            return 2 * groups * r + groups * 0.025f + (groups - 1) * 0.11f + (Cfg.H24 ? 0f : 0.19f);
        }

        void FocusDims(float availW, float hMax, int groups, out float h, out float r)
        {
            h = hMax;
            float fixedPart = ClockFactor(groups, 0f);
            r = (availW / h - fixedPart) / (2 * groups);
            if (r < FocusRatioMin) { r = FocusRatioMin; h = availW / ClockFactor(groups, r); }
            if (r > FocusRatioMax) r = FocusRatioMax;
        }

        // 한 줄 시계 그리기 (좁은 간격)
        void DrawClockRowC(Graphics g, float cx, float top, float h, float r, long now)
        {
            int groups = Cfg.ShowSeconds ? 3 : 2;
            float w = r * h, cg = 0.025f * h, gg = 0.03f * h, colonW = 0.05f * h;
            float ampmW = Cfg.H24 ? 0f : h * 0.16f + gg;
            float total = h * ClockFactor(groups, r);
            float x = cx - total / 2f;
            if (!Cfg.H24)
            {
                var a = TextPath("digit", ampm.Substring(0, 1), h * 0.08f);
                var m = TextPath("digit", ampm.Substring(1, 1), h * 0.08f);
                using (var b = new SolidBrush(Accent))
                {
                    var st = g.Save(); g.TranslateTransform(x + h * 0.07f, top + h * 0.10f); g.FillPath(b, a); g.Restore(st);
                    st = g.Save(); g.TranslateTransform(x + h * 0.07f, top + h * 0.22f); g.FillPath(b, m); g.Restore(st);
                }
                x += ampmW;
            }
            FlipCard[][] cgp = Cfg.ShowSeconds ? new[] { hh, mm, ss } : new[] { hh, mm };
            for (int gi = 0; gi < cgp.Length; gi++)
            {
                for (int i = 0; i < 2; i++)
                {
                    DrawCard(g, new RectangleF(x, top, w, h), cgp[gi][i], "digit", Digit, now);
                    x += w + (i == 0 ? cg : 0);
                }
                if (gi < cgp.Length - 1)
                {
                    x += gg;
                    float dr = h * 0.026f, ccx = x + colonW / 2f, cy = top + h / 2f;
                    using (var b = new SolidBrush(Color.FromArgb(180, Muted)))
                    {
                        g.FillEllipse(b, ccx - dr, cy - h * 0.12f - dr, 2 * dr, 2 * dr);
                        g.FillEllipse(b, ccx - dr, cy + h * 0.12f - dr, 2 * dr, 2 * dr);
                    }
                    x += colonW + gg;
                }
            }
        }

        // 세로 화면: 시·분(·초)을 위아래로 쌓은 시계
        void DrawClockStack(Graphics g, float cx, float top, float h, float w, float rowGap, long now)
        {
            FlipCard[][] cgp = Cfg.ShowSeconds ? new[] { hh, mm, ss } : new[] { hh, mm };
            float cg = 0.025f * h, y = top;
            for (int gi = 0; gi < cgp.Length; gi++)
            {
                float x = cx - (2 * w + cg) / 2f;
                for (int i = 0; i < 2; i++) { DrawCard(g, new RectangleF(x, y, w, h), cgp[gi][i], "digit", Digit, now); x += w + cg; }
                if (gi == 0 && !Cfg.H24)
                    DrawText(g, "infob", ampm == "AM" ? "오전" : "오후", h * 0.08f, Accent, cx - (2 * w + cg) / 2f + h * 0.06f, y + h * 0.07f);
                y += h + rowGap;
            }
        }

        // 날짜 블록. 가로: 연도(작게) / 날짜(크게) / 요일(주황) 세 줄, 세로: 한 줄 "11월 13일 금요일 2026년"을 가운데 정렬
        void DrawDateBlock(Graphics g, float x, float y, float bandH, bool inlineWeekday)
        {
            var now = shown;
            string year = now.Year + "년", date = now.Month + "월 " + now.Day + "일", wd = Days[(int)now.DayOfWeek];
            if (!inlineWeekday)
            {
                DrawText(g, "info", year, bandH * 0.15f, Muted, x, y + bandH * 0.12f);
                DrawText(g, "infob", date, bandH * 0.36f, Digit, x, y + bandH * 0.46f);
                DrawText(g, "infob", wd, bandH * 0.24f, Accent, x, y + bandH * 0.82f);
            }
            else
            {
                // x 는 가운데 기준
                float sd = bandH * 0.56f, sw = bandH * 0.44f, sy = bandH * 0.26f, gap = bandH * 0.28f;
                float wDate = Measure("infob", date, sd), wWd = Measure("infob", wd, sw), wYear = Measure("info", year, sy);
                float left = x - (wDate + gap + wWd + gap * 0.8f + wYear) / 2f, cy = y + bandH * 0.5f;
                DrawText(g, "infob", date, sd, Digit, left, cy);
                DrawText(g, "infob", wd, sw, Accent, left + wDate + gap, cy);
                DrawText(g, "info", year, sy, Muted, left + wDate + gap + wWd + gap * 0.8f, cy + bandH * 0.08f);
            }
        }

        float DateBlockWidth(float bandH, bool inlineWeekday)
        {
            float sample = Measure("infob", "00월 00일", inlineWeekday ? bandH * 0.56f : bandH * 0.36f);
            if (inlineWeekday) return sample;
            return Math.Max(sample, Measure("infob", "수요일", bandH * 0.24f));
        }

        // 급식 띠(시계 아래): 한 줄에 들어가면 크게, 아니면 줄 수를 늘리되 띠 높이 안에 맞춤
        void DrawFocusMeal(Graphics g, InfoView v, float cx, float top, float bandH, float availW, int maxLines, float maxS, long now)
        {
            const float LineK = 1.45f;                       // 줄 높이 = 글자 크기 × 1.45
            int lines = 1; float s = 0, dh = 0;
            // 글자를 조금(22%까지)만 줄이면 한 줄에 들어갈 때는 한 줄을 유지
            s = Math.Min(maxS, bandH / LineK); dh = s / 0.27f;
            float need1 = MealWidth(v, dh, 1);
            if (need1 > availW && availW / need1 >= 0.78f)
            {
                dh *= availW / need1; s = dh * 0.27f;
                float lh = s * LineK;
                mealLinesForced = 1; mealLineK = LineK;
                DrawMealBand(g, v, cx, top + (bandH - lh) / 2f, dh, availW / 0.86f, now);
                mealLinesForced = 0; mealLineK = 2.2f;
                return;
            }
            for (lines = 1; lines <= maxLines; lines++)
            {
                s = Math.Min(maxS, bandH / (lines * LineK));
                dh = s / 0.27f;
                if (MealWidth(v, dh, lines) <= availW) break;
            }
            if (lines > maxLines)                             // 줄을 최대로 늘려도 안 들어가면 글자를 줄임
            {
                lines = maxLines;
                s = Math.Min(maxS, bandH / (lines * LineK)); dh = s / 0.27f;
                dh *= availW / MealWidth(v, dh, lines);
            }
            s = dh * 0.27f;
            float lineH = s * LineK;
            mealLinesForced = lines; mealLineK = LineK;
            DrawMealBand(g, v, cx, top + (bandH - lines * lineH) / 2f, dh, availW / 0.86f, now);
            mealLinesForced = 0; mealLineK = 2.2f;
        }

        // 시계 중심 화면: 아래쪽 모서리의 버전 표기와 겹치지 않도록 급식 띠 폭을 줄임
        float MealW(float availW, Rectangle b)
        {
            float capH = CornerCapH(b.Width, b.Height) * 0.8f;
            float lw = TextPath("corner", Settings.AppName + " " + Settings.Version, capH).GetBounds().Width;
            float m = Math.Max(8f, b.Height * 0.012f) + Math.Abs(drift.X) * b.Width * 0.3f;
            return Math.Min(availW, b.Width - 2f * (lw + m + b.Width * 0.01f));
        }

        void PaintFocus(Graphics g, Rectangle b, long now)
        {
            float W = b.Width, H = b.Height;
            bool portrait = H > W * 1.05f;
            var v = BuildInfoView();
            bool wOn = v.Weather != null, sOn = v.SchedLine1 != null, mOn = v.MealMenu != null, dOn = Cfg.ShowDate;
            float availW = W * 0.94f;
            float cx = b.X + W / 2f + drift.X * W * 0.25f;
            float shiftY = drift.Y * H * 0.2f;
            int groups = Cfg.ShowSeconds ? 3 : 2;
            rotating = false; mealRotating = false; swapping = false; panelAlt = false;
            float corner = H * (portrait ? 0.022f : 0.04f);

            if (!portrait)
            {
                float topH = (wOn || sOn || dOn) ? H * 0.158f : 0f;
                float mealH = mOn ? H * 0.07f : 0f;
                float gap1 = topH > 0 ? H * 0.014f : 0f, gap2 = mOn ? H * 0.012f : 0f, bottom = H * 0.008f;
                float hMax = H - corner - topH - gap1 - gap2 - mealH - bottom;
                float h, r;
                FocusDims(availW, Math.Min(hMax, H * 0.82f), groups, out h, out r);
                float extra = hMax - h;                                           // 4:3 처럼 시계가 줄어든 만큼 위·아래 띠에 나눠 줌
                if (extra > 0) { if (topH > 0) topH += extra * 0.6f; if (mealH > 0) mealH += extra * 0.4f; else topH += extra * 0.4f; }
                float y = corner + shiftY;

                // ── 위 띠: 날짜 | 날씨 + 일정 ──
                if (topH > 0)
                {
                    float x0 = b.X + W * 0.035f, x1 = b.X + W - W * 0.035f;
                    float rightX0 = x0, rightW = x1 - x0;
                    if (dOn)
                    {
                        float dW = DateBlockWidth(topH, false);
                        DrawDateBlock(g, x0, y, topH, false);
                        float divX = x0 + dW + W * 0.025f;
                        if (wOn || sOn)
                            using (var pen = new Pen(Color.FromArgb(60, Muted), Math.Max(1f, topH * 0.012f)))
                                g.DrawLine(pen, divX, y + topH * 0.12f, divX, y + topH * 0.88f);
                        rightX0 = divX + W * 0.025f; rightW = x1 - rightX0;
                    }
                    if (wOn || sOn)
                    {
                        float wW, sW;
                        TopBandWidths(v, 100f, out wW, out sW);
                        float need100 = wW + sW + ((wOn && sOn) ? 90f : 0f);
                        float dh = Math.Min(topH / 0.95f, need100 > 0 ? rightW / need100 * 100f : topH);
                        DrawTopBand(g, v, rightX0 + rightW / 2f, y + topH / 2f, dh, rightW, now);
                    }
                    y += topH + gap1;
                }

                // ── 시계 ──
                DrawClockRowC(g, cx, y, h, r, now);
                y += h + gap2;

                // ── 급식 (시계 아래) ──
                if (mOn) DrawFocusMeal(g, v, cx, y, mealH, MealW(availW, b), 2, mealH * 0.46f, now);
            }
            else
            {
                // 세로: 모서리 / 날짜 / 날씨 / 일정 / 시계(두 줄 이상) / 급식
                float dateH = dOn ? H * 0.0625f : 0f, wsH = H * 0.0535f;
                var rows = new List<char>();
                if (dOn) rows.Add('D');
                if (wOn) rows.Add('W');
                if (sOn) rows.Add('S');
                float infoH = 0f;
                foreach (var rk in rows) infoH += rk == 'D' ? dateH : wsH;
                float mealH = mOn ? H * 0.073f : 0f, bottom = H * 0.008f;
                int sections = rows.Count + (mOn ? 1 : 0) + 1;                    // 간격 수 계산용 (+시계)
                float gapBase = H * 0.0072f;
                float clockTotal = H - corner - infoH - mealH - bottom - gapBase * sections;
                float rowGap = H * 0.015f;
                float h = (clockTotal - (groups - 1) * rowGap) / groups;
                float cg = 0.025f * h;
                float w = Math.Min(0.78f * h, (availW - cg) / 2f);
                h = Math.Min(h, w / 0.55f);                                       // 카드가 너무 넓어지지 않게
                float used = infoH + mealH + bottom + corner + h * groups + rowGap * (groups - 1);
                float gap = (H - used) / sections;                                // 남는 높이는 간격으로 고르게

                float y = corner + shiftY;
                // 날씨·일정 줄은 10분마다 위아래 자리를 바꿈 (번인 방지)
                bool sw = false, prevSw = false; float st = 1f;
                if (wOn && sOn) { swapping = true; SwapState(now, out sw, out prevSw, out st); }
                bool schedOnTop = st < 0.5f ? prevSw : sw;
                float flipY = st >= 1f ? 1f : (float)Math.Abs(Math.Cos(st * Math.PI));
                float x0 = b.X + W * 0.04f;

                foreach (var rk0 in rows)
                {
                    char rk = rk0;
                    if ((rk == 'W' || rk == 'S') && wOn && sOn && schedOnTop) rk = rk == 'W' ? 'S' : 'W';
                    float rh = rk0 == 'D' ? dateH : wsH;
                    if (rk == 'D') DrawDateBlock(g, cx, y, rh, true);
                    else if (flipY >= 0.03f)
                    {
                        var gs = g.Save();
                        float ccy = y + rh / 2f;
                        if (flipY < 0.999f) { g.TranslateTransform(0, ccy); g.ScaleTransform(1, flipY); g.TranslateTransform(0, -ccy); }
                        DrawTopBandContent(g, v, cx, ccy, rh / 0.95f, availW, now, false, rk == 'W' ? 1 : 2);
                        g.Restore(gs);
                    }
                    y += rh + gap;
                }
                DrawClockStack(g, cx, y, h, w, rowGap, now);
                y += h * groups + rowGap * (groups - 1) + gap;
                if (mOn) DrawFocusMeal(g, v, cx, y, mealH, MealW(availW, b), 4, mealH * 0.30f, now);
            }
        }

        // 급식 메뉴를 한 줄 또는 두 줄로 나누기
        List<string>[] SplitMeal(List<string> menu, float s, float maxW, float chipW)
        {
            if (mealLinesForced > 0) return SplitMealN(menu, s, mealLinesForced);
            string sepT = " · ";
            string all = string.Join(sepT, menu.ToArray());
            if (menu.Count < 2 || chipW + s * 0.7f + Measure("info", all, s) <= maxW) return new[] { menu };
            int best = 1; float bestDiff = float.MaxValue;
            for (int i = 1; i < menu.Count; i++)
            {
                float a = Measure("info", string.Join(sepT, menu.GetRange(0, i).ToArray()), s);
                float b = Measure("info", string.Join(sepT, menu.GetRange(i, menu.Count - i).ToArray()), s);
                float diff = Math.Abs(a - b);
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return new[] { menu.GetRange(0, best), menu.GetRange(best, menu.Count - best) };
        }

        int MealLineCount(InfoView v, float dh, float W)
        {
            float s = dh * 0.27f;
            int n = 1;
            for (int i = 0; i < v.MealMenus.Count; i++)
                n = Math.Max(n, SplitMeal(v.MealMenus[i], s, W * 0.86f, ChipWidth(v.MealLabels[i], s)).Length);
            return n;
        }

        void DrawMealBand(Graphics g, InfoView v, float cx, float top, float dh, float W, long now)
        {
            float s = dh * 0.27f, lineH = s * mealLineK, maxW = W * 0.86f, gap = s * 0.7f;
            string sepT = " · ";
            int n = v.MealMenus.Count;
            // 끼니마다 줄 나누기, 그리고 가장 넓은 것 기준으로 왼쪽 위치 고정 (넘길 때 옆으로 흔들리지 않게)
            var texts = new string[n][];
            float chipMax = 0, widest = 0;
            for (int k = 0; k < n; k++) chipMax = Math.Max(chipMax, ChipWidth(v.MealLabels[k], s));
            int maxLines = 1;
            for (int k = 0; k < n; k++)
            {
                var lines = SplitMeal(v.MealMenus[k], s, maxW, chipMax);
                texts[k] = new string[lines.Length];
                maxLines = Math.Max(maxLines, lines.Length);
                for (int i = 0; i < lines.Length; i++)
                {
                    texts[k][i] = Fit("info", string.Join(sepT, lines[i].ToArray()), s, maxW - chipMax - gap);
                    widest = Math.Max(widest, Measure("info", texts[k][i], s));
                }
            }
            float x = cx - (chipMax + gap + widest) / 2f;
            float y = top + lineH / 2f;
            float bandMid = top + maxLines * lineH / 2f;

            int idx = 0, prev = 0; float t = 1f;
            mealRotating = n > 1;
            if (n > 1) RotState(now, n, MealInterval / 4, MealInterval, out idx, out prev, out t);
            int show = t < 0.5f ? prev : idx;
            float sY = t >= 1f ? 1f : (float)Math.Abs(Math.Cos(t * Math.PI));
            if (sY < 0.03f) return;
            var st = g.Save();
            if (sY < 0.999f)
            {
                g.TranslateTransform(0, bandMid);
                g.ScaleTransform(1, sY);
                g.TranslateTransform(0, -bandMid);
            }
            DrawChip(g, v.MealLabels[show], s, x, y);
            var col = Color.FromArgb((int)(255 * (0.3f + 0.7f * sY)), InfoText);
            for (int i = 0; i < texts[show].Length; i++)
                DrawText(g, "info", texts[show][i], s, col, x + chipMax + gap, y + i * lineH);
            g.Restore(st);
        }

        void DrawCloud(Graphics g, float cx, float cy, float S, Color c)
        {
            // 도형을 하나씩 같은 색으로 칠해 겹친 부분에 구멍이 생기지 않게 함
            using (var b = new SolidBrush(c))
            {
                g.FillEllipse(b, cx - S * 0.36f, cy - S * 0.08f, S * 0.34f, S * 0.30f);
                g.FillEllipse(b, cx - S * 0.16f, cy - S * 0.26f, S * 0.42f, S * 0.42f);
                g.FillEllipse(b, cx + S * 0.08f, cy - S * 0.08f, S * 0.30f, S * 0.30f);
                using (var rr = Rounded(new RectangleF(cx - S * 0.36f, cy - S * 0.02f, S * 0.74f, S * 0.24f), S * 0.12f))
                    g.FillPath(b, rr);
            }
        }

        void DrawSun(Graphics g, float cx, float cy, float S)
        {
            float r = S * 0.19f;
            using (var b = new SolidBrush(Accent)) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
            using (var pen = new Pen(Accent, Math.Max(1.2f, S * 0.055f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    float c0 = (float)Math.Cos(a), s0 = (float)Math.Sin(a);
                    g.DrawLine(pen, cx + c0 * r * 1.45f, cy + s0 * r * 1.45f, cx + c0 * r * 2.05f, cy + s0 * r * 2.05f);
                }
            }
        }

        void DrawWeatherIcon(Graphics g, string icon, float cx, float cy, float S)
        {
            switch (icon)
            {
                case "sun": DrawSun(g, cx, cy, S); break;
                case "partly":
                    DrawSun(g, cx - S * 0.14f, cy - S * 0.14f, S * 0.8f);
                    DrawCloud(g, cx + S * 0.04f, cy + S * 0.08f, S * 0.95f, CloudC);
                    break;
                case "rain":
                case "snow":
                case "thunder":
                    DrawCloud(g, cx, cy - S * 0.1f, S, CloudC);
                    if (icon == "rain")
                        using (var pen = new Pen(Rain, Math.Max(1.2f, S * 0.05f)))
                        {
                            pen.StartCap = pen.EndCap = LineCap.Round;
                            for (int i = -1; i <= 1; i++)
                                g.DrawLine(pen, cx + i * S * 0.18f + S * 0.03f, cy + S * 0.2f, cx + i * S * 0.18f - S * 0.04f, cy + S * 0.38f);
                        }
                    else if (icon == "snow")
                        using (var b = new SolidBrush(Digit))
                            for (int i = -1; i <= 1; i++)
                            {
                                float r = S * 0.045f, yy0 = cy + S * (i == 0 ? 0.34f : 0.26f);
                                g.FillEllipse(b, cx + i * S * 0.18f - r, yy0 - r, 2 * r, 2 * r);
                            }
                    else
                        using (var b = new SolidBrush(Accent))
                            g.FillPolygon(b, new[] {
                                new PointF(cx + S * 0.03f, cy + S * 0.12f), new PointF(cx - S * 0.10f, cy + S * 0.30f),
                                new PointF(cx, cy + S * 0.30f), new PointF(cx - S * 0.06f, cy + S * 0.46f),
                                new PointF(cx + S * 0.12f, cy + S * 0.24f), new PointF(cx + S * 0.02f, cy + S * 0.24f) });
                    break;
                case "fog":
                    using (var pen = new Pen(CloudC, Math.Max(1.2f, S * 0.06f)))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        for (int i = -1; i <= 1; i++)
                            g.DrawLine(pen, cx - S * 0.32f + (i == 0 ? S * 0.08f : 0), cy + i * S * 0.16f, cx + S * 0.32f, cy + i * S * 0.16f);
                    }
                    break;
                default: DrawCloud(g, cx, cy, S, CloudC); break;
            }
        }

        public void Paint(Graphics g, Rectangle bounds, long now)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (photos != null && photos.HasImage)
            {
                // 사진 배경 + 모서리에 작은 시계 판
                photos.Draw(g, bounds, now);
                using (var dim = new SolidBrush(Color.FromArgb(38, 0, 0, 0))) g.FillRectangle(dim, bounds);   // 눈부심을 조금 줄임
                var panel = PanelRect(bounds, now);
                using (var rr = Rounded(panel, panel.Height * 0.06f))
                {
                    using (var bg = new SolidBrush(Color.FromArgb(170, 10, 11, 13))) g.FillPath(bg, rr);
                    using (var pen = new Pen(Color.FromArgb(36, 255, 255, 255), Math.Max(1f, panel.Height * 0.004f))) g.DrawPath(pen, rr);
                }
                var inner = RectangleF.Inflate(panel, -panel.Width * 0.025f, -panel.Height * 0.03f);
                driftScale = 0f;                      // 판 안에서는 움직이지 않음 (판 자체가 움직임)
                PaintContent(g, Rectangle.Round(inner), now);
                DrawCorners(g, bounds, now, true);
            }
            else
            {
                g.Clear(Bg);
                driftScale = 1f;
                if (Cfg.DisplaySize == 1)
                {
                    PaintFocus(g, bounds, now);
                    cornerMargin = Math.Max(8f, bounds.Height * 0.012f);
                    DrawCorners(g, bounds, now, false, true);
                    cornerMargin = 0f;
                }
                else
                {
                    PaintContent(g, bounds, now);
                    DrawCorners(g, bounds, now, false);
                }
            }
        }

        float driftScale = 1f;

        // 시계 판 위치: 화면의 30~46% 크기, 오른쪽 아래 / 왼쪽 아래 / 10분마다 번갈아. 판 자체도 1분마다 조금씩 이동
        bool PanelLeft(long now) { return Cfg.PanelPos == 1 || (Cfg.PanelPos == 2 && ((now + CornerOffsetMs + CornerMs / 2) / CornerMs) % 2 == 1); }

        RectangleF PanelRect(Rectangle b, long now)
        {
            float[] sizes = { 0.34f, 0.42f, 0.50f };
            float f = sizes[Math.Max(0, Math.Min(2, Cfg.PanelSize))];
            float pw = b.Width * f, ph = b.Height * f;
            float m = Math.Min(b.Width, b.Height) * 0.035f;
            bool left = PanelLeft(now);
            float dx = Math.Abs(drift.X) * b.Width * 0.35f, dy = Math.Abs(drift.Y) * b.Height * 0.35f;
            float x = left ? b.X + m + dx : b.Right - m - pw - dx;
            float y = b.Bottom - m - ph - dy;
            return new RectangleF(x, y, pw, ph);
        }

        void PaintContent(Graphics g, Rectangle bounds, long now)
        {
            float W = bounds.Width, Hs = bounds.Height;
            if (W < 10 || Hs < 10) return;

            // ── 크기 계산 (웹 버전과 같은 비율) ──
            bool portrait = Hs > W;
            float h = Cfg.ShowSeconds ? Math.Min(0.36f * Hs, 0.14f * W) : Math.Min(0.40f * Hs, 0.19f * W);
            if (portrait) h = Cfg.ShowSeconds ? Math.Min(0.22f * Hs, 0.14f * W) : Math.Min(0.22f * Hs, 0.19f * W);
            float dh = Math.Max(Math.Min(30f, Hs * 0.08f), Math.Min(130f, Math.Min(0.10f * Hs, 0.048f * W)));
            if (Cfg.ShowSeconds == false) dh = Math.Max(dh, Math.Min(0.10f * Hs, 0.058f * W));
            // 작은 시계 판 안에서는 날씨·일정·급식 글자가 너무 작아지지 않도록 정보 줄을 키우고 시계는 조금 줄임
            if (driftScale == 0f) { dh = Math.Min(dh * 1.5f, Hs * 0.16f); h *= 0.95f; }

            // ── 추가 정보: 위 띠(날씨·일정) / 아래 띠(급식) ──
            var view = BuildInfoView();
            bool topOn = view.Weather != null || view.SchedLine1 != null;
            bool mealOn = view.MealMenu != null;
            int mealLines = mealOn ? MealLineCount(view, dh, W) : 0;
            // 세로로 다 들어가지 않으면 전체를 같은 비율로 줄임
            float stackH = h * (1f + (topOn ? 0.16f : 0f) + (Cfg.ShowDate ? 0.18f : 0f))
                         + dh * ((topOn ? 0.95f : 0f) + (Cfg.ShowDate ? 1f : 0f) + (mealOn ? 0.5f + mealLines * 0.5f : 0f));
            float avail = Hs * 0.80f;
            if (stackH > avail) { float k = avail / stackH; h *= k; dh *= k; stackH = avail; }

            float w = h * 0.7f;
            float cardGap = w * 0.06f, groupGap = w * 0.12f, colonW = h * 0.06f + w * 0.12f;
            float dw = dh * 0.7f;
            float dCardGap = dw * 0.05f, dUnitGap = dw * 0.35f, dLblGap = dw * 0.12f;
            float lblH = dh * 0.24f;
            float lblW = dh * 0.36f;

            int groups = Cfg.ShowSeconds ? 3 : 2;
            float groupW = 2 * w + cardGap;
            float ampmW = Cfg.H24 ? 0 : h * 0.16f + groupGap;
            float clockW = ampmW + groups * groupW + (groups - 1) * (colonW + 2 * groupGap);

            float wordW = dh * 2.1f;
            float dateW = (4 * dw + 3 * dCardGap) + (2 * dw + dCardGap) * 2 + 3 * (dLblGap + lblW) + 3 * dUnitGap + wordW;
            float dateGapV = h * 0.18f;
            float topBandH = topOn ? dh * 0.95f : 0f, topGap = topOn ? h * 0.16f : 0f;

            float ox = bounds.X + W / 2f + drift.X * W * driftScale;
            float oy = bounds.Y + Hs / 2f + drift.Y * Hs * driftScale;
            float bandTop = oy - stackH / 2f;
            float top = bandTop + topBandH + topGap;      // 시계 카드 윗변

            rotating = false; mealRotating = false; swapping = false; panelAlt = false;
            if (topOn) DrawTopBand(g, view, ox, bandTop + topBandH / 2f, dh, Math.Min(W * 0.92f, Math.Max(clockW, W * 0.62f)), now);
            if (mealOn)
            {
                float mealTop = top + h + (Cfg.ShowDate ? dateGapV + dh : 0f) + dh * 0.5f;
                DrawMealBand(g, view, ox, mealTop, dh, W, now);
            }

            DrawClockRow(g, ox, top, h, now);
            if (Cfg.ShowDate) DrawDateRow(g, ox, top + h + dateGapV, dh, now);
        }

        // ── 시계 줄 / 날짜 줄 (기본 화면용) ──
        float ClockWidth(float h)
        {
            float w = h * 0.7f, cardGap = w * 0.06f, groupGap = w * 0.12f, colonW = h * 0.06f + w * 0.12f;
            int groups = Cfg.ShowSeconds ? 3 : 2;
            float ampmW = Cfg.H24 ? 0 : h * 0.16f + groupGap;
            return ampmW + groups * (2 * w + cardGap) + (groups - 1) * (colonW + 2 * groupGap);
        }

        float DateWidth(float dh)
        {
            float dw = dh * 0.7f, dCardGap = dw * 0.05f, dUnitGap = dw * 0.35f, dLblGap = dw * 0.12f, lblW = dh * 0.36f;
            return (4 * dw + 3 * dCardGap) + (2 * dw + dCardGap) * 2 + 3 * (dLblGap + lblW) + 3 * dUnitGap + dh * 2.1f;
        }

        void DrawClockRow(Graphics g, float cx, float top, float h, long now)
        {
            float w = h * 0.7f;
            float cardGap = w * 0.06f, groupGap = w * 0.12f, colonW = h * 0.06f + w * 0.12f;
            float ampmW = Cfg.H24 ? 0 : h * 0.16f + groupGap;
            float x = cx - ClockWidth(h) / 2f;
            if (!Cfg.H24)
            {
                var a = TextPath("digit", ampm.Substring(0, 1), h * 0.10f);
                var m = TextPath("digit", ampm.Substring(1, 1), h * 0.10f);
                using (var b = new SolidBrush(Accent))
                {
                    var s = g.Save(); g.TranslateTransform(x + h * 0.08f, top + h * 0.12f); g.FillPath(b, a); g.Restore(s);
                    s = g.Save(); g.TranslateTransform(x + h * 0.08f, top + h * 0.27f); g.FillPath(b, m); g.Restore(s);
                }
                x += ampmW;
            }
            FlipCard[][] cg = Cfg.ShowSeconds ? new[] { hh, mm, ss } : new[] { hh, mm };
            for (int gi = 0; gi < cg.Length; gi++)
            {
                for (int i = 0; i < 2; i++)
                {
                    DrawCard(g, new RectangleF(x, top, w, h), cg[gi][i], "digit", Digit, now);
                    x += w + (i == 0 ? cardGap : 0);
                }
                if (gi < cg.Length - 1)
                {
                    x += groupGap;
                    float r = h * 0.03f, ccx = x + colonW / 2f, cy = top + h / 2f;
                    using (var b = new SolidBrush(Color.FromArgb(180, Muted)))
                    {
                        g.FillEllipse(b, ccx - r, cy - h * 0.13f - r, 2 * r, 2 * r);
                        g.FillEllipse(b, ccx - r, cy + h * 0.13f - r, 2 * r, 2 * r);
                    }
                    x += colonW + groupGap;
                }
            }
        }

        void DrawDateRow(Graphics g, float cx, float dy, float dh, long now)
        {
            float dw = dh * 0.7f;
            float dCardGap = dw * 0.05f, dUnitGap = dw * 0.35f, dLblGap = dw * 0.12f;
            float lblH = dh * 0.24f, lblW = dh * 0.36f, wordW = dh * 2.1f;
            float dx = cx - DateWidth(dh) / 2f;
            FlipCard[][] dg = { yy, mo, dd };
            string[] lbl = { "년", "월", "일" };
            for (int gi = 0; gi < 3; gi++)
            {
                for (int i = 0; i < dg[gi].Length; i++)
                {
                    DrawCard(g, new RectangleF(dx, dy, dw, dh), dg[gi][i], "digit", Digit, now);
                    dx += dw + (i < dg[gi].Length - 1 ? dCardGap : 0);
                }
                dx += dLblGap;
                var lp = TextPath("label", lbl[gi], lblH);
                using (var b = new SolidBrush(Muted))
                {
                    var st = g.Save();
                    g.TranslateTransform(dx + lblW / 2f, dy + dh - dh * 0.06f - lblH / 2f);
                    g.FillPath(b, lp);
                    g.Restore(st);
                }
                dx += lblW + dUnitGap;
            }
            DrawCard(g, new RectangleF(dx, dy, wordW, dh), wd, "word", Accent, now);
        }

        // 모서리 문구(학교·부서) 글자 높이: 화면의 짧은 변 기준으로 고정.
        // 기본 화면·시계 중심 화면, 가로·세로 어느 경우에도 같은 크기 (1080p 에서 약 22px)
        static float CornerCapH(float W, float Hs) { return Math.Max(6f, Math.Min(40f, Math.Min(W, Hs) * 0.02f)); }

        // ── 모서리 문구: 10분마다 자리를 바꿔 번인 방지. 사진 배경일 때는 위쪽 두 모서리만 쓰고 그림자를 넣음 ──
        float cornerMargin;                   // 0 이면 자동, 아니면 모서리 여백 지정 (시계 중심 화면)

        void DrawCorners(Graphics g, Rectangle bounds, long now, bool onPhoto) { DrawCorners(g, bounds, now, onPhoto, onPhoto); }

        void DrawCorners(Graphics g, Rectangle bounds, long now, bool onPhoto, bool topOnly)
        {
            float W = bounds.Width, Hs = bounds.Height;
            float capH = CornerCapH(W, Hs);
            float margin = cornerMargin > 0f ? cornerMargin : Math.Max(8f, Math.Min(W, Hs) * 0.035f);
            float cdx = Math.Abs(drift.X) * W * 0.3f, cdy = Math.Abs(drift.Y) * Hs * 0.3f; // 자리 안에서도 조금씩 이동
            int layout; float alpha;
            CornerState(now, out layout, out alpha);
            // 모서리 번호: 0 왼쪽 위, 1 오른쪽 위, 2 왼쪽 아래, 3 오른쪽 아래
            int[][] layouts = topOnly ? new[] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 0 } }
                                      : new[] { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 1, 0 }, new[] { 3, 2 } };
            if (alpha > 0.01f)
            {
                if (onPhoto)
                {
                    var st = g.Save();
                    float off = Math.Max(1f, capH * 0.07f);
                    g.TranslateTransform(off, off);
                    using (var sh = new SolidBrush(Color.FromArgb((int)(alpha * 170), 0, 0, 0)))
                    {
                        DrawCorner(g, sh, Cfg.LeftText, layouts[layout][0], bounds, capH, margin, cdx, cdy);
                        DrawCorner(g, sh, Cfg.RightText, layouts[layout][1], bounds, capH, margin, cdx, cdy);
                    }
                    g.Restore(st);
                }
                using (var b = new SolidBrush(onPhoto ? Color.FromArgb((int)(alpha * 235), 236, 235, 230) : Color.FromArgb((int)(alpha * 255), Muted)))
                {
                    DrawCorner(g, b, Cfg.LeftText, layouts[layout][0], bounds, capH, margin, cdx, cdy);
                    DrawCorner(g, b, Cfg.RightText, layouts[layout][1], bounds, capH, margin, cdx, cdy);
                }
            }
            DrawVersion(g, bounds, now, onPhoto, topOnly, layouts[layout][0], capH, margin, cdx, cdy, alpha);
        }

        // 남은 모서리에 "배정플립 1.5" 표기 (설치된 버전을 알아보기 위함)
        //  - 기본 화면: 학교 이름 모서리의 대각선 반대편 (학교·부서와 함께 10분마다 자리 이동)
        //  - 사진 배경: 시계 판의 반대쪽 아래 모서리
        //  - 시계 중심 화면: 아래쪽 모서리 (학교 이름의 반대 대각선)
        void DrawVersion(Graphics g, Rectangle bounds, long now, bool onPhoto, bool topOnly, int schoolCorner,
                         float capH, float margin, float cdx, float cdy, float alpha)
        {
            string text = Settings.AppName + " " + Settings.Version;
            int corner = onPhoto ? (PanelLeft(now) ? 3 : 2) : 3 - schoolCorner;
            if (!onPhoto && topOnly) corner = 2 + (corner & 1);
            float c = capH * 0.8f;
            if (onPhoto)
            {
                var st = g.Save();
                float off = Math.Max(1f, c * 0.07f);
                g.TranslateTransform(off, off);
                using (var sh = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) DrawCorner(g, sh, text, corner, bounds, c, margin, 0f, 0f);
                g.Restore(st);
                using (var b = new SolidBrush(Color.FromArgb(200, 236, 235, 230))) DrawCorner(g, b, text, corner, bounds, c, margin, 0f, 0f);
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb((int)(alpha * 255), Muted))) DrawCorner(g, b, text, corner, bounds, c, margin, cdx, cdy);
            }
        }
    }

    // ───────────────────────── 화면 창 ─────────────────────────
    class SaverForm : Form
    {
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

        readonly ClockRenderer renderer;
        static readonly List<SaverForm> All = new List<SaverForm>();   // 모니터마다 하나: 키를 누르면 모든 화면의 사진을 함께 넘김
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly IntPtr previewParent;
        Point? firstMouse;
        int lastSecond = -1;
        int infoVersion = -1;
        long nextRefresh = 0;
        readonly Settings cfg;

        public SaverForm(Rectangle bounds, Settings cfg, IntPtr previewParent)
        {
            this.previewParent = previewParent;
            this.cfg = cfg;
            renderer = new ClockRenderer(cfg);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(11, 12, 14);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);

            if (previewParent != IntPtr.Zero)
            {
                // 화면보호기 설정 창의 작은 모니터 그림 안에 자식 창으로 붙이기
                SetParent(Handle, previewParent);
                SetWindowLong(Handle, -16, GetWindowLong(Handle, -16) | 0x40000000); // GWL_STYLE |= WS_CHILD
                RECT r; GetClientRect(previewParent, out r);
                Size = new Size(r.R - r.L, r.B - r.T);
                Location = new Point(0, 0);
            }
            else
            {
                Bounds = bounds;
                TopMost = true;
                Cursor.Hide();
                MouseMove += OnMouseMoveExit;
                MouseDown += (s, e) => Close();
                KeyDown += (s, e) => Close();
                All.Add(this);
            }

            renderer.Update(DateTime.Now, -100000); // 시작할 때는 넘김 없이 바로 표시
            timer.Interval = 15;
            timer.Tick += OnTick;
            timer.Start();
        }

        // ← / → 는 사진 배경일 때 사진을 넘기고 화면보호기를 유지. 그 밖의 키는 KeyDown 에서 화면보호기 종료
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (previewParent == IntPtr.Zero && (keyData == Keys.Left || keyData == Keys.Right))
            {
                int dir = keyData == Keys.Left ? -1 : 1;
                bool handled = false;
                foreach (var f in All.ToArray())
                    if (!f.IsDisposed && f.renderer.NavigatePhoto(dir, f.clock.ElapsedMilliseconds)) { handled = true; f.Invalidate(); }
                if (handled) return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void OnMouseMoveExit(object s, MouseEventArgs e)
        {
            if (firstMouse == null) { firstMouse = e.Location; return; }
            var p = firstMouse.Value;
            if (Math.Abs(e.X - p.X) > 12 || Math.Abs(e.Y - p.Y) > 12) Close();
        }

        void OnTick(object s, EventArgs e)
        {
            if (previewParent != IntPtr.Zero && !IsWindow(previewParent)) { Close(); return; }
            long now = clock.ElapsedMilliseconds;
            var t = DateTime.Now;
            bool changed = false;
            if (t.Second != lastSecond) { lastSecond = t.Second; changed = renderer.Update(t, now); }
            renderer.TickDrift(now, previewParent == IntPtr.Zero);
            // 날씨·일정·급식: 실행 중에는 1분마다 확인해서 오래된 것만 새로 받음 (미리보기 창은 저장된 정보만 사용)
            if (previewParent == IntPtr.Zero && now >= nextRefresh) { nextRefresh = now + 60000; InfoService.RefreshIfStale(cfg); }
            int ver = InfoService.Version;
            if (ver != infoVersion) { infoVersion = ver; changed = true; }
            if (renderer.TickPhotos(now, ClientSize)) changed = true;
            if (changed || renderer.NeedsFrames(now)) Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            renderer.Paint(e.Graphics, ClientRectangle, clock.ElapsedMilliseconds);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            renderer.DisposePhotos();
            base.OnFormClosed(e);
            Application.Exit();
        }
    }

    // ───────────────────────── 설정 창 ─────────────────────────
    class ConfigForm : Form
    {
        CheckBox c24, cSec, cDate, cSched, cMeal, cWeather;
        TextBox tL, tR, tNeis, tAtpt, tSchool, tKma, tLat, tLon;
        Label lNeisState, lKmaState;
        RadioButton rPhoto;
        TextBox tDir;
        CheckBox cSub;
        ComboBox cPhotoSec, cFit, cPos, cPSize, cDisp;
        static readonly int[] PhotoSecChoices = { 10, 30, 60, 300, 600 };
        static readonly string[] PhotoSecNames = { "10초", "30초", "1분", "5분", "10분" };

        static ComboBox Combo(int x, int y, int w, string[] items, int sel, int def)
        {
            var c = new ComboBox { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SelectedIndex = sel >= 0 && sel < items.Length ? sel : def;
            return c;
        }
        bool clearNeis, clearKma;

        // 인증키 입력칸: 저장된 키는 보여 주지 않음. 새 키를 입력하면 교체, 비워 두면 그대로 유지
        static TextBox SecretBox(int x, int y, int w) { return new TextBox { Left = x, Top = y, Width = w, UseSystemPasswordChar = true }; }
        static string StateText(string key) { return string.IsNullOrEmpty(key) ? "없음" : "● 저장됨"; }
        RadioButton rMeteo, rKma;
        ComboBox cRot;
        static readonly int[] RotChoices = { 5, 8, 15, 30 };
        ComboBox cSpeed;
        static readonly int[] SpeedChoices = { 250, 400, 600, 1000, 1600 };
        static readonly string[] SpeedNames = { "아주 빠르게 (0.25초)", "빠르게 (0.4초)", "보통 (0.6초)", "천천히 (1초)", "아주 천천히 (1.6초)" };

        static Label L(string t, int x, int y) { return new Label { Text = t, Left = x, Top = y + 3, AutoSize = true }; }
        static TextBox T(string t, int x, int y, int w) { return new TextBox { Text = t, Left = x, Top = y, Width = w }; }
        static CheckBox C(string t, bool v, int x, int y) { return new CheckBox { Text = t, Checked = v, Left = x, Top = y, AutoSize = true }; }
        static LinkLabel Link(string text, string url, int x, int y)
        {
            var l = new LinkLabel { Text = text, Left = x, Top = y, AutoSize = true };
            l.LinkClicked += (s, e) => { try { Process.Start(url); } catch { } };
            return l;
        }

        public ConfigForm()
        {
            var cfg = Settings.Load();
            Text = Settings.AppName + " " + Settings.Version + " 설정";
            Font = new Font("Malgun Gothic", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(548, 548);

            var gClock = new GroupBox { Text = "시계", Left = 12, Top = 8, Width = 496, Height = 94 };
            cDisp = Combo(100, 58, 250, new[] { "기본 화면", "시계 중심 화면 (시계를 화면의 70%로)" }, cfg.DisplaySize, 0);
            gClock.Controls.AddRange(new Control[] {
                c24 = C("24시간 형식", cfg.H24, 16, 26), cSec = C("초 표시", cfg.ShowSeconds, 150, 26), cDate = C("날짜·요일 표시", cfg.ShowDate, 260, 26),
                L("화면 크기", 16, 58), cDisp,
                new Label { Text = "가로·세로 자동", Left = 360, Top = 61, AutoSize = true, ForeColor = SystemColors.GrayText } });

            var gCorner = new GroupBox { Text = "모서리 문구 (10분마다 자리 이동, 비우면 숨김)", Left = 12, Top = 110, Width = 496, Height = 62 };
            gCorner.Controls.AddRange(new Control[] {
                L("학교", 16, 26), tL = T(cfg.LeftText, 60, 26, 170), L("부서", 250, 26), tR = T(cfg.RightText, 294, 26, 186) });

            Button bNeisClear, bKmaClear, bFind;
            var gSchool = new GroupBox { Text = "학교 정보 · NEIS 교육정보 개방 포털", Left = 12, Top = 8, Width = 496, Height = 218 };
            cRot = new ComboBox { Left = 120, Top = 80, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            cSpeed = new ComboBox { Left = 350, Top = 80, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int si = 0; si < SpeedChoices.Length; si++) cSpeed.Items.Add(SpeedNames[si]);
            int spi = Array.IndexOf(SpeedChoices, cfg.FlipSpeedMs);
            cSpeed.SelectedIndex = spi >= 0 ? spi : 2;
            foreach (var sec in RotChoices) cRot.Items.Add(sec + "초");
            int ri = Array.IndexOf(RotChoices, cfg.RotateSec);
            cRot.SelectedIndex = ri >= 0 ? ri : 1;
            gSchool.Controls.AddRange(new Control[] {
                cSched = C("학사일정 (시계 위, 오늘 일정과 다가오는 일정)", cfg.ShowSchedule, 16, 24),
                cMeal = C("급식 메뉴 (날짜 아래, 시간에 맞춰 중식/석식)", cfg.ShowMeal, 16, 50),
                L("넘김 간격", 34, 80), cRot,
                L("넘김 속도", 270, 80), cSpeed,
                L("교육청 코드", 16, 114), tAtpt = T(cfg.AtptCode, 110, 114, 60),
                L("학교 코드", 196, 114), tSchool = T(cfg.SchoolCode, 270, 114, 90),
                bFind = new Button { Text = "학교 찾기", Left = 382, Top = 112, Width = 100, Height = 26 },
                L("인증키", 16, 148), tNeis = SecretBox(110, 148, 220),
                lNeisState = new Label { Text = StateText(cfg.NeisKey), Left = 338, Top = 151, AutoSize = true, ForeColor = SystemColors.GrayText },
                bNeisClear = new Button { Text = "지우기", Left = 424, Top = 146, Width = 58, Height = 26 },
                new Label { Text = "새 키를 입력하면 교체, 비워 두면 그대로 유지", Left = 110, Top = 178, AutoSize = true, ForeColor = SystemColors.GrayText },
                Link("인증키 발급", "https://open.neis.go.kr/portal/guide/actKeyPage.do", 16, 178) });

            var gW = new GroupBox { Text = "날씨 (시계 위)", Left = 12, Top = 234, Width = 496, Height = 220 };
            gW.Controls.AddRange(new Control[] {
                cWeather = C("날씨 표시", cfg.ShowWeather, 16, 24),
                rMeteo = new RadioButton { Text = "Open-Meteo (인증키 필요 없음)", Left = 32, Top = 52, AutoSize = true, Checked = cfg.WeatherSource == 0 },
                rKma = new RadioButton { Text = "기상청 단기예보 (공공데이터포털 인증키 필요)", Left = 32, Top = 78, AutoSize = true, Checked = cfg.WeatherSource == 1 },
                L("기상청 인증키", 32, 110), tKma = SecretBox(130, 110, 200),
                lKmaState = new Label { Text = StateText(cfg.KmaKey), Left = 338, Top = 113, AutoSize = true, ForeColor = SystemColors.GrayText },
                bKmaClear = new Button { Text = "지우기", Left = 424, Top = 108, Width = 58, Height = 26 },
                Link("인증키 발급 (공공데이터포털: 기상청_단기예보 조회서비스)", "https://www.data.go.kr/data/15084084/openapi.do", 130, 138),
                L("위도", 32, 172), tLat = T(cfg.Lat, 70, 172, 90), L("경도", 176, 172), tLon = T(cfg.Lon, 214, 172, 100),
                new Label { Text = "기본값: 배정고등학교", Left = 330, Top = 175, AutoSize = true, ForeColor = SystemColors.GrayText } });

            // 배경 사진
            var gBg = new GroupBox { Text = "배경", Left = 12, Top = 8, Width = 496, Height = 272 };
            Button bBrowse;
            cPhotoSec = Combo(110, 146, 100, PhotoSecNames, Array.IndexOf(PhotoSecChoices, cfg.PhotoSec), 1);
            cFit = Combo(320, 146, 162, new[] { "화면 채우기", "사진 전체 보이기" }, cfg.PhotoFit, 0);
            cPos = Combo(110, 180, 170, new[] { "오른쪽 아래", "왼쪽 아래", "10분마다 번갈아" }, cfg.PanelPos, 2);
            cPSize = Combo(370, 180, 112, new[] { "작게", "보통", "크게" }, cfg.PanelSize, 1);
            gBg.Controls.AddRange(new Control[] {
                new RadioButton { Text = "검은 바탕 (화면 가득 플립시계)", Left = 16, Top = 24, AutoSize = true, Checked = cfg.BgMode != 1 },
                rPhoto = new RadioButton { Text = "사진 배경 + 모서리에 작은 시계 판", Left = 16, Top = 50, AutoSize = true, Checked = cfg.BgMode == 1 },
                L("사진 폴더", 34, 82), tDir = T(cfg.PhotoDir, 110, 82, 290),
                bBrowse = new Button { Text = "찾아보기", Left = 406, Top = 80, Width = 76, Height = 26 },
                cSub = C("하위 폴더 사진도 포함", cfg.PhotoSub, 110, 112),
                L("사진 간격", 34, 146), cPhotoSec, L("맞춤", 270, 146), cFit,
                L("시계 위치", 34, 180), cPos, L("크기", 320, 180), cPSize,
                new Label { Text = "사진이 없거나 폴더를 찾을 수 없으면 검은 바탕으로 표시합니다.", Left = 34, Top = 220, AutoSize = true, ForeColor = SystemColors.GrayText },
                new Label { Text = "지원 형식: JPG, PNG, BMP, GIF, TIFF (휴대폰 사진 회전 자동 반영)", Left = 34, Top = 244, AutoSize = true, ForeColor = SystemColors.GrayText } });
            bBrowse.Click += (s, e) =>
            {
                using (var fb = new FolderBrowserDialog { Description = "화면보호기 배경으로 쓸 사진 폴더를 고르세요.", ShowNewFolderButton = false })
                {
                    if (System.IO.Directory.Exists(tDir.Text)) fb.SelectedPath = tDir.Text;
                    if (fb.ShowDialog(this) == DialogResult.OK) { tDir.Text = fb.SelectedPath; rPhoto.Checked = true; }
                }
            };

            var test = new Button { Text = "지금 가져와 보기", Left = 16, Top = 28, Width = 150, Height = 34 };
            var ok = new Button { Text = "확인", Left = 358, Top = 502, Width = 85, Height = 34 };
            var cancel = new Button { Text = "취소", Left = 451, Top = 502, Width = 85, Height = 34, DialogResult = DialogResult.Cancel };
            var hint = new Label { Text = "선택한 정보를 지금 받아 보고 결과를 보여 줍니다.", Left = 176, Top = 37, AutoSize = true, ForeColor = SystemColors.GrayText };

            bFind.Click += (s, e) =>
            {
                string key = tNeis.Text.Trim().Length > 0 ? tNeis.Text.Trim() : (clearNeis ? "" : cfg.NeisKey);
                string guess = tL.Text.Trim();
                using (var f = new SchoolSearchForm(key, guess == "배정고등학교" ? "" : guess))
                {
                    if (f.ShowDialog(this) != DialogResult.OK || f.Picked == null) return;
                    var hit = f.Picked;
                    tAtpt.Text = hit.AtptCode;
                    tSchool.Text = hit.Code;
                    string oldName = tL.Text.Trim();
                    if (oldName.Length == 0 || oldName == "배정고등학교") tL.Text = hit.Name;
                    // 주소의 시·군·구로 날씨 위치(위도·경도)도 채움 (인터넷 없이 프로그램 안의 표에서 찾음)
                    double la, lo; string where;
                    string msg = hit.Name + "(" + hit.AtptName + ")로 설정했습니다.\n\n";
                    if (Geo.TryLocate(hit.Address, out la, out lo, out where))
                    {
                        tLat.Text = la.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                        tLon.Text = lo.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                        msg += "날씨 위치도 " + where + " 중심으로 바꿨습니다 (위도 " + tLat.Text + ", 경도 " + tLon.Text + ").\n" +
                               "기상청 날씨는 5km 단위라 이 정도면 충분합니다. 더 정확히 하려면 위도·경도를 직접 고쳐도 됩니다.";
                    }
                    else
                        msg += "주소로 날씨 위치를 찾지 못했습니다. 위도·경도를 우리 학교 위치로 바꿔 주세요.\n" +
                               "주소: " + hit.Address + "\n(구글 지도에서 학교를 오른쪽 클릭하면 위도, 경도가 나옵니다.)";
                    MessageBox.Show(this, msg, "학교 찾기", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            bNeisClear.Click += (s, e) => { clearNeis = true; tNeis.Text = ""; lNeisState.Text = "지움 (확인을 누르면 삭제)"; };
            bKmaClear.Click += (s, e) => { clearKma = true; tKma.Text = ""; lKmaState.Text = "지움 (확인을 누르면 삭제)"; };
            tNeis.TextChanged += (s, e) => { if (tNeis.Text.Length > 0) { clearNeis = false; lNeisState.Text = "새 키로 교체"; } };
            tKma.TextChanged += (s, e) => { if (tKma.Text.Length > 0) { clearKma = false; lKmaState.Text = "새 키로 교체"; } };

            EventHandler sync = (s, e) =>
            {
                tNeis.Enabled = bNeisClear.Enabled = tAtpt.Enabled = tSchool.Enabled = bFind.Enabled = cSched.Checked || cMeal.Checked;
                cRot.Enabled = cSpeed.Enabled = cSched.Checked;
                rMeteo.Enabled = rKma.Enabled = tLat.Enabled = tLon.Enabled = cWeather.Checked;
                tKma.Enabled = bKmaClear.Enabled = cWeather.Checked && rKma.Checked;
                tDir.Enabled = bBrowse.Enabled = cSub.Enabled = cPhotoSec.Enabled = cFit.Enabled = cPos.Enabled = cPSize.Enabled = rPhoto.Checked;
            };
            rPhoto.CheckedChanged += sync;
            sync += (s, e) => { cDisp.Enabled = !rPhoto.Checked; };
            sync(null, null);
            foreach (var cb in new[] { cSched, cMeal, cWeather }) cb.CheckedChanged += sync;
            rKma.CheckedChanged += sync;
            sync(null, null);

            test.Click += (s, e) =>
            {
                var tmp = Collect(Settings.Load());     // 저장된 인증키를 그대로 쓰되, 새로 입력한 키가 있으면 그것으로 시험
                Cursor = Cursors.WaitCursor;
                string r;
                try { InfoService.LoadCache(); r = InfoService.Refresh(tmp, true); }
                catch (Exception ex) { r = "오류: " + ex.Message; }
                finally { Cursor = Cursors.Default; }
                MessageBox.Show(this, r, "가져오기 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            ok.Click += (s, e) => { Collect(cfg).Save(); DialogResult = DialogResult.OK; Close(); };

            
            // ── 업데이트: 버튼 한 번으로 GitHub 의 새 버전을 받아 설치 ──
            var upd = new Button { Text = "업데이트 확인", Left = 16, Top = 54, Width = 130, Height = 34 };
            var updInfo = new Label { Left = 156, Top = 56, Width = 330, Height = 32, ForeColor = SystemColors.GrayText,
                                      Text = "버튼을 누르면 GitHub에서 새 버전을 확인합니다." };
            string foundVer = null; bool busy = false;
            Action<string> say = t => { if (!IsDisposed) BeginInvoke(new Action(() => { updInfo.Text = t; })); };
            upd.Click += (s, e) =>
            {
                if (busy) return;
                busy = true; upd.Enabled = false;
                if (foundVer == null)
                {
                    updInfo.Text = "확인하는 중…";
                    new System.Threading.Thread(() =>
                    {
                        string err; string v = Updater.CheckLatest(out err);
                        if (IsDisposed) return;
                        BeginInvoke(new Action(() =>
                        {
                            busy = false; upd.Enabled = true;
                            if (v == null) updInfo.Text = "확인 실패: " + err;
                            else if (Updater.IsNewer(v, Settings.Version))
                            {
                                foundVer = v; upd.Text = "지금 업데이트";
                                updInfo.Text = "새 버전 " + v + " 이(가) 있습니다.";
                            }
                            else updInfo.Text = "최신 버전입니다.";
                        }));
                    }) { IsBackground = true }.Start();
                }
                else
                {
                    string ver = foundVer;
                    new System.Threading.Thread(() =>
                    {
                        string err = Updater.DownloadAndStart(ver, say);
                        if (IsDisposed) return;
                        BeginInvoke(new Action(() =>
                        {
                            if (err == null) { updInfo.Text = "업데이트를 시작합니다."; Application.Exit(); return; }
                            busy = false; upd.Enabled = true; updInfo.Text = err;
                        }));
                    }) { IsBackground = true }.Start();
                }
            };

            // ── 탭: 화면 / 배경 / 정보 / 업데이트·정보 가져오기 ──
            Func<string, string> fmtAt = at =>
            {
                DateTime t;
                return at != null && DateTime.TryParseExact(at, "yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out t) ? t.ToString("M월 d일 HH:mm") : "아직 없음";
            };
            var cur = InfoService.Current;
            var gUpd = new GroupBox { Text = "업데이트", Left = 12, Top = 8, Width = 496, Height = 100 };
            gUpd.Controls.AddRange(new Control[] {
                new Label { Text = "설치된 버전: " + Settings.AppName + " " + Settings.Version, Left = 16, Top = 26, AutoSize = true, Font = new Font(Font, FontStyle.Bold) },
                upd, updInfo });
            var gFetch = new GroupBox { Text = "정보 가져오기", Left = 12, Top = 116, Width = 496, Height = 130 };
            gFetch.Controls.AddRange(new Control[] {
                test, hint,
                new Label { Text = "날씨 마지막 갱신: " + fmtAt(cur == null ? null : cur.WeatherAt) + "  (30분마다 확인)", Left = 16, Top = 76, AutoSize = true, ForeColor = SystemColors.GrayText },
                new Label { Text = "일정·급식 마지막 갱신: " + fmtAt(cur == null ? null : cur.SchoolAt) + "  (3시간마다, 날짜가 바뀌면 다시)", Left = 16, Top = 98, AutoSize = true, ForeColor = SystemColors.GrayText } });
            var about = new Label { Text = "배정고등학교 교육연구부 · MIT License", Left = 16, Top = 262, AutoSize = true, ForeColor = SystemColors.GrayText };
            var aboutLink = Link("github.com/" + Updater.Repo, "https://github.com/" + Updater.Repo, 16, 284);

            var tabs = new TabControl { Left = 4, Top = 4, Width = 540, Height = 488 };
            var pg1 = new TabPage("화면"); pg1.Controls.AddRange(new Control[] { gClock, gCorner });
            var pg2 = new TabPage("배경"); pg2.Controls.Add(gBg);
            var pg3 = new TabPage("학교·날씨 정보"); pg3.Controls.AddRange(new Control[] { gSchool, gW });
            var pg4 = new TabPage("업데이트"); pg4.Controls.AddRange(new Control[] { gUpd, gFetch, about, aboutLink });
            foreach (var pg in new[] { pg1, pg2, pg3, pg4 }) { pg.UseVisualStyleBackColor = true; pg.AutoScroll = true; tabs.TabPages.Add(pg); }
            Controls.AddRange(new Control[] { tabs, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }

        Settings Collect(Settings c)
        {
            c.H24 = c24.Checked; c.ShowSeconds = cSec.Checked; c.ShowDate = cDate.Checked;
            c.LeftText = tL.Text.Trim(); c.RightText = tR.Text.Trim();
            c.ShowSchedule = cSched.Checked; c.ShowMeal = cMeal.Checked;
            c.RotateSec = RotChoices[Math.Max(0, cRot.SelectedIndex)];
            c.FlipSpeedMs = SpeedChoices[Math.Max(0, cSpeed.SelectedIndex)];
            if (tNeis.Text.Trim().Length > 0) c.NeisKey = tNeis.Text.Trim(); else if (clearNeis) c.NeisKey = "";
            c.AtptCode = tAtpt.Text.Trim(); c.SchoolCode = tSchool.Text.Trim();
            c.ShowWeather = cWeather.Checked; c.WeatherSource = rKma.Checked ? 1 : 0;
            if (tKma.Text.Trim().Length > 0) c.KmaKey = tKma.Text.Trim(); else if (clearKma) c.KmaKey = "";
            c.DisplaySize = Math.Max(0, cDisp.SelectedIndex);
            c.BgMode = rPhoto.Checked ? 1 : 0;
            c.PhotoDir = tDir.Text.Trim(); c.PhotoSub = cSub.Checked;
            c.PhotoSec = PhotoSecChoices[Math.Max(0, cPhotoSec.SelectedIndex)];
            c.PhotoFit = Math.Max(0, cFit.SelectedIndex); c.PanelPos = Math.Max(0, cPos.SelectedIndex); c.PanelSize = Math.Max(0, cPSize.SelectedIndex);
            c.Lat = tLat.Text.Trim(); c.Lon = tLon.Text.Trim();
            return c;
        }
    }

    // ───────────────────────── 학교 찾기 창 ─────────────────────────
    class SchoolSearchForm : Form
    {
        public InfoService.SchoolHit Picked;
        readonly string neisKey;
        readonly TextBox tName;
        readonly ListView list;
        readonly Label status;
        readonly Button ok;

        public SchoolSearchForm(string neisKey, string initial)
        {
            this.neisKey = neisKey;
            Text = "학교 찾기";
            Font = new Font("Malgun Gothic", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(640, 400);

            var lbl = new Label { Text = "학교 이름", Left = 14, Top = 17, AutoSize = true };
            tName = new TextBox { Left = 90, Top = 14, Width = 420, Text = initial };
            var find = new Button { Text = "찾기", Left = 520, Top = 12, Width = 106, Height = 28 };
            list = new ListView { Left = 14, Top = 50, Width = 612, Height = 270, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
            list.Columns.Add("학교", 170);
            list.Columns.Add("교육청", 150);
            list.Columns.Add("주소", 270);
            status = new Label { Left = 14, Top = 328, Width = 612, Height = 22, ForeColor = SystemColors.GrayText,
                                 Text = "학교 이름의 일부만 입력해도 됩니다. 예: 배정고" };
            ok = new Button { Text = "선택", Left = 448, Top = 356, Width = 85, Height = 32, Enabled = false };
            var cancel = new Button { Text = "취소", Left = 541, Top = 356, Width = 85, Height = 32, DialogResult = DialogResult.Cancel };

            find.Click += (s, e) => Search();
            tName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Search(); } };
            list.SelectedIndexChanged += (s, e) => ok.Enabled = list.SelectedItems.Count > 0;
            list.DoubleClick += (s, e) => Pick();
            ok.Click += (s, e) => Pick();
            Controls.AddRange(new Control[] { lbl, tName, find, list, status, ok, cancel });
            CancelButton = cancel;
            Shown += (s, e) => { tName.Focus(); if (tName.Text.Length > 0) Search(); };
        }

        void Search()
        {
            string name = tName.Text.Trim();
            if (name.Length < 2) { status.Text = "학교 이름을 두 글자 이상 입력하세요."; return; }
            list.Items.Clear();
            ok.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                var hits = InfoService.SearchSchools(name, neisKey);
                foreach (var h in hits)
                {
                    var it = new ListViewItem(new[] { h.Name, h.AtptName, h.Address }) { Tag = h };
                    list.Items.Add(it);
                }
                if (hits.Count == 0) status.Text = "찾은 학교가 없습니다. 이름을 다르게 입력해 보세요. (예: \"배정고등학교\" 대신 \"배정고\")";
                else
                {
                    status.Text = hits.Count + "곳을 찾았습니다. 학교를 고르고 [선택]을 누르세요." +
                                  (string.IsNullOrEmpty(neisKey) ? " (인증키가 없어 최대 5곳까지만 보여 줍니다)" : "");
                    list.Items[0].Selected = true;
                    list.Focus();
                }
            }
            catch (Exception ex) { status.Text = "찾지 못했습니다: " + ex.Message; }
            finally { Cursor = Cursors.Default; }
        }

        void Pick()
        {
            if (list.SelectedItems.Count == 0) return;
            Picked = list.SelectedItems[0].Tag as InfoService.SchoolHit;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ───────────────────────── 진입점 ─────────────────────────
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool LockWorkStation();

        [STAThread]
        static void Main(string[] args)
        {
            string mode = "/c", arg = null;
            // 업데이트 설치 파일이 새 파일을 시험해 볼 때 사용: 한 장 그려 보고 정상이면 0 으로 끝남
            if (args.Length > 0 && string.Equals(args[0], "/selftest", StringComparison.OrdinalIgnoreCase)) { Environment.Exit(SelfTest()); return; }
            if (args.Length > 0)
            {
                string a0 = args[0].Trim();
                // "/p:1234", "/p 1234", "-s" 등 모두 처리
                mode = a0.Length >= 2 ? a0.Substring(0, 2).ToLowerInvariant().Replace('-', '/') : a0.ToLowerInvariant();
                if (a0.Length > 3 && a0[2] == ':') arg = a0.Substring(3);
                else if (args.Length > 1) arg = args[1];
            }

            // 설치하기.bat / 제거하기.bat 에서 호출: 화면보호기로 지정 / 해제
            if (mode == "/i") { Register(arg); return; }
            if (mode == "/u") { Unregister(); return; }

            // 진단용: 설정대로 정보를 받아 결과 출력  (/fetch)
            // 진단용: 학교 이름으로 찾기 결과 출력  (/query 이름)
            if (mode == "/q" && args.Length > 2 && args[2] == "gui")      // 진단용: 학교 찾기 창만 띄움
            {
                Application.EnableVisualStyles();
                using (var f = new SchoolSearchForm(Settings.Load().NeisKey, arg ?? "")) f.ShowDialog();
                return;
            }
            if (mode == "/q")
            {
                foreach (var h in InfoService.SearchSchools(arg ?? "", Settings.Load().NeisKey))
                {
                    double la, lo; string where;
                    string pos = Geo.TryLocate(h.Address, out la, out lo, out where)
                        ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0000},{1:0.0000} ({2})", la, lo, where) : "위치 못 찾음";
                    Console.WriteLine(h.Name + " | " + h.AtptName + " " + h.AtptCode + " | " + h.Code + " | " + h.Address + " | " + pos);
                }
                return;
            }

            if (mode == "/f")
            {
                InfoService.LoadCache();
                var fc = Settings.Load();
                if (arg == "all") fc.ShowWeather = fc.ShowSchedule = fc.ShowMeal = true;
                Console.WriteLine(InfoService.Refresh(fc, true));
                return;
            }

            // 개발용: 화면 없이 PNG로 한 장 그려보기  (/render 파일.png 너비 높이)
            if (mode == "/r")
            {
                RenderPng(args);
                return;
            }

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InfoService.LoadCache();
            if (mode == "/s")
            {
                var cfg = Settings.Load();
                InfoService.RefreshIfStale(cfg);
                foreach (var scr in Screen.AllScreens)
                    new SaverForm(scr.Bounds, cfg, IntPtr.Zero).Show();
                Application.Run();
                // "/s /lock": 바로 가기·단축키로 직접 실행했을 때, 화면보호기가 끝나면 PC를 잠가 로그인 화면으로 넘김
                // (대기 시간으로 켜진 경우의 잠금은 Windows 가 처리하므로 이 옵션은 쓰지 않음)
                if (Array.Exists(args, a => string.Equals(a, "/lock", StringComparison.OrdinalIgnoreCase)))
                    try { LockWorkStation(); } catch { }
            }
            else if (mode == "/p")
            {
                long h;
                if (arg == null || !long.TryParse(arg, out h)) return;
                var f = new SaverForm(Rectangle.Empty, Settings.Load(), new IntPtr(h));
                Application.Run(f);
            }
            else
            {
                Application.Run(new ConfigForm());
            }
        }

        static int SelfTest()
        {
            try
            {
                var cfg = new Settings();
                var r = new ClockRenderer(cfg);
                r.Update(DateTime.Now, -100000);
                using (var bmp = new Bitmap(480, 270))
                using (var g = Graphics.FromImage(bmp)) r.Paint(g, new Rectangle(0, 0, 480, 270), 0);
                return 0;
            }
            catch { return 1; }
        }

        [DllImport("user32.dll")] static extern bool SystemParametersInfo(int action, int param, IntPtr pv, int winIni);
        const int SPI_SETSCREENSAVETIMEOUT = 15, SPI_SETSCREENSAVEACTIVE = 17, SPIF_UPDATE_AND_BROADCAST = 3;
        const string DesktopKey = @"Control Panel\Desktop";

        static void Register(string secondsArg)
        {
            int sec;
            if (secondsArg == null || !int.TryParse(secondsArg, out sec) || sec < 60) sec = 300;
            string self = Assembly.GetExecutingAssembly().Location;
            using (var k = Registry.CurrentUser.CreateSubKey(DesktopKey))
            {
                k.SetValue("SCRNSAVE.EXE", self, RegistryValueKind.String);
                k.SetValue("ScreenSaveActive", "1", RegistryValueKind.String);
                k.SetValue("ScreenSaveTimeOut", sec.ToString(), RegistryValueKind.String);
            }
            try
            {
                SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, IntPtr.Zero, SPIF_UPDATE_AND_BROADCAST);
                SystemParametersInfo(SPI_SETSCREENSAVETIMEOUT, sec, IntPtr.Zero, SPIF_UPDATE_AND_BROADCAST);
            }
            catch { }
        }

        static void Unregister()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(DesktopKey, true))
            {
                if (k == null) return;
                var cur = k.GetValue("SCRNSAVE.EXE") as string;
                if (cur != null && (cur.IndexOf("BaejeongFlip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cur.IndexOf("FlipClock", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    k.DeleteValue("SCRNSAVE.EXE", false);
                    k.SetValue("ScreenSaveActive", "0", RegistryValueKind.String);
                    try { SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 0, IntPtr.Zero, SPIF_UPDATE_AND_BROADCAST); } catch { }
                }
            }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\BaejeongFlip", false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\FlipClockSaver", false); } catch { }
        }

        // 그림 확인용 예시 데이터 (일정·급식은 배정고 2026년 11월 NEIS 실제 자료, 날씨는 예시 값)
        static InfoData SampleInfo(DateTime t, string icon)
        {
            var d = new InfoData { HasWeather = true, Temp = 14.3, TMin = 9, TMax = 17, Pop = 10, Icon = icon, WeatherAt = t.ToString("yyyyMMddHHmm") };
            d.Sky = icon == "sun" ? "맑음" : icon == "partly" ? "구름많음" : icon == "cloud" ? "흐림" : icon == "rain" ? "비" : icon == "snow" ? "눈" : icon == "thunder" ? "뇌우" : "안개";
            if (icon == "rain") d.Pop = 80;
            Func<string, string, string, SchoolEvent> E = (n, a, b) => new SchoolEvent { Name = n, Start = a, End = b };
            d.Events = new List<SchoolEvent> {
                E("수학여행(2학년)", "20261103", "20261105"), E("현장체험학습(1학년)", "20261105", "20261106"),
                E("수능 예비소집", "20261118", "20261118"), E("대학수학능력시험(3년)", "20261119", "20261119"),
                E("2차 정기시험(1,2년)", "20261214", "20261216") };
            d.Meals = new List<MealDay> {
                new MealDay { Date = "20261113", Kind = "중식", Menu = new List<string> { "현미밥", "백순두부찌개", "오리불고기", "감자채피망볶음", "고추튀김", "단호박튀김", "배추김치", "알마늘궁채장아찌" } },
                new MealDay { Date = "20261113", Kind = "석식", Menu = new List<string> { "혼합잡곡밥", "떡국", "미트볼강정", "꽈리고추계란장조림", "생선까스/타르타르소스", "배추김치" } } };
            return d;
        }

        static void RenderPng(string[] args)
        {
            string file = args.Length > 1 ? args[1] : "preview.png";
            int w = args.Length > 2 ? int.Parse(args[2]) : 1920;
            int h = args.Length > 3 ? int.Parse(args[3]) : 1080;
            int flipAt = args.Length > 4 ? int.Parse(args[4]) : -1; // 넘김 중간 장면 확인용(ms)
            var cfg = new Settings();
            if (args.Length > 5) { cfg.H24 = args[5].Contains("24"); cfg.ShowSeconds = args[5].Contains("s"); cfg.ShowDate = args[5].Contains("d"); }
            var t = args.Length > 6 ? DateTime.Parse(args[6]) : new DateTime(2026, 11, 13, 21, 47, 58);
            // 9번째 인자: 표시할 정보 (w 날씨, s 일정, m 급식, 예: wsm) / 10번째: 날씨 아이콘
            cfg.ShowWeather = cfg.ShowSchedule = cfg.ShowMeal = false;
            if (args.Length > 8)
            {
                cfg.ShowWeather = args[8].Contains("w"); cfg.ShowSchedule = args[8].Contains("s"); cfg.ShowMeal = args[8].Contains("m");
                if (args.Length > 9 && args[9] == "cache") InfoService.LoadCache();
                else InfoService.SetForTest(SampleInfo(t, args.Length > 9 ? args[9] : "sun"));
            }
            if (args.Length > 13) { cfg.BgMode = 1; cfg.PhotoDir = args[13]; }
            if (args.Length > 14) cfg.PanelPos = int.Parse(args[14]);
            if (args.Length > 15) cfg.PanelSize = int.Parse(args[15]);
            if (args.Length > 16) cfg.DisplaySize = int.Parse(args[16]);
            var r = new ClockRenderer(cfg);
            r.Update(t, -100000);
            if (args.Length > 7 && args[7] != "-") r.CornerOffsetMs = long.Parse(args[7]);
            if (args.Length > 10) r.RotOffsetMs = long.Parse(args[10]);
            if (args.Length > 11) cfg.FlipSpeedMs = int.Parse(args[11]);
            if (args.Length > 12) r.SwapOffsetMs = long.Parse(args[12]);
            long now = 0;
            if (flipAt >= 0) { r.Update(t.AddSeconds(1), 0); now = flipAt; }
            r.LoadPhotoNowForTest(new Size(w, h));
            using (var bmp = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bmp))
            {
                r.Paint(g, new Rectangle(0, 0, w, h), now);
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
