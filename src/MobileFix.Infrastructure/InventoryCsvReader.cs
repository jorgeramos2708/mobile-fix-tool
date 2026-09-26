using System.Globalization;
using System.Text;
using MobileFix.Domain.Inventory;
using MobileFix.Ports;

namespace MobileFix.Infrastructure;

/// <summary>
/// Lee el inventario del banco en CSV.
///
/// Principios de diseño:
/// 1. Una fila malformada <b>no</b> tumba la carga: se rechaza con su motivo y se sigue.
///    Un banco de 300 equipos no se puede quedar sin cargar por un punto y coma de más.
/// 2. Acepta tanto los identificadores de máquina como las etiquetas en español de la plantilla
///    de Excel, para que exportar desde la hoja no obligue a renombrar columnas.
/// 3. La procedencia del dato se declara en la cabecera (<c># provenance=...</c>). Sin declaración
///    se asume «desconocida», que nunca habilita declarar cobertura.
/// </summary>
public sealed class InventoryCsvReader : IInventorySource
{
    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "id",
        ["brand"] = "brand", ["marca"] = "brand",
        ["model"] = "model", ["modelo"] = "model",
        ["region"] = "region", ["variante"] = "region", ["variante / region"] = "region",
        ["soc_vendor"] = "soc_vendor", ["soc_fabricante"] = "soc_vendor", ["soc fabricante"] = "soc_vendor",
        ["soc_model"] = "soc_model", ["soc_modelo"] = "soc_model", ["soc modelo"] = "soc_model",
        ["platform"] = "platform", ["plataforma"] = "platform",
        ["mechanism"] = "mechanism", ["mecanismo"] = "mechanism", ["mecanismo accesible"] = "mechanism",
        ["requires_auth"] = "requires_auth", ["requiere_auth"] = "requires_auth", ["requiere auth / token"] = "requires_auth",
        ["bootloader"] = "bootloader",
        ["condition"] = "condition", ["estado"] = "condition", ["estado del equipo"] = "condition",
        ["max_level"] = "max_level", ["nivel"] = "max_level", ["nivel max. alcanzable"] = "max_level",
        ["t1"] = "t1", ["t2"] = "t2", ["t3"] = "t3", ["t4"] = "t4",
        ["bench_location"] = "bench_location", ["ubicacion"] = "bench_location", ["ubicacion en banco"] = "bench_location",
        ["notes"] = "notes", ["notas"] = "notes",
    };

    private static readonly Dictionary<string, AccessMechanism> MechanismValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = AccessMechanism.None, ["ninguno"] = AccessMechanism.None,
        ["adb_only"] = AccessMechanism.AdbOnly, ["solo adb"] = AccessMechanism.AdbOnly,
        ["fastboot"] = AccessMechanism.Fastboot,
        ["brom_mtk"] = AccessMechanism.BromMediaTek, ["brom / preloader (mtk)"] = AccessMechanism.BromMediaTek, ["brom"] = AccessMechanism.BromMediaTek,
        ["edl_qc"] = AccessMechanism.EdlQualcomm, ["edl 9008 + sahara (qc)"] = AccessMechanism.EdlQualcomm, ["edl"] = AccessMechanism.EdlQualcomm,
        ["fdl_unisoc"] = AccessMechanism.FdlUnisoc, ["fdl (unisoc)"] = AccessMechanism.FdlUnisoc, ["fdl"] = AccessMechanism.FdlUnisoc,
        ["eub_exynos"] = AccessMechanism.EubExynos, ["eub (exynos)"] = AccessMechanism.EubExynos, ["eub"] = AccessMechanism.EubExynos,
        ["dfu_apple"] = AccessMechanism.DfuApple, ["dfu (apple)"] = AccessMechanism.DfuApple, ["dfu"] = AccessMechanism.DfuApple,
    };

    public InventoryDataset LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Load(reader, Path.GetFullPath(path));
    }

    public InventoryDataset Load(TextReader reader, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var devices = new List<DeviceRecord>();
        var rejected = new List<string>();
        var provenance = DataProvenance.Unknown;
        string[]? headers = null;
        var lineNumber = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                var metadata = ParseMetadata(line);
                if (metadata.TryGetValue("provenance", out var declared))
                {
                    provenance = ParseProvenance(declared);
                }

                continue;
            }

            var fields = SplitCsvLine(line);

            if (headers is null)
            {
                headers = fields.Select(MapHeader).ToArray();
                continue;
            }

            try
            {
                devices.Add(BuildRecord(headers, fields));
            }
            catch (FormatException ex)
            {
                rejected.Add($"línea {lineNumber}: {ex.Message}");
            }
        }

        return new InventoryDataset
        {
            Provenance = provenance,
            Devices = devices,
            RejectedRows = rejected,
            SourcePath = sourcePath,
        };
    }

    private static DeviceRecord BuildRecord(string[] headers, string[] fields)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var key = headers[i];
            if (key.Length == 0)
            {
                continue;
            }

            values[key] = i < fields.Length ? fields[i].Trim() : string.Empty;
        }

        string Get(string name) => values.TryGetValue(name, out var value) ? value : string.Empty;

        var id = Get("id");
        if (id.Length == 0)
        {
            throw new FormatException("falta el identificador (id)");
        }

        var model = Get("model");
        if (model.Length == 0)
        {
            throw new FormatException($"falta el modelo en {id}");
        }

        var socVendor = Get("soc_vendor");
        if (socVendor.Length == 0)
        {
            throw new FormatException($"falta el fabricante del SoC en {id}");
        }

        var mechanismText = Get("mechanism");
        if (!TryParseMechanism(mechanismText, out var mechanism))
        {
            throw new FormatException($"mecanismo no reconocido en {id}: '{mechanismText}'");
        }

        return new DeviceRecord
        {
            Id = id,
            Brand = Get("brand"),
            Model = model,
            Region = Empty(Get("region")),
            SocVendor = socVendor,
            SocModel = Empty(Get("soc_model")),
            Platform = Get("platform").Length == 0 ? "android" : Get("platform"),
            Mechanism = mechanism,
            RequiresAuth = ParseAuth(Get("requires_auth")),
            Condition = ParseCondition(Get("condition")),
            MaxLevel = ParseLevel(Get("max_level")),
            T1 = ParseSupport(Get("t1")),
            T2 = ParseSupport(Get("t2")),
            T3 = ParseSupport(Get("t3")),
            T4 = ParseSupport(Get("t4")),
            BenchLocation = Empty(Get("bench_location")),
            Notes = Empty(Get("notes")),
        };
    }

    private static string? Empty(string value) => value.Length == 0 ? null : value;

    private static string MapHeader(string raw)
    {
        var normalised = Normalise(raw);
        return HeaderAliases.TryGetValue(normalised, out var canonical) ? canonical : string.Empty;
    }

    /// <summary>
    /// Normaliza un texto para compararlo: minúsculas, sin acentos y sin espacios dobles.
    ///
    /// El plegado de acentos no es cosmético: la plantilla de Excel en español escribe
    /// «Nivel máx. alcanzable», y sin plegar la columna se perdería en silencio al exportar.
    /// </summary>
    private static string Normalise(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Replace("  ", " ").Trim();
    }

    private static bool TryParseMechanism(string text, out AccessMechanism mechanism)
    {
        if (MechanismValues.TryGetValue(text.Trim(), out mechanism))
        {
            return true;
        }

        // Segunda pasada normalizada: «BROM / Preloader (MTK)» y «brom / preloader (mtk)» son el mismo mecanismo.
        if (MechanismValues.TryGetValue(Normalise(text), out mechanism))
        {
            return true;
        }

        mechanism = AccessMechanism.None;
        return false;
    }

    private static DataProvenance ParseProvenance(string value) => Normalise(value) switch
    {
        "bench" or "banco" or "measured" or "medido" => DataProvenance.Bench,
        "seeded-demo" or "seeded_demo" or "demo" or "ejemplo" => DataProvenance.SeededDemo,
        _ => DataProvenance.Unknown,
    };

    private static AuthRequirement ParseAuth(string text) => Normalise(text) switch
    {
        "own" or "propio" or "sí - propio" or "si - propio" => AuthRequirement.OwnCredential,
        "third_party" or "terceros" or "sí - requiere tercero legítimo" or "si - requiere tercero legitimo" => AuthRequirement.ThirdPartyLegitimate,
        "none" or "no" => AuthRequirement.None,
        "" or "unknown" or "desconocido" => AuthRequirement.Unknown,
        var other => throw new FormatException($"valor de autenticación no reconocido: '{other}'"),
    };

    /// <summary>El estado es descriptivo, no decide nada: un valor raro se marca como desconocido.</summary>
    private static DeviceCondition ParseCondition(string text) => Normalise(text) switch
    {
        "boots" or "funciona / enciende" or "funciona" or "enciende" => DeviceCondition.Boots,
        "bootloop" => DeviceCondition.Bootloop,
        "noboot" or "no enciende" => DeviceCondition.NoBoot,
        "recovery_only" or "solo recovery" => DeviceCondition.RecoveryOnly,
        "low_level_only" or "solo brom/edl" => DeviceCondition.LowLevelOnly,
        "bricked" or "brick total" => DeviceCondition.Bricked,
        "dead" or "muerto" or "muerto (hardware)" => DeviceCondition.Dead,
        _ => DeviceCondition.Unknown,
    };

    private static EscaleraLevel ParseLevel(string text)
    {
        var normalised = Normalise(text);
        if (normalised.Length == 0)
        {
            return EscaleraLevel.None;
        }

        // Acepta «L4», «l4» y «L4 build.prop» (etiqueta de la plantilla de Excel).
        var index = normalised.IndexOf('l');
        if (index >= 0 && index + 1 < normalised.Length && char.IsDigit(normalised[index + 1]))
        {
            var digit = normalised[index + 1] - '0';
            if (digit <= 6)
            {
                return (EscaleraLevel)(digit + 1);
            }
        }

        throw new FormatException($"nivel no reconocido: '{text}'");
    }

    private static SupportLevel ParseSupport(string text) => Normalise(text) switch
    {
        "yes" or "sí" or "si" or "s" or "true" or "1" => SupportLevel.Yes,
        "partial" or "parcial" or "p" => SupportLevel.Partial,
        "no" or "n" or "false" or "0" => SupportLevel.No,
        "" or "—" or "-" or "n/a" or "na" or "no aplica" => SupportLevel.NotApplicable,
        var other => throw new FormatException($"valor de soporte no reconocido: '{other}'"),
    };

    private static Dictionary<string, string> ParseMetadata(string line)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = line.TrimStart('#').Trim();
        var separator = body.IndexOf('=');

        if (separator <= 0)
        {
            return result;
        }

        result[body[..separator].Trim()] = body[(separator + 1)..].Trim();
        return result;
    }

    /// <summary>Separador CSV con soporte de comillas dobles escapadas («"» dentro de un campo).</summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(character);
                    break;
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
