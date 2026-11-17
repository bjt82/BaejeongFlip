using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BaejeongFlip
{
    // 한 번 눌러 업데이트: GitHub 릴리스에서 새 설치 파일을 받아 검증하고 조용히 설치한다.
    //  - 최신 버전 확인은 GitHub API 대신 "releases/latest" 주소의 이동(redirect)만 읽는다 (호출 횟수 제한 없음)
    //  - 받은 Setup.exe 는 릴리스의 SHA256SUMS.txt 와 대조해 일치할 때만 실행한다
    //  - 설치 파일의 /UPDATE 모드는 새 파일을 시험 실행해 보고, 실패하면 이전 파일로 되돌린다
    static class Updater
    {
        public const string Repo = "bjt82/BaejeongFlip";
        static string Base { get { return "https://github.com/" + Repo; } }

        static void Prepare()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }   // TLS 1.2
        }

        // 최신 릴리스 버전 (예: "1.5"). 실패하면 null 이고 error 에 이유가 들어간다.
        public static string CheckLatest(out string error)
        {
            error = null;
            Prepare();
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(Base + "/releases/latest");
                req.AllowAutoRedirect = false;
                req.Method = "HEAD";
                req.UserAgent = "BaejeongFlip-Updater";
                req.Timeout = 15000;
                using (var res = (HttpWebResponse)req.GetResponse())
                {
                    string loc = res.Headers["Location"];
                    return ParseTag(loc, out error);
                }
            }
            catch (WebException ex)
            {
                var r = ex.Response as HttpWebResponse;
                if (r != null && (int)r.StatusCode >= 300 && (int)r.StatusCode < 400)
                    return ParseTag(r.Headers["Location"], out error);
                error = "인터넷에 연결할 수 없습니다 (" + ex.Message + ")";
                return null;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        static string ParseTag(string loc, out string error)
        {
            error = null;
            int i = loc == null ? -1 : loc.LastIndexOf("/tag/", StringComparison.Ordinal);
            if (i < 0) { error = "릴리스 정보를 찾을 수 없습니다."; return null; }
            string tag = loc.Substring(i + 5).Trim().TrimStart('v', 'V');
            if (tag.Length == 0) { error = "릴리스 정보를 읽을 수 없습니다."; return null; }
            return tag;
        }

        // latest 가 current 보다 새 버전이면 true (점으로 나뉜 숫자 비교)
        public static bool IsNewer(string latest, string current)
        {
            var a = latest.Split('.'); var b = current.Split('.');
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = 0, y = 0;
                if (i < a.Length) int.TryParse(a[i], out x);
                if (i < b.Length) int.TryParse(b[i], out y);
                if (x != y) return x > y;
            }
            return false;
        }

        static string UpdateDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BaejeongFlip\update");
            }
        }

        // 설치 파일을 받아 검증하고 실행한다. 성공하면 null, 실패하면 이유를 돌려준다.
        // report: 진행 상황 문구 (백그라운드 스레드에서 호출됨)
        public static string DownloadAndStart(string version, Action<string> report)
        {
            Prepare();
            string name = "BaejeongFlip-" + version + "-Setup.exe";
            string rel = Base + "/releases/download/v" + version + "/";
            try
            {
                Directory.CreateDirectory(UpdateDir);
                string exe = Path.Combine(UpdateDir, name);
                string sums = Path.Combine(UpdateDir, "SHA256SUMS.txt");
                foreach (var f in Directory.GetFiles(UpdateDir)) { try { File.Delete(f); } catch { } }

                report("확인 파일을 받는 중…");
                using (var wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "BaejeongFlip-Updater";
                    wc.DownloadFile(rel + "SHA256SUMS.txt", sums);
                    report("새 버전을 받는 중…");
                    wc.DownloadFile(rel + Uri.EscapeDataString(name), exe);
                }

                report("파일을 검증하는 중…");
                string want = FindHash(File.ReadAllText(sums, Encoding.UTF8), name);
                if (want == null) return "확인 파일에 " + name + " 의 값이 없어 업데이트를 중단했습니다.";
                string got = Sha256(exe);
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(exe); } catch { }
                    return "받은 파일이 올바르지 않아(검증 실패) 업데이트를 중단했습니다.";
                }

                report("설치하는 중… (설정 창이 곧 닫힙니다)");
                Process.Start(new ProcessStartInfo(exe, "/S /UPDATE") { UseShellExecute = false, WorkingDirectory = UpdateDir });
                return null;
            }
            catch (WebException ex)
            {
                var r = ex.Response as HttpWebResponse;
                if (r != null && r.StatusCode == HttpStatusCode.NotFound)
                    return "v" + version + " 릴리스에 설치 파일이 아직 없습니다. 잠시 후 다시 시도해 주세요.";
                return "받기에 실패했습니다: " + ex.Message;
            }
            catch (Exception ex) { return "업데이트에 실패했습니다: " + ex.Message; }
        }

        static string FindHash(string text, string name)
        {
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length < 66) continue;
                string hash = line.Substring(0, 64);
                string file = line.Substring(64).Trim().TrimStart('*');
                if (string.Equals(file, name, StringComparison.OrdinalIgnoreCase)) return hash;
            }
            return null;
        }

        static string Sha256(string path)
        {
            using (var s = File.OpenRead(path))
            using (var h = SHA256.Create())
            {
                var b = h.ComputeHash(s);
                var sb = new StringBuilder();
                foreach (var x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
