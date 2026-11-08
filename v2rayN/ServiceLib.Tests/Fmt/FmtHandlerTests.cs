namespace ServiceLib.Tests.Fmt;

public class FmtHandlerTests
{
    /// <summary>
    /// One profile factory per protocol that <see cref="FmtHandler.GetShareUri" /> can export.
    /// The suite below asserts that this map and <see cref="Global.ProtocolShares" /> agree, so a
    /// newly exportable protocol cannot be added without a round-trip case.
    /// </summary>
    private static readonly Dictionary<EConfigType, Func<ProfileItem>> ShareProfileFactories = new()
    {
        [EConfigType.VMess] = CreateVmessProfile,
        [EConfigType.Shadowsocks] = CreateShadowsocksProfile,
        [EConfigType.SOCKS] = CreateSocksProfile,
        [EConfigType.VLESS] = CreateVlessProfile,
        [EConfigType.Trojan] = CreateTrojanProfile,
        [EConfigType.Hysteria2] = CreateHysteria2Profile,
        [EConfigType.TUIC] = CreateTuicProfile,
        [EConfigType.WireGuard] = CreateWireguardProfile,
        [EConfigType.Anytls] = CreateAnytlsProfile,
        [EConfigType.Naive] = () => CreateNaiveProfile(false),
    };

    [Test]
    public async Task ShareUriSuite_ShouldCoverAndRoundTripEveryExportableProtocol()
    {
        var uncovered = string.Join(", ", Global.ProtocolShares.Keys.Except(ShareProfileFactories.Keys));
        var unexpected = string.Join(", ", ShareProfileFactories.Keys.Except(Global.ProtocolShares.Keys));

        await uncovered.Should().BeEqualTo(string.Empty);
        await unexpected.Should().BeEqualTo(string.Empty);

        foreach (var (configType, factory) in ShareProfileFactories)
        {
            var source = factory();

            var resolved = await ExportThenImport(source);

            await resolved.ConfigType.Should().BeEqualTo(configType);
            await resolved.Address.Should().BeEqualTo(source.Address);
            await resolved.Port.Should().BeEqualTo(source.Port);
        }
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Vmess_ShouldRoundTripBasicFields()
    {
        var source = CreateVmessProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.VMess);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().AlterId.Should().BeEqualTo(source.GetProtocolExtra().AlterId);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Vless_ShouldRoundTripBasicFields()
    {
        var source = CreateVlessProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.VLESS);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().VlessEncryption.Should().BeEqualTo(Global.None);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Shadowsocks_ShouldRoundTripBasicFields()
    {
        var source = CreateShadowsocksProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.Shadowsocks);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().SsMethod.Should().BeEqualTo(source.GetProtocolExtra().SsMethod);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Socks_ShouldRoundTripBasicFields()
    {
        var source = CreateSocksProfile();

        var resolved = await ExportThenImport(source);

        await resolved.ConfigType.Should().BeEqualTo(EConfigType.SOCKS);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Trojan_ShouldRoundTripBasicFields()
    {
        var source = CreateTrojanProfile();

        var resolved = await ExportThenImport(source);

        await AssertCommonShareFields(source, resolved);
        await AssertRawTransportFields(source, resolved);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Sni.Should().BeEqualTo(source.Sni);
        await resolved.GetProtocolExtra().Flow.Should().BeEqualTo(source.GetProtocolExtra().Flow);
        await resolved.GetAllowInsecure().Should().BeTrue();

        // Trojan is the one exporter that writes both spellings of the flag.
        await AssertExportContains(source, "allowInsecure=1", "insecure=1");
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Tuic_ShouldRoundTripUserInfoAndCongestionControl()
    {
        var source = CreateTuicProfile();

        var resolved = await ExportThenImport(source);

        await AssertCommonShareFields(source, resolved);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Sni.Should().BeEqualTo(source.Sni);
        await resolved.Alpn.Should().BeEqualTo(source.Alpn);
        await resolved.GetProtocolExtra().CongestionControl.Should()
            .BeEqualTo(source.GetProtocolExtra().CongestionControl);
        await resolved.GetAllowInsecure().Should().BeTrue();

        await AssertExportContains(source, "allow_insecure=1");
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Anytls_ShouldRoundTripBasicFields()
    {
        var source = CreateAnytlsProfile();

        var resolved = await ExportThenImport(source);

        await AssertCommonShareFields(source, resolved);
        await AssertRawTransportFields(source, resolved);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Sni.Should().BeEqualTo(source.Sni);
        await resolved.Alpn.Should().BeEqualTo(source.Alpn);
        await resolved.GetAllowInsecure().Should().BeTrue();

        await AssertExportContains(source, "insecure=1");
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Hysteria2_ShouldRoundTripObfsAndNormalizePortRange()
    {
        var source = CreateHysteria2Profile();

        var resolved = await ExportThenImport(source);
        var sourceExtra = source.GetProtocolExtra();
        var resolvedExtra = resolved.GetProtocolExtra();

        await AssertCommonShareFields(source, resolved);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Sni.Should().BeEqualTo(source.Sni);
        await resolved.Alpn.Should().BeEqualTo(source.Alpn);
        await resolved.EchConfigList.Should().BeEqualTo(source.EchConfigList);
        await resolved.GetAllowInsecure().Should().BeTrue();
        await resolvedExtra.SalamanderPass.Should().BeEqualTo(sourceExtra.SalamanderPass);

        // Hysteria2Fmt stores a port range internally as "5000:6000" and emits the URI form.
        await resolvedExtra.Ports.Should().BeEqualTo("5000-6000");

        await AssertExportContains(source, "insecure=1", "obfs=salamander", "mport=5000-6000");
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Wireguard_ShouldRoundTripKeysAndInterface()
    {
        var source = CreateWireguardProfile();

        var resolved = await ExportThenImport(source);
        var extra = resolved.GetProtocolExtra();
        var sourceExtra = source.GetProtocolExtra();

        await AssertCommonShareFields(source, resolved);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await extra.WgPublicKey.Should().BeEqualTo(sourceExtra.WgPublicKey);
        await extra.WgPresharedKey.Should().BeEqualTo(sourceExtra.WgPresharedKey);
        await extra.WgReserved.Should().BeEqualTo(sourceExtra.WgReserved);
        await extra.WgInterfaceAddress.Should().BeEqualTo(sourceExtra.WgInterfaceAddress);
        await extra.WgMtu.Should().BeEqualTo(sourceExtra.WgMtu);
    }

    [Test]
    public async Task GetShareUri_Wireguard_ShouldEncodeKeysAndBracketIpv6()
    {
        var source = CreateWireguardProfile();

        // Base64 keys carry '/', '+' and '=', and the address is an IPv6 literal: both have to
        // survive the wire form, which a round trip through the same encoder would not prove.
        await AssertExportContains(
            source,
            Uri.EscapeDataString(source.Password),
            Uri.EscapeDataString(source.GetProtocolExtra().WgPublicKey ?? string.Empty),
            "@[2001:db8::40]:51820");
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_Naive_ShouldRoundTripCredentialsOverHttps()
    {
        var source = CreateNaiveProfile(false);

        var resolved = await ExportThenImport(source, Global.NaiveHttpsProtocolShare);

        await AssertCommonShareFields(source, resolved);
        await AssertRawTransportFields(source, resolved);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().InsecureConcurrency.Should()
            .BeEqualTo(source.GetProtocolExtra().InsecureConcurrency);

        // NaiveFmt only ever sets this flag on the quic branch, so the https branch leaves it
        // unset rather than false - assert "is not quic" instead of an explicit false.
        await (resolved.GetProtocolExtra().NaiveQuic == true).Should().BeFalse();
    }

    [Test]
    public async Task GetShareUriAndResolveConfig_NaiveQuic_ShouldRoundTripQuicScheme()
    {
        var source = CreateNaiveProfile(true);

        var resolved = await ExportThenImport(source, Global.NaiveQuicProtocolShare);

        await AssertCommonShareFields(source, resolved);
        await AssertRawTransportFields(source, resolved);
        await resolved.Username.Should().BeEqualTo(source.Username);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.GetProtocolExtra().InsecureConcurrency.Should()
            .BeEqualTo(source.GetProtocolExtra().InsecureConcurrency);
        await resolved.GetProtocolExtra().NaiveQuic.Should().BeTrue();
    }

    [Test]
    [Arguments("p:a@ss#% +/=")]
    [Arguments("пароль 東京")]
    public async Task GetShareUriAndResolveConfig_Trojan_ShouldRoundTripEncodedCredentials(string password)
    {
        var source = CreateTrojanProfile();
        source.Password = password;
        source.Remarks = "Trojan — тест 東京 #1";

        var resolved = await ExportThenImport(source);

        await resolved.Password.Should().BeEqualTo(password);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
    }

    [Test]
    public async Task ResolveConfig_UnsupportedProtocol_ShouldReturnNull()
    {
        var resolved = FmtHandler.ResolveConfig("not-a-share-uri", out var msg);

        await resolved.Should().BeNull();
        await msg.Should().NotBeNull();
        await msg.Should().NotBeEmpty();
    }

    [Test]
    public async Task GetShareUri_UnsupportedConfigType_ShouldReturnNull()
    {
        var item = new ProfileItem { ConfigType = EConfigType.PolicyGroup, Remarks = "group", };

        var uri = FmtHandler.GetShareUri(item);

        await uri.Should().BeNull();
    }

    private static async Task AssertCommonShareFields(ProfileItem source, ProfileItem resolved)
    {
        await resolved.ConfigType.Should().BeEqualTo(source.ConfigType);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
    }

    /// <summary>
    /// Only for protocols whose exporter goes through the shared transport query
    /// (<c>security</c>, <c>type</c>, <c>headerType</c>). TUIC, Hysteria2 and WireGuard do not.
    /// </summary>
    private static async Task AssertRawTransportFields(ProfileItem source, ProfileItem resolved)
    {
        await resolved.Network.Should().BeEqualTo(source.Network);
        await resolved.StreamSecurity.Should().BeEqualTo(source.StreamSecurity);
        await resolved.GetTransportExtra().RawHeaderType.Should()
            .BeEqualTo(source.GetTransportExtra().RawHeaderType);
    }

    /// <summary>
    /// Asserts on the wire form itself. A round trip cannot catch an exporter and an importer that
    /// agree on the wrong spelling of a parameter, and the insecure flag is spelled differently by
    /// every protocol.
    /// </summary>
    private static async Task AssertExportContains(ProfileItem source, params string[] expectedFragments)
    {
        var uri = FmtHandler.GetShareUri(source);

        await uri.Should().NotBeNull();

        foreach (var fragment in expectedFragments)
        {
            await uri!.Contains(fragment, StringComparison.Ordinal).Should()
                .BeTrue().Because($"uri: {uri}, expected fragment: {fragment}");
        }
    }

    private static string ExpectedShareScheme(ProfileItem item)
    {
        if (item.ConfigType != EConfigType.Naive)
        {
            return Global.ProtocolShares[item.ConfigType];
        }

        // NaiveFmt never emits the "naive://" prefix that Global.ProtocolShares records for the
        // type; that entry is only read when importing.
        return item.GetProtocolExtra().NaiveQuic == true
            ? Global.NaiveQuicProtocolShare
            : Global.NaiveHttpsProtocolShare;
    }

    private static async Task<ProfileItem> ExportThenImport(ProfileItem source)
    {
        return await ExportThenImport(source, ExpectedShareScheme(source));
    }

    private static async Task<ProfileItem> ExportThenImport(ProfileItem source, string expectedPrefix)
    {
        var uri = FmtHandler.GetShareUri(source);

        await uri.Should().NotBeNull();
        await uri.Should().NotBeEmpty();
        await uri!.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase).Should().BeTrue();

        var resolved = FmtHandler.ResolveConfig(uri, out var msg);

        await resolved.Should().NotBeNull().Because($"uri: {uri}, msg: {msg}");
        return resolved!;
    }

    private static ProfileItem CreateVmessProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VMess,
            Remarks = "vmess demo",
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { AlterId = "0", VmessSecurity = Global.DefaultSecurity, });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateVlessProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.VLESS,
            Remarks = "vless demo",
            Address = "vless.example",
            Port = 8443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { VlessEncryption = Global.None, });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateShadowsocksProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Shadowsocks,
            Remarks = "ss demo",
            Address = "1.2.3.4",
            Port = 8388,
            Password = "pass123",
            Network = nameof(ETransport.raw),
            StreamSecurity = string.Empty,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { SsMethod = "aes-128-gcm", });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateSocksProfile()
    {
        return new ProfileItem
        {
            ConfigType = EConfigType.SOCKS,
            Remarks = "socks demo",
            Address = "127.0.0.1",
            Port = 1080,
            Username = "user",
            Password = "pass",
        };
    }

    private static ProfileItem CreateTrojanProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Trojan,
            Remarks = "trojan demo",
            Address = "trojan.example",
            Port = 443,
            Password = "trojan-pass",
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.StreamSecurity,
            Sni = "sni.trojan.example",
            AllowInsecure = Global.StringTrue,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { Flow = Global.Flows[1], });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateTuicProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.TUIC,
            Remarks = "tuic demo",
            Address = "tuic.example",
            Port = 8443,
            // A colon separates the two halves of the TUIC user info, so it cannot appear in the
            // uuid; a fixed value also keeps a failure reproducible.
            Username = "01234567-89ab-cdef-0123-456789abcdef",
            Password = "tuic-pass",
            Sni = "sni.tuic.example",
            Alpn = "h3",
            AllowInsecure = Global.StringTrue,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { CongestionControl = "bbr", });

        return item;
    }

    private static ProfileItem CreateAnytlsProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Anytls,
            Remarks = "anytls demo",
            Address = "anytls.example",
            Port = 8443,
            Password = "anytls-pass",
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.StreamSecurity,
            Sni = "sni.anytls.example",
            Alpn = "h2,http/1.1",
            AllowInsecure = Global.StringTrue,
        };

        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    private static ProfileItem CreateHysteria2Profile()
    {
        // CertSha is deliberately left unset: the importer turns AllowInsecure on by itself when a
        // pinSHA256 is present, which would mask an exporter that stopped emitting insecure=1.
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Hysteria2,
            Remarks = "hysteria2 demo",
            Address = "hy2.example",
            Port = 8443,
            Password = "demo-user:demo-pass",
            Sni = "sni.hy2.example",
            Alpn = "h3",
            EchConfigList = "AAj+DQAEAAAAAA==",
            AllowInsecure = Global.StringTrue,
        };

        item.SetProtocolExtra(new ProtocolExtraItem
        {
            SalamanderPass = "salamander-pass",
            Ports = "5000:6000",
        });

        return item;
    }

    private static string CreateWireguardKey(byte value)
    {
        return Convert.ToBase64String(Enumerable.Repeat(value, 32).ToArray());
    }

    private static ProfileItem CreateWireguardProfile()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.WireGuard,
            Remarks = "WireGuard — тест 東京 #1",
            Address = "2001:db8::40",
            Port = 51820,
            Password = CreateWireguardKey(0xFE),
        };

        item.SetProtocolExtra(new ProtocolExtraItem
        {
            WgPublicKey = CreateWireguardKey(0xFD),
            WgPresharedKey = CreateWireguardKey(0xFC),
            WgReserved = "1,2,255",
            WgInterfaceAddress = "10.0.0.2/32,fd00::2/128",
            WgMtu = 1420,
        });

        return item;
    }

    private static ProfileItem CreateNaiveProfile(bool quic)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Naive,
            Remarks = quic ? "naive quic demo" : "naive https demo",
            Address = "naive.example",
            Port = 443,
            Username = "naive-user",
            Password = "päss:word@/?#&=+ 東京",
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.None,
        };

        item.SetProtocolExtra(new ProtocolExtraItem { NaiveQuic = quic, InsecureConcurrency = 4, });
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }

    [Test]
    public async Task ResolveConfig_Xdrive_GoogleDrive_ShouldParseAllFields()
    {
        var uri = "vless://11111111-2222-3333-4444-555555555555@www.google.com:443?type=xdrive&service=google-drive&remoteFolder=test_folder&encryption=test_enc_key&security=tls&sni=www.google.com&alpn=h2&mux=true&secrets=gdrive_token#Aegis-GDrive";

        var item = FmtHandler.ResolveConfig(uri, out var msg);

        await item.Should().NotBeNull();
        await msg.Should().BeEmpty();
        await item!.ConfigType.Should().BeEqualTo(EConfigType.VLESS);
        await item.Address.Should().BeEqualTo("www.google.com");
        await item.Port.Should().BeEqualTo(443);
        await item.Password.Should().BeEqualTo("11111111-2222-3333-4444-555555555555");
        await item.Remarks.Should().BeEqualTo("Aegis-GDrive");
        await item.Network.Should().BeEqualTo(nameof(ETransport.xdrive));
        await item.StreamSecurity.Should().BeEqualTo(Global.StreamSecurity);
        await item.Sni.Should().BeEqualTo("www.google.com");
        await item.Alpn.Should().BeEqualTo("h2");
        await item.MuxEnabled.Should().BeEqualTo(true);
        await item.GetProtocolExtra().VlessEncryption.Should().BeEqualTo("test_enc_key");

        var transport = item.GetTransportExtra();
        await transport.XdriveService.Should().BeEqualTo("google-drive");
        await transport.XdriveRemoteFolder.Should().BeEqualTo("test_folder");
        await transport.XdriveSecrets.Should().BeEqualTo("gdrive_token");
    }

    [Test]
    public async Task ResolveConfig_Xdrive_S3_ShouldParseAllFields()
    {
        var uri = "vless://11111111-2222-3333-4444-555555555555@127.0.0.1:10080?type=xdrive&service=s3&remoteFolder=r2_folder&encryption=test_enc_key&mux=true&endpoint=https%3A%2F%2Faccount.r2.cloudflarestorage.com&bucket=mybucket&region=auto&pathStyle=false&secrets=ak123%2Csk456#Aegis-R2";

        var item = FmtHandler.ResolveConfig(uri, out var msg);

        await item.Should().NotBeNull();
        await msg.Should().BeEmpty();
        await item!.ConfigType.Should().BeEqualTo(EConfigType.VLESS);
        await item.Address.Should().BeEqualTo("127.0.0.1");
        await item.Port.Should().BeEqualTo(10080);
        await item.Password.Should().BeEqualTo("11111111-2222-3333-4444-555555555555");
        await item.Remarks.Should().BeEqualTo("Aegis-R2");
        await item.Network.Should().BeEqualTo(nameof(ETransport.xdrive));
        await item.MuxEnabled.Should().BeEqualTo(true);
        await item.GetProtocolExtra().VlessEncryption.Should().BeEqualTo("test_enc_key");

        var transport = item.GetTransportExtra();
        await transport.XdriveService.Should().BeEqualTo("s3");
        await transport.XdriveRemoteFolder.Should().BeEqualTo("r2_folder");
        await transport.XdriveS3Endpoint.Should().BeEqualTo("https://account.r2.cloudflarestorage.com");
        await transport.XdriveS3Bucket.Should().BeEqualTo("mybucket");
        await transport.XdriveS3Region.Should().BeEqualTo("auto");
        await transport.XdriveS3PathStyle.Should().BeEqualTo(false);
        await transport.XdriveSecrets.Should().BeEqualTo("ak123,sk456");
    }

    [Test]
    public async Task GetShareUri_Xdrive_ShouldRoundTrip()
    {
        var source = new ProfileItem
        {
            ConfigType = EConfigType.VLESS,
            Address = "127.0.0.1",
            Port = 10080,
            Password = "11111111-2222-3333-4444-555555555555",
            Remarks = "Xdrive RoundTrip",
            Network = nameof(ETransport.xdrive),
            MuxEnabled = true,
        };
        source.SetProtocolExtra(new ProtocolExtraItem
        {
            VlessEncryption = "0123456789abcdef0123456789abcdef",
        });
        source.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "s3",
            XdriveRemoteFolder = "my_folder",
            XdriveSecrets = "AK_TEST,SK_TEST",
            XdriveS3Endpoint = "https://s3.example.com",
            XdriveS3Region = "us-east-1",
            XdriveS3Bucket = "testbucket",
            XdriveS3PathStyle = true,
        });

        var uri = FmtHandler.GetShareUri(source);
        await uri.Should().NotBeNull();
        await uri.Should().NotBeEmpty();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);
        await resolved.Should().NotBeNull();
        await msg.Should().BeEmpty();
        await resolved!.Address.Should().BeEqualTo(source.Address);
        await resolved.Port.Should().BeEqualTo(source.Port);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Network.Should().BeEqualTo(nameof(ETransport.xdrive));
        await resolved.MuxEnabled.Should().BeEqualTo(true);
        await resolved.GetProtocolExtra().VlessEncryption.Should().BeEqualTo("0123456789abcdef0123456789abcdef");

        var transport = resolved.GetTransportExtra();
        await transport.XdriveService.Should().BeEqualTo("s3");
        await transport.XdriveRemoteFolder.Should().BeEqualTo("my_folder");
        await transport.XdriveSecrets.Should().BeEqualTo("AK_TEST,SK_TEST");
        await transport.XdriveS3Endpoint.Should().BeEqualTo("https://s3.example.com");
        await transport.XdriveS3Region.Should().BeEqualTo("us-east-1");
        await transport.XdriveS3Bucket.Should().BeEqualTo("testbucket");
        await transport.XdriveS3PathStyle.Should().BeEqualTo(true);
    }

    [Test]
    public async Task GetShareUri_Xdrive_GoogleDrive_ShouldRoundTrip()
    {
        var source = new ProfileItem
        {
            ConfigType = EConfigType.VLESS,
            Address = "www.google.com",
            Port = 443,
            Password = "11111111-2222-3333-4444-555555555555",
            Remarks = "Google Drive RoundTrip",
            Network = nameof(ETransport.xdrive),
            StreamSecurity = Global.StreamSecurity,
            Sni = "www.google.com",
            Alpn = "h2",
            MuxEnabled = true,
        };
        source.SetProtocolExtra(new ProtocolExtraItem
        {
            VlessEncryption = "0123456789abcdef0123456789abcdef",
        });
        source.SetTransportExtra(new TransportExtraItem
        {
            XdriveService = "google-drive",
            XdriveRemoteFolder = "my_gdrive_folder",
            XdriveSecrets = "token_abc_123",
        });

        var uri = FmtHandler.GetShareUri(source);
        await uri.Should().NotBeNull();
        await uri.Should().NotBeEmpty();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);
        await resolved.Should().NotBeNull();
        await msg.Should().BeEmpty();
        await resolved!.Address.Should().BeEqualTo("www.google.com");
        await resolved.Port.Should().BeEqualTo(443);
        await resolved.Password.Should().BeEqualTo(source.Password);
        await resolved.Remarks.Should().BeEqualTo(source.Remarks);
        await resolved.Network.Should().BeEqualTo(nameof(ETransport.xdrive));
        await resolved.StreamSecurity.Should().BeEqualTo(Global.StreamSecurity);
        await resolved.Sni.Should().BeEqualTo("www.google.com");
        await resolved.Alpn.Should().BeEqualTo("h2");
        await resolved.MuxEnabled.Should().BeEqualTo(true);
        await resolved.GetProtocolExtra().VlessEncryption.Should().BeEqualTo("0123456789abcdef0123456789abcdef");

        var transport = resolved.GetTransportExtra();
        await transport.XdriveService.Should().BeEqualTo("google-drive");
        await transport.XdriveRemoteFolder.Should().BeEqualTo("my_gdrive_folder");
        await transport.XdriveSecrets.Should().BeEqualTo("token_abc_123");
    }
}
