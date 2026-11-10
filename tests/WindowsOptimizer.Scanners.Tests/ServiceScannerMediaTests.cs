using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;
using Xunit;

namespace WindowsOptimizer.Scanners.Tests;

/// <summary>
/// Tests de integración de ServiceScanner con la detección de media de disco.
/// Verifican que las reglas ssd_only se omiten en HDD y se muestran en SSD.
/// </summary>
public class ServiceScannerMediaTests
{
    // -------------------------------------------------------------------------
    // Fakes
    // -------------------------------------------------------------------------

    private sealed class FakeRuleProvider : IRuleProvider
    {
        private readonly IReadOnlyList<RuleDefinition> _rules;

        public FakeRuleProvider(IReadOnlyList<RuleDefinition> rules) => _rules = rules;

        public Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct = default)
            => Task.FromResult(_rules);

        public Task<IReadOnlyList<RuleDefinition>> GetRulesByCategoryAsync(Category category, CancellationToken ct = default)
        {
            var filtered = _rules.Where(r => r.Category == category).ToList();
            return Task.FromResult<IReadOnlyList<RuleDefinition>>(filtered);
        }

        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        private readonly Dictionary<string, ServiceInfo> _services;

        public FakeServiceManager(Dictionary<string, ServiceInfo> services) => _services = services;

        public ServiceInfo? GetServiceInfo(string serviceName)
            => _services.TryGetValue(serviceName, out var info) ? info : null;

        public void SetStartupType(string serviceName, ServiceStartMode mode) { }
        public void StopService(string serviceName) { }
        public void StartService(string serviceName) { }
        public bool ServiceExists(string serviceName) => _services.ContainsKey(serviceName);
    }

    private sealed class FakeDriveMediaService : IDriveMediaService
    {
        private readonly bool _isSsd;

        public FakeDriveMediaService(bool isSsd) => _isSsd = isSsd;

        public Task<bool> IsSystemDriveSolidStateAsync(CancellationToken ct = default)
            => Task.FromResult(_isSsd);

        public Task<DriveMediaType> GetSystemDriveMediaTypeAsync(CancellationToken ct = default)
            => Task.FromResult(_isSsd ? DriveMediaType.Ssd : DriveMediaType.Hdd);

        public Task<IReadOnlyList<PhysicalDriveInfo>> GetAllDrivesAsync(CancellationToken ct = default)
        {
            var drives = new List<PhysicalDriveInfo>
            {
                new()
                {
                    FriendlyName  = _isSsd ? "Samsung SSD 980 PRO" : "WD Blue 1TB HDD",
                    MediaType     = _isSsd ? DriveMediaType.Ssd : DriveMediaType.Hdd,
                    IsSystemDrive = true,
                    SizeBytes     = 1_000_000_000_000L
                }
            };
            return Task.FromResult<IReadOnlyList<PhysicalDriveInfo>>(drives);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static RuleDefinition MakeSysMainRule(bool ssdOnly = true) => new()
    {
        Id          = "services.sysmain",
        Name        = "Desactivar SysMain (Superfetch) — Solo SSD",
        Category    = Category.Services,
        Risk        = RiskLevel.Medium,
        SsdOnly     = ssdOnly,
        Presets     = new List<Preset> { Preset.Advanced },
        Detect      = new DetectBlock
        {
            Service = new ServiceDetect
            {
                Name              = "SysMain",
                ExpectedStartType = "Disabled",
                MediaType         = "ssd"
            }
        },
        Apply = new List<ApplyStep>
        {
            new() { Type = "service_set", ServiceName = "SysMain", StartType = "Disabled" }
        },
        Explain = new ExplainBlock
        {
            What    = "Precarga apps en RAM.",
            Why     = "En SSD moderno no aporta beneficio real.",
            Changes = "Cambia SysMain a Deshabilitado."
        }
    };

    private static ServiceInfo RunningAutomatic => new()
    {
        Name        = "SysMain",
        DisplayName = "SysMain (Superfetch)",
        StartType   = ServiceStartMode.Automatic,
        Status      = ServiceCurrentStatus.Running
    };

    private static ServiceInfo RunningDisabled => new()
    {
        Name        = "SysMain",
        DisplayName = "SysMain (Superfetch)",
        StartType   = ServiceStartMode.Disabled,
        Status      = ServiceCurrentStatus.Stopped
    };

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SsdOnly_OnHdd_RuleIsSkipped()
    {
        var rule     = MakeSysMainRule(ssdOnly: true);
        var provider = new FakeRuleProvider(new List<RuleDefinition> { rule });
        var services = new FakeServiceManager(new Dictionary<string, ServiceInfo>
        {
            ["SysMain"] = RunningAutomatic
        });
        var media   = new FakeDriveMediaService(isSsd: false);   // HDD
        var scanner = new ServiceScanner(provider, services, media);

        var findings = await scanner.ScanAsync();

        // La regla ssd_only NO debe aparecer en un equipo con HDD
        Assert.Empty(findings);
    }

    [Fact]
    public async Task SsdOnly_OnSsd_RuleIsShown_Detected()
    {
        var rule     = MakeSysMainRule(ssdOnly: true);
        var provider = new FakeRuleProvider(new List<RuleDefinition> { rule });
        var services = new FakeServiceManager(new Dictionary<string, ServiceInfo>
        {
            ["SysMain"] = RunningAutomatic   // Running → Detected (no está en Disabled)
        });
        var media   = new FakeDriveMediaService(isSsd: true);    // SSD
        var scanner = new ServiceScanner(provider, services, media);

        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        var f = findings[0];
        Assert.Equal("services.sysmain", f.Id);
        Assert.Equal(FindingStatus.Detected, f.Status);
    }

    [Fact]
    public async Task SsdOnly_OnSsd_AlreadyDisabled_IsAlreadyOptimal()
    {
        var rule     = MakeSysMainRule(ssdOnly: true);
        var provider = new FakeRuleProvider(new List<RuleDefinition> { rule });
        var services = new FakeServiceManager(new Dictionary<string, ServiceInfo>
        {
            ["SysMain"] = RunningDisabled    // Disabled → AlreadyOptimal
        });
        var media   = new FakeDriveMediaService(isSsd: true);
        var scanner = new ServiceScanner(provider, services, media);

        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal(FindingStatus.AlreadyOptimal, findings[0].Status);
    }

    [Fact]
    public async Task WithoutMediaService_SsdOnlyRule_IsStillShown()
    {
        // Si no se inyecta IDriveMediaService, las reglas ssd_only se muestran siempre
        // (comportamiento seguro: no podemos saber el tipo de disco)
        var rule     = MakeSysMainRule(ssdOnly: true);
        var provider = new FakeRuleProvider(new List<RuleDefinition> { rule });
        var services = new FakeServiceManager(new Dictionary<string, ServiceInfo>
        {
            ["SysMain"] = RunningAutomatic
        });
        var scanner = new ServiceScanner(provider, services, driveMediaService: null);

        var findings = await scanner.ScanAsync();

        // Sin info de media, no se puede filtrar → se muestra
        Assert.Single(findings);
        Assert.Equal("services.sysmain", findings[0].Id);
    }

    [Fact]
    public async Task NonSsdOnlyRule_OnHdd_IsShown()
    {
        // Una regla sin ssd_only debe aparecer siempre, independientemente del disco
        var rule = new RuleDefinition
        {
            Id      = "services.diagtrack",
            Name    = "Desactivar Telemetría",
            Category = Category.Services,
            Risk    = RiskLevel.Low,
            SsdOnly = false,   // No es ssd_only
            Presets = new List<Preset> { Preset.Minimal, Preset.Standard },
            Detect  = new DetectBlock
            {
                Service = new ServiceDetect
                {
                    Name              = "DiagTrack",
                    ExpectedStartType = "Disabled"
                }
            },
            Explain = new ExplainBlock
            {
                What    = "Servicio de telemetría.",
                Why     = "Envía datos a Microsoft.",
                Changes = "Deshabilita DiagTrack."
            }
        };

        var provider = new FakeRuleProvider(new List<RuleDefinition> { rule });
        var services = new FakeServiceManager(new Dictionary<string, ServiceInfo>
        {
            ["DiagTrack"] = new() { Name = "DiagTrack", DisplayName = "Telemetría", StartType = ServiceStartMode.Automatic, Status = ServiceCurrentStatus.Running }
        });
        var media   = new FakeDriveMediaService(isSsd: false);   // HDD
        var scanner = new ServiceScanner(provider, services, media);

        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal("services.diagtrack", findings[0].Id);
        Assert.Equal(FindingStatus.Detected, findings[0].Status);
    }

    [Fact]
    public async Task ExplanationText_OnSsd_ContainsConfirmationHint()
    {
        var rule     = MakeSysMainRule(ssdOnly: true);
        var provider = new FakeRuleProvider(new List<RuleDefinition> { rule });
        var services = new FakeServiceManager(new Dictionary<string, ServiceInfo>
        {
            ["SysMain"] = RunningAutomatic
        });
        var media   = new FakeDriveMediaService(isSsd: true);
        var scanner = new ServiceScanner(provider, services, media);

        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        var why = findings[0].Explanation?.Why ?? string.Empty;
        Assert.Contains("SSD", why, StringComparison.OrdinalIgnoreCase);
    }
}
