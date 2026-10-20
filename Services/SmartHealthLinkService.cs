using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed record SmartHealthLinkCreado(
    string Shlink,
    string UriConVisor,
    string Shc,
    byte[] QrPng,
    string FichaSmartHealthCard,
    string PaginaHtml);

public static class SmartHealthLinkService
{
    private static readonly JsonSerializerOptions JsonMin = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static SmartHealthLinkCreado Crear(Doctor doctor, ListaReceta lista, RecetaEmitida receta)
    {
        var bundle = CrearDocumentoFhir(doctor, lista, receta);
        var fhirJson = JsonSerializer.Serialize(bundle, JsonMin);
        return new SmartHealthLinkCreado(
            "application/fhir+json",
            "https://hl7.org/fhir/R4/bundle.html",
            fhirJson,
            [],
            fhirJson,
            "");
    }

    private static string CrearHtmlReceta(Doctor doctor, ListaReceta lista, RecetaEmitida receta, string fhirJson)
    {
        var diagnostico = string.IsNullOrWhiteSpace(receta.Diagnostico) ? lista.Diagnostico : receta.Diagnostico;
        var indicaciones = string.IsNullOrWhiteSpace(receta.Indicaciones)
            ? lista.IndicacionesGenerales
            : receta.Indicaciones;
        var filas = string.Join("", lista.Items.Select(i =>
            $"<tr><td>{Esc(i.Nombre)}</td><td>{Esc(string.Join(" · ", new[] { i.Dosis, i.Frecuencia, i.Duracion }.Where(s => !string.IsNullOrWhiteSpace(s))))}</td></tr>"));
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(fhirJson));
        var dataUri = "data:application/fhir+json;charset=utf-8;base64," + b64;
        var intent = "intent://import#Intent;scheme=content;type=application/fhir+json;" +
                     "S.browser_fallback_url=https://play.google.com/store/apps/details?id=org.hl7.fhir.validator;end";
        return
            "<!doctype html><html lang=es><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\">" +
            "<title>Receta HL7 FHIR · Iderma</title>" +
            "<style>body{font-family:sans-serif;margin:16px;color:#1A305A;max-width:42em}a,button{display:block;margin:10px 0;padding:12px;background:#1A305A;color:#fff;text-align:center;text-decoration:none;border:0;border-radius:8px;font-size:16px}h2{color:#C5A059;font-size:1rem}pre{white-space:pre-wrap;font-size:11px;background:#f4efe4;padding:8px}</style>" +
            "<p><b>IDERMA CAPILAR</b> · HL7 FHIR R4</p>" +
            $"<h1>Receta {Esc(receta.Numero)}</h1>" +
            "<p>Documento clínico FHIR (<code>Bundle</code> tipo <code>document</code> con <code>MedicationRequest</code>).</p>" +
            $"<p><a id=dl href=\"{dataUri}\" download=\"{Esc(receta.Numero)}.fhir.json\">Descargar receta FHIR (.json)</a></p>" +
            "<p><a href=\"x-apple-health://\">Abrir Salud (iPhone)</a></p>" +
            $"<p><a href=\"{Esc(intent)}\">Enviar a app FHIR (Android)</a></p>" +
            $"<h2>Médico</h2><p>{Esc(doctor.Nombre)} · CMP {Esc(doctor.Colegiatura)}</p>" +
            $"<h2>Paciente</h2><p>{Esc(receta.PacienteNombre)}</p>" +
            $"<p><b>Diagnóstico:</b> {Esc(diagnostico)}</p>" +
            $"<h2>Medicamentos</h2><table>{filas}</table>" +
            $"<p>{Esc(indicaciones)}</p>" +
            "<h2>JSON HL7 FHIR R4</h2><pre id=fhir></pre>" +
            "<script>(function(){var j=atob(\"" + b64 + "\");document.getElementById(\"fhir\").textContent=j;" +
            "var a=document.getElementById(\"dl\");" +
            "try{var b=new Blob([j],{type:\"application/fhir+json\"});a.href=URL.createObjectURL(b);}catch(e){}" +
            "if(navigator.share){navigator.share({title:\"Receta FHIR\",files:[new File([j],\"receta.fhir.json\",{type:\"application/fhir+json\"})]}).catch(function(){});}" +
            "})();</script></html>";
    }

    private static string Esc(string? texto) =>
        System.Net.WebUtility.HtmlEncode(texto ?? "");

    private static JsonObject CrearDocumentoFhir(Doctor doctor, ListaReceta lista, RecetaEmitida receta)
    {
        var pacienteRef = "urn:uuid:paciente";
        var medicoRef = "urn:uuid:medico";
        var clinicaRef = "urn:uuid:iderma";
        var diagnosticoRef = "urn:uuid:diagnostico";
        var rxRefs = new JsonArray();
        var entradasRx = new List<JsonObject>();
        var i = 1;
        foreach (var item in lista.Items)
        {
            var full = $"urn:uuid:rx-{i}";
            rxRefs.Add(new JsonObject { ["reference"] = full, ["display"] = item.Nombre });
            entradasRx.Add(Entrada(full, Medicacion(item, receta, pacienteRef, medicoRef, i)));
            i++;
        }

        var composition = new JsonObject
        {
            ["resourceType"] = "Composition",
            ["id"] = "composition",
            ["status"] = "final",
            ["type"] = Codeable("57833-6", "Prescription for medication", "http://loinc.org"),
            ["subject"] = new JsonObject { ["reference"] = pacienteRef, ["display"] = receta.PacienteNombre },
            ["date"] = receta.Fecha.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["author"] = new JsonArray(new JsonObject { ["reference"] = medicoRef, ["display"] = doctor.Nombre }),
            ["title"] = $"Receta médica {receta.Numero} · {IdermaMarca.Nombre}",
            ["custodian"] = new JsonObject { ["reference"] = clinicaRef, ["display"] = IdermaMarca.Nombre },
            ["section"] = new JsonArray(
                new JsonObject
                {
                    ["title"] = "Medicamentos recetados",
                    ["code"] = Codeable("57828-6", "Prescription list", "http://loinc.org"),
                    ["entry"] = rxRefs
                },
                new JsonObject
                {
                    ["title"] = "Diagnóstico",
                    ["code"] = Codeable("29548-5", "Diagnosis", "http://loinc.org"),
                    ["entry"] = new JsonArray(new JsonObject { ["reference"] = diagnosticoRef })
                })
        };

        var entradas = new JsonArray
        {
            Entrada("urn:uuid:composition", composition),
            Entrada(pacienteRef, Paciente(receta)),
            Entrada(medicoRef, Medico(doctor)),
            Entrada(clinicaRef, Clinica()),
            Entrada(diagnosticoRef, Diagnostico(receta, lista, pacienteRef))
        };
        foreach (var rx in entradasRx)
        {
            entradas.Add(rx);
        }

        return new JsonObject
        {
            ["resourceType"] = "Bundle",
            ["id"] = receta.Id.ToString(),
            ["meta"] = new JsonObject
            {
                ["profile"] = new JsonArray(JsonValue.Create("http://hl7.org/fhir/StructureDefinition/Bundle")),
                ["tag"] = new JsonArray(new JsonObject
                {
                    ["system"] = "https://iderma.pe/fhir",
                    ["code"] = "receta",
                    ["display"] = "Receta Iderma Capilar"
                })
            },
            ["identifier"] = new JsonObject
            {
                ["system"] = "https://iderma.pe/receta",
                ["value"] = receta.Numero
            },
            ["type"] = "document",
            ["timestamp"] = receta.Fecha.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["entry"] = entradas
        };
    }

    private static JsonObject Clinica() => new()
    {
        ["resourceType"] = "Organization",
        ["id"] = "iderma",
        ["name"] = IdermaMarca.Nombre,
        ["alias"] = new JsonArray(JsonValue.Create(IdermaMarca.NombreMayusculas)),
        ["type"] = new JsonArray(Codeable("prov", "Healthcare Provider", "http://terminology.hl7.org/CodeSystem/organization-type")),
        ["telecom"] = new JsonArray(new JsonObject { ["system"] = "url", ["value"] = "https://iderma.pe" })
    };

    private static JsonObject Medico(Doctor doctor) => new()
    {
        ["resourceType"] = "Practitioner",
        ["id"] = "medico",
        ["identifier"] = new JsonArray(
            Identificador("https://www.cmp.org.pe", doctor.Colegiatura, "CMP"),
            Identificador("https://www.cmp.org.pe/rne", doctor.Rne, "RNE")),
        ["name"] = new JsonArray(new JsonObject
        {
            ["text"] = doctor.Nombre,
            ["family"] = doctor.Nombre
        }),
        ["qualification"] = new JsonArray(new JsonObject
        {
            ["code"] = new JsonObject { ["text"] = doctor.Especialidad }
        })
    };

    private static JsonObject Paciente(RecetaEmitida receta)
    {
        return new JsonObject
        {
            ["resourceType"] = "Patient",
            ["id"] = "paciente",
            ["identifier"] = new JsonArray(
                Identificador("https://www.gob.pe/reniec", receta.PacienteDocumento, "DNI")),
            ["name"] = new JsonArray(new JsonObject { ["text"] = receta.PacienteNombre })
        };
    }

    private static JsonObject Diagnostico(RecetaEmitida receta, ListaReceta lista, string pacienteRef)
    {
        var texto = string.IsNullOrWhiteSpace(receta.Diagnostico) ? lista.Diagnostico : receta.Diagnostico;
        return new JsonObject
        {
            ["resourceType"] = "Condition",
            ["id"] = "diagnostico",
            ["clinicalStatus"] = Codeable("active", "Active", "http://terminology.hl7.org/CodeSystem/condition-clinical"),
            ["code"] = new JsonObject { ["text"] = string.IsNullOrWhiteSpace(texto) ? lista.Nombre : texto },
            ["subject"] = new JsonObject { ["reference"] = pacienteRef }
        };
    }

    private static JsonObject Medicacion(
        ItemListaReceta item,
        RecetaEmitida receta,
        string pacienteRef,
        string medicoRef,
        int indice)
    {
        var recurso = new JsonObject
        {
            ["resourceType"] = "MedicationRequest",
            ["id"] = $"rx-{indice}",
            ["status"] = "active",
            ["intent"] = "order",
            ["category"] = new JsonArray(Codeable("outpatient", "Outpatient", "http://terminology.hl7.org/CodeSystem/medicationrequest-category")),
            ["authoredOn"] = receta.Fecha.ToUniversalTime().ToString("yyyy-MM-dd"),
            ["subject"] = new JsonObject { ["reference"] = pacienteRef, ["display"] = receta.PacienteNombre },
            ["requester"] = new JsonObject { ["reference"] = medicoRef },
            ["medicationCodeableConcept"] = ConceptoMedicamento(item),
            ["dosageInstruction"] = new JsonArray(new JsonObject
            {
                ["text"] = string.IsNullOrWhiteSpace(item.PosologiaTexto) ? item.Nombre : item.PosologiaTexto,
                ["patientInstruction"] = string.Join(". ", new[] { item.Indicaciones, receta.Indicaciones }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                ["route"] = string.IsNullOrWhiteSpace(item.ViaAdministracion)
                    ? null
                    : new JsonObject { ["text"] = item.ViaAdministracion },
                ["timing"] = new JsonObject
                {
                    ["code"] = new JsonObject
                    {
                        ["text"] = string.Join(" · ", new[] { item.Frecuencia, item.Horario, item.Duracion }
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                    }
                },
                ["doseAndRate"] = string.IsNullOrWhiteSpace(item.DosisTexto)
                    ? null
                    : new JsonArray(new JsonObject
                    {
                        ["doseQuantity"] = new JsonObject
                        {
                            ["value"] = double.TryParse(item.Dosis, out var dosisN) ? JsonValue.Create(dosisN) : JsonValue.Create(item.Dosis),
                            ["unit"] = string.IsNullOrWhiteSpace(item.DosisUnidad) ? "unidad" : item.DosisUnidad
                        }
                    })
            }),
        };

        if (!string.IsNullOrWhiteSpace(item.Cantidad))
        {
            recurso["dispenseRequest"] = new JsonObject
            {
                ["quantity"] = new JsonObject
                {
                    ["value"] = double.TryParse(item.Cantidad, out var n) ? JsonValue.Create(n) : JsonValue.Create(item.Cantidad),
                    ["unit"] = string.IsNullOrWhiteSpace(item.Unidad) ? "unidad" : item.Unidad
                }
            };
        }

        return recurso;
    }

    private static JsonObject ConceptoMedicamento(ItemListaReceta item)
    {
        var concepto = new JsonObject { ["text"] = item.Nombre };
        if (!string.IsNullOrWhiteSpace(item.Codigo))
        {
            concepto["coding"] = new JsonArray(new JsonObject
            {
                ["system"] = "https://iderma.pe/ficha",
                ["code"] = item.Codigo,
                ["display"] = item.Nombre
            });
        }

        return concepto;
    }

    private static JsonObject Entrada(string fullUrl, JsonObject resource) => new()
    {
        ["fullUrl"] = fullUrl,
        ["resource"] = recursoConNullsLimpios(resource)
    };

    private static JsonObject recursoConNullsLimpios(JsonObject recurso)
    {
        var quitar = recurso.Where(p => p.Value is null).Select(p => p.Key).ToList();
        foreach (var clave in quitar)
        {
            recurso.Remove(clave);
        }

        return recurso;
    }

    private static JsonObject Codeable(string code, string display, string system) => new()
    {
        ["coding"] = new JsonArray(new JsonObject
        {
            ["system"] = system,
            ["code"] = code,
            ["display"] = display
        }),
        ["text"] = display
    };

    private static JsonObject Identificador(string system, string valor, string tipo) => new()
    {
        ["system"] = system,
        ["value"] = string.IsNullOrWhiteSpace(valor) ? "s/d" : valor,
        ["type"] = new JsonObject { ["text"] = tipo }
    };

    private static byte[] Deflate(byte[] datos)
    {
        using var salida = new MemoryStream();
        using (var deflate = new DeflateStream(salida, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(datos);
        }

        return salida.ToArray();
    }

    private static string Base64Url(byte[] datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
