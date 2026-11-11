using System.Net.WebSockets;
using Taveloom.Host.Native;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host.Tests;

/// <summary>Drives a real host over real TLS WebSockets, exactly as the Android client will.</summary>
public class ConnectionTests
{
    [Fact]
    public async Task PairThenReconnectWithCredential()
    {
        await using var t = await TestHost.StartAsync();
        var (c, id, cred) = await t.PairAsync("Pixel");
        Assert.Single(t.Host.Devices.List());
        Assert.Equal("Pixel", t.Host.Devices.List()[0].Name);
        await c.DisposeAsync();

        await using var c2 = await t.LoginAsync(id, cred);   // challenge-response, credential never re-sent
        await Task.Delay(100);
        Assert.Equal(1, t.Host.Sessions.AuthenticatedCount);
    }

    [Fact]
    public async Task PairedStateContainsRealWorkspaces()
    {
        await using var t = await TestHost.StartAsync();
        var qr = t.Host.CreatePairing().Qr;
        await using var c = await TestClient.ConnectAsync(t.Host.Port, qr.Fingerprint);
        await c.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, t.NewDeviceId(), "P"));
        await c.ExpectAsync(MessageTypes.PairApproved);
        var s = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal(3, s.Workspaces.Count);
        Assert.NotNull(s.Active);
        Assert.Equal("Productivity", s.Active!.Name);            // the default workspace
        Assert.All(s.Active.Controls, c => Assert.True(c.Id.Length > 0));
    }

    [Fact]
    public async Task WrongPinnedCertificateIsRefusedByClient()
    {
        await using var t = await TestHost.StartAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => TestClient.ConnectAsync(t.Host.Port, new string('0', 64)));
    }

    [Fact]
    public async Task InvalidPairingTokenIsRejected()
    {
        await using var t = await TestHost.StartAsync();
        await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await c.SendAsync(MessageTypes.PairRequest, new PairRequestPayload("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", t.NewDeviceId(), "P"));
        var err = TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error));
        Assert.Equal(ErrorCodes.AuthFailed, err.Code);
        Assert.True(await c.WaitClosedAsync());
        Assert.Empty(t.Host.Devices.List());
    }

    [Fact]
    public async Task PairingTokenCannotBeReused()
    {
        await using var t = await TestHost.StartAsync();
        var qr = t.Host.CreatePairing().Qr;
        await using var c1 = await TestClient.ConnectAsync(t.Host.Port, qr.Fingerprint);
        await c1.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, t.NewDeviceId(), "One"));
        await c1.ExpectAsync(MessageTypes.PairApproved);
        await using var c2 = await TestClient.ConnectAsync(t.Host.Port, qr.Fingerprint);
        await c2.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, t.NewDeviceId(), "Two"));
        Assert.Equal(ErrorCodes.AuthFailed, TestClient.Read<ErrorPayload>(await c2.ExpectAsync(MessageTypes.Error)).Code);
        Assert.Single(t.Host.Devices.List());
    }

    [Fact]
    public async Task DeclinedPairingCreatesNoDevice()
    {
        await using var t = await TestHost.StartAsync(autoApprove: false);
        t.Host.Pairing.ApprovalRequested += p => t.Host.Pairing.Resolve(p.RequestId, false);
        var qr = t.Host.CreatePairing().Qr;
        await using var c = await TestClient.ConnectAsync(t.Host.Port, qr.Fingerprint);
        await c.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, t.NewDeviceId(), "P"));
        await c.ExpectAsync(MessageTypes.PairRejected);
        Assert.Empty(t.Host.Devices.List());
    }

    [Fact]
    public async Task PairingWithNobodyToApproveIsRefused()
    {
        await using var t = await TestHost.StartAsync(autoApprove: false);
        var qr = t.Host.CreatePairing().Qr;
        await using var c = await TestClient.ConnectAsync(t.Host.Port, qr.Fingerprint);
        await c.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, t.NewDeviceId(), "P"));
        await c.ExpectAsync(MessageTypes.PairRejected);
        Assert.Empty(t.Host.Devices.List());
    }

    [Fact]
    public async Task WrongProofFailsAndRepeatedFailuresAreRateLimited()
    {
        await using var t = await TestHost.StartAsync();
        var (c0, id, cred) = await t.PairAsync(); await c0.DisposeAsync();
        var bad = (byte[])cred.Clone(); bad[0] ^= 0xFF;

        for (var i = 0; i < 5; i++)
        {
            await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
            await c.SendAsync(MessageTypes.AuthHello, new AuthHelloPayload(id));
            var nonce = TestClient.Read<AuthChallengePayload>(await c.ExpectAsync(MessageTypes.AuthChallenge)).Nonce;
            await c.SendAsync(MessageTypes.AuthResponse, new AuthResponsePayload(AuthProof.Compute(bad, nonce, id)));
            Assert.Equal(ErrorCodes.AuthFailed, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error)).Code);
        }

        await using var blocked = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await blocked.SendAsync(MessageTypes.AuthHello, new AuthHelloPayload(id));
        Assert.Equal(ErrorCodes.RateLimited, TestClient.Read<ErrorPayload>(await blocked.ExpectAsync(MessageTypes.Error)).Code);
    }

    [Fact]
    public async Task UnknownDeviceIsRefused()
    {
        await using var t = await TestHost.StartAsync();
        await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await c.SendAsync(MessageTypes.AuthHello, new AuthHelloPayload("dev-doesnotexist1"));
        Assert.Equal(ErrorCodes.AuthFailed, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error)).Code);
    }

    [Fact]
    public async Task UnauthenticatedMessagesAreRefused()
    {
        await using var t = await TestHost.StartAsync();
        await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload("anything"), "r1");
        var err = TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error));
        Assert.Equal(ErrorCodes.Unauthenticated, err.Code);
        Assert.Empty(t.Input.Sent);
        await c.SendAsync(MessageTypes.WorkspaceSelect, new WorkspaceSelectPayload("x"));
        Assert.Equal(ErrorCodes.Unauthenticated, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error)).Code);
    }

    [Fact]
    public async Task UnauthenticatedConnectionTimesOut()
    {
        await using var t = await TestHost.StartAsync(authTimeout: TimeSpan.FromMilliseconds(300));
        await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        Assert.True(await c.WaitClosedAsync(4000));
    }

    [Fact]
    public async Task MalformedMessagesAreAnsweredAndRepeatOffendersDropped()
    {
        await using var t = await TestHost.StartAsync();
        await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await c.SendTextAsync("{ this is not json");
        Assert.Equal(ErrorCodes.InvalidMessage, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error)).Code);
        await c.SendTextAsync("{\"v\":7,\"type\":\"ping\",\"ts\":1}");
        Assert.Equal(ErrorCodes.InvalidMessage, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error)).Code);
        for (var i = 0; i < 4; i++) await c.SendTextAsync("garbage");
        Assert.True(await c.WaitClosedAsync());
    }

    [Fact]
    public async Task BinaryFramesAndOversizeMessagesCloseTheConnection()
    {
        await using var t = await TestHost.StartAsync();
        await using var bin = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await bin.SendRawAsync([1, 2, 3], WebSocketMessageType.Binary);
        Assert.True(await bin.WaitClosedAsync());

        await using var big = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await big.SendRawAsync(new byte[ProtocolInfo.MaxMessageBytes + 100]);
        Assert.True(await big.WaitClosedAsync());
    }

    [Fact]
    public async Task HeartbeatPingGetsPong()
    {
        await using var t = await TestHost.StartAsync();
        await using var c = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await c.SendRawAsync(Envelope.Empty(MessageTypes.Ping, "hb1").ToUtf8());
        Assert.Equal("hb1", (await c.ExpectAsync(MessageTypes.Pong)).Id);
    }

    [Fact]
    public async Task AbruptDisconnectReleasesSession()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync();
        Assert.Equal(1, t.Host.Sessions.AuthenticatedCount);
        c.Abort();
        for (var i = 0; i < 50 && t.Host.Sessions.AuthenticatedCount > 0; i++) await Task.Delay(100);
        Assert.Equal(0, t.Host.Sessions.AuthenticatedCount);
    }

    [Fact]
    public async Task HostRestartKeepsTrustedDevicesAndCertificate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "taveloom-restart-" + Guid.NewGuid().ToString("N"));
        string fp, id; byte[] cred;
        TaveloomHost Make() => new(new HostOptions { DataDir = dir, Port = 0, LanExposure = false, Foreground = new FakeForeground(), Input = new FakeInput(), Audio = new FakeAudio() });

        await using (var h1 = Make())
        {
            h1.Pairing.ApprovalRequested += p => h1.Pairing.Resolve(p.RequestId, true);
            await h1.StartAsync(); fp = h1.Fingerprint;
            var qr = h1.CreatePairing().Qr;
            await using var c = await TestClient.ConnectAsync(h1.Port, fp);
            id = "dev-" + Guid.NewGuid().ToString("N")[..12];
            await c.SendAsync(MessageTypes.PairRequest, new PairRequestPayload(qr.Token, id, "Phone"));
            cred = System.Buffers.Text.Base64Url.DecodeFromChars(TestClient.Read<PairApprovedPayload>(await c.ExpectAsync(MessageTypes.PairApproved)).Credential);
        }

        await using var h2 = Make();
        await h2.StartAsync();
        Assert.Equal(fp, h2.Fingerprint);                         // same pinned identity: existing phones keep working
        await using var again = await TestClient.ConnectAsync(h2.Port, fp);
        await again.SendAsync(MessageTypes.AuthHello, new AuthHelloPayload(id));
        var nonce = TestClient.Read<AuthChallengePayload>(await again.ExpectAsync(MessageTypes.AuthChallenge)).Nonce;
        await again.SendAsync(MessageTypes.AuthResponse, new AuthResponsePayload(AuthProof.Compute(cred, nonce, id)));
        await again.ExpectAsync(MessageTypes.AuthOk);
        try { Directory.Delete(dir, true); } catch { }
    }

    [Fact]
    public async Task RevokingDeviceDisconnectsItAndBlocksReconnect()
    {
        await using var t = await TestHost.StartAsync();
        var (c, id, cred) = await t.PairAsync();
        t.Host.Devices.Revoke(id);
        Assert.Equal(ErrorCodes.DeviceRevoked, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.DeviceRevoked)).Code);
        Assert.True(await c.WaitClosedAsync());

        await using var c2 = await TestClient.ConnectAsync(t.Host.Port, t.Host.Fingerprint);
        await c2.SendAsync(MessageTypes.AuthHello, new AuthHelloPayload(id));
        Assert.Equal(ErrorCodes.DeviceRevoked, TestClient.Read<ErrorPayload>(await c2.ExpectAsync(MessageTypes.Error)).Code);
        Assert.Null(t.Host.Devices.GetCredential(id));
        Assert.NotNull(cred);
    }

    [Fact]
    public async Task TelemetryStreamsOnlyRealValues()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        TelemetryPayload tel = null!;
        for (var i = 0; i < 4; i++) tel = TestClient.Read<TelemetryPayload>(await c.ExpectAsync(MessageTypes.Telemetry));
        Assert.Equal(40, tel.VolumePercent);                       // from the (fake) audio device, not invented
        Assert.Null(tel.MicMuted);                                 // unavailable => null, not false
        Assert.InRange(tel.RamPercent!.Value, 1, 100);
    }
}

public class ActionOverWireTests
{
    private static async Task<(TestHost t, TestClient c, ControlDef play, ControlDef vol)> SetupAsync()
    {
        var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync();
        var ws = t.Host.Workspaces.All.First(w => w.Name == "Media");
        t.Host.Workspaces.SelectManual(ws.Id);
        await c.ExpectAsync(MessageTypes.WorkspaceState);
        return (t, c, ws.Controls.First(x => x.Label == "Play / Pause"), ws.Controls.First(x => x.Widget == WidgetType.Volume));
    }

    [Fact]
    public async Task ButtonPressRunsRealActionAndReturnsResult()
    {
        var (t, c, play, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(play.Id), "req-1");
        var res = TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult));
        Assert.True(res.Ok); Assert.Equal("req-1", res.RequestId); Assert.Equal(play.Id, res.ControlId); Assert.Null(res.Error);
        Assert.True(res.Timestamp > 0);
        Assert.Equal((ushort)0xB3, t.Input.Sent.Single().Key);
        Assert.Single(t.Host.Activity.Recent());
    }

    [Fact]
    public async Task FailedInputIsReportedAsFailure()
    {
        var (t, c, play, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        t.Input.Block = true;
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(play.Id), "req-1");
        var res = TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult));
        Assert.False(res.Ok); Assert.Equal(ErrorCodes.ActionFailed, res.Error!.Code);
    }

    [Fact]
    public async Task DuplicateRequestIdExecutesOnce()
    {
        var (t, c, play, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        for (var i = 0; i < 3; i++) await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(play.Id), "same-id");
        for (var i = 0; i < 3; i++) Assert.True(TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult)).Ok);
        Assert.Single(t.Input.Sent);
    }

    [Fact]
    public async Task VolumeSliderSetsRealLevelAndReturnsNewState()
    {
        var (t, c, _, vol) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(vol.Id, 65), "v1");
        var res = TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult));
        Assert.True(res.Ok); Assert.Equal(65, res.State!.VolumePercent); Assert.Equal(65, t.Audio.State!.Value.VolumePercent);
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(vol.Id, 500), "v2");
        Assert.False(TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult)).Ok);
    }

    [Fact]
    public async Task ControlsOutsideActiveWorkspaceAreRejected()
    {
        var (t, c, _, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        var other = t.Host.Workspaces.All.First(w => w.Name == "Development").Controls[0];
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(other.Id), "x");
        var res = TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult));
        Assert.False(res.Ok); Assert.Equal(ErrorCodes.UnknownControl, res.Error!.Code);
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload("'; calc.exe"), "y");
        Assert.Equal(ErrorCodes.UnknownControl, TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult)).Error!.Code);
    }

    [Fact]
    public async Task UnconfiguredControlReportsUnavailable()
    {
        var (t, c, _, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        var dev = t.Host.Workspaces.All.First(w => w.Name == "Development");
        t.Host.Workspaces.SelectManual(dev.Id); await c.ExpectAsync(MessageTypes.WorkspaceState);
        var project = dev.Controls.First(x => x.Label == "Project");
        var state = t.Host.Workspaces.BuildState().Active!.Controls.First(x => x.Id == project.Id);
        Assert.False(state.Available); Assert.Contains("Not configured", state.UnavailableReason);
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(project.Id), "u1");
        var res = TestClient.Read<ActionResultPayload>(await c.ExpectAsync(MessageTypes.ActionResult));
        Assert.False(res.Ok); Assert.Equal(ErrorCodes.ActionUnavailable, res.Error!.Code);
    }

    [Fact]
    public async Task MissingRequestIdIsRejected()
    {
        var (t, c, play, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(play.Id));
        Assert.Equal(ErrorCodes.InvalidMessage, TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error)).Code);
        Assert.Empty(t.Input.Sent);
    }

    [Fact]
    public async Task FloodIsRateLimited()
    {
        var (t, c, play, _) = await SetupAsync();
        await using var _t = t; await using var _c = c;
        for (var i = 0; i < 60; i++) await c.SendAsync(MessageTypes.ActionInvoke, new ActionInvokePayload(play.Id), "f" + i);
        var limited = 0; var ok = 0;
        for (var i = 0; i < 60; i++)
        {
            var e = await c.ReceiveAsync();
            while (e!.Type is MessageTypes.Telemetry) e = await c.ReceiveAsync();
            if (e.Type == MessageTypes.Error && TestClient.Read<ErrorPayload>(e).Code == ErrorCodes.RateLimited) limited++; else ok++;
        }
        Assert.True(limited > 0); Assert.True(ok >= 20);
        Assert.True(t.Input.Sent.Count < 60);
    }
}

public class WorkspaceSyncTests
{
    [Fact]
    public async Task ForegroundAppSwitchesWorkspaceAndPhoneReceivesNewerRevision()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        t.Foreground.Raise("Code.exe", "main.cs - Visual Studio Code");
        var s = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal("Development", s.Active!.Name); Assert.Equal("auto", s.Mode); Assert.Contains("Code.exe", s.Reason);

        t.Foreground.Raise("Spotify.exe");
        var s2 = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal("Media", s2.Active!.Name);
        Assert.True(s2.Revision > s.Revision);
    }

    [Fact]
    public async Task RapidFocusChangesAreDebouncedToFinalApp()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        t.Foreground.Raise("Code.exe"); t.Foreground.Raise("Spotify.exe"); t.Foreground.Raise("Code.exe");
        var s = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal("Development", s.Active!.Name);               // one switch, to the app that stayed focused
        await Task.Delay(300);
        Assert.Equal(1, t.Host.Workspaces.Revision - s.Revision + 1);
    }

    [Fact]
    public async Task ManualSelectionOverridesAutoUntilResumed()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        var media = t.Host.Workspaces.All.First(w => w.Name == "Media").Id;
        await c.SendAsync(MessageTypes.WorkspaceSelect, new WorkspaceSelectPayload(media));
        var s = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal(("Media", "manual"), (s.Active!.Name, s.Mode));

        t.Foreground.Raise("Code.exe"); await Task.Delay(300);
        Assert.Equal(media, t.Host.Workspaces.ActiveId);           // foreground change ignored while overridden

        await c.SendAsync(MessageTypes.WorkspaceAuto, new WorkspaceAutoPayload(true));
        var resumed = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal("auto", resumed.Mode);
        for (var i = 0; i < 30 && t.Host.Workspaces.Active?.Name != "Development"; i++) await Task.Delay(50);
        Assert.Equal("Development", t.Host.Workspaces.Active?.Name);
    }

    [Fact]
    public async Task DisablingAutoStopsSwitching()
    {
        await using var t = await TestHost.StartAsync();
        var before = t.Host.Workspaces.ActiveId;
        t.Host.Workspaces.SetAuto(false);
        t.Foreground.Raise("Code.exe"); await Task.Delay(300);
        Assert.Equal(before, t.Host.Workspaces.ActiveId);
        Assert.Equal("manual", t.Host.Workspaces.BuildState().Mode);
    }

    [Fact]
    public async Task EditingActiveWorkspacePushesLiveUpdate()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        var active = t.Host.Workspaces.Active!;
        Assert.Empty(t.Host.Workspaces.Save(active with { Name = "Renamed" }));
        var s = TestClient.Read<WorkspaceStatePayload>(await c.ExpectAsync(MessageTypes.WorkspaceState));
        Assert.Equal("Renamed", s.Active!.Name);
    }

    [Fact]
    public async Task UnknownWorkspaceSelectionIsAnError()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        await c.SendAsync(MessageTypes.WorkspaceSelect, new WorkspaceSelectPayload("nope"), "w1");
        var err = TestClient.Read<ErrorPayload>(await c.ExpectAsync(MessageTypes.Error));
        Assert.Equal((ErrorCodes.UnknownWorkspace, "w1"), (err.Code, err.RequestId));
    }

    [Fact]
    public async Task StateAckIsRecorded()
    {
        await using var t = await TestHost.StartAsync();
        var (c, _, _) = await t.PairAsync(); await using var _c = c;
        var rev = t.Host.Workspaces.Revision;
        await c.SendAsync(MessageTypes.StateAck, new StateAckPayload(rev));
        for (var i = 0; i < 30 && t.Host.Sessions.Authenticated().All(s => s.LastAckedRevision < rev); i++) await Task.Delay(50);
        Assert.Equal(rev, t.Host.Sessions.Authenticated().Single().LastAckedRevision);
    }
}

public class PersistenceTests
{
    [Fact]
    public async Task WorkspacesSettingsAndActiveSelectionSurviveRestart()
    {
        var dir = Path.Combine(Path.GetTempPath(), "taveloom-persist-" + Guid.NewGuid().ToString("N"));
        TaveloomHost Make() => new(new HostOptions { DataDir = dir, Port = 0, LanExposure = false, Foreground = new FakeForeground(), Input = new FakeInput(), Audio = new FakeAudio() });
        string createdId, activeId;

        await using (var h = Make())
        {
            var w = h.Workspaces.Create("My Workspace");
            createdId = w.Id;
            Assert.Empty(h.Workspaces.Save(w with { Icon = "rocket", Rules = [new WorkspaceRule("r1", "notepad.exe", null, 3)] }));
            var media = h.Workspaces.All.First(x => x.Name == "Media");
            h.Workspaces.SelectManual(media.Id); activeId = media.Id;
            h.Workspaces.SetAuto(false);
            h.Settings.Set("theme", "dark");
        }

        await using var h2 = Make();
        var back = h2.Workspaces.Get(createdId)!;
        Assert.Equal(("My Workspace", "rocket"), (back.Name, back.Icon));
        Assert.Equal("notepad.exe", back.Rules.Single().ProcessName);
        Assert.Equal(activeId, h2.Workspaces.ActiveId);
        Assert.False(h2.Workspaces.AutoEnabled);
        Assert.Equal("dark", h2.Settings.Get("theme"));
        Assert.Equal(4, h2.Workspaces.All.Count);                  // built-ins were seeded once, not again
        try { Directory.Delete(dir, true); } catch { }
    }

    [Fact]
    public async Task CrudDuplicateDeleteAndSingleDefault()
    {
        await using var t = await TestHost.StartAsync();
        var ws = t.Host.Workspaces;
        var dev = ws.All.First(w => w.Name == "Development");
        var copy = ws.Duplicate(dev.Id)!;
        Assert.Equal("Development copy", copy.Name);
        Assert.Empty(copy.Controls.Select(c => c.Id).Intersect(dev.Controls.Select(c => c.Id)));
        Assert.Empty(ws.Save(copy with { IsDefault = true }));
        Assert.Single(ws.All, w => w.IsDefault);
        Assert.True(ws.Delete(copy.Id)); Assert.False(ws.Delete(copy.Id));
        Assert.NotEmpty(ws.Save(dev with { Name = "" }));          // invalid edits are refused, nothing is overwritten
        Assert.Equal("Development", ws.Get(dev.Id)!.Name);
    }

    [Fact]
    public async Task DeviceCredentialsAreNotStoredInPlaintext()
    {
        await using var t = await TestHost.StartAsync();
        var (c, id, cred) = await t.PairAsync(); await c.DisposeAsync();
        var raw = t.Host.Database.Query("SELECT credential FROM devices WHERE id=$i", r => (byte[])r[0], ("$i", id)).Single();
        Assert.NotEqual(cred, raw);
        Assert.DoesNotContain(System.Text.Encoding.UTF8.GetString(raw), System.Buffers.Text.Base64Url.EncodeToString(cred));
        foreach (var e in t.Host.Audit.Recent()) Assert.DoesNotContain(System.Buffers.Text.Base64Url.EncodeToString(cred), e.Detail ?? "");
    }
}

public class NativeSmokeTests
{
    [Fact]
    public void RealTelemetryProducesPlausibleValues()
    {
        var s = new Taveloom.Host.Server.TelemetrySampler(new CoreAudioController());
        s.Sample(); Thread.Sleep(300);
        var t = s.Sample();
        Assert.InRange(t.RamPercent!.Value, 1, 100);
        Assert.InRange(t.CpuPercent!.Value, 0, 100);
        if (t.VolumePercent is { } v) Assert.InRange(v, 0, 100);   // null on machines without an audio endpoint
    }

    [Fact]
    public async Task RealScreenshotWritesAPng()
    {
        var dir = Path.Combine(Path.GetTempPath(), "taveloom-shot-" + Guid.NewGuid().ToString("N"));
        var r = await new Taveloom.Host.Actions.SystemHandler(dir).ExecuteAsync(
            new ActionSpec(ActionType.System, new() { ["command"] = "screenshot" }), null, CancellationToken.None);
        if (!r.Ok) { Assert.Contains("display", r.Message, StringComparison.OrdinalIgnoreCase); return; } // headless runner: reported honestly
        var file = Directory.GetFiles(dir, "*.png").Single();
        Assert.True(new FileInfo(file).Length > 1000);
        Directory.Delete(dir, true);
    }
}
