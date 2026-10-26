// God of War: Ascension -- lanzador (GowAscension.exe).
//
// Una sola ventana con el estilo de los menús del juego:
//   1. la primera vez pide la carpeta de TU copia del juego y prepara el juego (comprueba
//      e instala las herramientas, descarga la actualización 1.04 de Sony, saca el
//      firmware y llama a gerar.ps1, con el progreso en la ventana);
//   2. después, los ajustes y JUGAR.
// Va en la raíz del repositorio (junto a gerar.ps1). .NET Framework 4.8 (incluido en
// Windows 10/11): se compila con launcher\exe\compilar.cmd.
// Licencia: MIT (ver LICENSE). Fuente: Cinzel (SIL OFL, launcher\OFL-Cinzel.txt).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WF = System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("God of War: Ascension")]
[assembly: System.Reflection.AssemblyProduct("brunoGowAscension")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]

namespace GowAscension
{
    class Opcion
    {
        public string K, T, D, A;
        public string[][] V;   // texto mostrado, valor
        public Opcion(string k, string t, string[][] v, string d, string a) { K = k; T = t; V = v; D = d; A = a; }
    }

    class Fila
    {
        public Border Borde; public TextBlock Etiqueta, Valor; public Opcion Opcion; public string Accion;
    }

    static class Modo
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra; public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels; public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Ansi)] static extern bool EnumDisplaySettingsA(string dev, int mode, ref DEVMODE dm);
        // el modo actual del monitor principal, sin el escalado de Windows: ancho, alto, Hz
        public static int[] Actual()
        {
            var d = new DEVMODE(); d.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettingsA(null, -1, ref d)) return new[] { 0, 0, 0 };
            return new[] { d.dmPelsWidth, d.dmPelsHeight, d.dmDisplayFrequency };
        }
    }

    class Lanzador : Window
    {
        const string UrlUpdate = "http://b0.ww.np.dl.playstation.net/tppkg/np/BCUS98232/BCUS98232_T76/5525fbb7b6b5456d/UP9000-BCUS98232_00-GOWABDPATCH00001-A0104-V0100-PE.pkg";
        const string NombrePkg = "UP9000-BCUS98232_00-GOWABDPATCH00001-A0104-V0100-PE.pkg";

        readonly string Raiz, Vfs, Elf, ArchAjustes;
        readonly int[] Monitor = Modo.Actual();
        readonly Dictionary<string, string> Ajustes = new Dictionary<string, string>();
        readonly List<Opcion> Opciones = new List<Opcion>();
        readonly List<Fila> Filas = new List<Fila>();
        int Sel;
        bool Listo;
        string Exe, Disco;
        Process Juego, Tarea;

        FontFamily Cinzel;
        readonly Brush Oro = Pincel("#F2C87A"), Pergamino = Pincel("#CDBFA6"), Tenue = Pincel("#7E705C"), Texto = Pincel("#BFB09A");
        Brush Brasa;
        Image Fondo;
        StackPanel Menu, Prep;
        TextBlock Ayuda, Estado, PrepTitulo, PrepTexto, PrepPaso, PrepLinea, Teclas;
        ProgressBar Barra;
        Button BotonA, BotonB;
        Action AccionA, AccionB;
        DispatcherTimer Reloj;

        static Brush Pincel(string c) { var b = (Brush)new BrushConverter().ConvertFrom(c); b.Freeze(); return b; }

        public Lanzador()
        {
            Raiz = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Vfs = System.IO.Path.Combine(Raiz, "vfs");
            Elf = System.IO.Path.Combine(Raiz, "jogo", "GOWA.elf");
            ArchAjustes = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GowAscensionLanzador", "ajustes.json");
            Title = "God of War: Ascension";
            Width = 1280; Height = 720; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Pincel("#0B0806");
            var fuente = System.IO.Path.Combine(Raiz, "launcher", "Cinzel-Black.ttf");
            Cinzel = File.Exists(fuente) ? new FontFamily(new Uri(System.IO.Path.GetDirectoryName(fuente) + "\\"), "./#Cinzel") : new FontFamily("Georgia");
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            g.GradientStops.Add(new GradientStop(Color.FromArgb(150, 150, 60, 18), 0));
            g.GradientStops.Add(new GradientStop(Color.FromArgb(0, 150, 60, 18), 1));
            Brasa = g;
            DefinirOpciones();
            CargarAjustes();
            Construir();
            Reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            Reloj.Tick += (s, e) => Vigilar();
            PreviewKeyDown += Teclado;
            MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is Image || e.OriginalSource is Rectangle) DragMove(); };
            Revisar();
        }

        // ---------------------------------------------------------------- opciones
        void DefinirOpciones()
        {
            string mon = Monitor[0] > 0 ? Monitor[0] + " x " + Monitor[1] + " (detectada), " : "";
            Opciones.Add(new Opcion("res", "Resolución de renderizado", new[] { new[] { "720p", "1" }, new[] { "1440p (2K)", "2" }, new[] { "2160p (4K)", "3" }, new[] { "2880p", "4" } }, "2",
                "A qué resolución dibuja el juego. Más alta = más nítido y más trabajo para la GPU. La ventana se puede poner de cualquier tamaño; Alt+Intro o F11 pasan a pantalla completa."));
            Opciones.Add(new Opcion("fsr", "Escalado FSR 1", new[] { new[] { "Sí", "1" }, new[] { "No", "0" } }, "1",
                "Escala la imagen hasta la resolución de tu monitor, " + mon + "con bordes nítidos (AMD FidelityFX Super Resolution 1). Si la resolución de renderizado ya es la del monitor, no hace nada."));
            Opciones.Add(new Opcion("nit", "Nitidez FSR", new[] { new[] { "Alta", "0" }, new[] { "Media", "0.2" }, new[] { "Suave", "0.5" }, new[] { "Mínima", "1.0" } }, "0.2",
                "Cuánto realza los bordes el paso de nitidez (RCAS) del FSR."));
            Opciones.Add(new Opcion("pant", "Modo de pantalla", new[] { new[] { "Pantalla completa", "1" }, new[] { "Ventana", "0" } }, "1",
                "Pantalla completa sin bordes. Durante el juego, Alt+Intro o F11 alternan."));
            Opciones.Add(new Opcion("idi", "Idioma", new[] { new[] { "Español", "3" }, new[] { "English", "1" }, new[] { "Français", "2" }, new[] { "Português", "17" } }, "3",
                "Idioma de textos y voces."));
            Opciones.Add(new Opcion("hz", "Límite de fps", new[] { new[] { "60", "60" }, new[] { "90", "90" }, new[] { "120", "120" }, new[] { "30", "30" } }, "60",
                "Jugando, el juego casi nunca pasa de 60 por sí solo (lo frena el procesador), así que 90 y 120 apenas cambian nada; si alguna escena ligera pasa de 60, esa escena va más rápida. Para 120 cuadros en pantalla, deja 60 y activa la generación de cuadros."));
            Opciones.Add(new Opcion("fg", "Generación de cuadros", new[] { new[] { "No", "0" }, new[] { "Sí", "1" } }, Monitor[2] >= 100 ? "1" : "0",
                "Inserta un cuadro intermedio entre cada dos del juego: con el juego a 60, 120 cuadros por segundo en pantalla. Útil en monitores de 100 Hz o más (activada por defecto en ellos)."));
            Opciones.Add(new Opcion("vs", "Sincronía vertical", new[] { new[] { "Sí", "1" }, new[] { "No", "0" } }, "1",
                "Espera al refresco del monitor al presentar cada cuadro (sin cortes de imagen)."));
            Opciones.Add(new Opcion("osd", "Medidor en pantalla", new[] { new[] { "Sí", "1" }, new[] { "No", "0" } }, "1",
                "FPS, resolución, generación de cuadros, CPU, GPU, memoria y tarjeta gráfica arriba a la izquierda. F10 lo muestra u oculta."));
            Opciones.Add(new Opcion("son", "Sonido", new[] { new[] { "Sí", "0" }, new[] { "Silencio", "1" } }, "0",
                "Sonido del juego por el dispositivo de salida predeterminado de Windows."));
            foreach (var o in Opciones) Ajustes[o.K] = o.D;
        }

        void CargarAjustes()
        {
            try
            {
                if (!File.Exists(ArchAjustes)) return;
                foreach (Match m in Regex.Matches(File.ReadAllText(ArchAjustes, Encoding.UTF8), "\"(\\w+)\"\\s*:\\s*\"([^\"]*)\""))
                    if (Ajustes.ContainsKey(m.Groups[1].Value)) Ajustes[m.Groups[1].Value] = m.Groups[2].Value;
            }
            catch { }
        }

        void GuardarAjustes()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ArchAjustes));
                var sb = new StringBuilder("{\r\n");
                sb.Append(string.Join(",\r\n", Ajustes.Select(kv => "    \"" + kv.Key + "\":  \"" + kv.Value + "\"")));
                sb.Append("\r\n}\r\n");
                File.WriteAllText(ArchAjustes, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        // ---------------------------------------------------------------- interfaz
        TextBlock Txt(string t, double tam, Brush c, FontFamily f = null)
        {
            return new TextBlock { Text = t, FontSize = tam, Foreground = c, FontFamily = f ?? Cinzel };
        }

        void Construir()
        {
            var raiz = new Grid();
            Fondo = new Image { Stretch = Stretch.Fill, Width = 1280, Height = 720, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(330, 0, 0, 0) };
            raiz.Children.Add(Fondo);
            var g1 = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            g1.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F5070504"), 0));
            g1.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E00B0705"), 0.36));
            g1.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#550B0705"), 0.58));
            g1.GradientStops.Add(new GradientStop(Colors.Transparent, 0.8));
            raiz.Children.Add(new Rectangle { Fill = g1 });
            var g2 = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g2.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            g2.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#CC000000"), 1));
            raiz.Children.Add(new Rectangle { Fill = g2, Height = 140, VerticalAlignment = VerticalAlignment.Bottom });

            var izq = new StackPanel { Margin = new Thickness(64, 26, 0, 0), Width = 560, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var titulo = Txt("GOD OF WAR", 58, Pincel("#E2B86A"));
            titulo.Effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#FF6A1E"), BlurRadius = 22, ShadowDepth = 0, Opacity = 0.55 };
            izq.Children.Add(titulo);
            var sub = Txt("A  S  C  E  N  S  I  O  N", 19, Pincel("#C9A46A")); sub.Margin = new Thickness(6, -4, 0, 14);
            izq.Children.Add(sub);
            Menu = new StackPanel(); izq.Children.Add(Menu);
            Prep = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(14, 10, 0, 0) };
            izq.Children.Add(Prep);
            raiz.Children.Add(izq);

            Ayuda = new TextBlock { Margin = new Thickness(66, 0, 0, 58), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, Width = 560, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Georgia"), FontSize = 14, Foreground = Texto };
            Estado = new TextBlock { Margin = new Thickness(66, 0, 0, 24), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, FontFamily = Cinzel, FontSize = 13, Foreground = Pincel("#9C8A70") };
            Teclas = new TextBlock { Margin = new Thickness(0, 0, 40, 24), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, FontFamily = Cinzel, FontSize = 13, Foreground = Pergamino, Text = "▲▼ ELEGIR     ◀ ▶ CAMBIAR     INTRO  JUGAR     ESC  SALIR" };
            raiz.Children.Add(Ayuda); raiz.Children.Add(Estado); raiz.Children.Add(Teclas);
            Content = raiz;

            // menu: JUGAR, opciones, CARPETA DEL JUEGO, SALIR
            NuevaFila("JUGAR", 26, null, "jugar");
            Menu.Children.Add(Separador());
            foreach (var o in Opciones) NuevaFila(o.T, 16, o, "");
            Menu.Children.Add(Separador());
            NuevaFila("CARPETA DEL JUEGO", 16, null, "carpeta");
            NuevaFila("SALIR", 18, null, "salir");

            // preparacion
            PrepTitulo = Txt("PREPARAR EL JUEGO", 26, Oro); Prep.Children.Add(PrepTitulo);
            PrepTexto = new TextBlock { FontFamily = new FontFamily("Georgia"), FontSize = 16, Foreground = Pergamino, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 20, 16), Width = 520, HorizontalAlignment = HorizontalAlignment.Left };
            Prep.Children.Add(PrepTexto);
            PrepPaso = Txt("", 15, Oro); PrepPaso.Margin = new Thickness(0, 6, 0, 6); Prep.Children.Add(PrepPaso);
            Barra = new ProgressBar { Height = 10, Width = 500, HorizontalAlignment = HorizontalAlignment.Left, Minimum = 0, Maximum = 100, Foreground = Pincel("#C9862E"), Background = Pincel("#2A1E14"), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
            Prep.Children.Add(Barra);
            PrepLinea = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Tenue, Margin = new Thickness(0, 6, 0, 18), Width = 520, HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis };
            Prep.Children.Add(PrepLinea);
            var bp = new StackPanel { Orientation = Orientation.Horizontal };
            BotonA = Boton(); BotonB = Boton(); BotonB.Margin = new Thickness(14, 0, 0, 0);
            BotonA.Click += (s, e) => { if (AccionA != null) AccionA(); }; BotonB.Click += (s, e) => { if (AccionB != null) AccionB(); };
            bp.Children.Add(BotonA); bp.Children.Add(BotonB);
            Prep.Children.Add(bp);
        }

        Border Separador() { return new Border { Height = 1, Margin = new Thickness(14, 5, 40, 5), Background = Tenue }; }

        Button Boton()
        {
            var b = new Button { FontFamily = Cinzel, FontSize = 17, Foreground = Pincel("#1A120B"), Background = Pincel("#E2B86A"), BorderThickness = new Thickness(0), Padding = new Thickness(22, 8, 22, 8), Cursor = Cursors.Hand };
            return b;
        }

        void NuevaFila(string texto, double tam, Opcion o, string accion)
        {
            var b = new Border { Padding = new Thickness(14, 3, 14, 3), BorderThickness = new Thickness(3, 0, 0, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var gr = new Grid();
            gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = Txt(texto, tam, Pergamino); gr.Children.Add(l);
            TextBlock v = null;
            if (o != null) { v = Txt("", tam, Pergamino); Grid.SetColumn(v, 1); gr.Children.Add(v); }
            b.Child = gr;
            var f = new Fila { Borde = b, Etiqueta = l, Valor = v, Opcion = o, Accion = accion };
            int i = Filas.Count;
            Filas.Add(f);
            b.MouseLeftButtonDown += (s, e) =>
            {
                Sel = i;
                if (f.Opcion != null) Cambiar(e.GetPosition(b).X < b.ActualWidth * 0.5 ? -1 : 1);
                else { Pintar(); Activar(); }
                e.Handled = true;
            };
            Menu.Children.Add(b);
        }

        string TextoValor(Opcion o)
        {
            foreach (var p in o.V) if (p[1] == Ajustes[o.K]) return p[0];
            return o.V[0][0];
        }

        void Pintar()
        {
            for (int i = 0; i < Filas.Count; i++)
            {
                var f = Filas[i]; bool on = i == Sel;
                f.Borde.Background = on ? Brasa : Brushes.Transparent;
                f.Borde.BorderBrush = on ? Oro : null;
                f.Etiqueta.Foreground = on ? Oro : Pergamino;
                if (f.Opcion != null)
                {
                    f.Valor.Text = on ? "◀  " + TextoValor(f.Opcion) + "  ▶" : TextoValor(f.Opcion);
                    f.Valor.Foreground = on ? Oro : Pergamino;
                    if (f.Opcion.K == "nit" && Ajustes["fsr"] == "0" && !on) { f.Etiqueta.Foreground = Tenue; f.Valor.Foreground = Tenue; }
                }
            }
            var fs = Filas[Sel];
            Ayuda.Text = fs.Opcion != null ? fs.Opcion.A
                : fs.Accion == "jugar" ? "Empezar la partida con estos ajustes. La ventana del lanzador se oculta mientras juegas."
                : fs.Accion == "carpeta" ? "Elegir otra copia del juego (otra carpeta del disco)." + (Disco != null ? " Ahora: " + Disco : "")
                : "Cerrar el lanzador.";
        }

        void Cambiar(int d)
        {
            var o = Filas[Sel].Opcion;
            if (o == null) return;
            int n = o.V.Length, i = 0;
            for (int k = 0; k < n; k++) if (o.V[k][1] == Ajustes[o.K]) i = k;
            Ajustes[o.K] = o.V[((i + d) % n + n) % n][1];
            GuardarAjustes(); Pintar();
        }

        void Teclado(object s, KeyEventArgs e)
        {
            if (Prep.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape && Tarea == null) { Close(); e.Handled = true; }
                else if (e.Key == Key.Return && BotonA.IsVisible && BotonA.IsEnabled) { { if (AccionA != null) AccionA(); }; e.Handled = true; }
                return;
            }
            bool h = true;
            switch (e.Key)
            {
                case Key.Up: Sel = (Sel - 1 + Filas.Count) % Filas.Count; Pintar(); break;
                case Key.Down: Sel = (Sel + 1) % Filas.Count; Pintar(); break;
                case Key.Left: Cambiar(-1); break;
                case Key.Right: Cambiar(1); break;
                case Key.Return: case Key.Space: Activar(); break;
                case Key.Escape: Close(); break;
                default: h = false; break;
            }
            e.Handled = h;
        }

        void CargarFondo()
        {
            var pic = System.IO.Path.Combine(Vfs, "PS3_GAME", "PIC1.PNG");
            if (Fondo.Source != null || !File.Exists(pic)) return;
            try
            {
                var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(pic); bi.EndInit();
                Fondo.Source = bi;
            }
            catch { }
        }

        // ---------------------------------------------------------------- estado
        string Ruta(params string[] p) { return System.IO.Path.Combine(new[] { Raiz }.Concat(p).ToArray()); }

        void Revisar()
        {
            Exe = Ruta("port", "build_nativerz", "gowa.exe");
            if (!File.Exists(Exe)) Exe = Ruta("port", "build_native", "gowa.exe");
            Disco = LeerEnlace(Ruta("vfs", "PS3_GAME"));
            bool disco = Directory.Exists(Ruta("vfs", "PS3_GAME", "USRDIR"));
            Listo = disco && File.Exists(Exe) && File.Exists(Elf) && File.Exists(Ruta("fw", "lle", "libsre_image.bin"));
            CargarFondo();
            if (!File.Exists(Ruta("gerar.ps1")))
            {
                MostrarPrep("FALTAN ARCHIVOS", "Este programa tiene que estar en la carpeta del proyecto, junto a gerar.ps1, ps3recomp y scripts. Descomprime el .zip completo y abre GowAscension.exe desde ahí.", null, null, null, null);
                return;
            }
            if (Listo) { MostrarMenu(); Estado.Text = "Listo para jugar"; }
            else if (!disco) PasoCarpeta();
            else PasoPreparar(null);
        }

        static string LeerEnlace(string ruta)
        {
            try
            {
                var di = new DirectoryInfo(ruta);
                if (!di.Exists || (di.Attributes & FileAttributes.ReparsePoint) == 0) return null;
                var o = new StringBuilder();
                var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c dir /al \"" + System.IO.Path.GetDirectoryName(ruta) + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                string s = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                var m = Regex.Match(s, "<JUNCTION>\\s+PS3_GAME \\[(.*)\\]");
                return m.Success ? System.IO.Path.GetDirectoryName(m.Groups[1].Value) : null;
            }
            catch { return null; }
        }

        void MostrarMenu()
        {
            Prep.Visibility = Visibility.Collapsed; Menu.Visibility = Visibility.Visible; Teclas.Visibility = Visibility.Visible;
            Sel = 0; Pintar();
        }

        void MostrarPrep(string titulo, string texto, string a, Action accionA, string b, Action accionB)
        {
            Menu.Visibility = Visibility.Collapsed; Teclas.Visibility = Visibility.Collapsed; Prep.Visibility = Visibility.Visible;
            Ayuda.Text = "";
            PrepTitulo.Text = titulo; PrepTexto.Text = texto;
            BotonA.Content = a; BotonA.Visibility = a != null ? Visibility.Visible : Visibility.Collapsed; AccionA = accionA; BotonA.IsEnabled = true;
            BotonB.Content = b; BotonB.Visibility = b != null ? Visibility.Visible : Visibility.Collapsed; AccionB = accionB;
            if (a == null && b == null) { BotonB.Content = "SALIR"; BotonB.Visibility = Visibility.Visible; AccionB = Close; }
        }

        // ---------------------------------------------------------------- 1. la carpeta del juego
        void PasoCarpeta()
        {
            PrepPaso.Text = ""; PrepLinea.Text = ""; Barra.Visibility = Visibility.Collapsed;
            Estado.Text = "Sin preparar";
            MostrarPrep("BIENVENIDO",
                "Para empezar, elige la carpeta de tu copia del juego: la que contiene PS3_GAME. Sirve la europea (BCES01741) o la americana (BCUS98232).\n\n" +
                "Este proyecto no incluye el juego: se traduce y compila en tu PC a partir de tu copia. Lo demás lo hace el lanzador.",
                "ELEGIR CARPETA DEL JUEGO", ElegirCarpeta, Listo ? "VOLVER" : "SALIR", Listo ? (Action)MostrarMenu : Close);
        }

        void ElegirCarpeta()
        {
            var d = new WF.FolderBrowserDialog { Description = "Carpeta de tu copia de God of War: Ascension (la que contiene PS3_GAME)", ShowNewFolderButton = false };
            if (d.ShowDialog() != WF.DialogResult.OK) return;
            string disco = d.SelectedPath;
            if (System.IO.Path.GetFileName(disco).Equals("PS3_GAME", StringComparison.OrdinalIgnoreCase)) disco = System.IO.Path.GetDirectoryName(disco);
            if (!Directory.Exists(System.IO.Path.Combine(disco, "PS3_GAME", "USRDIR")))
            {
                MessageBox.Show(this, "En esa carpeta no hay PS3_GAME\\USRDIR:\n" + disco + "\n\nElige la carpeta que contiene PS3_GAME.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PasoPreparar(disco);
        }

        // ---------------------------------------------------------------- 2. herramientas
        static string RutaCompleta()
        {
            return Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) + ";" + Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User);
        }

        static string Ejecutar(string exe, string args, int ms = 20000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                psi.EnvironmentVariables["Path"] = RutaCompleta();
                var p = Process.Start(psi);
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(ms);
                return p.ExitCode == 0 ? o.Trim() : null;
            }
            catch { return null; }
        }

        List<string> FaltanHerramientas()
        {
            var f = new List<string>();
            int v;
            var py = Ejecutar("python", "-c \"import sys; print(sys.version_info[0]*100+sys.version_info[1])\"");
            if (py == null || !int.TryParse(py, out v) || v < 310) f.Add("Python 3.10 o más nuevo");
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            bool vc = false, clang = File.Exists(System.IO.Path.Combine(pf, "LLVM", "bin", "clang-cl.exe"));
            foreach (var raiz in new[] { pf86, pf })
                foreach (var ed in new[] { "BuildTools", "Community", "Professional", "Enterprise" })
                {
                    var d = System.IO.Path.Combine(raiz, "Microsoft Visual Studio", "2022", ed);
                    if (File.Exists(System.IO.Path.Combine(d, "VC", "Auxiliary", "Build", "vcvars64.bat"))) vc = true;
                    if (File.Exists(System.IO.Path.Combine(d, "VC", "Tools", "Llvm", "x64", "bin", "clang-cl.exe"))) clang = true;
                }
            if (!vc || !clang) f.Add("Visual Studio 2022 Build Tools con C++ y Clang");
            return f;
        }

        void InstalarHerramientas()
        {
            var cmd = Ruta("out", "instalar_herramientas.cmd");
            Directory.CreateDirectory(Ruta("out"));
            File.WriteAllText(cmd, string.Join("\r\n", new[] {
                "@echo off", "chcp 65001 >nul", "title Instalando las herramientas",
                "echo Instalando Python 3.12 y Visual Studio 2022 Build Tools (C++ y Clang) con winget.",
                "echo Windows puede pedir permiso de administrador. Tarda varios minutos (unos 3-4 GB).", "echo.",
                "winget install --id Python.Python.3.12 -e --accept-package-agreements --accept-source-agreements",
                "winget install --id Microsoft.VisualStudio.2022.BuildTools -e --accept-package-agreements --accept-source-agreements --override \"--passive --wait --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Llvm.Clang --add Microsoft.VisualStudio.Component.VC.CMake.Project --includeRecommended\"",
                "echo.", "echo Terminado. Esta ventana se cierra sola.", "timeout /t 6 >nul" }) + "\r\n", Encoding.ASCII);
            try { Tarea = Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo iniciar winget: " + ex.Message, Title); return; }
            BotonA.IsEnabled = false;
            PrepPaso.Text = "Instalando las herramientas... (mira la ventana de consola)";
            Reloj.Start();
        }

        // ---------------------------------------------------------------- 3. preparar
        string DiscoElegido;

        void PasoPreparar(string disco)
        {
            if (disco != null) DiscoElegido = disco;
            if (DiscoElegido == null) DiscoElegido = Disco;
            if (DiscoElegido == null) { PasoCarpeta(); return; }
            Barra.Visibility = Visibility.Collapsed; PrepLinea.Text = "";
            var faltan = FaltanHerramientas();
            if (faltan.Count > 0)
            {
                PrepPaso.Text = "Falta: " + string.Join(", ", faltan);
                MostrarPrep("HERRAMIENTAS",
                    "Para traducir y compilar el juego en tu PC hacen falta Python y el compilador de C++ de Microsoft (gratuitos). El lanzador puede instalarlos con winget (incluido en Windows 10/11).",
                    "INSTALAR HERRAMIENTAS", InstalarHerramientas, "REVISAR DE NUEVO", () => PasoPreparar(null));
                return;
            }
            string fw = BuscarFirmware();
            bool tieneFw = File.Exists(Ruta("out", "gerar", "3_firmware.ok"));
            PrepPaso.Text = "Juego: " + DiscoElegido;
            MostrarPrep("PREPARAR EL JUEGO",
                "Todo listo para preparar el juego a partir de tu copia:\n" +
                (File.Exists(Ruta("dl", NombrePkg)) ? "" : "  · se descarga la actualización 1.04 del servidor oficial de Sony (gratuita, 235 MB);\n") +
                (tieneFw ? "" : fw != null ? "  · el firmware se toma de " + fw + ";\n"
                                          : "  · se descarga el firmware oficial del PS3 del servidor de Sony (gratuito, unos 200 MB);\n") +
                "  · después se traduce y se compila el juego para tu CPU (unos minutos en un PC de 8 núcleos).",
                "EMPEZAR", () => Preparar(fw), "CAMBIAR CARPETA", PasoCarpeta);
        }

        // el firmware: el PS3UPDAT.PUP oficial ya descargado o RPCS3 con el firmware instalado;
        // si no hay ninguno se descarga el oficial (null). Solo cambia, entre versiones, la
        // constante de version que libsre lleva dentro (4.92 / 4.93): el codigo es el mismo.
        string BuscarFirmware()
        {
            var pupDl = Ruta("dl", "PS3UPDAT.PUP");
            if (File.Exists(pupDl)) return pupDl;
            string u = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var s in new[] { System.IO.Path.Combine(u, "Desktop", "rpcs3"), System.IO.Path.Combine(u, "Downloads", "rpcs3"), System.IO.Path.Combine(u, "rpcs3"),
                                      System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rpcs3"),
                                      @"C:\rpcs3", @"D:\rpcs3", @"E:\rpcs3", @"C:\Program Files\RPCS3", @"C:\Games\rpcs3" })
                if (File.Exists(System.IO.Path.Combine(s, "dev_flash", "sys", "external", "libsre.sprx"))) return s;
            return null;
        }

        // la direccion del PS3UPDAT.PUP actual, de la lista de actualizaciones de Sony
        static string UrlFirmware()
        {
            try
            {
                var lista = new WebClient().DownloadString("http://feu01.ps3.update.playstation.net/update/ps3/list/eu/ps3-updatelist.txt");
                var m = Regex.Match(lista, @"CDN=(http://[^;]*/PS3UPDAT\.PUP)");
                if (m.Success) return m.Groups[1].Value;
            }
            catch { }
            return "http://deu01.ps3.update.playstation.net/update/ps3/image/eu/2026_0318_a2b60b6ac1d2e49e230144345616927c/PS3UPDAT.PUP";
        }

        void Preparar(string fw)
        {
            BotonA.IsEnabled = false; BotonB.Visibility = Visibility.Collapsed;
            Barra.Visibility = Visibility.Visible; Barra.Value = 0;
            string pkg = Ruta("dl", NombrePkg), pup = Ruta("dl", "PS3UPDAT.PUP");
            Directory.CreateDirectory(Ruta("dl")); Directory.CreateDirectory(Ruta("out"));
            bool tieneFw = File.Exists(Ruta("out", "gerar", "3_firmware.ok"));
            Action generar = () => Generar(pkg, fw ?? pup);
            Action conFirmware = () =>
            {
                if (fw == null && !tieneFw && !File.Exists(pup))
                    Descargar(UrlFirmware(), pup, "Descargando el firmware oficial del PS3 (servidor de Sony)...", 8, 7, generar);
                else generar();
            };
            if (!File.Exists(pkg)) Descargar(UrlUpdate, pkg, "Descargando la actualización 1.04 (servidor de Sony, 235 MB)...", 0, 8, conFirmware);
            else conFirmware();
        }

        void Descargar(string url, string destino, string texto, double base0, double rango, Action luego)
        {
            PrepPaso.Text = texto;
            var wc = new WebClient();
            wc.DownloadProgressChanged += (s, e) => { Barra.Value = base0 + e.ProgressPercentage * rango / 100.0; PrepLinea.Text = (e.BytesReceived >> 20) + " / " + (e.TotalBytesToReceive >> 20) + " MB"; };
            wc.DownloadFileCompleted += (s, e) =>
            {
                if (e.Error != null || e.Cancelled)
                {
                    try { File.Delete(destino + ".part"); } catch { }
                    Fallo("No se pudo descargar " + System.IO.Path.GetFileName(destino) + ": " + (e.Error != null ? e.Error.Message : "cancelada"));
                    return;
                }
                try { if (File.Exists(destino)) File.Delete(destino); File.Move(destino + ".part", destino); }
                catch (Exception ex) { Fallo("No se pudo guardar " + destino + ": " + ex.Message); return; }
                luego();
            };
            try { if (File.Exists(destino + ".part")) File.Delete(destino + ".part"); wc.DownloadFileAsync(new Uri(url), destino + ".part"); }
            catch (Exception ex) { Fallo("No se pudo descargar " + System.IO.Path.GetFileName(destino) + ": " + ex.Message); }
        }

        static readonly string[][] Etapas = {
            new[] { "1_ferramentas", "Comprobando las herramientas", "18" },
            new[] { "2_jogo", "Preparando los archivos del juego", "22" },
            new[] { "3_firmware", "Sacando el firmware", "28" },
            new[] { "4_ppu", "Traduciendo el código principal", "38" },
            new[] { "5_spu", "Traduciendo el código de las SPU", "70" },
            new[] { "6_compilar", "Compilando el juego (lo más largo)", "97" },
            new[] { "7_jogar", "Terminando", "100" } };

        void Generar(string pkg, string fw)
        {
            // el enlace a un disco anterior se quita y gerar rehace solo la etapa del juego (el
            // ejecutable es siempre el mismo: no hace falta volver a traducir ni compilar)
            var pg = Ruta("vfs", "PS3_GAME");
            if (Directory.Exists(pg) || LeerEnlace(pg) != null) Ejecutar("cmd.exe", "/c rmdir \"" + pg + "\"");
            try { File.Delete(Ruta("out", "gerar", "2_jogo.ok")); } catch { }
            string args = "-NoProfile -ExecutionPolicy Bypass -File \"" + Ruta("gerar.ps1") + "\" -Disco \"" + DiscoElegido + "\" -Update \"" + pkg + "\"";
            if (!File.Exists(Ruta("out", "gerar", "3_firmware.ok"))) args += " -Firmware \"" + fw + "\"";
            var log = Ruta("out", "preparar.log");
            var psi = new ProcessStartInfo("powershell.exe", args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Raiz };
            psi.EnvironmentVariables["Path"] = RutaCompleta();
            var escritor = new StreamWriter(log, false, new UTF8Encoding(false)) { AutoFlush = true };
            var inicio = DateTime.Now;
            PrepPaso.Text = "Preparando el juego...";
            try
            {
                Tarea = new Process { StartInfo = psi, EnableRaisingEvents = true };
                DataReceivedEventHandler linea = (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (escritor) escritor.WriteLine(e.Data);
                    Dispatcher.BeginInvoke((Action)(() => Linea(e.Data, inicio)));
                };
                Tarea.OutputDataReceived += linea; Tarea.ErrorDataReceived += linea;
                Tarea.Exited += (s, e) => Dispatcher.BeginInvoke((Action)(() =>
                {
                    int code = Tarea.ExitCode; Tarea = null; escritor.Dispose();
                    if (code == 0) { Revisar(); if (!Listo) Fallo("La preparación terminó pero falta algo (registro: out\\preparar.log)."); }
                    else Fallo("La preparación falló. Lee el final del registro: out\\preparar.log (y out\\gerar\\).");
                }));
                Tarea.Start(); Tarea.BeginOutputReadLine(); Tarea.BeginErrorReadLine();
            }
            catch (Exception ex) { Fallo("No se pudo ejecutar gerar.ps1: " + ex.Message); }
        }

        void Linea(string l, DateTime inicio)
        {
            var t = l.Trim();
            if (t.Length > 0) PrepLinea.Text = t;
            var m = Regex.Match(t, @"\[gerar\] (\d_\w+) \.\.\.");
            if (m.Success)
                foreach (var e in Etapas)
                    if (e[0] == m.Groups[1].Value)
                    {
                        int idx = Array.IndexOf(Etapas, e);
                        PrepPaso.Text = (idx + 1) + "/7  " + e[1];
                        Barra.Value = idx == 0 ? 15 : double.Parse(Etapas[idx - 1][2]);
                    }
            if (t.Contains("Falta instalar")) PrepPaso.Text = "Faltan herramientas: " + t;
            Estado.Text = "Preparando... " + (int)(DateTime.Now - inicio).TotalMinutes + " min";
        }

        void Fallo(string msg)
        {
            Barra.Visibility = Visibility.Collapsed;
            PrepPaso.Text = msg;
            MostrarPrep("NO SE PUDO PREPARAR", msg, "REINTENTAR", () => PasoPreparar(null), "ABRIR REGISTRO", () =>
            {
                var l = Ruta("out", "preparar.log");
                if (File.Exists(l)) Process.Start("notepad.exe", "\"" + l + "\"");
            });
        }

        // ---------------------------------------------------------------- jugar
        void Activar()
        {
            var f = Filas[Sel];
            if (f.Accion == "salir") { Close(); return; }
            if (f.Accion == "carpeta") { PasoCarpeta(); return; }
            if (f.Accion != "jugar") return;
            if (!Listo) { Revisar(); return; }
            var e = new Dictionary<string, string> {
                { "PS3_VFS_ROOT", Vfs }, { "PS3_LIBSRE_IMAGE", Ruta("fw", "lle", "libsre_image.bin") }, { "PS3_SAVE_ROOT", Ruta("saves") },
                { "GOWA_RES_SCALE", Ajustes["res"] }, { "GOWA_FSR", Ajustes["fsr"] }, { "GOWA_FSR_SHARP", Ajustes["nit"] },
                { "GOWA_FULLSCREEN", Ajustes["pant"] }, { "GOWA_LANG", Ajustes["idi"] }, { "GOWA_FLIP_HZ", Ajustes["hz"] },
                { "GOWA_FRAMEGEN", Ajustes["fg"] }, { "GOWA_PRESENT_SYNC", Ajustes["vs"] }, { "GOWA_OSD", Ajustes["osd"] }, { "GOWA_AUDIO_MUTE", Ajustes["son"] } };
            Directory.CreateDirectory(Ruta("saves")); Directory.CreateDirectory(Ruta("out"));
            var log = Ruta("out", "jogo.log");
            var psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + Exe + "\" \"" + Elf + "\" > \"" + log + "\" 2>&1\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = System.IO.Path.GetDirectoryName(Exe) };
            foreach (var kv in e) psi.EnvironmentVariables[kv.Key] = kv.Value;
            try { Juego = Process.Start(psi); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo abrir el juego: " + ex.Message, Title); return; }
            Hide();
            Reloj.Start();
        }

        void Vigilar()
        {
            if (Tarea != null && Tarea.HasExited && Tarea.StartInfo.UseShellExecute)   // la instalacion de herramientas
            {
                Tarea = null; Reloj.Stop(); PasoPreparar(null); Activate(); return;
            }
            if (Juego != null && Juego.HasExited && Process.GetProcessesByName("gowa").Length == 0)
            {
                Juego = null; Reloj.Stop();
                Estado.Text = "Partida terminada (registro en out\\jogo.log)";
                Show(); Activate();
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var w = new Lanzador();
            if (args.Contains("-JugarYa")) w.ContentRendered += (s, e) => { if (w.Listo) { w.Sel = 0; w.Activar(); } };
            app.Run(w);
        }
    }
}
