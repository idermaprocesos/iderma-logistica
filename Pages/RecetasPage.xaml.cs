using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class RecetasPage : Page
{
    private Doctor? _doctor;
    private ListaReceta? _lista;
    private FichaTecnica? _fichaElegida;
    private bool _silenciarDoctor;

    public RecetasPage()
    {
        InitializeComponent();
    }

    private static PersonalMedicoRepository Repo => App.Instance.PersonalMedico;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        CargarOpcionesPosologia();
        RefrescarDoctores(null);
        AplicarCambioDoctor();
    }

    private void CargarOpcionesPosologia()
    {
        Llenar(ComboDosisUnidad, "ml", "mg", "g", "tableta", "cápsula", "aplicación", "gotas", "puff", "UI");
        Llenar(ComboVia, "Oral", "Tópica (cuero cabelludo)", "Tópica", "Sublingual", "Subcutánea", "Intramuscular", "Intranasal");
        Llenar(ComboFrecuencia, "1 vez al día", "2 veces al día", "3 veces al día", "cada 8 horas", "cada 12 horas", "3 veces por semana", "si presenta dolor", "si precisa");
        Llenar(ComboHorario, "En la mañana", "En la noche", "Mañana y noche", "Con el desayuno", "Con el almuerzo", "Con la cena", "En ayunas", "Antes de dormir");
        Llenar(ComboDuracion, "5 días", "7 días", "14 días", "30 días", "60 días", "90 días", "uso continuo");
        Llenar(ComboUnidadDespacho, "frasco", "frascos", "tabletas", "cápsulas", "caja", "tubo", "ampolla", "unidad");
    }

    private static void Llenar(ComboBox combo, params string[] opciones)
    {
        combo.ItemsSource = opciones;
    }

    private static string TextoCombo(ComboBox combo)
    {
        var texto = combo.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        return combo.SelectedItem as string ?? "";
    }

    private void ComboDoctores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _doctor = ComboDoctores.SelectedItem as Doctor;
        if (_silenciarDoctor)
        {
            return;
        }

        AplicarCambioDoctor();
    }

    private void AplicarCambioDoctor()
    {
        _lista = null;
        Formulario.Visibility = Visibility.Collapsed;
        if (_doctor is null)
        {
            PanelTrabajo.Visibility = Visibility.Collapsed;
            AyudaInicio.Visibility = Visibility.Visible;
            ListaListas.ItemsSource = null;
            ActualizarVistaPrevia();
            return;
        }

        AyudaInicio.Visibility = Visibility.Collapsed;
        PanelTrabajo.Visibility = Visibility.Visible;
        CargarCamposDoctor();
        RefrescarListas();
        AbrirListaVacia();
    }

    private void CargarCamposDoctor()
    {
        if (_doctor is null)
        {
            return;
        }

        CajaNombreDoctor.Text = _doctor.Nombre;
        CajaColegiatura.Text = _doctor.Colegiatura;
        CajaRne.Text = _doctor.Rne;
        CajaEspecialidad.Text = _doctor.Especialidad;
        CajaTelefonoDoctor.Text = _doctor.Telefono;
        CajaCorreoDoctor.Text = _doctor.Correo;
        SwitchDoctorActivo.IsOn = _doctor.Activo;
    }

    private void RefrescarDoctores(Guid? seleccionar)
    {
        var lista = Repo.DoctoresOrdenados();
        _silenciarDoctor = true;
        ComboDoctores.ItemsSource = lista;
        if (seleccionar is Guid id)
        {
            ComboDoctores.SelectedItem = lista.FirstOrDefault(d => d.Id == id);
        }
        else if (lista.Count > 0)
        {
            ComboDoctores.SelectedIndex = 0;
        }
        else
        {
            ComboDoctores.SelectedItem = null;
        }

        _doctor = ComboDoctores.SelectedItem as Doctor;
        _silenciarDoctor = false;
    }

    private async void NuevoDoctor_Click(object sender, RoutedEventArgs e)
    {
        var doctor = new Doctor
        {
            Nombre = "Nuevo médico",
            Especialidad = "",
            Activo = true
        };
        Repo.GuardarDoctor(doctor);
        RefrescarDoctores(doctor.Id);
        CargarCamposDoctor();
        AvisoDoctor.Visibility = Visibility.Collapsed;
        if (DialogoDoctor.IsLoaded && DialogoDoctor.Visibility == Visibility.Visible)
        {
            return;
        }

        await AbrirFichaDoctorAsync();
    }

    private async void EditarDoctor_Click(object sender, RoutedEventArgs e)
    {
        if (_doctor is null)
        {
            await CrearPrimerDoctorAsync();
            return;
        }

        await AbrirFichaDoctorAsync();
    }

    private async Task CrearPrimerDoctorAsync()
    {
        var doctor = new Doctor
        {
            Nombre = "Nuevo médico",
            Activo = true
        };
        Repo.GuardarDoctor(doctor);
        RefrescarDoctores(doctor.Id);
        await AbrirFichaDoctorAsync();
    }

    private async Task AbrirFichaDoctorAsync()
    {
        if (_doctor is null)
        {
            return;
        }

        CargarCamposDoctor();
        AvisoDoctor.Visibility = Visibility.Collapsed;
        DialogoDoctor.XamlRoot = XamlRoot;
        await DialogoDoctor.ShowAsync();
    }

    private void DialogoDoctor_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!AplicarCamposDoctor())
        {
            args.Cancel = true;
            return;
        }

        Repo.GuardarDoctor(_doctor!);
        RefrescarDoctores(_doctor!.Id);
        ActualizarVistaPrevia();
        MostrarAviso("Médico guardado.", InfoBarSeverity.Success);
    }

    private async void BorrarDoctor_Click(object sender, RoutedEventArgs e)
    {
        if (_doctor is null)
        {
            return;
        }

        var cerrado = new TaskCompletionSource();
        void AlCerrar(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            DialogoDoctor.Closed -= AlCerrar;
            cerrado.TrySetResult();
        }

        DialogoDoctor.Closed += AlCerrar;
        DialogoDoctor.Hide();
        await cerrado.Task;
        var dialogo = new ContentDialog
        {
            Title = "Eliminar médico",
            Content = "Se borrarán también sus listas de receta.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        Repo.BorrarDoctor(_doctor.Id);
        _doctor = null;
        _lista = null;
        Formulario.Visibility = Visibility.Collapsed;
        PanelTrabajo.Visibility = Visibility.Collapsed;
        AyudaInicio.Visibility = Visibility.Visible;
        RefrescarDoctores(null);
        ActualizarVistaPrevia();
        MostrarAviso("Médico eliminado.", InfoBarSeverity.Success);
    }

    private void ListaListas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaListas.SelectedItem is ListaReceta lista)
        {
            Mostrar(lista);
        }
    }

    private void NuevaLista_Click(object sender, RoutedEventArgs e)
    {
        if (_doctor is null)
        {
            MostrarAviso("Elija un médico.", InfoBarSeverity.Warning);
            return;
        }

        ListaListas.SelectedItem = null;
        AbrirListaVacia();
    }

    private void AbrirListaVacia()
    {
        if (_doctor is null)
        {
            return;
        }

        ListaListas.SelectedItem = null;
        Mostrar(new ListaReceta
        {
            DoctorId = _doctor.Id,
            Nombre = "Nueva receta"
        });
    }

    private void Mostrar(ListaReceta lista)
    {
        _lista = lista;
        Formulario.Visibility = Visibility.Visible;
        CajaNombreLista.Text = lista.Nombre;
        CajaDiagnosticoLista.Text = lista.Diagnostico;
        CajaIndicacionesLista.Text = lista.IndicacionesGenerales;
        CajaPaciente.Text = "";
        CajaDocumento.Text = "";
        CajaDiagnosticoPaciente.Text = lista.Diagnostico;
        CajaIndicacionesPaciente.Text = "";
        RefrescarItems();
        ActualizarVistaPrevia();
    }

    private void RefrescarListas()
    {
        var id = _lista?.Id;
        ListaListas.ItemsSource = _doctor is null ? null : Repo.ListasDe(_doctor.Id);
        if (id is Guid elegido)
        {
            ListaListas.SelectedItem = Repo.BuscarLista(elegido);
        }
    }

    private void RefrescarItems()
    {
        ListaItems.ItemsSource = _lista is null ? null : Repo.VistasDe(_lista);
        ActualizarVistaPrevia();
    }

    private void CajaBuscarProducto_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var texto = sender.Text?.Trim() ?? "";
        if (texto.Length < 2)
        {
            sender.ItemsSource = null;
            return;
        }

        sender.ItemsSource = App.Instance.Repositorio.BuscarLimitado(texto, 12)
            .Select(f => $"{f.Codigo} · {f.Nombre}")
            .ToList();
    }

    private void CajaBuscarProducto_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not string texto)
        {
            return;
        }

        var codigo = texto.Split('·')[0].Trim();
        _fichaElegida = App.Instance.Repositorio.Todas
            .FirstOrDefault(f => string.Equals(f.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        if (_fichaElegida is not null)
        {
            CajaNombreMedicamento.Text = _fichaElegida.Nombre;
            ComboUnidadDespacho.Text = _fichaElegida.UnidadMedida;
            ActualizarVistaPrevia();
        }
    }

    private void CajaNombreMedicamento_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = Repo.BuscarMedicacionComun(sender.Text ?? "", 12)
                .Select(m => m.Nombre)
                .ToList();
        }

        ActualizarVistaPrevia();
    }

    private void CajaNombreMedicamento_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not string nombre)
        {
            return;
        }

        var comun = Repo.BuscarMedicacionPorNombre(nombre);
        if (comun is not null)
        {
            AplicarMedicacionComun(comun);
        }
    }

    private void AplicarMedicacionComun(MedicacionComun comun)
    {
        CajaNombreMedicamento.Text = comun.Nombre;
        CajaDosis.Text = comun.Dosis;
        ComboDosisUnidad.Text = comun.DosisUnidad;
        ComboVia.Text = comun.ViaAdministracion;
        ComboFrecuencia.Text = comun.Frecuencia;
        ComboHorario.Text = comun.Horario;
        ComboDuracion.Text = comun.Duracion;
        CajaCantidad.Text = comun.Cantidad;
        ComboUnidadDespacho.Text = comun.Unidad;
        CajaIndicacionItem.Text = comun.Indicaciones;
        ActualizarVistaPrevia();
    }

    private void AgregarItem_Click(object sender, RoutedEventArgs e)
    {
        if (_lista is null || !AplicarCamposLista())
        {
            return;
        }
        var nombre = CajaNombreMedicamento.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MostrarAviso("Escriba el medicamento o elíjalo del catálogo.", InfoBarSeverity.Error);
            return;
        }

        _lista.Items.Add(new ItemListaReceta
        {
            FichaId = _fichaElegida?.Id,
            Codigo = _fichaElegida?.Codigo ?? "",
            Nombre = nombre,
            Dosis = CajaDosis.Text.Trim(),
            DosisUnidad = TextoCombo(ComboDosisUnidad),
            ViaAdministracion = TextoCombo(ComboVia),
            Frecuencia = TextoCombo(ComboFrecuencia),
            Horario = TextoCombo(ComboHorario),
            Duracion = TextoCombo(ComboDuracion),
            Cantidad = CajaCantidad.Text.Trim(),
            Unidad = TextoCombo(ComboUnidadDespacho),
            Indicaciones = CajaIndicacionItem.Text.Trim()
        });

        _fichaElegida = null;
        CajaBuscarProducto.Text = "";
        CajaNombreMedicamento.Text = "";
        CajaDosis.Text = "";
        ComboDosisUnidad.Text = "";
        ComboVia.Text = "";
        ComboFrecuencia.Text = "";
        ComboHorario.Text = "";
        ComboDuracion.Text = "";
        CajaCantidad.Text = "";
        ComboUnidadDespacho.Text = "";
        CajaIndicacionItem.Text = "";
        GuardarListaSilencioso();
        RefrescarItems();
    }

    private void QuitarItem_Click(object sender, RoutedEventArgs e)
    {
        if (_lista is null || sender is not Button boton)
        {
            return;
        }

        Guid? id = boton.Tag switch
        {
            Guid guid => guid,
            string texto when Guid.TryParse(texto, out var parsed) => parsed,
            _ => null
        };
        if (id is null)
        {
            return;
        }

        _lista.Items.RemoveAll(i => i.Id == id);
        GuardarListaSilencioso();
        RefrescarItems();
    }

    private void GuardarLista_Click(object sender, RoutedEventArgs e)
    {
        if (!AplicarCamposLista())
        {
            return;
        }

        Repo.GuardarLista(_lista!);
        MostrarAviso("Lista guardada.", InfoBarSeverity.Success);
        RefrescarListas();
    }

    private async void BorrarLista_Click(object sender, RoutedEventArgs e)
    {
        if (_lista is null || Repo.BuscarLista(_lista.Id) is null)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Eliminar lista",
            Content = "Se borrará esta lista de receta del médico.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        Repo.BorrarLista(_lista.Id);
        _lista = null;
        Formulario.Visibility = Visibility.Collapsed;
        RefrescarListas();
        ActualizarVistaPrevia();
    }

    private async void ExportarPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_doctor is null || !AplicarCamposDoctor() || !AplicarCamposLista())
        {
            return;
        }

        if (_lista!.Items.Count == 0)
        {
            MostrarAviso("Agregue al menos un medicamento.", InfoBarSeverity.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(CajaPaciente.Text))
        {
            MostrarAviso("El nombre del paciente es obligatorio para el PDF.", InfoBarSeverity.Error);
            return;
        }

        Repo.GuardarDoctor(_doctor);
        Repo.GuardarLista(_lista);
        var receta = Repo.RegistrarEmision(_doctor, _lista, new RecetaEmitida
        {
            PacienteNombre = CajaPaciente.Text.Trim(),
            PacienteDocumento = CajaDocumento.Text.Trim(),
            Diagnostico = CajaDiagnosticoPaciente.Text.Trim(),
            Indicaciones = CajaIndicacionesPaciente.Text.Trim()
        });

        var ratios = Repo.RatiosDe(_doctor.Id);
        var smartHealth = SmartHealthLinkService.Crear(_doctor, _lista, receta);
        receta.SmartHealthLink = "application/fhir+json";
        receta.SmartHealthCard = smartHealth.FichaSmartHealthCard;
        App.Instance.PersonalMedico.Guardar();
        try
        {
            var resultado = await ExcelUi.GuardarPdfAsync(
                $"receta-{Sanitizar(receta.Numero)}",
                ruta => PdfRecetaService.Exportar(ruta, _doctor, _lista, receta, ratios));
            if (resultado is null)
            {
                return;
            }

            MostrarAviso($"Se exportó {receta.Numero} en HL7 FHIR R4 (PDF y .fhir.json).", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
            RefrescarListas();
        }
        catch (Exception ex)
        {
            MostrarAviso($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private bool AplicarCamposDoctor()
    {
        if (_doctor is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CajaNombreDoctor.Text))
        {
            AvisoDoctor.Text = "El nombre del médico es obligatorio.";
            AvisoDoctor.Visibility = Visibility.Visible;
            MostrarAviso("El nombre del médico es obligatorio.", InfoBarSeverity.Error);
            return false;
        }

        AvisoDoctor.Visibility = Visibility.Collapsed;

        _doctor.Nombre = CajaNombreDoctor.Text.Trim();
        _doctor.Colegiatura = CajaColegiatura.Text.Trim();
        _doctor.Rne = CajaRne.Text.Trim();
        _doctor.Especialidad = CajaEspecialidad.Text.Trim();
        _doctor.Telefono = CajaTelefonoDoctor.Text.Trim();
        _doctor.Correo = CajaCorreoDoctor.Text.Trim();
        _doctor.Activo = SwitchDoctorActivo.IsOn;
        return true;
    }

    private bool AplicarCamposLista()
    {
        if (_doctor is null || _lista is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CajaNombreLista.Text))
        {
            MostrarAviso("El nombre de la lista es obligatorio.", InfoBarSeverity.Error);
            return false;
        }

        _lista.DoctorId = _doctor.Id;
        _lista.Nombre = CajaNombreLista.Text.Trim();
        _lista.Diagnostico = CajaDiagnosticoLista.Text.Trim();
        _lista.IndicacionesGenerales = CajaIndicacionesLista.Text.Trim();
        return true;
    }

    private void GuardarListaSilencioso()
    {
        if (_lista is null || string.IsNullOrWhiteSpace(_lista.Nombre))
        {
            return;
        }

        if (Repo.BuscarLista(_lista.Id) is not null || _lista.Items.Count > 0)
        {
            Repo.GuardarLista(_lista);
        }
    }

    private void MostrarAviso(string mensaje, InfoBarSeverity nivel)
    {
        Barra.Severity = nivel;
        Barra.Message = mensaje;
        Barra.IsOpen = true;
    }

    private void Campo_TextChanged(object sender, TextChangedEventArgs e) => ActualizarVistaPrevia();

    private void Campo_LostFocus(object sender, RoutedEventArgs e) => ActualizarVistaPrevia();

    private void Campo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ActualizarVistaPrevia();

    private void Campo_Toggled(object sender, RoutedEventArgs e) => ActualizarVistaPrevia();

    private void ActualizarVistaPrevia()
    {
        if (PreviewMarca is null)
        {
            return;
        }

        PreviewMarca.Text = IdermaMarca.NombreMayusculas;
        var medico = Texto(CajaNombreDoctor);
        if (string.IsNullOrWhiteSpace(medico))
        {
            medico = _doctor?.Nombre ?? "";
        }

        PreviewMedico.Text = string.IsNullOrWhiteSpace(medico) ? "Médico sin nombre" : medico;

        var cmp = PrimeroConTexto(CajaColegiatura);
        if (string.IsNullOrWhiteSpace(cmp))
        {
            cmp = _doctor?.Colegiatura ?? "";
        }

        var rne = Texto(CajaRne);
        if (string.IsNullOrWhiteSpace(rne))
        {
            rne = _doctor?.Rne ?? "";
        }

        var especialidad = Texto(CajaEspecialidad);
        if (string.IsNullOrWhiteSpace(especialidad))
        {
            especialidad = _doctor?.Especialidad ?? "";
        }
        PreviewMedicoDetalle.Text = string.Join("  ·  ", new[]
        {
            string.IsNullOrWhiteSpace(cmp) ? "" : "CMP " + cmp,
            rne,
            especialidad
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var paciente = Texto(CajaPaciente);
        PreviewPaciente.Text = string.IsNullOrWhiteSpace(paciente) ? "Paciente (aún no indicado)" : paciente;
        var documento = Texto(CajaDocumento);
        var diagnostico = PrimeroConTexto(CajaDiagnosticoPaciente, CajaDiagnosticoLista);
        PreviewPacienteDetalle.Text = string.Join("  ·  ", new[]
        {
            string.IsNullOrWhiteSpace(documento) ? "" : "Doc. " + documento,
            string.IsNullOrWhiteSpace(diagnostico) ? "" : diagnostico
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var listaNombre = Texto(CajaNombreLista);
        PreviewFolio.Text = string.IsNullOrWhiteSpace(listaNombre)
            ? DateTime.Now.ToString("dd/MM/yyyy")
            : listaNombre + "  ·  " + DateTime.Now.ToString("dd/MM/yyyy");

        var lineas = _lista is null
            ? new List<ItemRecetaVista>()
            : Repo.VistasDe(_lista).ToList();
        var borrador = BorradorMedicamento();
        if (borrador is not null)
        {
            lineas.Add(borrador);
        }

        ListaPreview.ItemsSource = lineas;
        PreviewVacioMeds.Visibility = lineas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var indicaciones = PrimeroConTexto(CajaIndicacionesPaciente, CajaIndicacionesLista);
        PreviewIndicaciones.Text = indicaciones;
        PreviewTituloIndicaciones.Visibility = string.IsNullOrWhiteSpace(indicaciones)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private ItemRecetaVista? BorradorMedicamento()
    {
        var nombre = CajaNombreMedicamento.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(nombre) || _lista is null)
        {
            return null;
        }

        var item = new ItemListaReceta
        {
            Nombre = nombre,
            Dosis = Texto(CajaDosis),
            DosisUnidad = TextoCombo(ComboDosisUnidad),
            ViaAdministracion = TextoCombo(ComboVia),
            Frecuencia = TextoCombo(ComboFrecuencia),
            Horario = TextoCombo(ComboHorario),
            Duracion = TextoCombo(ComboDuracion),
            Cantidad = Texto(CajaCantidad),
            Unidad = TextoCombo(ComboUnidadDespacho),
            Indicaciones = Texto(CajaIndicacionItem)
        };
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.PosologiaTexto))
        {
            partes.Add(item.PosologiaTexto);
        }

        if (!string.IsNullOrWhiteSpace(item.DespachoTexto))
        {
            partes.Add("Despachar: " + item.DespachoTexto);
        }

        if (!string.IsNullOrWhiteSpace(item.Indicaciones))
        {
            partes.Add("Uso: " + item.Indicaciones);
        }

        return new ItemRecetaVista
        {
            Nombre = nombre + "  (escribiendo…)",
            Detalle = string.Join("  ·  ", partes)
        };
    }

    private static string Texto(TextBox? caja) => caja?.Text?.Trim() ?? "";

    private static string PrimeroConTexto(params TextBox[] cajas)
    {
        foreach (var caja in cajas)
        {
            var texto = Texto(caja);
            if (!string.IsNullOrWhiteSpace(texto))
            {
                return texto;
            }
        }

        return "";
    }

    private static string Sanitizar(string texto)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            texto = texto.Replace(c, '-');
        }

        return texto;
    }
}
