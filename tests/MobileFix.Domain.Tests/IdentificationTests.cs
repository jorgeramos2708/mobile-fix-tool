using MobileFix.Domain;
using MobileFix.Domain.Identification;
using MobileFix.Domain.Inventory;
using MobileFix.Infrastructure;
using MobileFix.Infrastructure.Hal;
using MobileFix.UseCases;

namespace MobileFix.Domain.Tests;

/// <summary>
/// Escalera de identificación L0–L6 y transporte simulado.
///
/// Lo que se protege aquí: que un peldaño que no se alcanzó no se invente, que dos peldaños que se
/// contradicen produzcan una identidad dudosa, y que el IMEI no se guarde nunca en claro.
/// </summary>
public sealed class IdentificationTests
{
    private static IEnumerable<GptPartition> SamplePartitions =>
    [
        new() { Name = "boot", FirstSector = 16384, LastSector = 147455, SizeBytes = 67_108_864 },
        new() { Name = "persist", FirstSector = 8_912_896, LastSector = 8_978_431, SizeBytes = 33_554_432 },
        new() { Name = "userdata", FirstSector = 10_485_760, LastSector = 10_585_759, SizeBytes = 51_200_000 },
    ];

    private static IdentificationInputs CompleteInputs() => new()
    {
        Descriptor = new TransportDescriptor
        {
            VendorId = 0x04E8, ProductId = 0x685D, Manufacturer = "SAMSUNG", Product = "Gadget Serial",
            SuspectedMechanism = AccessMechanism.EubExynos,
        },
        Handshake = new HandshakeResult
        {
            SocVendor = "Exynos", SocModel = "Exynos 850", SecureBootEnabled = false, RequiresAuthentication = false,
        },
        Partitions = SamplePartitions.ToList(),
        Build = new BuildProperties
        {
            Oem = "samsung", Model = "SM-A135M", RegionVariant = "MXO", BuildId = "TP1A.220624.014", AndroidVersion = "13",
        },
        Board = new BoardIdentity { BoardCodename = "a13", HardwareRevision = "0.6" },
        Baseband = new BasebandIdentity { BasebandVersion = "A135MUBU6DXA1", ModemRegion = "MXO", Imei = "352999110012345" },
        Evidence = IdentificationEvidence.MeasuredFromDevice,
    };

    // ------------------------------------------------------------------ escalera

    [Fact]
    public void Con_todos_los_peldanos_la_identidad_llega_a_L6_y_es_fiable()
    {
        var identity = new IdentificationLadder().Assemble(CompleteInputs());

        Assert.Equal(ProbeLevel.L6_Baseband, identity.HighestLevel);
        Assert.Empty(identity.Conflicts);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
        Assert.Equal("SM-A135M", identity.Model);
        Assert.Equal("a13", identity.BoardCodename);
        Assert.Equal("A135MUBU6DXA1", identity.BasebandVersion);
        Assert.Equal(IdentificationEvidence.MeasuredFromDevice, identity.Evidence);
        Assert.False(identity.IsProvisional);
    }

    [Fact]
    public void Sin_handshake_la_identidad_se_queda_en_el_descriptor()
    {
        var identity = new IdentificationLadder().Assemble(new IdentificationInputs
        {
            Descriptor = CompleteInputs().Descriptor,
            Evidence = IdentificationEvidence.MeasuredFromDevice,
        });

        Assert.Equal(ProbeLevel.L1_Interface, identity.HighestLevel);
        Assert.Equal(IdentityConfidence.Low, identity.Confidence);
        Assert.Equal("Desconocido", identity.Model);
    }

    [Fact]
    public void Un_peldano_que_no_se_alcanzo_no_se_inventa()
    {
        // Hay identidad de placa pero no se pudo leer la baseband: el campo queda vacío.
        var inputs = CompleteInputs() with { Baseband = null };
        var identity = new IdentificationLadder().Assemble(inputs);

        Assert.Equal(ProbeLevel.L5_BoardIdentity, identity.HighestLevel);
        Assert.Null(identity.BasebandVersion);
        Assert.Null(identity.ImeiHash);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
    }

    [Fact]
    public void Un_soc_imposible_para_el_fabricante_produce_conflicto()
    {
        // Un Pixel no puede llevar un SoC MediaTek: o la placa está cambiada o el firmware es de otro modelo.
        var inputs = CompleteInputs() with
        {
            Build = CompleteInputs().Build! with { Oem = "Google", Model = "Pixel 8" },
        };

        var identity = new IdentificationLadder().Assemble(inputs);

        Assert.Contains(identity.Conflicts, conflict => conflict.Contains("placa cambiada", StringComparison.Ordinal));
        Assert.Equal(IdentityConfidence.Conflict, identity.Confidence);
    }

    [Fact]
    public void Un_huawei_con_snapdragon_no_es_un_conflicto()
    {
        // Huawei monta silicio de varios fabricantes. Marcarlo como conflicto sería un falso positivo.
        var inputs = CompleteInputs() with
        {
            Handshake = CompleteInputs().Handshake! with { SocVendor = "Qualcomm", SocModel = "SM7325 Snapdragon 778G" },
            Build = CompleteInputs().Build! with { Oem = "Huawei", Model = "Nova 9" },
        };

        var identity = new IdentificationLadder().Assemble(inputs);

        Assert.Empty(identity.Conflicts);
    }

    [Fact]
    public void La_region_del_sistema_distinta_de_la_del_modem_produce_conflicto()
    {
        var inputs = CompleteInputs() with
        {
            Baseband = CompleteInputs().Baseband! with { ModemRegion = "EU" },
        };

        var identity = new IdentificationLadder().Assemble(inputs);

        Assert.Contains(identity.Conflicts, conflict => conflict.Contains("región del sistema", StringComparison.Ordinal));
        Assert.Equal(IdentityConfidence.Conflict, identity.Confidence);
    }

    [Fact]
    public void El_arranque_seguro_se_informa_pero_no_mancha_la_identidad()
    {
        var inputs = CompleteInputs() with
        {
            Handshake = CompleteInputs().Handshake! with { SecureBootEnabled = true, RequiresAuthentication = true },
        };

        var identity = new IdentificationLadder().Assemble(inputs);

        Assert.True(identity.SecureBootEnabled);
        Assert.True(identity.AuthenticationRequired);
        Assert.Empty(identity.Conflicts);
        Assert.Equal(IdentityConfidence.High, identity.Confidence);
    }

    [Fact]
    public void El_imei_nunca_se_guarda_en_claro()
    {
        var identity = new IdentificationLadder().Assemble(CompleteInputs());

        Assert.NotNull(identity.ImeiHash);
        Assert.Equal(16, identity.ImeiHash!.Length);
        Assert.DoesNotContain("352999110012345", identity.Describe(), StringComparison.Ordinal);
        Assert.DoesNotContain("352999110012345", identity.ImeiHash, StringComparison.Ordinal);
    }

    [Fact]
    public void La_identidad_se_convierte_en_huella_para_el_motor_de_cobertura()
    {
        var identity = new IdentificationLadder().Assemble(CompleteInputs());

        var fingerprint = identity.ToFingerprint();

        Assert.Equal("Exynos", fingerprint.SocVendor);
        Assert.Equal("SM-A135M", fingerprint.Model);
        Assert.Equal(FingerprintSource.Baseband, fingerprint.HighestSource);
        Assert.Equal(identity.Conflicts, fingerprint.Conflicts);
    }

    [Fact]
    public void Una_identidad_medida_no_es_provisional_y_una_simulada_si()
    {
        var measured = new IdentificationLadder().Assemble(CompleteInputs());
        var simulated = new IdentificationLadder().Assemble(
            CompleteInputs() with { Evidence = IdentificationEvidence.ScriptedSimulation });

        Assert.False(measured.IsProvisional);
        Assert.True(simulated.IsProvisional);
        Assert.Contains("PROVISIONAL", simulated.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void El_imei_se_enmascara_para_informes()
    {
        Assert.Equal("***********2345", SensitiveData.Mask("352999110012345"));
        Assert.Equal("****", SensitiveData.Mask(null));
        Assert.Equal("****", SensitiveData.Mask("123"));
    }

    // ------------------------------------------------------------------ transporte simulado

    [Fact]
    public async Task El_handshake_no_responde_si_el_mecanismo_no_corresponde()
    {
        var transport = new ScriptedDeviceTransport(ScriptedProfiles.GalaxyA13Lte());

        var correcto = await transport.HandshakeAsync(AccessMechanism.EubExynos, CancellationToken.None);
        var incorrecto = await transport.HandshakeAsync(AccessMechanism.EdlQualcomm, CancellationToken.None);

        Assert.NotNull(correcto);
        Assert.Null(incorrecto);
    }

    [Fact]
    public async Task El_servicio_completo_identifica_y_marca_la_simulacion()
    {
        var transport = new ScriptedDeviceTransport(ScriptedProfiles.GalaxyA13Lte());
        var service = new IdentifyDeviceService(transport, IdentificationEvidence.ScriptedSimulation);

        var outcome = await service.RunAsync(null, CancellationToken.None);

        Assert.Equal("SM-A135M", outcome.Identity.Model);
        Assert.Equal(ProbeLevel.L6_Baseband, outcome.Identity.HighestLevel);
        Assert.Equal(IdentificationEvidence.ScriptedSimulation, outcome.Identity.Evidence);
        Assert.True(outcome.Identity.IsProvisional);
        Assert.True(outcome.Link.IsSafeToWrite);
        Assert.Null(outcome.Coverage);

        // Sin evidencia de banco NO se escribe, aunque la identidad sea perfecta y el enlace sea bueno:
        // la cobertura tiene que estar demostrada, no supuesta. Es la misma regla que impide prometer
        // cobertura sin banco, aplicada en el último punto donde todavía se puede parar.
        Assert.False(outcome.CanWrite);
    }

    [Fact]
    public async Task Un_enlace_inestable_impide_escribir_aunque_la_identidad_sea_perfecta()
    {
        var transport = new ScriptedDeviceTransport(ScriptedProfiles.ConEnlaceInestable());
        var service = new IdentifyDeviceService(transport, IdentificationEvidence.ScriptedSimulation);

        var outcome = await service.RunAsync(null, CancellationToken.None);

        Assert.Empty(outcome.Identity.Conflicts);
        Assert.False(outcome.Link.IsSafeToWrite);
        Assert.False(outcome.CanWrite);
        Assert.Contains("INESTABLE", outcome.Link.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_placa_cambiada_impide_escribir_por_conflicto_de_identidad()
    {
        var transport = new ScriptedDeviceTransport(ScriptedProfiles.PlacaCambiada());
        var service = new IdentifyDeviceService(transport, IdentificationEvidence.ScriptedSimulation);

        var outcome = await service.RunAsync(null, CancellationToken.None);

        Assert.NotEmpty(outcome.Identity.Conflicts);
        Assert.False(outcome.CanWrite);
    }

    [Fact]
    public async Task El_servicio_resuelve_cobertura_cuando_hay_evidencia_de_banco()
    {
        var dataset = new InventoryDataset
        {
            Provenance = DataProvenance.Bench,
            Devices =
            [
                new DeviceRecord
                {
                    Id = "BF-X", Brand = "Samsung", Model = "SM-A135M", SocVendor = "Exynos",
                    Platform = "android", Mechanism = AccessMechanism.EubExynos,
                    RequiresAuth = AuthRequirement.None, Condition = DeviceCondition.Boots,
                    MaxLevel = EscaleraLevel.L6, T1 = SupportLevel.Yes, T2 = SupportLevel.Yes,
                    T3 = SupportLevel.Partial, T4 = SupportLevel.Partial,
                    Sim = SimType.PhysicalDual, CarrierLock = CarrierLockState.NotApplicable,
                    Unlock = UnlockPath.NotNeeded,
                },
            ],
        };

        var coverage = BenchCoverageService.From(dataset);
        var transport = new ScriptedDeviceTransport(ScriptedProfiles.GalaxyA13Lte());
        var service = new IdentifyDeviceService(transport, IdentificationEvidence.MeasuredFromDevice);

        var outcome = await service.RunAsync(coverage, CancellationToken.None);

        Assert.NotNull(outcome.Coverage);
        Assert.Equal(CoverageVerdict.Partial, outcome.Coverage!.Verdict);
        Assert.True(outcome.CanWrite);
    }

    [Fact]
    public async Task El_bloqueo_de_operador_del_equipo_llega_hasta_la_orientacion()
    {
        var locked = ScriptedProfiles.RedmiNote11Qualcomm() with
        {
            Build = ScriptedProfiles.RedmiNote11Qualcomm().Build! with
            {
                CarrierLock = CarrierLockState.LockedToCarrier,
                Carrier = "att",
            },
        };

        var service = new IdentifyDeviceService(
            new ScriptedDeviceTransport(locked), IdentificationEvidence.MeasuredFromDevice);
        var outcome = await service.RunAsync(null, CancellationToken.None);

        Assert.Equal(CarrierLockState.LockedToCarrier, outcome.Identity.CarrierLock);
        Assert.Contains("att", outcome.UnlockGuidance, StringComparison.Ordinal);
        Assert.Contains("no libera por bypass", outcome.UnlockGuidance, StringComparison.Ordinal);
    }
}
