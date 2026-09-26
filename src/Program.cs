using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Cursor Hub")]
[assembly: AssemblyDescription("Importa packs de cursores y aplícalos con un clic.")]
[assembly: AssemblyProduct("Cursor Hub")]
[assembly: AssemblyCompany("kisnner26")]
[assembly: AssemblyCopyright("© 2026 kisnner26 · github.com/kisnner26")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace CursorHub
{
    class Role
    {
        public string Key, Label;
        public Role(string k, string l) { Key = k; Label = l; }
    }

    class Pack
    {
        public string Name, Dir;
        public Dictionary<string, string> Map = new Dictionary<string, string>();
        public string PathOf(string role)
        {
            string f;
            return Map.TryGetValue(role, out f) && f.Length > 0 ? Path.Combine(Dir, f) : "";
        }
        public void Save()
        {
            var lines = Map.Where(kv => kv.Value.Length > 0).Select(kv => kv.Key + "=" + kv.Value).ToArray();
            File.WriteAllLines(Path.Combine(Dir, "pack.ini"), lines);
        }
    }

    static class Store
    {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CursorHub");
        public static readonly string Packs = Path.Combine(Root, "packs");
        public static readonly string Backup = Path.Combine(Root, "backup.txt");

        public static readonly Role[] Roles = {
            new Role("Arrow","Selección normal"), new Role("Help","Ayuda"), new Role("AppStarting","En segundo plano"),
            new Role("Wait","Ocupado"), new Role("Crosshair","Precisión"), new Role("IBeam","Texto"),
            new Role("NWPen","Escritura a mano"), new Role("No","No disponible"), new Role("SizeNS","Tamaño vertical"),
            new Role("SizeWE","Tamaño horizontal"), new Role("SizeNWSE","Diagonal 1"), new Role("SizeNESW","Diagonal 2"),
            new Role("SizeAll","Mover"), new Role("UpArrow","Selección alternativa"), new Role("Hand","Vínculo"),
            new Role("Pin","Ubicación"), new Role("Person","Persona") };

        static readonly Dictionary<string, string> CrsMap = new Dictionary<string, string> {
            {"Arrow","Arrow"},{"Help","Help"},{"AppStarting","AppStarting"},{"Wait","Wait"},{"Cross","Crosshair"},{"Crosshair","Crosshair"},
            {"IBeam","IBeam"},{"NWPen","NWPen"},{"No","No"},{"SizeNS","SizeNS"},{"SizeWE","SizeWE"},{"SizeNWSE","SizeNWSE"},
            {"SizeNESW","SizeNESW"},{"SizeAll","SizeAll"},{"UpArrow","UpArrow"},{"Hand","Hand"},{"Pin","Pin"},{"Person","Person"} };

        // ordered: the first keyword found in the file name wins
        static readonly string[][] Guess = {
            new[]{"handwriting","NWPen"}, new[]{"nwpen","NWPen"}, new[]{"pen","NWPen"},
            new[]{"link","Hand"}, new[]{"hand","Hand"},
            new[]{"help","Help"}, new[]{"text","IBeam"}, new[]{"beam","IBeam"},
            new[]{"unavail","No"}, new[]{"unvail","No"}, new[]{"not allowed","No"}, new[]{"forbidden","No"}, new[]{"precision","Crosshair"}, new[]{"cross","Crosshair"},
            new[]{"vertical","SizeNS"}, new[]{"horizontal","SizeWE"}, new[]{"diagonal1","SizeNWSE"}, new[]{"diagonal2","SizeNESW"},
            new[]{"nwse","SizeNWSE"}, new[]{"nesw","SizeNESW"}, new[]{"move","SizeAll"},
            new[]{"alternate","UpArrow"}, new[]{"uparrow","UpArrow"}, new[]{"location","Pin"}, new[]{"pin","Pin"}, new[]{"person","Person"},
            new[]{"working","AppStarting"}, new[]{"background","AppStarting"}, new[]{"busy","Wait"}, new[]{"wait","Wait"},
            new[]{"normal","Arrow"}, new[]{"arrow","Arrow"}, new[]{"pointer","Arrow"}, new[]{"select","Arrow"} };

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        static string UniqueDir(string name)
        {
            name = Sanitize(name); if (name.Length == 0) name = "Pack";
            string d = Path.Combine(Packs, name); int i = 2;
            while (Directory.Exists(d)) d = Path.Combine(Packs, name + " (" + (i++) + ")");
            return d;
        }

        public static List<Pack> LoadPacks()
        {
            var list = new List<Pack>();
            if (!Directory.Exists(Packs)) return list;
            foreach (var d in Directory.GetDirectories(Packs).OrderBy(x => x))
            {
                var p = new Pack { Name = Path.GetFileName(d), Dir = d };
                string ini = Path.Combine(d, "pack.ini");
                if (File.Exists(ini))
                    foreach (var l in File.ReadAllLines(ini))
                    {
                        int i = l.IndexOf('='); if (i > 0) p.Map[l.Substring(0, i)] = l.Substring(i + 1);
                    }
                list.Add(p);
            }
            return list;
        }

        public static Pack NewPack(string name)
        {
            var d = UniqueDir(name); Directory.CreateDirectory(d);
            var p = new Pack { Name = Path.GetFileName(d), Dir = d }; p.Save(); return p;
        }

        public static Pack Duplicate(Pack src)
        {
            var p = NewPack(src.Name + " copia");
            foreach (var f in Directory.GetFiles(src.Dir)) File.Copy(f, Path.Combine(p.Dir, Path.GetFileName(f)), true);
            p.Map = new Dictionary<string, string>(src.Map); p.Save(); return p;
        }

        public static string CopyIn(Pack p, string file)
        {
            string name = Path.GetFileName(file), dest = Path.Combine(p.Dir, name); int i = 2;
            while (File.Exists(dest) && !SameFile(dest, file))
                dest = Path.Combine(p.Dir, Path.GetFileNameWithoutExtension(name) + " (" + (i++) + ")" + Path.GetExtension(name));
            if (!File.Exists(dest)) File.Copy(file, dest);
            return Path.GetFileName(dest);
        }

        static bool SameFile(string a, string b)
        {
            return new FileInfo(a).Length == new FileInfo(b).Length && File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
        }

        // Imports a .zip, .crs, .cur/.ani or folder. Returns the new pack.
        public static Pack Import(string path)
        {
            string name, folder, tmp = null;
            string ext = Path.GetExtension(path).ToLower();
            if (Directory.Exists(path)) { folder = path; name = Path.GetFileName(path.TrimEnd('\\', '/')); }
            else if (ext == ".zip")
            {
                tmp = Path.Combine(Path.GetTempPath(), "cursorhub_" + Guid.NewGuid().ToString("N"));
                ZipFile.ExtractToDirectory(path, tmp); folder = tmp; name = Path.GetFileNameWithoutExtension(path);
            }
            else if (ext == ".crs" || ext == ".cur" || ext == ".ani")
            {
                folder = Path.GetDirectoryName(path);
                name = ext == ".crs" ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(folder);
            }
            else throw new Exception("Formato no soportado: " + ext);

            try
            {
                var pack = NewPack(name);
                var map = new Dictionary<string, string>();
                string crs = ext == ".crs" ? path : Directory.GetFiles(folder, "*.crs", SearchOption.AllDirectories).FirstOrDefault();
                if (crs != null)
                {
                    string cur = null, cdir = Path.GetDirectoryName(crs);
                    foreach (var raw in File.ReadAllLines(crs))
                    {
                        var l = raw.Trim();
                        if (l.StartsWith("[") && l.EndsWith("]")) cur = l.Substring(1, l.Length - 2);
                        else if (cur != null && l.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                        {
                            string role; string f = Path.Combine(cdir, l.Substring(5));
                            if (CrsMap.TryGetValue(cur, out role) && File.Exists(f)) map[role] = f;
                        }
                    }
                }
                if (map.Count == 0)
                {
                    var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.ToLower().EndsWith(".cur") || f.ToLower().EndsWith(".ani")).OrderBy(f => f).ToList();
                    foreach (var f in files)
                    {
                        string n = Path.GetFileNameWithoutExtension(f).ToLower();
                        if (n.Contains("diagonal"))
                        {
                            string dr = n.EndsWith("2") ? "SizeNESW" : n.EndsWith("1") ? "SizeNWSE" : null;
                            if (dr != null) { if (!map.ContainsKey(dr)) map[dr] = f; continue; }
                        }
                        foreach (var g in Guess)
                            if (n.Contains(g[0])) { if (!map.ContainsKey(g[1])) map[g[1]] = f; break; }
                    }
                }
                if (map.Count == 0)
                {
                    Directory.Delete(pack.Dir, true);
                    throw new Exception("No se encontraron cursores (.cur / .ani) en " + path);
                }
                foreach (var kv in map) pack.Map[kv.Key] = CopyIn(pack, kv.Value);
                pack.Save();
                return pack;
            }
            finally { if (tmp != null) try { Directory.Delete(tmp, true); } catch { } }
        }

        public static void SeedSample()
        {
            string marker = Path.Combine(Root, "seeded");
            if (File.Exists(marker)) return;
            Directory.CreateDirectory(Root);
            var asm = Assembly.GetExecutingAssembly();
            var p = NewPack("Aesthetic pack 3");
            string[] order = { "Arrow","Help","AppStarting","Wait","Crosshair","IBeam","NWPen","No","SizeNS","SizeWE","SizeNWSE","SizeNESW","SizeAll","UpArrow","Hand","Pin","Person" };
            string[] files = { "Aesthetic normal select.cur","Aesthetic help select.ani","Aesthetic working n background.ani","Aesthetic busy.ani",
                "Aesthetic precision select.cur","Aesthetic text select.ani","Aesthetic Handwriting.cur","Aesthetic unvailable select.ani",
                "Aesthetic vertical resize.cur","Aesthetic horizontal resize.cur","Aesthetic diagonal resize 1.cur","Aesthetic diagonal resize 2.cur",
                "Aesthetic move.cur","Aesthetic alternate select.cur","Aesthetic Link Select.cur","Aesthetic location select.cur","Aesthetic person select.cur" };
            for (int i = 0; i < order.Length; i++)
                using (var s = asm.GetManifestResourceStream(files[i]))
                using (var o = File.Create(Path.Combine(p.Dir, files[i]))) { s.CopyTo(o); p.Map[order[i]] = files[i]; }
            p.Save();
            File.WriteAllText(marker, "1");
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint action, uint p1, IntPtr p2, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadCursorFromFile(string file);

        static void Broadcast() { SystemParametersInfo(0x0057, 0, IntPtr.Zero, 0x03); } // SPI_SETCURSORS

        static void EnsureBackup(RegistryKey k)
        {
            if (File.Exists(Backup)) return;
            Directory.CreateDirectory(Root);
            string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AestheticPack3", "backup.txt");
            if (File.Exists(old)) { File.Copy(old, Backup); return; }
            var lines = new List<string> { "(Default)=" + (k.GetValue("") ?? "") };
            foreach (var r in Roles) lines.Add(r.Key + "=" + (k.GetValue(r.Key) ?? ""));
            File.WriteAllLines(Backup, lines.ToArray());
        }

        public static void Apply(Pack p)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
            {
                EnsureBackup(k);
                var paths = new List<string>();
                foreach (var r in Roles)
                {
                    string path = p.PathOf(r.Key);
                    k.SetValue(r.Key, path, RegistryValueKind.ExpandString);
                    paths.Add(path);
                }
                k.SetValue("", p.Name, RegistryValueKind.String);
                k.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
                using (var sch = k.CreateSubKey("Schemes")) sch.SetValue(p.Name, string.Join(",", paths.ToArray()), RegistryValueKind.ExpandString);
            }
            Broadcast();
        }

        public static void Restore()
        {
            if (!File.Exists(Backup)) throw new Exception("Todavía no hay copia de seguridad.");
            using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
                foreach (var l in File.ReadAllLines(Backup))
                {
                    int i = l.IndexOf('='); if (i < 0) continue;
                    string key = l.Substring(0, i);
                    k.SetValue(key == "(Default)" ? "" : key, l.Substring(i + 1), RegistryValueKind.ExpandString);
                }
            Broadcast();
        }
    }

    static class Theme
    {
        // paleta del repo "creador-de-flores": papel crema, tinta oscura, trazos finos.
        // el modo oscuro sigue su escenario "noche": papel tostado y tinta crema
        public static Color Paper, PaperDark, Ink, Mid, Faint, Tile1, Tile2, ListBg;
        public static bool Dark { get; private set; }
        static Theme() { SetDark(false); }

        public static void SetDark(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Paper = Color.FromArgb(30, 24, 16); PaperDark = Color.FromArgb(46, 38, 27);
                Ink = Color.FromArgb(238, 230, 214); Mid = Color.FromArgb(180, 164, 134); Faint = Color.FromArgb(84, 72, 54);
                Tile1 = Color.FromArgb(16, 12, 7); Tile2 = Color.FromArgb(40, 32, 22); ListBg = Color.FromArgb(34, 28, 20);
            }
            else
            {
                Paper = Color.FromArgb(245, 240, 232); PaperDark = Color.FromArgb(232, 224, 208);
                Ink = Color.FromArgb(26, 18, 8); Mid = Color.FromArgb(90, 74, 48); Faint = Color.FromArgb(200, 191, 170);
                Tile1 = Color.FromArgb(52, 40, 26); Tile2 = Color.FromArgb(26, 18, 8); ListBg = Color.FromArgb(241, 236, 227);
            }
            if (noise != null) { noise.Dispose(); noise = null; }
        }

        // preferencia guardada: "dark", "light" o nada (sigue a Windows)
        static string SettingsFile { get { return Path.Combine(Store.Root, "theme.txt"); } }
        public static bool LoadPreference()
        {
            try { if (File.Exists(SettingsFile)) return File.ReadAllText(SettingsFile).Trim() == "dark"; } catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (k != null) { var v = k.GetValue("AppsUseLightTheme"); if (v is int) return (int)v == 0; }
            }
            catch { }
            return false;
        }
        public static void SavePreference(bool dark)
        {
            try { Directory.CreateDirectory(Store.Root); File.WriteAllText(SettingsFile, dark ? "dark" : "light"); } catch { }
        }

        // todo se diseña en píxeles lógicos (96 dpi) y se escala con K
        static float k;
        public static float K { get { if (k == 0) using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f; return k; } }
        public static int S(float v) { return (int)Math.Round(v * K); }
        public static Padding S(int l, int t, int r, int b) { return new Padding(S(l), S(t), S(r), S(b)); }

        static string serif, body;
        static string Pick(params string[] names)
        {
            var have = new InstalledFontCollection().Families.Select(f => f.Name).ToList();
            foreach (var n in names) if (have.Contains(n)) return n;
            return "Georgia";
        }
        public static Font Serif(float px, FontStyle st) { if (serif == null) serif = Pick("IM Fell English", "Palatino Linotype", "Book Antiqua", "Georgia"); return new Font(serif, px, st, GraphicsUnit.Pixel); }
        public static Font Body(float px, FontStyle st) { if (body == null) body = Pick("Cormorant Garamond", "EB Garamond", "Garamond", "Georgia"); return new Font(body, px, st, GraphicsUnit.Pixel); }

        static TextureBrush noise;
        public static void PaintPaper(Graphics g, Rectangle r)
        {
            if (noise == null)
            {
                var bmp = new Bitmap(160, 160); var rnd = new Random(7);
                for (int y = 0; y < 160; y++) for (int x = 0; x < 160; x++) bmp.SetPixel(x, y, Dark ? Color.FromArgb(rnd.Next(0, 9), 255, 236, 200) : Color.FromArgb(rnd.Next(0, 12), 70, 50, 20));
                noise = new TextureBrush(bmp);
            }
            using (var b = new SolidBrush(Paper)) g.FillRectangle(b, r);
            g.FillRectangle(noise, r);
        }

        // prepara el Graphics para dibujar en píxeles lógicos
        public static void Begin(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(K, K);
        }

        public static float SpacedWidth(Graphics g, string t, Font f, float sp)
        {
            float w = 0;
            foreach (char c in t) w += (c == ' ' ? f.Size * 0.32f : g.MeasureString(c.ToString(), f, PointF.Empty, StringFormat.GenericTypographic).Width) + sp;
            return t.Length > 0 ? w - sp : 0;
        }
        public static void Spaced(Graphics g, string t, Font f, Color col, float x, float y, float sp)
        {
            using (var b = new SolidBrush(col))
                foreach (char c in t)
                {
                    if (c == ' ') { x += f.Size * 0.32f + sp; continue; }
                    g.DrawString(c.ToString(), f, b, x, y, StringFormat.GenericTypographic);
                    x += g.MeasureString(c.ToString(), f, PointF.Empty, StringFormat.GenericTypographic).Width + sp;
                }
        }
        public static void Text(Graphics g, string t, Font f, Color col, RectangleF r, bool wrap)
        {
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            if (!wrap) sf.FormatFlags |= StringFormatFlags.NoWrap;
            using (var b = new SolidBrush(col)) g.DrawString(t, f, b, r, sf);
        }
    }

    class PaperPanel : Panel
    {
        public PaperPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e) { Theme.PaintPaper(e.Graphics, ClientRectangle); }
    }

    class PaperFlow : FlowLayoutPanel
    {
        public PaperFlow() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e) { Theme.PaintPaper(e.Graphics, ClientRectangle); }
    }

    // botón de contorno que se rellena de tinta desde abajo al pasar el cursor
    class InkButton : Control
    {
        public bool Primary; float t; bool hover; Timer tm = new Timer { Interval = 15 };
        const float Px = 13.5f, Sp = 1.8f;

        public static int WidthFor(string text, int pad)
        {
            using (var bmp = new Bitmap(1, 1)) using (var g = Graphics.FromImage(bmp)) using (var f = Theme.Body(Px, FontStyle.Regular))
                return (int)Math.Ceiling(Theme.SpacedWidth(g, text.ToUpper(), f, Sp)) + pad * 2;
        }

        public InkButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
            tm.Tick += delegate
            {
                float target = hover && Enabled ? 1f : 0f; t += (target - t) * 0.4f;
                if (Math.Abs(target - t) < 0.03f) { t = target; tm.Stop(); }
                Invalidate();
            };
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; tm.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; tm.Start(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.PaintPaper(g, ClientRectangle); Theme.Begin(g);
            float W = Width / Theme.K, H = Height / Theme.K;
            var r = new RectangleF(0.5f, 0.5f, W - 1.5f, H - 1.5f);
            if (Primary && Enabled) using (var b = new SolidBrush(Theme.Ink)) g.FillRectangle(b, r);
            float h = H * t;
            if (h > 0.5f) using (var b = new SolidBrush(Primary ? Theme.Mid : Theme.Ink)) g.FillRectangle(b, 0.5f, H - h, W - 1.5f, h);
            using (var pen = new Pen(Enabled ? Theme.Ink : Theme.Faint, 1f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            Color tc = !Enabled ? Theme.Faint : Primary ? Theme.Paper : (t > 0.5f ? Theme.Paper : Theme.Ink);
            using (var f = Theme.Body(Px, FontStyle.Regular))
            {
                string s = Text.ToUpper(); float sp = Sp, w = Theme.SpacedWidth(g, s, f, sp);
                if (w > W - 12 && s.Length > 1) { sp = Math.Max(0, sp - (w - (W - 12)) / (s.Length - 1)); w = Theme.SpacedWidth(g, s, f, sp); }
                Theme.Spaced(g, s, f, tc, (W - w) / 2, (H - f.GetHeight(g)) / 2 + 1, sp);
            }
        }
    }

    class CursorCard : Control
    {
        public const int LW = 248, LH = 108;
        public string RoleLabel, FileName; public Bitmap Preview; public bool Empty;
        public event EventHandler Change, Clear;
        bool hover, hoverX;
        RectangleF XRect { get { return new RectangleF(LW - 28, 8, 20, 20); } }
        PointF L(Point p) { return new PointF(p.X / Theme.K, p.Y / Theme.K); }
        public CursorCard() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true); Cursor = Cursors.Hand; Size = new Size(Theme.S(LW), Theme.S(LH)); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; hoverX = false; Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e) { bool x = !Empty && XRect.Contains(L(e.Location)); if (x != hoverX) { hoverX = x; Invalidate(); } }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!Empty && XRect.Contains(L(e.Location))) { if (Clear != null) Clear(this, EventArgs.Empty); }
            else if (Change != null) Change(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.PaintPaper(g, ClientRectangle); Theme.Begin(g);
            var r = new RectangleF(0.5f, 0.5f, LW - 1.5f, LH - 1.5f);
            if (hover) using (var b = new SolidBrush(Color.FromArgb(70, Theme.PaperDark))) g.FillRectangle(b, r);
            using (var pen = new Pen(hover ? Theme.Ink : Theme.Faint, 1f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            var tile = new RectangleF(10, 10, 88, 88);
            using (var b = new LinearGradientBrush(tile, Theme.Tile1, Theme.Tile2, 60f)) g.FillRectangle(b, tile);
            if (Preview != null) g.DrawImage(Preview, tile.X + 12, tile.Y + 12, 64, 64);
            float tx = tile.Right + 12, tw = LW - tx - 10;
            using (var f = Theme.Serif(15f, FontStyle.Regular))
                Theme.Text(g, RoleLabel, f, Theme.Ink, new RectangleF(tx, 12, tw - 18, 42), true);
            using (var f = Theme.Body(13.5f, FontStyle.Italic))
                Theme.Text(g, Empty ? "por defecto · clic para elegir" : FileName, f, Theme.Mid, new RectangleF(tx, 60, tw, 36), true);
            if (!Empty && hover)
                using (var pen = new Pen(hoverX ? Theme.Ink : Theme.Mid, hoverX ? 1.8f : 1.1f))
                {
                    var x = XRect; g.DrawLine(pen, x.X + 5, x.Y + 5, x.Right - 5, x.Bottom - 5); g.DrawLine(pen, x.Right - 5, x.Y + 5, x.X + 5, x.Bottom - 5);
                }
        }
    }

    class SectionHeader : Control
    {
        public SectionHeader() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Height = Theme.S(32); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.PaintPaper(g, ClientRectangle); Theme.Begin(g);
            float W = Width / Theme.K;
            using (var f = Theme.Body(12.5f, FontStyle.Regular)) Theme.Spaced(g, Text.ToUpper(), f, Theme.Mid, 1, 7, 3f);
            using (var pen = new Pen(Theme.Faint, 1f)) g.DrawLine(pen, 0, 27, W - 4, 27);
        }
    }

    class WindowButtons : Control
    {
        public event EventHandler CloseClick, MinClick, MaxClick;
        bool hover;
        public WindowButtons() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Size = new Size(Theme.S(74), Theme.S(20)); }
        int Hit(Point p) { float x = p.X / Theme.K; for (int i = 0; i < 3; i++) if (x >= i * 24 && x < i * 24 + 20) return i; return -1; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h == 0 && CloseClick != null) CloseClick(this, EventArgs.Empty);
            if (h == 1 && MinClick != null) MinClick(this, EventArgs.Empty);
            if (h == 2 && MaxClick != null) MaxClick(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.PaintPaper(g, ClientRectangle); Theme.Begin(g);
            for (int i = 0; i < 3; i++)
            {
                var r = new RectangleF(i * 24 + 2, 3, 13, 13);
                using (var pen = new Pen(Theme.Ink, 1f))
                {
                    g.DrawEllipse(pen, r);
                    if (!hover) continue;
                    float cx = r.X + 6.5f, cy = r.Y + 6.5f;
                    if (i == 0) { g.DrawLine(pen, cx - 3, cy - 3, cx + 3, cy + 3); g.DrawLine(pen, cx + 3, cy - 3, cx - 3, cy + 3); }
                    else { g.DrawLine(pen, cx - 3.5f, cy, cx + 3.5f, cy); if (i == 2) g.DrawLine(pen, cx, cy - 3.5f, cx, cy + 3.5f); }
                }
            }
        }
    }

    // luna / sol dibujados a trazo para cambiar de modo
    class ThemeToggle : Control
    {
        bool hover;
        public ThemeToggle() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Size = new Size(Theme.S(24), Theme.S(24)); Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.PaintPaper(g, ClientRectangle); Theme.Begin(g);
            using (var pen = new Pen(hover ? Theme.Ink : Theme.Mid, hover ? 1.4f : 1.1f))
            {
                if (Theme.Dark) // sol: pasa a modo claro
                {
                    g.DrawEllipse(pen, 8, 8, 8, 8);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                        g.DrawLine(pen, 12 + c * 6.5f, 12 + s * 6.5f, 12 + c * 9.5f, 12 + s * 9.5f);
                    }
                }
                else // luna: pasa a modo oscuro
                {
                    // media luna: círculo exterior sin la parte que tapa el interior, y viceversa
                    using (var outer = new GraphicsPath()) using (var inner = new GraphicsPath())
                    {
                        outer.AddEllipse(5, 5, 14, 14); inner.AddEllipse(9.5f, 2.5f, 12, 12);
                        var st = g.Save();
                        g.SetClip(inner, CombineMode.Exclude); g.DrawEllipse(pen, 5, 5, 14, 14); g.Restore(st);
                        st = g.Save();
                        g.SetClip(outer, CombineMode.Intersect); g.DrawEllipse(pen, 9.5f, 2.5f, 12, 12); g.Restore(st);
                    }
                }
            }
        }
    }

    class InkMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { using (var b = new SolidBrush(Theme.Paper)) e.Graphics.FillRectangle(b, e.AffectedBounds); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using (var p = new Pen(Theme.Faint)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected) using (var b = new SolidBrush(Theme.PaperDark)) e.Graphics.FillRectangle(b, 2, 0, e.Item.Width - 4, e.Item.Height);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Theme.Ink; base.OnRenderItemText(e); }
    }

    class MainForm : Form
    {
        ListBox list = new ListBox();
        PaperFlow grid = new PaperFlow();
        PaperPanel head, foot;
        string status = "";
        const string Credit = "creado por @kisnner26", CreditUrl = "https://github.com/kisnner26";
        RectangleF creditRect; bool creditHover;
        InkButton apply = new InkButton(), restore = new InkButton(), import = new InkButton(), add = new InkButton(), dup = new InkButton(), del = new InkButton();
        List<Pack> packs = new List<Pack>();
        Pack Current { get { return list.SelectedIndex >= 0 && list.SelectedIndex < packs.Count ? packs[list.SelectedIndex] : null; } }

        static readonly string[][] Groups = {
            new[]{"Principales","Arrow","Help","AppStarting","Wait","No","Hand"},
            new[]{"Texto y precisión","IBeam","Crosshair","NWPen"},
            new[]{"Tamaño y movimiento","SizeNS","SizeWE","SizeNWSE","SizeNESW","SizeAll"},
            new[]{"Extras","UpArrow","Pin","Person"} };

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string idList);
        ThemeToggle toggle = new ThemeToggle();

        void ApplyTheme()
        {
            BackColor = Theme.Mid; list.BackColor = Theme.ListBg;
            // barras de desplazamiento oscuras en Windows 10/11
            try { SetWindowTheme(grid.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null); SetWindowTheme(list.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
            Invalidate(true);
        }

        void ToggleTheme()
        {
            Theme.SetDark(!Theme.Dark); Theme.SavePreference(Theme.Dark);
            ApplyTheme(); Say(Theme.Dark ? "modo noche." : "modo día.");
        }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

        static int S(float v) { return Theme.S(v); }

        void Drag(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks == 2) { ToggleMax(); return; }
            ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0);
        }
        void ToggleMax() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && WindowState == FormWindowState.Normal) // WM_NCHITTEST: redimensionar desde los bordes
            {
                long lp = m.LParam.ToInt64();
                var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int g = S(7); bool l = p.X < g, r = p.X >= Width - g, t = p.Y < g, b = p.Y >= Height - g;
                if (t && l) { m.Result = (IntPtr)13; return; }
                if (t && r) { m.Result = (IntPtr)14; return; }
                if (b && l) { m.Result = (IntPtr)16; return; }
                if (b && r) { m.Result = (IntPtr)17; return; }
                if (l) { m.Result = (IntPtr)10; return; }
                if (r) { m.Result = (IntPtr)11; return; }
                if (t) { m.Result = (IntPtr)12; return; }
                if (b) { m.Result = (IntPtr)15; return; }
            }
            base.WndProc(ref m);
        }

        public MainForm()
        {
            FormBorderStyle = FormBorderStyle.None; Padding = new Padding(1); // 1px de borde tipo trazo
            Text = "Cursor Hub"; ClientSize = new Size(S(1120), S(740)); MinimumSize = new Size(S(900), S(600));
            StartPosition = FormStartPosition.CenterScreen; Theme.SetDark(Theme.LoadPreference()); BackColor = Theme.Mid; DoubleBuffered = true;
            AllowDrop = true; DragEnter += OnDragEnter; DragDrop += OnDragDrop;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ---- barra lateral ----
            var side = new PaperPanel { Dock = DockStyle.Left, Width = S(262), Padding = Theme.S(18, 0, 18, 16) };
            side.Paint += delegate (object o, PaintEventArgs e) { using (var pen = new Pen(Theme.Faint)) e.Graphics.DrawLine(pen, side.Width - 1, 0, side.Width - 1, side.Height); };

            var chrome = new PaperPanel { Dock = DockStyle.Top, Height = S(120) };
            var wb = new WindowButtons { Left = 0, Top = S(16) };
            wb.CloseClick += delegate { Close(); }; wb.MinClick += delegate { WindowState = FormWindowState.Minimized; }; wb.MaxClick += delegate { ToggleMax(); };
            toggle.Left = S(202); toggle.Top = S(14); toggle.Click += delegate { ToggleTheme(); };
            new ToolTip().SetToolTip(toggle, "Cambiar modo día / noche");
            chrome.Controls.Add(wb); chrome.Controls.Add(toggle); chrome.MouseDown += Drag;
            chrome.Paint += delegate (object o, PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Begin(g); float w = chrome.Width / Theme.K;
                using (var f = Theme.Serif(27f, FontStyle.Regular)) Theme.Text(g, "Cursor Hub", f, Theme.Ink, new RectangleF(0, 44, w, 40), false);
                using (var f = Theme.Body(15f, FontStyle.Italic)) Theme.Text(g, "un cuaderno de cursores", f, Theme.Mid, new RectangleF(2, 84, w, 24), false);
            };

            var hint = new PaperPanel { Dock = DockStyle.Top, Height = S(34) };
            hint.Paint += delegate (object o, PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Begin(g);
                using (var f = Theme.Body(12.5f, FontStyle.Regular)) Theme.Spaced(g, "PACKS", f, Theme.Mid, 1, 8, 3f);
                using (var pen = new Pen(Theme.Faint)) g.DrawLine(pen, 0, 29, hint.Width / Theme.K, 29);
            };

            list.Dock = DockStyle.Fill; list.BorderStyle = BorderStyle.None; list.BackColor = Theme.ListBg; list.IntegralHeight = false;
            list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = S(56);
            list.DrawItem += DrawPackItem; list.SelectedIndexChanged += delegate { RefreshCards(); };
            var lpad = new PaperPanel { Dock = DockStyle.Fill, Padding = Theme.S(0, 10, 0, 0) }; lpad.Controls.Add(list);

            var sbtn = new PaperPanel { Dock = DockStyle.Bottom, Height = S(204) };
            import.Text = "Importar pack"; import.Primary = true; add.Text = "Nuevo pack vacío"; dup.Text = "Duplicar"; del.Text = "Eliminar";
            import.SetBounds(0, S(10), S(226), S(38)); add.SetBounds(0, S(56), S(226), S(36));
            dup.SetBounds(0, S(100), S(109), S(36)); del.SetBounds(S(117), S(100), S(109), S(36));
            sbtn.Controls.AddRange(new Control[] { import, add, dup, del });
            sbtn.Paint += delegate (object o, PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Begin(g);
                using (var f = Theme.Body(14f, FontStyle.Italic)) Theme.Text(g, "o arrastra un .zip, .crs o carpeta a la ventana", f, Theme.Mid, new RectangleF(2, 148, 224, 50), true);
            };
            import.Click += ImportClick; add.Click += delegate { Reload(Store.NewPack("Nuevo pack").Name); };
            dup.Click += delegate { if (Current != null) Reload(Store.Duplicate(Current).Name); }; del.Click += DeleteClick;
            side.Controls.Add(lpad); side.Controls.Add(hint); side.Controls.Add(chrome); side.Controls.Add(sbtn);

            // ---- cabecera ----
            head = new PaperPanel { Dock = DockStyle.Top, Height = S(130) };
            apply.Text = "Aplicar pack"; apply.Primary = true; apply.SetBounds(0, S(38), S(InkButton.WidthFor(apply.Text, 20)), S(40));
            restore.Text = "Restaurar anteriores"; restore.SetBounds(0, S(38), S(InkButton.WidthFor(restore.Text, 20)), S(40));
            head.Controls.AddRange(new Control[] { apply, restore });
            head.Paint += PaintHead;
            head.Resize += delegate { apply.Left = head.Width - S(34) - apply.Width; restore.Left = apply.Left - S(12) - restore.Width; head.Invalidate(); };
            head.MouseDown += Drag;
            apply.Click += ApplyClick; restore.Click += delegate { Run(delegate { Store.Restore(); Say("cursores anteriores restaurados."); }); };

            // ---- pie ----
            foot = new PaperPanel { Dock = DockStyle.Bottom, Height = S(42) };
            foot.Paint += delegate (object o, PaintEventArgs e)
            {
                var g = e.Graphics; Theme.Begin(g); float w = foot.Width / Theme.K;
                using (var pen = new Pen(Theme.Faint)) g.DrawLine(pen, 0, 0.5f, w, 0.5f);
                using (var f = Theme.Body(15f, FontStyle.Italic))
                {
                    float cw = g.MeasureString(Credit, f).Width;
                    creditRect = new RectangleF(w - 34 - cw, 9, cw, 24);
                    Theme.Text(g, status, f, Theme.Mid, new RectangleF(34, 11, creditRect.X - 60, 24), false);
                    using (var b = new SolidBrush(creditHover ? Theme.Ink : Theme.Mid)) g.DrawString(Credit, f, b, creditRect.X, 11);
                    if (creditHover) using (var pen = new Pen(Theme.Ink, 1f)) g.DrawLine(pen, creditRect.X + 3, 31, creditRect.Right - 5, 31);
                }
            };
            foot.MouseMove += delegate (object o, MouseEventArgs e)
            {
                bool h = creditRect.Contains(e.X / Theme.K, e.Y / Theme.K);
                if (h != creditHover) { creditHover = h; foot.Cursor = h ? Cursors.Hand : Cursors.Default; foot.Invalidate(); }
            };
            foot.MouseLeave += delegate { if (creditHover) { creditHover = false; foot.Cursor = Cursors.Default; foot.Invalidate(); } };
            foot.MouseClick += delegate (object o, MouseEventArgs e)
            {
                if (creditRect.Contains(e.X / Theme.K, e.Y / Theme.K)) try { System.Diagnostics.Process.Start(CreditUrl); } catch { }
            };

            // ---- tarjetas ----
            grid.Dock = DockStyle.Fill; grid.AutoScroll = true; grid.Padding = Theme.S(30, 0, 0, 16);
            grid.Resize += delegate { foreach (Control c in grid.Controls) if (c is SectionHeader) c.Width = HeaderWidth(); };
            Controls.Add(grid); Controls.Add(head); Controls.Add(foot); Controls.Add(side);
            Load += delegate { ApplyTheme(); Run(delegate { Store.SeedSample(); Reload(null); Say("elige un pack y pulsa aplicar."); }); };
        }

        int HeaderWidth()
        {
            int cols = Math.Max(1, (grid.ClientSize.Width - grid.Padding.Horizontal) / (S(CursorCard.LW) + S(8)));
            return cols * (S(CursorCard.LW) + S(8)) - S(8);
        }

        void PaintHead(object o, PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Begin(g);
            float avail = restore.Left / Theme.K - 34 - 20;
            var p = Current;
            using (var f = Theme.Serif(38f, FontStyle.Regular)) Theme.Text(g, p == null ? "Sin packs" : p.Name, f, Theme.Ink, new RectangleF(32, 22, avail, 56), false);
            using (var f = Theme.Body(17f, FontStyle.Italic))
                Theme.Text(g, p == null ? "importa un pack para empezar" : "toca un cursor para cambiarlo · la × lo quita", f, Theme.Mid, new RectangleF(35, 80, avail, 26), false);
            using (var pen = new Pen(Color.FromArgb(90, Theme.Ink))) g.DrawLine(pen, 36, 114, 126, 114);
        }

        void Say(string s) { status = s; foot.Invalidate(); }

        void DrawPackItem(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= packs.Count) return;
            var g = e.Graphics;
            using (var bg = new SolidBrush(list.BackColor)) g.FillRectangle(bg, e.Bounds);
            var st = g.Save();
            g.TranslateTransform(e.Bounds.X, e.Bounds.Y); Theme.Begin(g);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            float w = e.Bounds.Width / Theme.K;
            var r = new RectangleF(0.5f, 3, w - 1.5f, 48);
            if (sel) using (var b = new SolidBrush(Theme.Ink)) g.FillRectangle(b, r);
            using (var pen = new Pen(sel ? Theme.Ink : Theme.Faint, 1f)) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            var p = packs[e.Index];
            using (var f = Theme.Serif(15f, FontStyle.Regular)) Theme.Text(g, p.Name, f, sel ? Theme.Paper : Theme.Ink, new RectangleF(12, 8, w - 24, 22), false);
            using (var f = Theme.Body(13.5f, FontStyle.Italic))
                Theme.Text(g, p.Map.Count(kv => kv.Value.Length > 0) + " de " + Store.Roles.Length + " cursores", f, sel ? Theme.Faint : Theme.Mid, new RectangleF(12, 29, w - 24, 20), false);
            g.Restore(st);
        }

        void Run(Action a)
        {
            try { a(); } catch (Exception e) { MessageBox.Show(e.Message, "Cursor Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        void Reload(string select)
        {
            packs = Store.LoadPacks(); list.Items.Clear();
            foreach (var p in packs) list.Items.Add(p.Name);
            int i = select == null ? 0 : packs.FindIndex(p => p.Name == select);
            if (list.Items.Count > 0) list.SelectedIndex = Math.Max(0, i);
            RefreshCards();
        }

        Bitmap Preview(string path)
        {
            int n = S(64); var bmp = new Bitmap(n, n);
            using (var g = Graphics.FromImage(bmp))
                if (path.Length > 0 && File.Exists(path))
                    try
                    {
                        var h = Store.LoadCursorFromFile(path);
                        if (h != IntPtr.Zero) new Cursor(h).DrawStretched(g, new Rectangle(0, 0, n, n));
                    }
                    catch { }
            return bmp;
        }

        void RefreshCards()
        {
            grid.SuspendLayout(); grid.Controls.Clear();
            var p = Current;
            apply.Enabled = p != null;
            if (p != null)
            {
                Control last = null;
                foreach (var grp in Groups)
                {
                    if (last != null) grid.SetFlowBreak(last, true);
                    var hd = new SectionHeader { Text = grp[0], Margin = Theme.S(4, 10, 4, 4), Width = HeaderWidth() };
                    grid.Controls.Add(hd); grid.SetFlowBreak(hd, true); last = hd;
                    for (int gi = 1; gi < grp.Length; gi++)
                    {
                        var role = Store.Roles.First(x => x.Key == grp[gi]); string path = p.PathOf(role.Key);
                        var c = new CursorCard { RoleLabel = role.Label, Empty = path.Length == 0, FileName = path.Length > 0 ? Path.GetFileName(path) : "", Preview = Preview(path), Margin = Theme.S(4, 4, 4, 4) };
                        c.Change += delegate { Run(delegate { ChangeRole(p, role); }); };
                        c.Clear += delegate { p.Map.Remove(role.Key); p.Save(); RefreshCards(); };
                        grid.Controls.Add(c); last = c;
                    }
                }
            }
            grid.ResumeLayout(); list.Invalidate(); head.Invalidate();
        }

        void ChangeRole(Pack p, Role r)
        {
            using (var d = new OpenFileDialog { Filter = "Cursores (*.cur;*.ani)|*.cur;*.ani", Title = "Elige el cursor para: " + r.Label })
                if (d.ShowDialog() == DialogResult.OK)
                {
                    p.Map[r.Key] = Store.CopyIn(p, d.FileName); p.Save(); RefreshCards();
                }
        }

        void ImportClick(object s, EventArgs e)
        {
            var menu = new ContextMenuStrip { Renderer = new InkMenuRenderer(), ShowImageMargin = false, Font = Theme.Body(15f, FontStyle.Regular) };
            menu.Items.Add("Archivo (.zip / .crs)...", null, delegate
            {
                using (var d = new OpenFileDialog { Filter = "Packs (*.zip;*.crs)|*.zip;*.crs", Multiselect = true })
                    if (d.ShowDialog() == DialogResult.OK) ImportPaths(d.FileNames);
            });
            menu.Items.Add("Carpeta...", null, delegate
            {
                using (var d = new FolderBrowserDialog()) if (d.ShowDialog() == DialogResult.OK) ImportPaths(new[] { d.SelectedPath });
            });
            menu.Show(Cursor.Position);
        }

        void ImportPaths(string[] paths)
        {
            Run(delegate
            {
                string last = null;
                foreach (var p in paths) last = Store.Import(p).Name;
                Reload(last); Say("importado: " + last);
            });
        }

        void DeleteClick(object s, EventArgs e)
        {
            var p = Current; if (p == null) return;
            if (MessageBox.Show("¿Eliminar el pack \"" + p.Name + "\" de la biblioteca?", "Cursor Hub", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Run(delegate { Directory.Delete(p.Dir, true); Reload(null); });
        }

        void ApplyClick(object s, EventArgs e)
        {
            var p = Current; if (p == null) return;
            Run(delegate { Store.Apply(p); Say("aplicado: " + p.Name); });
        }

        void OnDragEnter(object s, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; }
        void OnDragDrop(object s, DragEventArgs e) { ImportPaths((string[])e.Data.GetData(DataFormats.FileDrop)); }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
