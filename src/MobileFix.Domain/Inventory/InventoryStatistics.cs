namespace MobileFix.Domain.Inventory;

/// <summary>Equipos por mecanismo de acceso, y cuántos de ellos exigen credenciales.</summary>
public sealed record MechanismStats(AccessMechanism Mechanism, int Devices, int RequiringCredentials)
{
    public bool HasEvidence => Devices > 0;
}

/// <summary>Equipos por peldaño más alto alcanzado.</summary>
public sealed record EscaleraStats(EscaleraLevel Level, int Devices);

/// <summary>Cobertura de un nivel de intervención (T1 a T4) sobre el banco.</summary>
public sealed record InterventionStats(string Name, int Yes, int Partial, int No, int NotApplicable)
{
    /// <summary>Equipos sobre los que la pregunta tiene sentido (excluye «no aplica»).</summary>
    public int Assessed => Yes + Partial + No;

    public int Reachable => Yes + Partial;

    public double YesPercent => Assessed == 0 ? 0d : (double)Yes / Assessed;

    public double ReachablePercent => Assessed == 0 ? 0d : (double)Reachable / Assessed;
}

/// <summary>
/// Estadística del banco. Es lo que alimenta la matriz de cobertura y el informe de viabilidad de M0.
/// </summary>
public sealed record InventoryStatistics
{
    public required int Total { get; init; }

    public required DataProvenance Provenance { get; init; }

    public required IReadOnlyList<MechanismStats> ByMechanism { get; init; }

    public required IReadOnlyList<EscaleraStats> ByLevel { get; init; }

    public required IReadOnlyList<InterventionStats> ByIntervention { get; init; }

    /// <summary>Mecanismos con al menos un equipo. Objetivo de M0: 4.</summary>
    public required int MechanismsWithEvidence { get; init; }

    /// <summary>Porcentaje de equipos identificados a nivel de modelo o superior. Objetivo de M2: 85 %.</summary>
    public required double IdentifiedAtModelLevelPercent { get; init; }

    /// <summary>Equipos bloqueados a operador. En México es el mercado de importación de EEUU.</summary>
    public required int CarrierLocked { get; init; }

    /// <summary>Equipos cuya liberación la hace el operador en su portal (con requisitos de elegibilidad).</summary>
    public required int UnlockViaPortal { get; init; }

    /// <summary>Equipos cuya liberación es automática por parte del operador al cumplir su plazo.</summary>
    public required int UnlockAutomatic { get; init; }

    /// <summary>Equipos solo eSIM: no aceptan SIM física mexicana.</summary>
    public required int EsimOnly { get; init; }

    public bool IsProvisional => Provenance != DataProvenance.Bench;

    public InterventionStats? Intervention(string name) =>
        ByIntervention.FirstOrDefault(intervention => string.Equals(intervention.Name, name, StringComparison.OrdinalIgnoreCase));

    public MechanismStats? Mechanism(AccessMechanism mechanism) =>
        ByMechanism.FirstOrDefault(stats => stats.Mechanism == mechanism);

    public static InventoryStatistics From(InventoryDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        var devices = dataset.Devices;

        var byMechanism = Enum.GetValues<AccessMechanism>()
            .Select(mechanism =>
            {
                var group = devices.Where(device => device.Mechanism == mechanism).ToArray();
                return new MechanismStats(
                    mechanism,
                    group.Length,
                    group.Count(device => device.RequiresAuth is AuthRequirement.Unknown or AuthRequirement.ThirdPartyLegitimate));
            })
            .ToArray();

        var byLevel = Enum.GetValues<EscaleraLevel>()
            .Select(level => new EscaleraStats(level, devices.Count(device => device.MaxLevel == level)))
            .ToArray();

        var byIntervention = new[]
        {
            Intervention("T1 diagnóstico", devices.Select(device => device.T1)),
            Intervention("T2 arranque", devices.Select(device => device.T2)),
            Intervention("T3 particiones", devices.Select(device => device.T3)),
            Intervention("T4 datos", devices.Select(device => device.T4)),
        };

        return new InventoryStatistics
        {
            Total = devices.Count,
            Provenance = dataset.Provenance,
            ByMechanism = byMechanism,
            ByLevel = byLevel,
            ByIntervention = byIntervention,
            MechanismsWithEvidence = byMechanism.Count(stats => stats.HasEvidence),
            IdentifiedAtModelLevelPercent = devices.Count == 0
                ? 0d
                : (double)devices.Count(device => device.IdentifiedAtModelLevel) / devices.Count,
            CarrierLocked = devices.Count(device => device.CarrierLock == CarrierLockState.LockedToCarrier),
            UnlockViaPortal = devices.Count(device => device.Unlock == UnlockPath.CarrierPortal),
            UnlockAutomatic = devices.Count(device => device.Unlock == UnlockPath.CarrierAutomatic),
            EsimOnly = devices.Count(device => device.Sim == SimType.EsimOnly),
        };
    }

    private static InterventionStats Intervention(string name, IEnumerable<SupportLevel> levels)
    {
        var materialised = levels as SupportLevel[] ?? levels.ToArray();

        return new InterventionStats(
            name,
            materialised.Count(level => level == SupportLevel.Yes),
            materialised.Count(level => level == SupportLevel.Partial),
            materialised.Count(level => level == SupportLevel.No),
            materialised.Count(level => level == SupportLevel.NotApplicable));
    }
}
