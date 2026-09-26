using MobileFix.Domain;
using MobileFix.Domain.Inventory;
using MobileFix.Infrastructure;
using MobileFix.UseCases;

namespace MobileFix.Domain.Tests;

/// <summary>
/// Motor de cobertura: carga del inventario, estadística y resolución de veredictos.
///
/// Lo que se protege aquí es la <b>honestidad de la cobertura</b>: la plataforma no puede declarar
/// que soporta algo que no ha medido, y no puede confundir datos de ejemplo con mediciones del banco.
/// </summary>
public sealed class BenchCoverageTests
{
    private static DeviceFingerprint Fingerprint(string socVendor) => new()
    {
        SocVendor = socVendor,
        SocModel = "modelo de prueba",
        Oem = "marca de prueba",
        Model = "equipo de prueba",
        ConfidencePercent = 96,
        HighestSource = FingerprintSource.BuildProperties,
    };

    private static InventoryDataset Dataset(DataProvenance provenance, params DeviceRecord[] devices) =>
        new() { Provenance = provenance, Devices = devices };

    private static DeviceRecord Device(
        string id,
        string socVendor = "MediaTek",
        AccessMechanism mechanism = AccessMechanism.BromMediaTek,
        AuthRequirement auth = AuthRequirement.None,
        EscaleraLevel level = EscaleraLevel.L4,
        SupportLevel t1 = SupportLevel.Yes,
        SupportLevel t2 = SupportLevel.Yes,
        SupportLevel t3 = SupportLevel.Yes,
        SupportLevel t4 = SupportLevel.Partial) => new()
    {
        Id = id,
        Brand = "Marca",
        Model = "Modelo",
        SocVendor = socVendor,
        Platform = "android",
        Mechanism = mechanism,
        RequiresAuth = auth,
        Condition = DeviceCondition.Boots,
        MaxLevel = level,
        T1 = t1,
        T2 = t2,
        T3 = t3,
        T4 = t4,
    };

    // ---------------------------------------------------------------- lectura del CSV

    [Fact]
    public void Lee_el_inventario_de_ejemplo_sin_descartar_ninguna_fila()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "inventario-demo.csv");
        Assert.True(File.Exists(path), $"No se encontró el inventario de ejemplo en {path}");

        var dataset = new InventoryCsvReader().LoadFile(path);

        Assert.Empty(dataset.RejectedRows);
        Assert.Equal(60, dataset.Devices.Count);
        Assert.Equal(DataProvenance.SeededDemo, dataset.Provenance);
        Assert.True(dataset.IsProvisional);
    }

    [Fact]
    public void Acepta_las_etiquetas_en_espanol_de_la_plantilla_de_excel()
    {
        const string csv = """
            # provenance=bench
            ID,Marca,Modelo,SoC fabricante,Plataforma,Mecanismo accesible,Requiere auth / token,Estado del equipo,Nivel máx. alcanzable,T1,T2,T3,T4
            BF-900,Samsung,Galaxy A13,MediaTek,Android,BROM / Preloader (MTK),No,Bootloop,L4 build.prop,Sí,Parcial,No,—
            """;

        var dataset = new InventoryCsvReader().Load(new StringReader(csv));

        var device = Assert.Single(dataset.Devices);
        Assert.Equal(DataProvenance.Bench, dataset.Provenance);
        Assert.Equal(AccessMechanism.BromMediaTek, device.Mechanism);
        Assert.Equal(AuthRequirement.None, device.RequiresAuth);
        Assert.Equal(DeviceCondition.Bootloop, device.Condition);
        Assert.Equal(EscaleraLevel.L4, device.MaxLevel);
        Assert.Equal(SupportLevel.Yes, device.T1);
        Assert.Equal(SupportLevel.Partial, device.T2);
        Assert.Equal(SupportLevel.No, device.T3);
        Assert.Equal(SupportLevel.NotApplicable, device.T4);
    }

    [Fact]
    public void Una_fila_malformada_se_rechaza_con_su_motivo_y_no_tumba_la_carga()
    {
        const string csv = """
            # provenance=bench
            id,brand,model,soc_vendor,platform,mechanism,requires_auth,condition,max_level,t1,t2,t3,t4
            BF-901,Marca,Modelo bueno,MediaTek,android,brom_mtk,none,boots,L4,yes,yes,no,no
            BF-902,Marca,Modelo con mecanismo raro,MediaTek,android,telepatia,none,boots,L4,yes,no,no,no
            BF-903,Marca,Modelo sin SoC,,android,brom_mtk,none,boots,L4,yes,no,no,no
            """;

        var dataset = new InventoryCsvReader().Load(new StringReader(csv));

        Assert.Single(dataset.Devices);
        Assert.Equal("BF-901", dataset.Devices[0].Id);
        Assert.Equal(2, dataset.RejectedRows.Count);
        Assert.Contains(dataset.RejectedRows, row => row.Contains("mecanismo no reconocido", StringComparison.Ordinal));
        Assert.Contains(dataset.RejectedRows, row => row.Contains("fabricante del SoC", StringComparison.Ordinal));
    }

    [Fact]
    public void Sin_procedencia_declarada_el_dato_es_desconocido()
    {
        const string csv = """
            id,brand,model,soc_vendor,platform,mechanism,requires_auth,condition,max_level,t1,t2,t3,t4
            BF-910,Marca,Modelo,MediaTek,android,brom_mtk,none,boots,L4,yes,yes,no,no
            """;

        var dataset = new InventoryCsvReader().Load(new StringReader(csv));

        Assert.Equal(DataProvenance.Unknown, dataset.Provenance);
        Assert.True(dataset.IsProvisional);
    }

    // ---------------------------------------------------------------- estadística

    [Fact]
    public void Las_estadisticas_cuentan_equipos_mecanismos_y_niveles()
    {
        var dataset = Dataset(
            DataProvenance.Bench,
            Device("A", mechanism: AccessMechanism.BromMediaTek, level: EscaleraLevel.L6),
            Device("B", mechanism: AccessMechanism.BromMediaTek, level: EscaleraLevel.L5, auth: AuthRequirement.ThirdPartyLegitimate),
            Device("C", socVendor: "Qualcomm", mechanism: AccessMechanism.EdlQualcomm, level: EscaleraLevel.L3),
            Device("D", socVendor: "Unisoc", mechanism: AccessMechanism.FdlUnisoc, level: EscaleraLevel.L2));

        var statistics = InventoryStatistics.From(dataset);

        Assert.Equal(4, statistics.Total);
        Assert.Equal(3, statistics.MechanismsWithEvidence);
        Assert.Equal(2, statistics.Mechanism(AccessMechanism.BromMediaTek)!.Devices);
        Assert.Equal(1, statistics.Mechanism(AccessMechanism.BromMediaTek)!.RequiringCredentials);
        Assert.Equal(0, statistics.Mechanism(AccessMechanism.DfuApple)!.Devices);

        // Dos de cuatro equipos llegaron a L4 o más.
        Assert.Equal(0.5d, statistics.IdentifiedAtModelLevelPercent, 3);
    }

    [Fact]
    public void El_porcentaje_alcanzable_se_calcula_sobre_los_equipos_evaluables()
    {
        var dataset = Dataset(
            DataProvenance.Bench,
            Device("A", t2: SupportLevel.Yes, t3: SupportLevel.NotApplicable),
            Device("B", t2: SupportLevel.Partial, t3: SupportLevel.NotApplicable),
            Device("C", t2: SupportLevel.No, t3: SupportLevel.NotApplicable));

        var statistics = InventoryStatistics.From(dataset);
        var t2 = statistics.Intervention("T2 arranque")!;

        Assert.Equal(3, t2.Assessed);
        Assert.Equal(2, t2.Reachable);
        Assert.Equal(2d / 3d, t2.ReachablePercent, 3);
        Assert.Equal(1d / 3d, t2.YesPercent, 3);
    }

    // ---------------------------------------------------------------- resolución

    [Fact]
    public void Sin_evidencia_en_el_banco_el_veredicto_es_no_soportado_y_sin_procedencia()
    {
        var resolver = new CoverageResolver(InventoryDataset.Empty);

        var assessment = resolver.Resolve(Fingerprint("MediaTek"));

        Assert.Equal(CoverageVerdict.Unsupported, assessment.Verdict);
        Assert.Equal(CoverageEvidence.None, assessment.Evidence);
        Assert.False(assessment.AllowsWriting);
        Assert.Contains(assessment.Reasons, reason => reason.Contains("sin evidencia", StringComparison.Ordinal));
    }

    [Fact]
    public void Con_evidencia_de_reparacion_el_veredicto_es_pleno()
    {
        var resolver = new CoverageResolver(Dataset(DataProvenance.Bench, Device("A", t3: SupportLevel.Yes)));

        var assessment = resolver.Resolve(Fingerprint("MediaTek"));

        Assert.Equal(CoverageVerdict.Full, assessment.Verdict);
        Assert.True(assessment.AllowsWriting);
        Assert.Equal(CoverageEvidence.BenchMeasured, assessment.Evidence);
        Assert.False(assessment.IsProvisional);
    }

    [Fact]
    public void Si_toda_la_evidencia_depende_de_terceros_el_veredicto_baja_a_parcial()
    {
        var resolver = new CoverageResolver(Dataset(
            DataProvenance.Bench,
            Device("A", auth: AuthRequirement.ThirdPartyLegitimate, t3: SupportLevel.Yes),
            Device("B", auth: AuthRequirement.Unknown, t3: SupportLevel.Yes)));

        var assessment = resolver.Resolve(Fingerprint("MediaTek"));

        Assert.Equal(CoverageVerdict.Partial, assessment.Verdict);
    }

    [Fact]
    public void Si_solo_hay_diagnostico_el_veredicto_es_solo_lectura()
    {
        var resolver = new CoverageResolver(Dataset(
            DataProvenance.Bench,
            Device("A", t1: SupportLevel.Yes, t2: SupportLevel.No, t3: SupportLevel.No)));

        var assessment = resolver.Resolve(Fingerprint("MediaTek"));

        Assert.Equal(CoverageVerdict.ReadOnly, assessment.Verdict);
        Assert.False(assessment.AllowsWriting);
    }

    [Fact]
    public void Un_veredicto_calculado_con_datos_de_ejemplo_se_marca_como_provisional()
    {
        var resolver = new CoverageResolver(Dataset(DataProvenance.SeededDemo, Device("A")));

        var assessment = resolver.Resolve(Fingerprint("MediaTek"));

        Assert.True(assessment.IsProvisional);
        Assert.Equal(CoverageEvidence.SeededDemo, assessment.Evidence);
        Assert.Contains("PROVISIONAL", assessment.Describe(), StringComparison.Ordinal);
        Assert.Contains(assessment.Reasons, reason => reason.Contains("PROVISIONAL", StringComparison.Ordinal));
    }

    [Fact]
    public void La_cobertura_solo_deja_de_ser_provisional_cuando_el_banco_lo_declara()
    {
        var fromBench = BenchCoverageService.From(Dataset(DataProvenance.Bench, Device("A")));
        var fromDemo = BenchCoverageService.From(Dataset(DataProvenance.SeededDemo, Device("A")));

        Assert.False(fromBench.IsProvisional);
        Assert.True(fromDemo.IsProvisional);
        Assert.DoesNotContain("PROVISIONAL", fromBench.Describe(), StringComparison.Ordinal);
        Assert.Contains("PROVISIONAL", fromDemo.Describe(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MediaTek", AccessMechanism.BromMediaTek)]
    [InlineData("mtk", AccessMechanism.BromMediaTek)]
    [InlineData("Qualcomm", AccessMechanism.EdlQualcomm)]
    [InlineData("Unisoc", AccessMechanism.FdlUnisoc)]
    [InlineData("Spreadtrum", AccessMechanism.FdlUnisoc)]
    [InlineData("Exynos", AccessMechanism.EubExynos)]
    [InlineData("Apple", AccessMechanism.DfuApple)]
    [InlineData("Ficticio", AccessMechanism.None)]
    public void El_mecanismo_esperado_se_deriva_del_fabricante_del_soc(string socVendor, AccessMechanism expected)
    {
        Assert.Equal(expected, CoverageResolver.MechanismFor(socVendor));
    }
}
