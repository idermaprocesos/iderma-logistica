using System.Collections.ObjectModel;
using System.Globalization;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class MermaEnvioPage : Page
{
    private readonly ObservableCollection<LineaMermaEnvio> _lineas = [];

    public MermaEnvioPage()
    {
        InitializeComponent();
        Lista.ItemsSource = _lineas;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _lineas.Clear();
        if (e.Parameter is not IEnumerable<Guid> ids)
        {
            return;
        }

        foreach (var id in ids.Distinct())
        {
            var ficha = App.Instance.Repositorio.Obtener(id);
            if (ficha is null || ficha.Existencia <= 0.0001)
            {
                continue;
            }

            var proximo = PartidasInventario.PreferidaParaSalida(ficha);
            var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "und" : ficha.UnidadMedida.Trim();
            _lineas.Add(new LineaMermaEnvio
            {
                FichaId = ficha.Id,
                Codigo = ficha.Codigo,
                Nombre = ficha.Nombre,
                Unidad = unidad,
                Lote = proximo?.LoteTexto ?? "Sin lote",
                Caducidad = proximo?.FechaCaducidadTexto ?? "Sin caducidad",
                Stock = ficha.Existencia,
                Cantidad = proximo is null ? ficha.Existencia : Math.Min(proximo.Cantidad, ficha.Existencia),
                Motivo = MotivosMerma.Sugerir(proximo)
            });
        }
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void Enviar_Click(object sender, RoutedEventArgs e)
    {
        var elegidas = _lineas.Where(l => l.Cantidad > 0.0001).ToList();
        if (elegidas.Count == 0)
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Sin cantidad";
            Aviso.Message = "Indique una cantidad mayor a cero.";
            Aviso.IsOpen = true;
            return;
        }

        var registros = new List<MermaRegistrada>();
        var movimientos = new List<MovimientoInventario>();
        var fichas = new List<FichaTecnica>();
        var ahora = DateTimeOffset.Now;
        foreach (var linea in elegidas)
        {
            var original = App.Instance.Repositorio.Obtener(linea.FichaId);
            if (original is null)
            {
                continue;
            }

            var ficha = original.Clonar();
            if (!PartidasInventario.IntentarSalidaFefo(
                    ficha,
                    linea.Cantidad,
                    null,
                    null,
                    null,
                    automatico: true,
                    out var consumos,
                    out var error)
                || consumos.Count == 0)
            {
                Aviso.Severity = InfoBarSeverity.Warning;
                Aviso.Title = linea.Nombre;
                Aviso.Message = error ?? "No hay existencia suficiente.";
                Aviso.IsOpen = true;
                return;
            }

            var detalle = linea.Detalle.Trim();
            var nota = detalle.Length == 0 ? linea.Motivo : $"{linea.Motivo}. {detalle}";
            var resultante = ficha.Existencia + consumos.Sum(c => c.Cantidad);
            foreach (var consumo in consumos)
            {
                resultante -= consumo.Cantidad;
                movimientos.Add(new MovimientoInventario
                {
                    FichaId = ficha.Id,
                    PartidaId = consumo.Partida.Id,
                    Lote = consumo.Partida.Lote,
                    FechaCaducidad = consumo.Partida.FechaCaducidad,
                    Area = ficha.Area,
                    Fecha = ahora,
                    Cantidad = consumo.Cantidad,
                    ExistenciaResultante = resultante,
                    Tipo = "Salida",
                    Origen = "Merma",
                    TipoDocumento = "Merma",
                    NumeroDocumento = ahora.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture),
                    Nota = nota
                });
                registros.Add(new MermaRegistrada
                {
                    Fecha = ahora,
                    FichaId = ficha.Id,
                    Codigo = ficha.Codigo,
                    Nombre = ficha.Nombre,
                    Lote = consumo.Partida.Lote,
                    FechaCaducidad = consumo.Partida.FechaCaducidad,
                    Cantidad = consumo.Cantidad,
                    Unidad = linea.Unidad,
                    Motivo = linea.Motivo,
                    Detalle = detalle
                });
            }

            fichas.Add(ficha);
        }

        foreach (var ficha in fichas)
        {
            App.Instance.Repositorio.GuardarFicha(ficha, registrarIngresoAutomatico: false);
        }

        if (movimientos.Count == 0)
        {
            return;
        }

        App.Instance.Registro.Agregar(movimientos);
        App.Instance.Mermas.Agregar(registros);
        Frame.Navigate(typeof(MermaPage));
    }
}
