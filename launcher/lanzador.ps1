# Lanzador de God of War: Ascension (port nativo), con el estilo de los menus del juego.
# Fondo: la ilustracion de TU copia del juego (PS3_GAME\PIC1.PNG en vfs\), nada del juego
# va en el repositorio. Fuente: Cinzel (SIL Open Font License, launcher\OFL-Cinzel.txt).
# Ajustes en %APPDATA%\GowAscensionLanzador\ajustes.json. Teclado: flechas, Intro, Esc.
#   powershell -STA -ExecutionPolicy Bypass -File launcher\lanzador.ps1 [-JugarYa]
#   -JugarYa: empieza la partida enseguida con los ajustes guardados
param([switch]$JugarYa)
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$Raiz = Split-Path -Parent $PSScriptRoot
$Exe = Join-Path $Raiz 'port\build_nativerz\gowa.exe'
if (-not (Test-Path $Exe)) { $Exe = Join-Path $Raiz 'port\build_native\gowa.exe' }
$Elf = Join-Path $Raiz 'jogo\GOWA.elf'
$Vfs = Join-Path $Raiz 'vfs'
$DirAjustes = Join-Path $env:APPDATA 'GowAscensionLanzador'
$ArchAjustes = Join-Path $DirAjustes 'ajustes.json'

# ---- resolucion real del monitor principal (el modo actual, sin el escalado de Windows;
# es la misma que lee el juego con GOWA_FSR y sin GOWA_OUT_W/H) ----------------------
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class ModoPantalla {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
  public struct DEVMODE {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra; public int dmFields;
    public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
    public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public short dmLogPixels; public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight; }
  [DllImport("user32.dll", CharSet = CharSet.Ansi)] static extern bool EnumDisplaySettingsA(string dev, int mode, ref DEVMODE dm);
  public static int[] Actual() {
    DEVMODE d = new DEVMODE(); d.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
    if (!EnumDisplaySettingsA(null, -1, ref d)) return new int[] { 0, 0, 0 };
    return new int[] { d.dmPelsWidth, d.dmPelsHeight, d.dmDisplayFrequency }; } }
'@
$Monitor = [ModoPantalla]::Actual()
$TxtMonitor = if ($Monitor[0]) { "Monitor ($($Monitor[0]) x $($Monitor[1]))" } else { 'Monitor' }

# ---- opciones: clave, etiqueta, valores (texto mostrado, valor), ayuda -------------
$Opciones = @(
    @{ k = 'res';   t = 'Resolución de renderizado'; v = @(@('720p', '1'), @('1440p (2K)', '2'), @('2160p (4K)', '3'), @('2880p', '4')); d = '2'
       a = 'A qué resolución dibuja el juego. Más alta = más nítido y más trabajo para la GPU. La ventana se puede poner de cualquier tamaño; Alt+Intro o F11 pasan a pantalla completa.' }
    @{ k = 'fsr';   t = 'Escalado FSR 1'; v = @(@('Sí', '1'), @('No', '0')); d = '1'
       a = "Escala la imagen hasta la resolución de tu monitor, $(if ($Monitor[0]) { "$($Monitor[0]) x $($Monitor[1]) (detectada), " })con bordes nítidos (AMD FidelityFX Super Resolution 1). Si la resolución de renderizado ya es la del monitor, no hace nada." }
    @{ k = 'nit';   t = 'Nitidez FSR'; v = @(@('Alta', '0'), @('Media', '0.2'), @('Suave', '0.5'), @('Mínima', '1.0')); d = '0.2'
       a = 'Cuánto realza los bordes el paso de nitidez (RCAS) del FSR.' }
    @{ k = 'pant';  t = 'Modo de pantalla'; v = @(@('Pantalla completa', '1'), @('Ventana', '0')); d = '1'
       a = 'Pantalla completa sin bordes. Durante el juego, Alt+Intro o F11 alternan.' }
    @{ k = 'idi';   t = 'Idioma'; v = @(@('Español', '3'), @('English', '1'), @('Français', '2'), @('Português', '17')); d = '3'
       a = 'Idioma de textos y voces.' }
    @{ k = 'hz';    t = 'Límite de fps'; v = @(@('60', '60'), @('30', '30')); d = '60'
       a = 'El juego avanza parte de su lógica por cuadro: por encima de 60 todo va más rápido. Para más fluidez, usa la generación de cuadros.' }
    @{ k = 'fg';    t = 'Generación de cuadros'; v = @(@('No', '0'), @('Sí', '1')); d = '0'
       a = 'Inserta un cuadro intermedio entre cada dos del juego: hasta el doble de cuadros en pantalla. Cuesta rendimiento al juego (unos 45 fps en vez de 58).' }
    @{ k = 'vs';    t = 'Sincronía vertical'; v = @(@('Sí', '1'), @('No', '0')); d = '1'
       a = 'Espera al refresco del monitor al presentar cada cuadro (sin cortes de imagen).' }
    @{ k = 'osd';   t = 'Medidor en pantalla'; v = @(@('Sí', '1'), @('No', '0')); d = '1'
       a = 'FPS, resolución, generación de cuadros, CPU, GPU y memoria arriba a la izquierda. F10 lo muestra u oculta.' }
    @{ k = 'son';   t = 'Sonido'; v = @(@('Sí', '0'), @('Silencio', '1')); d = '0'
       a = 'Sonido del juego por el dispositivo de salida predeterminado de Windows.' }
)
$Ajustes = @{}
foreach ($o in $Opciones) { $Ajustes[$o.k] = $o.d }
try {
    if (Test-Path $ArchAjustes) {
        $j = Get-Content $ArchAjustes -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($p in $j.PSObject.Properties) { if ($Ajustes.ContainsKey($p.Name)) { $Ajustes[$p.Name] = [string]$p.Value } }
    }
} catch { }
function Guardar-Ajustes {
    try {
        New-Item -ItemType Directory -Force $DirAjustes | Out-Null
        $Ajustes | ConvertTo-Json | Set-Content -Path $ArchAjustes -Encoding UTF8
    } catch { }
}

# ---- interfaz ----------------------------------------------------------------------
$fuente = "file:///$($PSScriptRoot -replace '\\','/')/#Cinzel"
[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="God of War: Ascension" Width="1280" Height="720" WindowStyle="None" ResizeMode="NoResize"
        WindowStartupLocation="CenterScreen" Background="#0B0806" AllowsTransparency="False">
  <Grid>
    <Image x:Name="Fondo" Stretch="Fill" Width="1280" Height="720" HorizontalAlignment="Left" Margin="330,0,0,0"/>
    <Rectangle>
      <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
          <GradientStop Color="#F5070504" Offset="0"/>
          <GradientStop Color="#E00B0705" Offset="0.36"/>
          <GradientStop Color="#550B0705" Offset="0.58"/>
          <GradientStop Color="#00000000" Offset="0.8"/>
        </LinearGradientBrush>
      </Rectangle.Fill>
    </Rectangle>
    <Rectangle VerticalAlignment="Bottom" Height="140">
      <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
          <GradientStop Color="#00000000" Offset="0"/>
          <GradientStop Color="#CC000000" Offset="1"/>
        </LinearGradientBrush>
      </Rectangle.Fill>
    </Rectangle>
    <StackPanel Margin="64,26,0,0" HorizontalAlignment="Left" VerticalAlignment="Top" Width="540">
      <TextBlock Text="GOD OF WAR" FontFamily="$fuente" FontSize="58" Foreground="#E2B86A">
        <TextBlock.Effect><DropShadowEffect Color="#FF6A1E" BlurRadius="22" ShadowDepth="0" Opacity="0.55"/></TextBlock.Effect>
      </TextBlock>
      <TextBlock Text="A  S  C  E  N  S  I  O  N" FontFamily="$fuente" FontSize="19" Foreground="#C9A46A" Margin="6,-4,0,14"/>
      <StackPanel x:Name="Menu"/>
    </StackPanel>
    <TextBlock x:Name="Ayuda" Margin="66,0,0,58" VerticalAlignment="Bottom" HorizontalAlignment="Left" Width="560"
               TextWrapping="Wrap" FontFamily="Georgia" FontSize="14" Foreground="#BFB09A"/>
    <TextBlock x:Name="Estado" Margin="66,0,0,24" VerticalAlignment="Bottom" HorizontalAlignment="Left"
               FontFamily="$fuente" FontSize="13" Foreground="#9C8A70"/>
    <TextBlock Margin="0,0,40,24" VerticalAlignment="Bottom" HorizontalAlignment="Right"
               FontFamily="$fuente" FontSize="13" Foreground="#C9B89A"
               Text="▲▼ ELEGIR     ◀ ▶ CAMBIAR     INTRO  JUGAR     ESC  SALIR"/>
  </Grid>
</Window>
"@
$win = [Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlNodeReader $xaml))
$menu = $win.FindName('Menu'); $ayuda = $win.FindName('Ayuda'); $estado = $win.FindName('Estado')
$pic = Join-Path $Vfs 'PS3_GAME\PIC1.PNG'
if (Test-Path $pic) {
    $bi = New-Object Windows.Media.Imaging.BitmapImage
    $bi.BeginInit(); $bi.CacheOption = 'OnLoad'; $bi.UriSource = [Uri]$pic; $bi.EndInit()
    $win.FindName('Fondo').Source = $bi
}

$oro = [Windows.Media.BrushConverter]::new().ConvertFrom('#F2C87A')
$pergamino = [Windows.Media.BrushConverter]::new().ConvertFrom('#CDBFA6')
$tenue = [Windows.Media.BrushConverter]::new().ConvertFrom('#7E705C')
$brasa = New-Object Windows.Media.LinearGradientBrush
$brasa.StartPoint = '0,0'; $brasa.EndPoint = '1,0'
$brasa.GradientStops.Add((New-Object Windows.Media.GradientStop ([Windows.Media.Color]::FromArgb(150, 150, 60, 18)), 0))
$brasa.GradientStops.Add((New-Object Windows.Media.GradientStop ([Windows.Media.Color]::FromArgb(0, 150, 60, 18)), 1))

# filas: JUGAR, las opciones, SALIR
$Filas = New-Object System.Collections.ArrayList
function Nueva-Fila([string]$texto, [int]$tam, $opcion) {
    $b = New-Object Windows.Controls.Border
    $b.Padding = '14,3,14,3'; $b.Margin = '0,0,0,0'; $b.BorderThickness = '3,0,0,0'
    $g = New-Object Windows.Controls.Grid
    $c1 = New-Object Windows.Controls.ColumnDefinition; $c1.Width = '*'
    $c2 = New-Object Windows.Controls.ColumnDefinition; $c2.Width = 'Auto'
    $g.ColumnDefinitions.Add($c1); $g.ColumnDefinitions.Add($c2)
    $l = New-Object Windows.Controls.TextBlock
    $l.Text = $texto; $l.FontFamily = $fuente; $l.FontSize = $tam; $l.Foreground = $pergamino
    $g.Children.Add($l) | Out-Null
    $vt = $null
    if ($opcion) {
        $vt = New-Object Windows.Controls.TextBlock
        $vt.FontFamily = $fuente; $vt.FontSize = $tam; $vt.Foreground = $pergamino
        [Windows.Controls.Grid]::SetColumn($vt, 1)
        $g.Children.Add($vt) | Out-Null
    }
    $b.Child = $g
    $menu.Children.Add($b) | Out-Null
    $f = [pscustomobject]@{ Borde = $b; Etiqueta = $l; Valor = $vt; Opcion = $opcion }
    [void]$Filas.Add($f)
    # the row's index in Tag; the handler is a plain script block (GetNewClosure would
    # put it in its own module, where Cambiar, Pintar and $script:Sel are not seen)
    $b.Tag = $Filas.Count - 1
    $b.Background = [Windows.Media.Brushes]::Transparent   # clickable over its whole width
    $b.Cursor = [Windows.Input.Cursors]::Hand
    $b.Add_MouseLeftButtonDown($script:ClicFila)
}
$script:ClicFila = {
    param($s, $e)
    $script:Sel = [int]$s.Tag
    $f2 = $Filas[$script:Sel]
    if ($f2.Opcion) {
        # left half of the row: previous value, right half: next
        $x = $e.GetPosition($s).X
        Cambiar $(if ($x -lt $s.ActualWidth * 0.5) { -1 } else { 1 })
    } else { Pintar; Activar }
    $e.Handled = $true
}
Nueva-Fila 'JUGAR' 26 $null
$sep = New-Object Windows.Controls.Border; $sep.Height = 1; $sep.Margin = '14,5,40,5'; $sep.Background = $tenue
$menu.Children.Add($sep) | Out-Null
foreach ($o in $Opciones) { Nueva-Fila $o.t 16 $o }
$sep2 = New-Object Windows.Controls.Border; $sep2.Height = 1; $sep2.Margin = '14,5,40,5'; $sep2.Background = $tenue
$menu.Children.Add($sep2) | Out-Null
Nueva-Fila 'SALIR' 18 $null
$script:Sel = 0

function Texto-Valor($o) {
    $v = $Ajustes[$o.k]
    foreach ($par in $o.v) { if ($par[1] -eq $v) { return $par[0] } }
    return $o.v[0][0]
}
function Pintar {
    for ($i = 0; $i -lt $Filas.Count; $i++) {
        $f = $Filas[$i]; $on = $i -eq $script:Sel
        $f.Borde.Background = $(if ($on) { $brasa } else { [Windows.Media.Brushes]::Transparent })
        $f.Borde.BorderBrush = $(if ($on) { $oro } else { $null })
        $f.Etiqueta.Foreground = $(if ($on) { $oro } else { $pergamino })
        if ($f.Opcion) {
            $f.Valor.Text = $(if ($on) { '◀  ' + (Texto-Valor $f.Opcion) + '  ▶' } else { Texto-Valor $f.Opcion })
            $f.Valor.Foreground = $(if ($on) { $oro } else { $pergamino })
            # opciones de FSR atenuadas si el FSR esta apagado
            if ($f.Opcion.k -eq 'nit' -and $Ajustes['fsr'] -eq '0' -and -not $on) { $f.Etiqueta.Foreground = $tenue; $f.Valor.Foreground = $tenue }
        }
    }
    $f = $Filas[$script:Sel]
    $ayuda.Text = $(if ($f.Opcion) { $f.Opcion.a } elseif ($f.Etiqueta.Text -eq 'JUGAR') { 'Empezar la partida con estos ajustes. La ventana del lanzador se oculta mientras juegas.' } else { 'Cerrar el lanzador.' })
}
function Cambiar([int]$d) {
    $o = $Filas[$script:Sel].Opcion
    if (-not $o) { return }
    $n = $o.v.Count; $i = 0
    for ($k = 0; $k -lt $n; $k++) { if ($o.v[$k][1] -eq $Ajustes[$o.k]) { $i = $k } }
    $Ajustes[$o.k] = $o.v[(($i + $d) % $n + $n) % $n][1]
    Guardar-Ajustes; Pintar
}

# ---- comprobaciones y lanzamiento --------------------------------------------------
$faltan = @()
if (-not (Test-Path $Exe)) { $faltan += 'gowa.exe (ejecuta gerar.cmd)' }
if (-not (Test-Path $Elf)) { $faltan += 'jogo\GOWA.elf (ejecuta gerar.cmd)' }
if (-not (Test-Path (Join-Path $Vfs 'PS3_GAME'))) { $faltan += 'vfs\PS3_GAME (el disco)' }
$estado.Text = $(if ($faltan) { 'FALTA: ' + ($faltan -join ', ') } else { 'Listo para jugar' })

$script:Juego = $null
function Activar {
    $f = $Filas[$script:Sel]
    if ($f.Etiqueta.Text -eq 'SALIR') { $win.Close(); return }
    if ($f.Etiqueta.Text -ne 'JUGAR' -or $faltan) { return }
    $e = @{
        PS3_VFS_ROOT = $Vfs
        PS3_LIBSRE_IMAGE = Join-Path $Raiz 'fw\lle\libsre_image.bin'
        PS3_SAVE_ROOT = Join-Path $Raiz 'saves'
        GOWA_RES_SCALE = $Ajustes['res']; GOWA_FSR = $Ajustes['fsr']; GOWA_FSR_SHARP = $Ajustes['nit']
        GOWA_FULLSCREEN = $Ajustes['pant']; GOWA_LANG = $Ajustes['idi']; GOWA_FLIP_HZ = $Ajustes['hz']
        GOWA_FRAMEGEN = $Ajustes['fg']; GOWA_PRESENT_SYNC = $Ajustes['vs']; GOWA_OSD = $Ajustes['osd']
        GOWA_AUDIO_MUTE = $Ajustes['son']
    }
    New-Item -ItemType Directory -Force $e.PS3_SAVE_ROOT, (Join-Path $Raiz 'out') | Out-Null
    foreach ($k in $e.Keys) { [Environment]::SetEnvironmentVariable($k, [string]$e[$k], 'Process') }
    $log = Join-Path $Raiz 'out\jogo.log'
    $script:Juego = Start-Process cmd.exe -ArgumentList "/c `"`"$Exe`" `"$Elf`" > `"$log`" 2>&1`"" `
                    -WorkingDirectory (Split-Path $Exe) -WindowStyle Hidden -PassThru
    $win.Hide()
    $script:Reloj.Start()
}
$script:Reloj = New-Object Windows.Threading.DispatcherTimer
$script:Reloj.Interval = [TimeSpan]::FromSeconds(1)
$script:Reloj.Add_Tick({
    if ($script:Juego -and $script:Juego.HasExited -and -not (Get-Process gowa -ErrorAction SilentlyContinue)) {
        $script:Reloj.Stop(); $script:Juego = $null
        $estado.Text = 'Partida terminada (registro en out\jogo.log)'
        $win.Show(); $win.Activate() | Out-Null
    }
})

# PreviewKeyDown: WPF takes the arrow keys for focus navigation before KeyDown sees them
$win.Add_PreviewKeyDown({ param($s, $e)
    $h = $true
    switch ($e.Key) {
        'Up'     { $script:Sel = ($script:Sel - 1 + $Filas.Count) % $Filas.Count; Pintar }
        'Down'   { $script:Sel = ($script:Sel + 1) % $Filas.Count; Pintar }
        'Left'   { Cambiar -1 }
        'Right'  { Cambiar 1 }
        'Return' { Activar }
        'Space'  { Activar }
        'Escape' { $win.Close() }
        default  { $h = $false }
    }
    $e.Handled = $h
})
$win.Add_MouseLeftButtonDown({ param($s, $e) if ($e.OriginalSource -is [Windows.Controls.Image] -or $e.OriginalSource -is [Windows.Shapes.Rectangle]) { $win.DragMove() } })
Pintar
if ($JugarYa) { $win.Add_ContentRendered({ $script:Sel = 0; Activar }) }
# Application.Run, not ShowDialog: hiding a modal window would end the dialog (and the
# launcher) while the game runs
$app = New-Object Windows.Application
$app.ShutdownMode = 'OnMainWindowClose'
[void]$app.Run($win)
