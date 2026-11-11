using System.Net;
using System.Text;
using Taveloom.Host.Actions;
using Taveloom.Host.Security;
using Taveloom.Host.Server;
using Taveloom.Host.Workspaces;
using Taveloom.Protocol;

namespace Taveloom.Host.Tests;

public class KeyParserTests
{
    [Theory]
    [InlineData("ctrl+shift+s", 2, 'S')]
    [InlineData("Alt+F4", 1, 0x73)]
    [InlineData("win+d", 1, 'D')]
    [InlineData("ctrl+plus", 1, 0xBB)]
    [InlineData("f12", 0, 0x7B)]
    public void Valid(string text, int mods, int key)
    {
        Assert.True(KeyParser.TryParse(text, out var c, out _));
        Assert.Equal(mods, c.Modifiers.Count);
        Assert.Equal(key, c.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ctrl")]
    [InlineData("ctrl+ctrl+a")]
    [InlineData("a+b")]
    [InlineData("ctrl+")]
    [InlineData("ctrl+banana")]
    [InlineData("f25")]
    [InlineData(null)]
    public void Invalid(string? text) => Assert.False(KeyParser.TryParse(text, out _, out var err) || err == "");
}

public class EnvelopeTests
{
    [Fact]
    public void RoundTrip()
    {
        var e = Envelope.Create(MessageTypes.ActionInvoke, new ActionInvokePayload("c1", 42), "r1");
        Assert.True(Envelope.TryParse(e.ToUtf8(), out var back, out _));
        Assert.Equal("r1", back.Id);
        Assert.True(back.TryReadPayload<ActionInvokePayload>(out var p));
        Assert.Equal(("c1", 42.0), (p.ControlId, p.Value));
    }

    [Fact] public void WireUsesCamelCase() => Assert.Contains("\"controlId\":\"c1\"", Encoding.UTF8.GetString(Envelope.Create("x", new ActionInvokePayload("c1")).ToUtf8()));

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"v\":1,\"type\":\"\",\"ts\":1}")]
    [InlineData("{\"v\":2,\"type\":\"ping\",\"ts\":1}")]
    public void RejectsMalformed(string json) => Assert.False(Envelope.TryParse(Encoding.UTF8.GetBytes(json), out _, out _));

    [Fact] public void RejectsOversize() => Assert.False(Envelope.TryParse(new byte[ProtocolInfo.MaxMessageBytes + 1], out _, out _));
    [Fact] public void RejectsEmpty() => Assert.False(Envelope.TryParse([], out _, out _));

    [Fact]
    public void WrongPayloadShapeIsRejectedNotThrown()
    {
        Envelope.TryParse("{\"v\":1,\"type\":\"action.invoke\",\"ts\":1,\"payload\":{\"controlId\":5}}"u8, out var e, out _);
        Assert.False(e.TryReadPayload<ActionInvokePayload>(out _));
    }
}

public class PairingTests
{
    [Fact]
    public void QrRoundTrip()
    {
        var qr = new PairingQr(["192.168.1.5", "10.0.0.2"], 47821, new string('A', 64), "tok-0123456789abcdef", "My PC");
        Assert.True(PairingQr.TryParse(qr.ToUri(), out var back));
        Assert.Equal(qr.Hosts, back.Hosts); Assert.Equal(qr.Token, back.Token); Assert.Equal("My PC", back.HostName);
    }

    [Theory]
    [InlineData("https://evil.example/pair")]
    [InlineData("taveloom://pair?v=1&h=1.1.1.1&p=99999&f=AA&t=x")]
    [InlineData("taveloom://pair?v=9&h=1.1.1.1&p=1&f=AA&t=x")]
    [InlineData("garbage")]
    public void QrRejectsBad(string uri) => Assert.False(PairingQr.TryParse(uri, out _));

    [Fact]
    public void ProofVerifiesOnlyWithSameInputs()
    {
        var cred = new byte[32]; new Random(1).NextBytes(cred);
        var proof = AuthProof.Compute(cred, "nonce", "dev-12345678");
        Assert.True(AuthProof.Verify(cred, "nonce", "dev-12345678", proof));
        Assert.False(AuthProof.Verify(cred, "other", "dev-12345678", proof));
        Assert.False(AuthProof.Verify(cred, "nonce", "dev-87654321", proof));
        cred[0] ^= 1;
        Assert.False(AuthProof.Verify(cred, "nonce", "dev-12345678", proof));
    }

    /// <summary>Vector computed independently (Python hmac/hashlib). The Dart client test asserts the same value.</summary>
    [Fact]
    public void ProofMatchesCrossLanguageVector()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Assert.Equal("EKj-sPXs0muqUyu7ZrbbJgMoc1WosmlfHWYJT_3cy6A", AuthProof.Compute(key, "nonce-abc", "dev-12345678"));
    }

    [Fact]
    public void TokenIsSingleUse()
    {
        var p = new PairingService();
        var t = p.CreateOffer().Token;
        Assert.Equal(TokenResult.Ok, p.Consume(t));
        Assert.Equal(TokenResult.Unknown, p.Consume(t));
    }

    [Fact]
    public void TokenExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var p = new PairingService(() => now);
        var t = p.CreateOffer().Token;
        now += TimeSpan.FromMinutes(3);
        Assert.Equal(TokenResult.Expired, p.Consume(t));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("guess")]
    public void UnknownTokenRejected(string? t) => Assert.Equal(TokenResult.Unknown, new PairingService().Consume(t));

    [Fact]
    public async Task ApprovalWithoutHandlerIsRejected() =>
        Assert.False(await new PairingService().RequestApprovalAsync("d", "n", "r", CancellationToken.None));

    [Fact]
    public async Task ApprovalTimesOutAsRejection()
    {
        var p = new PairingService { ApprovalTimeout = TimeSpan.FromMilliseconds(100) };
        p.ApprovalRequested += _ => { };
        Assert.False(await p.RequestApprovalAsync("d", "n", "r", CancellationToken.None));
    }

    [Fact]
    public void ThrottleBlocksAfterRepeatedFailuresThenExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var t = new AuthThrottle(() => now, 3);
        for (var i = 0; i < 3; i++) { Assert.False(t.IsBlocked("a")); t.RecordFailure("a"); }
        Assert.True(t.IsBlocked("a")); Assert.False(t.IsBlocked("b"));
        now += TimeSpan.FromMinutes(2);
        Assert.False(t.IsBlocked("a"));
    }
}

public class NetworkGuardTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("10.4.5.6", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.1", true)]
    [InlineData("fd12:3456::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("::ffff:192.168.0.9", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("2001:4860:4860::8888", false)]
    public void Classifies(string ip, bool local) => Assert.Equal(local, NetworkGuard.IsLocalNetwork(IPAddress.Parse(ip)));

    [Fact] public void NullIsNotLocal() => Assert.False(NetworkGuard.IsLocalNetwork(null));
}

public class WorkspaceLogicTests
{
    private static Workspace W(string id, bool def = false, params WorkspaceRule[] rules) => new(id, id, "star", 4, 3, def, [], rules.ToList());
    private static WorkspaceRule R(string proc, int prio = 0, string? title = null) => new(Guid.NewGuid().ToString("N"), proc, title, prio);

    [Fact]
    public void MatchesProcessIgnoringCaseAndExe()
    {
        var m = WorkspaceResolver.Resolve([W("dev", false, R("code"))], new ForegroundInfo("Code.EXE", ""));
        Assert.Equal("dev", m.Workspace?.Id);
    }

    [Fact]
    public void HigherPriorityWins()
    {
        var m = WorkspaceResolver.Resolve([W("a", false, R("x.exe", 1)), W("b", false, R("x.exe", 9))], new ForegroundInfo("x.exe", ""));
        Assert.Equal("b", m.Workspace?.Id);
    }

    [Fact]
    public void TitleRuleBeatsPlainRuleAtEqualPriority()
    {
        var m = WorkspaceResolver.Resolve([W("plain", false, R("chrome.exe", 5)), W("docs", false, R("chrome.exe", 5, "Docs"))],
            new ForegroundInfo("chrome.exe", "Team Docs - Chrome"));
        Assert.Equal("docs", m.Workspace?.Id);
        Assert.Equal("plain", WorkspaceResolver.Resolve([W("plain", false, R("chrome.exe", 5)), W("docs", false, R("chrome.exe", 5, "Docs"))],
            new ForegroundInfo("chrome.exe", "Inbox")).Workspace?.Id);
    }

    [Fact]
    public void TiesResolveToWorkspaceOrderDeterministically()
    {
        var list = new[] { W("first", false, R("x")), W("second", false, R("x")) };
        for (var i = 0; i < 5; i++) Assert.Equal("first", WorkspaceResolver.Resolve(list, new ForegroundInfo("x", "")).Workspace?.Id);
    }

    [Fact]
    public void FallsBackToDefaultThenToNothing()
    {
        Assert.Equal("def", WorkspaceResolver.Resolve([W("a", false, R("x")), W("def", true)], new ForegroundInfo("unknown", "")).Workspace?.Id);
        Assert.Null(WorkspaceResolver.Resolve([W("a", false, R("x"))], new ForegroundInfo("unknown", "")).Workspace);
        Assert.Equal("def", WorkspaceResolver.Resolve([W("def", true)], null).Workspace?.Id);
    }

    private static string? NoCheck(ControlDef _) => null;
    private static ControlDef C(string id, int col, int row, int cs = 1, int rs = 1) => new(id, id, "star", WidgetType.Button, col, row, cs, rs, new ActionSpec(ActionType.None));

    [Fact]
    public void ValidatorCatchesOverlapAndBounds()
    {
        Assert.Empty(WorkspaceValidator.Validate(new Workspace("w", "ok", "star", 4, 3, false, [C("a", 0, 0), C("b", 1, 0)], []), NoCheck));
        Assert.Contains(WorkspaceValidator.Validate(new Workspace("w", "ok", "star", 4, 3, false, [C("a", 0, 0, 2), C("b", 1, 0)], []), NoCheck), e => e.Contains("overlaps"));
        Assert.Contains(WorkspaceValidator.Validate(new Workspace("w", "ok", "star", 4, 3, false, [C("a", 3, 0, 2)], []), NoCheck), e => e.Contains("fit"));
        Assert.NotEmpty(WorkspaceValidator.Validate(new Workspace("w", "", "star", 4, 3, false, [], []), NoCheck));
        Assert.NotEmpty(WorkspaceValidator.Validate(new Workspace("w", "ok", "star", 4, 3, false, [C("a", 0, 0), C("a", 1, 0)], []), NoCheck));
        Assert.NotEmpty(WorkspaceValidator.Validate(new Workspace("w", "ok", "star", 9, 3, false, [], []), NoCheck));
    }
}

public class ActionTests
{
    private static ControlDef Ctl(ActionSpec a, WidgetType w = WidgetType.Button) => new("c", "Label", "star", w, 0, 0, Action: a);
    private static ActionEngine Engine(FakeInput i, FakeAudio a) => new([new HotkeyHandler(i), new MediaHandler(i, a), new LaunchHandler(ActionType.LaunchApp), new LaunchHandler(ActionType.LaunchUrl), new LaunchHandler(ActionType.LaunchFolder)]);
    private static ActionSpec Media(string cmd) => new(ActionType.Media, new() { ["command"] = cmd });

    [Fact]
    public async Task MediaKeyReachesInputSenderWithHonestMessage()
    {
        var input = new FakeInput();
        var r = await Engine(input, new FakeAudio()).ExecuteAsync(Ctl(Media("playpause")), null);
        Assert.True(r.Ok); Assert.Equal((ushort)0xB3, input.Sent.Single().Key); Assert.Contains("sent", r.Message);
    }

    [Fact]
    public async Task BlockedInputIsReportedAsFailureNotSuccess()
    {
        var r = await Engine(new FakeInput { Block = true }, new FakeAudio()).ExecuteAsync(Ctl(Media("next")), null);
        Assert.False(r.Ok); Assert.Equal(ErrorCodes.ActionFailed, r.ErrorCode);
    }

    [Fact]
    public async Task VolumeValidatesRangeAndAppliesToAudio()
    {
        var audio = new FakeAudio(); var e = Engine(new FakeInput(), audio);
        Assert.True((await e.ExecuteAsync(Ctl(Media("volume"), WidgetType.Volume), 70)).Ok);
        Assert.Equal(70, audio.State!.Value.VolumePercent);
        Assert.False((await e.ExecuteAsync(Ctl(Media("volume"), WidgetType.Volume), 150)).Ok);
        Assert.False((await e.ExecuteAsync(Ctl(Media("volume"), WidgetType.Volume), null)).Ok);
        Assert.Equal(70, audio.State!.Value.VolumePercent);
    }

    [Fact]
    public async Task MuteTogglesRealState()
    {
        var audio = new FakeAudio(); var e = Engine(new FakeInput(), audio);
        await e.ExecuteAsync(Ctl(Media("mute")), null); Assert.True(audio.State!.Value.Muted);
        await e.ExecuteAsync(Ctl(Media("mute")), null); Assert.False(audio.State!.Value.Muted);
    }

    [Fact]
    public async Task NoAudioDeviceIsUnavailableNotFaked()
    {
        var r = await Engine(new FakeInput(), new FakeAudio { State = null }).ExecuteAsync(Ctl(Media("mute")), null);
        Assert.False(r.Ok); Assert.Equal(ErrorCodes.ActionUnavailable, r.ErrorCode);
    }

    [Fact]
    public void ValidationRejectsUnsafeLaunchTargets()
    {
        var e = Engine(new FakeInput(), new FakeAudio());
        Assert.NotNull(e.Validate(Ctl(new ActionSpec(ActionType.LaunchApp, new() { ["path"] = "calc.exe" }))));          // relative
        Assert.NotNull(e.Validate(Ctl(new ActionSpec(ActionType.LaunchApp, new() { ["path"] = @"C:\x\script.ps1" })))); // wrong type
        Assert.NotNull(e.Validate(Ctl(new ActionSpec(ActionType.LaunchUrl, new() { ["url"] = "file:///c:/windows/system32/cmd.exe" }))));
        Assert.NotNull(e.Validate(Ctl(new ActionSpec(ActionType.LaunchUrl, new() { ["url"] = "javascript:alert(1)" }))));
        Assert.Null(e.Validate(Ctl(new ActionSpec(ActionType.LaunchUrl, new() { ["url"] = "https://example.com" }))));
        Assert.NotNull(e.Validate(Ctl(new ActionSpec(ActionType.Hotkey, new() { ["keys"] = "ctrl+nonsense" }))));
    }

    [Fact]
    public void UnconfiguredLaunchIsUnavailableWithReason()
    {
        var a = Engine(new FakeInput(), new FakeAudio()).Check(Ctl(new ActionSpec(ActionType.LaunchFolder)));
        Assert.False(a.Available); Assert.Contains("Not configured", a.Reason);
    }

    [Fact]
    public async Task SlowHandlerTimesOut()
    {
        var slow = new SlowHandler();
        var e = new ActionEngine([slow]) { Timeout = TimeSpan.FromMilliseconds(150) };
        var r = await e.ExecuteAsync(Ctl(new ActionSpec(ActionType.System)), null);
        Assert.False(r.Ok); Assert.Equal(ErrorCodes.Timeout, r.ErrorCode);
    }

    private sealed class SlowHandler : IActionHandler
    {
        public ActionType Type => ActionType.System;
        public string? Validate(ActionSpec s) => null;
        public Availability Check(ActionSpec s) => Availability.Yes;
        public async Task<ActionOutcome> ExecuteAsync(ActionSpec s, double? v, CancellationToken ct) { await Task.Delay(5000, ct); return ActionOutcome.Success("late"); }
    }
}
