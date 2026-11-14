// 사진 배경 슬라이드쇼: 지정한 폴더의 사진을 무작위 순서로 화면 크기에 맞춰 미리 그려 두고,
// 정해진 간격마다 천천히 겹치며(크로스페이드) 다음 사진으로 바꿉니다.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace BaejeongFlip
{
    class PhotoShow : IDisposable
    {
        static readonly string[] Exts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
        const int FadeMs = 1500;
        const int MaxFiles = 5000;

        readonly string dir;
        readonly bool includeSub, fit;
        readonly long intervalMs;
        readonly Random rng = new Random();

        List<string> files;
        int pos;
        Bitmap cur, next, pending;          // pending: 백그라운드에서 다 그린 다음 사진
        Size target;
        long shownAt, fadeStart = -1;
        int loading;
        volatile bool noPhotos;

        public PhotoShow(string dir, bool includeSub, bool fit, int intervalSec)
        {
            this.dir = dir;
            this.includeSub = includeSub;
            this.fit = fit;
            intervalMs = Math.Max(5, intervalSec) * 1000L;
        }

        /// 보여 줄 사진이 준비됐는지
        public bool HasImage { get { return cur != null; } }
        /// 폴더가 없거나 사진이 한 장도 없음 → 검은 바탕으로 표시
        public bool NoPhotos { get { return noPhotos; } }
        public bool Fading { get { return next != null; } }

        /// 매 프레임 호출. 다시 그려야 하면 true
        public bool Tick(long now, Size size)
        {
            if (size.Width < 4 || size.Height < 4 || noPhotos) return false;
            bool changed = false;
            if (size != target)
            {
                target = size;
                DisposeAll();
                changed = true;
            }
            // 크로스페이드 끝
            if (next != null && now - fadeStart >= FadeMs)
            {
                cur.Dispose();
                cur = next; next = null; fadeStart = -1; shownAt = now;
                changed = true;
            }
            // 다 그려진 사진 받기
            var ready = Interlocked.Exchange(ref pending, null);
            if (ready != null)
            {
                if (ready.Size != target) ready.Dispose();
                else if (cur == null) { cur = ready; shownAt = now; }
                else { next = ready; fadeStart = now; }
                changed = true;
            }
            // 다음 사진 준비 시작
            if (Interlocked.CompareExchange(ref loading, 0, 0) == 0 && next == null)
            {
                if (cur == null || now - shownAt >= intervalMs) StartLoad(target);
            }
            return changed || next != null;
        }

        public void Draw(Graphics g, Rectangle b, long now)
        {
            if (cur == null) return;
            var st = g.Save();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(cur, new Rectangle(b.X, b.Y, cur.Width, cur.Height));
            g.CompositingMode = CompositingMode.SourceOver;
            if (next != null)
            {
                float a = Math.Min(1f, Math.Max(0f, (now - fadeStart) / (float)FadeMs));
                a = a * a * (3 - 2 * a);
                using (var ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(new ColorMatrix { Matrix33 = a });
                    g.DrawImage(next, new Rectangle(b.X, b.Y, next.Width, next.Height), 0, 0, next.Width, next.Height, GraphicsUnit.Pixel, ia);
                }
            }
            g.Restore(st);
        }

        /// 그림 확인용: 첫 사진을 바로 준비
        public void LoadNowForTest(Size size)
        {
            target = size;
            cur = LoadNext(size);
            if (cur == null) noPhotos = true;
        }

        void StartLoad(Size size)
        {
            Interlocked.Exchange(ref loading, 1);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var bmp = LoadNext(size);
                    if (bmp != null) { var old = Interlocked.Exchange(ref pending, bmp); if (old != null) old.Dispose(); }
                }
                catch { }
                finally { Interlocked.Exchange(ref loading, 0); }
            });
        }

        Bitmap LoadNext(Size size)
        {
            if (files == null) files = Scan();
            if (files.Count == 0) { noPhotos = true; return null; }
            for (int attempt = 0; attempt < Math.Min(files.Count, 10); attempt++)
            {
                if (pos >= files.Count) { pos = 0; Shuffle(files); }
                string path = files[pos++];
                try { return Render(path, size); }
                catch { /* 읽을 수 없는 파일은 건너뜀 */ }
            }
            return null;
        }

        List<string> Scan()
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0 && list.Count < MaxFiles)
            {
                string d = stack.Pop();
                try
                {
                    foreach (var f in Directory.GetFiles(d))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (Array.IndexOf(Exts, ext) >= 0) list.Add(f);
                        if (list.Count >= MaxFiles) break;
                    }
                    if (includeSub)
                        foreach (var s in Directory.GetDirectories(d)) stack.Push(s);
                }
                catch { /* 권한이 없는 폴더는 건너뜀 */ }
            }
            Shuffle(list);
            return list;
        }

        void Shuffle(List<string> l)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        // 사진을 화면 크기에 맞춰 한 번만 그려 둠 (매 프레임 축소하지 않도록)
        Bitmap Render(string path, Size t)
        {
            byte[] data = File.ReadAllBytes(path);          // 파일을 잠그지 않도록 메모리로 읽음
            using (var ms = new MemoryStream(data))
            using (var img = Image.FromStream(ms, false, false))
            {
                ApplyExifOrientation(img, data);
                var bmp = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                using (var ia = new ImageAttributes())
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    ia.SetWrapMode(WrapMode.TileFlipXY);      // 가장자리 번짐 방지
                    float sx = t.Width / (float)img.Width, sy = t.Height / (float)img.Height;
                    float sc = fit ? Math.Min(sx, sy) : Math.Max(sx, sy);
                    float w = img.Width * sc, h = img.Height * sc;
                    var dest = new Rectangle((int)Math.Round((t.Width - w) / 2f), (int)Math.Round((t.Height - h) / 2f),
                                             (int)Math.Round(w), (int)Math.Round(h));
                    g.DrawImage(img, dest, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
                }
                return bmp;
            }
        }

        // 휴대폰 사진의 회전 정보(EXIF Orientation) 반영.
        // JPEG 파일에서 직접 읽고, 못 읽으면 GDI+ 속성을 씀 (환경에 따라 GDI+가 EXIF를 못 읽는 경우가 있음)
        static void ApplyExifOrientation(Image img, byte[] data)
        {
            const int OrientationId = 0x0112;
            int o = JpegOrientation(data);
            if (o == 0)
            {
                try
                {
                    if (Array.IndexOf(img.PropertyIdList, OrientationId) >= 0) o = img.GetPropertyItem(OrientationId).Value[0];
                }
                catch { }
            }
            RotateFlipType rf;
            switch (o)
            {
                case 2: rf = RotateFlipType.RotateNoneFlipX; break;
                case 3: rf = RotateFlipType.Rotate180FlipNone; break;
                case 4: rf = RotateFlipType.Rotate180FlipX; break;
                case 5: rf = RotateFlipType.Rotate90FlipX; break;
                case 6: rf = RotateFlipType.Rotate90FlipNone; break;
                case 7: rf = RotateFlipType.Rotate270FlipX; break;
                case 8: rf = RotateFlipType.Rotate270FlipNone; break;
                default: return;
            }
            img.RotateFlip(rf);
        }

        // JPEG 의 APP1(Exif) → TIFF IFD0 에서 Orientation(0x0112) 값을 찾음. 없으면 0
        static int JpegOrientation(byte[] d)
        {
            try
            {
                if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return 0;
                int i = 2;
                while (i + 4 <= d.Length && d[i] == 0xFF)
                {
                    int marker = d[i + 1];
                    int len = (d[i + 2] << 8) | d[i + 3];
                    if (marker == 0xDA || marker == 0xD9) break;          // 영상 데이터 시작
                    if (marker == 0xE1 && len >= 8 && i + 10 <= d.Length &&
                        d[i + 4] == 'E' && d[i + 5] == 'x' && d[i + 6] == 'i' && d[i + 7] == 'f')
                    {
                        int t = i + 10;                                   // TIFF 헤더
                        bool le = d[t] == 'I';
                        Func<int, int> U16 = o => le ? d[o] | (d[o + 1] << 8) : (d[o] << 8) | d[o + 1];
                        Func<int, int> U32 = o => le ? d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24)
                                                     : (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
                        int ifd = t + U32(t + 4);
                        int n = U16(ifd);
                        for (int k = 0; k < n; k++)
                        {
                            int e = ifd + 2 + k * 12;
                            if (e + 12 > d.Length) break;
                            if (U16(e) == 0x0112) return U16(e + 8);
                        }
                        return 0;
                    }
                    i += 2 + len;
                }
            }
            catch { }
            return 0;
        }

        void DisposeAll()
        {
            if (cur != null) { cur.Dispose(); cur = null; }
            if (next != null) { next.Dispose(); next = null; }
            var p = Interlocked.Exchange(ref pending, null);
            if (p != null) p.Dispose();
            fadeStart = -1;
        }

        public void Dispose() { DisposeAll(); }
    }
}
