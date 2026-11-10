using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Worknotch.App.Servicios;

namespace Worknotch.App;

/// <summary>
/// La prueba de humo de la interfaz (la usa la CI en Windows): con datos de ejemplo y la hora fija,
/// pinta cada vista en Noche y en Día, la guarda como PNG y comprueba sobre lo pintado los criterios
/// de docs/design-system/README.md que se pueden comprobar solos. Sale con 0 si todo pasa; lo que
/// falla queda en humo.log.
/// </summary>
internal static class Smoke
{
    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime Now { get; set; } = now;
    }

    public static int Run(string outDir)
    {
        var log = new List<string>();
        int failures = 0;
        try
        {
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);
            string dataFolder = Path.Combine(outDir, "datos");
            if (Directory.Exists(dataFolder)) Directory.Delete(dataFolder, recursive: true);
            Environment.SetEnvironmentVariable("WORKNOTCH_DATOS", dataFolder);

            var clock = new FixedClock(new DateTime(2026, 11, 11, 10, 0, 0)); // miércoles
            var store = Store.Default();
            string detailId = Seed(store, clock.Now);

            foreach (var (theme, name) in new[] { (ThemeChoice.Night, "noche"), (ThemeChoice.Day, "dia") })
            {
                Theme.Apply(theme);
                var allowed = Theme.TextColors();
                var window = new NotchWindow(store, clock) { Instant = true };
                window.Show();

                void Shot(string view)
                {
                    Flush(window);
                    Save(window.ShellElement, Path.Combine(outDir, $"{name}-{view}.png"));
                    foreach (string problem in Criteria(window.ShellElement, allowed))
                    {
                        log.Add($"FALLA {name}/{view}: {problem}");
                        failures++;
                    }
                    log.Add($"ok {name}/{view}: {window.ShellElement.ActualWidth:0} × {window.ShellElement.ActualHeight:0}");
                }

                Shot("1-pastilla");
                window.Open(pinned: true);
                Shot("2-panel");
                window.OpenTask(detailId);
                Shot("3-detalle");
                window.OpenTask(null);
                window.ShowWeek(true);
                Shot("3b-semana");
                window.ShowWeek(false);
                window.OpenSettings();
                Shot("4-ajustes");
                window.OpenSettings(open: false);

                // Todas las tarjetas a la vez, con el temporizador en marcha
                store.Mutate(d =>
                {
                    d.Settings.Cards = ["tareas", "foco", "notas", "recortes", "progreso"];
                    FocusTimer.Start(d, detailId, null, TimerMode.Countdown, clock.Now.AddMinutes(-7).AddSeconds(-13));
                });
                window.Refresh();
                Shot("5-todas-las-tarjetas");

                window.ShowAlert(new AlertInfo("Llamar a Marta", "Aviso de las 10:00.",
                [
                    new AlertAction("Empezar foco", () => { }, true),
                    new AlertAction("Avisar en 5 min", () => { }),
                    new AlertAction("Hecha", () => { }),
                    new AlertAction("Cerrar", () => { }),
                ]));
                Shot("6-aviso");
                window.DismissAlert();
                Shot("7-pastilla-con-foco");

                store.Mutate(d =>
                {
                    FocusTimer.Stop(d, clock.Now.AddMinutes(-7));
                    d.Sessions.RemoveAt(d.Sessions.Count - 1);
                    d.Tasks.First(t => t.Id == detailId).FocusSeconds = 0;
                    d.Settings.Cards = new Settings().Cards;
                });
                window.Close();
            }
        }
        catch (Exception e)
        {
            log.Add("FALLA: " + e);
            failures++;
        }

        log.Add(failures == 0 ? "Todo bien." : $"{failures} fallos.");
        try { File.WriteAllLines(Path.Combine(outDir, "humo.log"), log); }
        catch (IOException) { }
        return failures == 0 ? 0 : 1;
    }

    // Un día creíble: tareas con todo tipo de datos, bandeja, días siguientes, foco de la semana, notas y recortes.
    private static string Seed(Store store, DateTime now) => store.Mutate(d =>
    {
        var today = DateOnly.FromDateTime(now);
        d.Settings.WelcomeSeen = true;
        d.LastDay = today;
        var informe = Planner.AddFromLine(d, "Terminar el informe de prácticas !alta #Universidad 50m", today, now);
        informe.Steps.AddRange([new Step { Title = "Repasar la introducción", Done = true }, new Step { Title = "Añadir las gráficas" }, new Step { Title = "Enviarlo al tutor" }]);
        informe.Notes = "El tutor lo quiere en PDF, con las gráficas en color.";
        informe.Deadline = today.AddDays(2);
        Planner.AddFromLine(d, "Llamar a Marta 17:30 #Casa", today, now);
        var compra = Planner.AddFromLine(d, "Hacer la compra de la semana !media", today, now);
        compra.CarriedFrom = today.AddDays(-1);
        Planner.AddFromLine(d, "Regar las plantas cada semana", today, now);
        var gimnasio = Planner.AddFromLine(d, "Gimnasio", today, now);
        Planner.Complete(d, gimnasio.Id, now.AddHours(-2));
        var correo = Planner.AddFromLine(d, "Contestar el correo de la beca", today, now);
        Planner.Complete(d, correo.Id, now.AddMinutes(-40));
        Planner.AddFromLine(d, "Idea: una revisión semanal sin día", today, now);
        Planner.AddFromLine(d, "Renovar el pasaporte", null, now);
        Planner.AddFromLine(d, "Preparar las diapositivas mañana 9:00", today, now);

        for (int ago = 1; ago <= 6; ago++)
        {
            for (int i = 0; i < (ago * 7 % 5); i++)
                d.Tasks.Add(new TaskItem { Id = Planner.NewId(d), Title = "Tarea de otro día", Day = today.AddDays(-ago), Done = true, CompletedAt = now.AddDays(-ago), Created = now.AddDays(-ago) });
            if (ago != 4) d.Sessions.Add(new FocusSession { Start = now.AddDays(-ago), Seconds = 60 * (20 + ago * 13 % 50) });
        }
        d.Sessions.Add(new FocusSession { Start = now.AddHours(-1), Seconds = 25 * 60 });

        d.Notepad = "Ideas para el trabajo de fin de grado\n- Comparar dos formas de planificar el día\n- Preguntar a Lucía por su plantilla\n\nComprar: pilas, café";
        d.Clips.Add(new Clip { Id = Planner.NewId(d), Kind = ClipKind.Link, Content = "https://es.wikipedia.org/wiki/Técnica_Pomodoro", Added = now.AddMinutes(-5) });
        d.Clips.Add(new Clip { Id = Planner.NewId(d), Kind = ClipKind.Text, Content = "Reunión del grupo: jueves a las 12:00 en el aula 2.3. Llevar el portátil y el borrador impreso.", Added = now.AddMinutes(-50) });
        d.Clips.Add(new Clip { Id = Planner.NewId(d), Kind = ClipKind.File, Content = @"C:\Users\ejemplo\Documents\Informe de prácticas.pdf", Added = now.AddDays(-1) });
        return informe.Id;
    });

    private static void Flush(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static void Save(FrameworkElement element, string path)
    {
        const double scale = 2;
        double w = element.ActualWidth, h = element.ActualHeight;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None }, null, new Rect(0, 0, w, h));
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>Los criterios de la guía de diseño que se pueden comprobar sobre lo pintado.</summary>
    private static List<string> Criteria(FrameworkElement root, HashSet<(byte, byte, byte)> allowed)
    {
        var problems = new List<string>();
        var rootBounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
        foreach (var element in Descendants(root))
        {
            if (element is not FrameworkElement { IsVisible: true } fe) continue;
            switch (fe)
            {
                case TextBlock t when t.Text.Length > 0:
                    if (t.FontSize < 12) problems.Add($"«{Short(t.Text)}» va a {t.FontSize} px: nada visible baja de 12");
                    if (t.Foreground is SolidColorBrush b && !allowed.Contains((b.Color.R, b.Color.G, b.Color.B)))
                        problems.Add($"«{Short(t.Text)}» usa el color {b.Color}, que no es un token de este tema");
                    if (!t.TextWrapping.Equals(TextWrapping.NoWrap) && t.TextTrimming == TextTrimming.None)
                    {
                        // un texto que ni se corta con puntos suspensivos ni tiene barra debe caber entero
                        var box = t.TransformToAncestor(root).TransformBounds(new Rect(0, 0, t.ActualWidth, t.ActualHeight));
                        if (!InScroll(t) && (box.Right > rootBounds.Right + 0.5 || box.Bottom > rootBounds.Bottom + 0.5))
                            problems.Add($"«{Short(t.Text)}» se sale del notch");
                    }
                    break;
                case TextBox box when box.FontSize < 12:
                    problems.Add($"El campo «{box.Tag}» va a {box.FontSize} px: nada visible baja de 12");
                    break;
                case ButtonBase button when button is not RepeatButton: // las de las barras de desplazamiento no cuentan
                    if (!HasName(button)) problems.Add($"Un botón ({button.Name}) no tiene texto ni nombre para lectores de pantalla");
                    if (button.ActualWidth < 20 || button.ActualHeight < 20) problems.Add($"El botón «{NameOf(button)}» mide {button.ActualWidth:0} × {button.ActualHeight:0}: demasiado pequeño para pulsarlo");
                    break;
            }
        }
        return problems;
    }

    private static bool InScroll(DependencyObject element)
    {
        for (var p = VisualTreeHelper.GetParent(element); p != null; p = VisualTreeHelper.GetParent(p))
            if (p is ScrollViewer) return true;
        return false;
    }

    private static bool HasName(ButtonBase button) => NameOf(button).Length > 0;

    private static string NameOf(ButtonBase button)
    {
        string name = AutomationProperties.GetName(button);
        if (!string.IsNullOrWhiteSpace(name)) return name;
        if (button.Content is string s && s.Trim().Length > 0) return s;
        return Descendants(button).OfType<TextBlock>().FirstOrDefault(t => t.Text.Trim().Length > 0)?.Text ?? "";
    }

    private static string Short(string s) => s.Length > 40 ? s[..40] + "…" : s;

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is UIElement { IsVisible: false }) continue;
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }
}
