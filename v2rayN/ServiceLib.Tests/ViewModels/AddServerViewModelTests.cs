using System.Reflection;
using ReactiveUI.Builder;
using ServiceLib.Enums;
using ServiceLib.Models.Entities;
using ServiceLib.Resx;
using ServiceLib.Tests.CoreConfig;
using ServiceLib.ViewModels;

namespace ServiceLib.Tests.ViewModels;

[NotInParallel]
public class AddServerViewModelTests
{
    static AddServerViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();
    }

    [Test]
    public async Task XdriveServices_ShouldContainExpectedServices()
    {
        await Global.XdriveServices.Count.Should().BeEqualTo(4);
        await Global.XdriveServices[0].Should().BeEqualTo("google-drive");
        await Global.XdriveServices[1].Should().BeEqualTo("onedrive");
        await Global.XdriveServices[2].Should().BeEqualTo("s3");
        await Global.XdriveServices[3].Should().BeEqualTo("local");
    }

    [Test]
    public async Task AddServerViewModel_ShouldBindXdrivePropertiesFromTransportExtra()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            ConfigType = EConfigType.VLESS,
            Address = "example.com",
            Port = 443,
            Network = nameof(ETransport.xdrive),
        };
        profile.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "s3",
            XdriveRemoteFolder = "/remote/path",
            XdriveSecrets = "sec1,sec2",
            XdriveS3Endpoint = "s3.example.com",
            XdriveS3Region = "us-west-2",
            XdriveS3Bucket = "my-bucket",
            XdriveS3PathStyle = true,
            XdriveExtra = "{\"concurrency\":4}",
        });

        var vm = new AddServerViewModel(profile);

        await vm.XdriveService.Should().BeEqualTo("s3");
        await vm.XdriveRemoteFolder.Should().BeEqualTo("/remote/path");
        await vm.XdriveSecrets.Should().BeEqualTo("sec1,sec2");
        await vm.XdriveS3Endpoint.Should().BeEqualTo("s3.example.com");
        await vm.XdriveS3Region.Should().BeEqualTo("us-west-2");
        await vm.XdriveS3Bucket.Should().BeEqualTo("my-bucket");
        await vm.XdriveS3PathStyle.Should().BeEqualTo(true);
        await vm.XdriveExtra.Should().BeEqualTo("{\"concurrency\":4}");
    }

    [Test]
    public async Task AddServerViewModel_TransportFriendlyProperties_ShouldAdaptToXdrive()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            ConfigType = EConfigType.VLESS,
            Address = "example.com",
            Port = 443,
            Network = nameof(ETransport.xdrive),
        };
        profile.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "s3",
            XdriveRemoteFolder = "/folder",
            XdriveS3Endpoint = "s3.custom.endpoint",
            XdriveExtra = "{\"segmentBytes\":1024}",
        });

        var vm = new AddServerViewModel(profile);

        // Getters
        await vm.TransportHeaderType.Should().BeEqualTo("s3");
        await vm.TransportHost.Should().BeEqualTo("s3.custom.endpoint");
        await vm.TransportPath.Should().BeEqualTo("/folder");
        await vm.TransportExtraText.Should().BeEqualTo("{\"segmentBytes\":1024}");

        // Setters
        vm.TransportHeaderType = "google-drive";
        await vm.XdriveService.Should().BeEqualTo("google-drive");
        await vm.TransportHeaderType.Should().BeEqualTo("google-drive");

        vm.TransportHost = "s3.new.endpoint";
        await vm.XdriveS3Endpoint.Should().BeEqualTo("s3.new.endpoint");
        await vm.TransportHost.Should().BeEqualTo("s3.new.endpoint");

        vm.TransportPath = "/new/folder";
        await vm.XdriveRemoteFolder.Should().BeEqualTo("/new/folder");
        await vm.TransportPath.Should().BeEqualTo("/new/folder");

        vm.TransportExtraText = "{\"flushIntervalMs\":500}";
        await vm.XdriveExtra.Should().BeEqualTo("{\"flushIntervalMs\":500}");
        await vm.TransportExtraText.Should().BeEqualTo("{\"flushIntervalMs\":500}");
    }

    [Test]
    public async Task AddServerViewModel_SaveServerAsync_ShouldPersistXdriveFields()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            Remarks = "test remarks",
            ConfigType = EConfigType.VLESS,
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.xdrive),
        };

        var vm = new AddServerViewModel(profile);
        vm.XdriveService = "s3";
        vm.XdriveRemoteFolder = "/backup";
        vm.XdriveSecrets = "key,secret";
        vm.XdriveS3Endpoint = "s3.amazonaws.com";
        vm.XdriveS3Region = "eu-central-1";
        vm.XdriveS3Bucket = "bucket-1";
        vm.XdriveS3PathStyle = false;
        vm.XdriveExtra = "{\"concurrency\":8}";

        var saveMethod = typeof(AddServerViewModel).GetMethod("SaveServerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)saveMethod.Invoke(vm, null)!;

        var extra = vm.SelectedSource.GetTransportExtra();
        await extra.XdriveService.Should().BeEqualTo("s3");
        await extra.XdriveRemoteFolder.Should().BeEqualTo("/backup");
        await extra.XdriveSecrets.Should().BeEqualTo("key,secret");
        await extra.XdriveS3Endpoint.Should().BeEqualTo("s3.amazonaws.com");
        await extra.XdriveS3Region.Should().BeEqualTo("eu-central-1");
        await extra.XdriveS3Bucket.Should().BeEqualTo("bucket-1");
        await extra.XdriveS3PathStyle.Should().BeEqualTo(false);
        await extra.XdriveExtra.Should().BeEqualTo("{\"concurrency\":8}");
    }

    [Test]
    public async Task AddServerViewModel_ApplyXdriveProviderPreset_ShouldConfigureOracleAndB2()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            ConfigType = EConfigType.VLESS,
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.xdrive),
        };

        var vm = new AddServerViewModel(profile);

        vm.XdriveProvider = "Oracle Cloud S3";
        await vm.XdriveService.Should().BeEqualTo("s3");
        await vm.XdriveS3Region.Should().BeEqualTo("ap-chuncheon-1");
        await vm.XdriveS3PathStyle.Should().BeEqualTo(true);

        vm.XdriveS3Region = string.Empty;
        vm.XdriveExtra = string.Empty;
        vm.XdriveProvider = "Backblaze B2";
        await vm.XdriveService.Should().BeEqualTo("s3");
        await vm.XdriveS3Region.Should().BeEqualTo("us-west-004");
        await vm.XdriveS3PathStyle.Should().BeEqualTo(false);
        await vm.XdriveExtra.Should().Contain("\"pollIntervalMs\":500");
    }

    [Test]
    public async Task AddServerViewModel_ApplyXdriveProviderPreset_ShouldConfigureCloudflareR2TuningParameters()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            ConfigType = EConfigType.VLESS,
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.xdrive),
        };
        profile.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "google-drive",
        });

        var vm = new AddServerViewModel(profile);
        await vm.XdriveExtra.Should().BeEmpty();

        vm.XdriveProvider = "Cloudflare R2";
        await vm.XdriveService.Should().BeEqualTo("s3");
        await vm.XdriveS3Region.Should().BeEqualTo("auto");
        await vm.XdriveS3PathStyle.Should().BeEqualTo(true);
        await vm.XdriveExtra.Should().BeEqualTo("{\"concurrency\":16,\"flushIntervalMs\":30,\"maxPollIntervalMs\":400,\"segmentBytes\":524288,\"eagerWindowMs\":1500}");
    }

    [Test]
    public async Task AddServerViewModel_ApplyXdriveProviderPreset_ShouldConfigureOneDrive()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            ConfigType = EConfigType.VLESS,
            Address = string.Empty,
            Port = 0,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.xdrive),
        };
        profile.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "s3",
        });

        var vm = new AddServerViewModel(profile);
        vm.XdriveProvider = "Microsoft OneDrive";

        await vm.XdriveService.Should().BeEqualTo("onedrive");
        await vm.SelectedSource.Address.Should().BeEqualTo("graph.microsoft.com");
        await vm.SelectedSource.Port.Should().BeEqualTo(443);
        await vm.SelectedSource.Sni.Should().BeEqualTo("graph.microsoft.com");
        await vm.SelectedSource.Alpn.Should().BeEqualTo("h2");
        await vm.XdriveExtra.Should().Contain("\"concurrency\":4");
        await vm.XdriveExtra.Should().Contain("\"segmentBytes\":65536");
        await vm.XdriveExtra.Should().Contain("\"flushIntervalMs\":30");
        await vm.XdriveExtra.Should().Contain("\"pollIntervalMs\":100");
        await vm.XdriveExtra.Should().Contain("\"maxPollIntervalMs\":1000");
    }

    [Test]
    public async Task AddServerViewModel_ShouldResolveOneDriveProviderFromTransportExtra()
    {
        CoreConfigTestFactory.BindAppManagerConfig(CoreConfigTestFactory.CreateConfig());

        var profile = new ProfileItem
        {
            IndexId = "test-node",
            ConfigType = EConfigType.VLESS,
            Address = "graph.microsoft.com",
            Port = 443,
            Network = nameof(ETransport.xdrive),
        };
        profile.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "onedrive",
        });

        var vm = new AddServerViewModel(profile);

        await vm.XdriveService.Should().BeEqualTo("onedrive");
        await vm.XdriveProvider.Should().BeEqualTo("Microsoft OneDrive");
    }

    [Test]
    public async Task Localization_ShouldProvideXdriveStrings()
    {
        await ResUI.XdriveService.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveRemoteFolder.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveSecrets.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveS3Endpoint.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveS3Region.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveS3Bucket.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveS3PathStyle.IsNotEmpty().Should().BeTrue();
        await ResUI.XdriveExtra.IsNotEmpty().Should().BeTrue();
    }
}
