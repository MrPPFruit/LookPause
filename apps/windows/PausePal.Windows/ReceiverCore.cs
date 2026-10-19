namespace PausePal.Windows;

internal interface IPhonePeer
{
    Guid Id { get; }
    bool Send(WireFrame frame, bool closeAfter = false);
    void Close();
}

internal enum DesktopStatus { disconnected, waiting, attending, away }
internal sealed record ReceiverSnapshot(long Revision, string Connection, string Media, DesktopStatus Icon);

// 锁内没有 I/O、UI 或外部同步回调；传输只入有界队列，通知异步发布。
internal sealed class ReceiverCore(IPhoneCredentials credentials, IntentInbox inbox, Func<long>? clock = null)
{
    private sealed class Client(IPhonePeer peer, long now)
    {
        public IPhonePeer Peer = peer;
        public long LastSeen = now, RateWindow = now, Sequence;
        public int Frames;
        public bool Approving, ConfirmationQueued;
        public string? Token, ExpectedToken;
        public string? DeviceId, Session;
    }
    private readonly object sync = new();
    private readonly Dictionary<Guid, Client> clients = [];
    private readonly Queue<long> attempts = [];
    private Guid? active, pending;
    private bool watching, stopped;
    private WatchingIntent intent = WatchingIntent.UNKNOWN;
    private long revision, generation;
    private string connection = "等待手机连接", media = "尚未开始观影";
    private long Now => clock?.Invoke() ?? Environment.TickCount64;
    public event Action<ReceiverSnapshot>? Changed;
    public event Action<Guid, string>? PairRequested;
    public event Action<Guid>? PairEnded;
    public long Generation { get { lock (sync) return generation; } }
    public ReceiverSnapshot Snapshot { get { lock (sync) return SnapshotLocked(); } }

    public bool Add(IPhonePeer peer)
    {
        lock (sync)
        {
            if (stopped || clients.Count >= 4) return false;
            clients.Add(peer.Id, new Client(peer, Now)); return true;
        }
    }

    public void Handle(Guid peerId, WireFrame frame)
    {
        lock (sync)
        {
            if (!clients.TryGetValue(peerId, out var client)) return;
            var now = Now;
            if (now - client.LastSeen > Limit(client)) { Remove(client); client.Peer.Close(); return; }
            if (now - client.RateWindow >= 1000) { client.RateWindow = now; client.Frames = 0; }
            if (++client.Frames > 20) { Reject(client, "RATE_LIMIT", frame.seq); return; }
            if (client.Session == null)
            {
                if (frame.kind != "hello" || pending == peerId) { Reject(client, "UNAUTHORIZED"); return; }
                Hello(client, frame); return;
            }
            if (active != peerId) { Reject(client, "INACTIVE_SESSION", frame.seq); return; }
            if (frame.session != client.Session || frame.seq is not > 0 || frame.seq <= client.Sequence)
            {
                Send(client, new WireFrame { kind = "error", session = client.Session, seq = frame.seq, error = "STALE_SESSION_OR_SEQUENCE" });
                return;
            }
            client.Sequence = frame.seq.Value;
            // 无效消息、重放与未开始意图不刷新六秒期限。
            if (frame.kind is not ("start" or "stop" or "intent" or "ping")) { Reject(client, "UNSUPPORTED_OPERATION", frame.seq); return; }
            WatchingIntent parsed = WatchingIntent.UNKNOWN;
            if (frame.kind == "intent" && (!watching || !Enum.TryParse(frame.intent, out parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != frame.intent))
            {
                Send(client, new WireFrame { kind = "error", session = client.Session, seq = frame.seq, error = "NOT_WATCHING_OR_INVALID_INTENT" });
                return;
            }
            client.LastSeen = now;
            if (!client.ConfirmationQueued && client.Token is { } acceptedToken)
            {
                client.ConfirmationQueued = true; var acceptedDevice = client.DeviceId!;
                _ = Task.Run(() => { try { credentials.ConfirmToken(acceptedDevice, acceptedToken); } catch { /* 保留未确认状态，下次无凭据仍须批准。 */ } });
            }
            switch (frame.kind)
            {
                case "start":
                    // 重复 start 也是一个新观影轮次，先撤销旧的在途采样。
                    generation++; inbox.Post("STOP", generation); watching = true; intent = WatchingIntent.UNKNOWN;
                    media = "观影中，等待观看方向";
                    if (!inbox.Post("START", generation)) { Reject(client, "MEDIA_UNAVAILABLE"); return; }
                    break;
                case "stop":
                    generation++; inbox.Post("STOP", generation); watching = false; intent = WatchingIntent.UNKNOWN; media = "已停止观影";
                    break;
                case "intent":
                    intent = parsed;
                    if (!inbox.Post(parsed.ToString(), generation)) { Reject(client, "MEDIA_UNAVAILABLE"); return; }
                    break;
            }
            Publish();
            Send(client, new WireFrame { kind = "ack", session = client.Session, seq = frame.seq, status = media, ok = true });
        }
    }

    private void Hello(Client client, WireFrame frame)
    {
        if (!Guid.TryParse(frame.deviceId, out var parsedId) || string.IsNullOrEmpty(frame.name) || System.Text.Encoding.UTF8.GetByteCount(frame.name) > 128)
        { Reject(client, "INVALID_HELLO"); return; }
        var id = parsedId.ToString();
        var name = new string(frame.name.Where(c => !char.IsControl(c) && !char.IsSurrogate(c) && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).Take(64).ToArray());
        if (string.IsNullOrWhiteSpace(name)) { Reject(client, "INVALID_HELLO"); return; }
        client.DeviceId = id;
        var saved = credentials.ReadToken(id);
        if (saved != null && (frame.token != null || credentials.IsConfirmed(id)))
        {
            if (!IdentityStore.EqualTokens(saved, frame.token)) { Reject(client, "INVALID_CREDENTIAL"); return; }
            if (pending != null) { Reject(client, "BUSY"); return; }
            client.Token = saved; Authorize(client, null); return;
        }
        while (attempts.TryPeek(out var first) && Now - first >= 60000) attempts.Dequeue();
        if (pending != null || active != null || attempts.Count >= 3 || frame.token != null)
        { Reject(client, "PAIRING_UNAVAILABLE"); return; }
        attempts.Enqueue(Now); pending = client.Peer.Id; client.LastSeen = Now; connection = "等待首次配对确认";
        client.ExpectedToken = saved;
        Publish();
        ThreadPool.QueueUserWorkItem(_ => PairRequested?.Invoke(client.Peer.Id, name));
    }

    public async Task ApproveAsync(Guid peerId, bool allowed)
    {
        string deviceId; string? expectedToken;
        lock (sync)
        {
            if (pending != peerId || !clients.TryGetValue(peerId, out var client)) return;
            if (client.Approving) return;
            if (!allowed) { Reject(client, "PAIRING_REJECTED"); return; }
            deviceId = client.DeviceId!;
            expectedToken = client.ExpectedToken;
            client.Approving = true;
        }
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        try
        {
            if (!await Task.Run(() => credentials.TryStageToken(deviceId, token, expectedToken)))
            {
                lock (sync) if (pending == peerId && clients.TryGetValue(peerId, out var changed)) Reject(changed, "CREDENTIAL_CHANGED");
                return;
            }
        }
        catch
        {
            lock (sync) if (pending == peerId && clients.TryGetValue(peerId, out var failed)) Reject(failed, "CREDENTIAL_STORE_FAILED");
            return;
        }
        lock (sync)
        {
            if (pending != peerId || !clients.TryGetValue(peerId, out var client)) return;
            if (Now - client.LastSeen > 60000) { Reject(client, "PAIRING_EXPIRED"); return; }
            client.Token = token; EndPair(peerId); Authorize(client, token);
        }
    }

    private void Authorize(Client client, string? token)
    {
        if (active is { } previous && previous != client.Peer.Id && clients.TryGetValue(previous, out var old))
        {
            if (old.DeviceId != client.DeviceId) { Reject(client, "BUSY"); return; }
            Remove(old); old.Peer.Close();
        }
        generation++; inbox.Post("DISCONNECT", generation);
        active = client.Peer.Id; watching = false; intent = WatchingIntent.UNKNOWN;
        client.Session = Guid.NewGuid().ToString(); client.LastSeen = Now; client.Sequence = 0;
        connection = "手机已连接"; media = "已连接，等待手机开始";
        Publish();
        Send(client, new WireFrame { kind = "ready", session = client.Session, token = token, status = media, ok = true });
    }

    public void Closed(Guid peerId) { lock (sync) if (clients.TryGetValue(peerId, out var client)) Remove(client); }
    public bool IsPending(Guid peerId) { lock (sync) return pending == peerId && clients.ContainsKey(peerId); }
    public void Expire()
    {
        lock (sync)
            foreach (var client in clients.Values.ToArray())
                if (Now - client.LastSeen > Limit(client)) { Remove(client); client.Peer.Close(); }
    }
    private long Limit(Client client) => client.Session != null ? 6000 : pending == client.Peer.Id ? 60000 : 10000;
    private void EndPair(Guid peerId)
    {
        if (pending != peerId) return;
        pending = null; ThreadPool.QueueUserWorkItem(_ => PairEnded?.Invoke(peerId));
    }
    private void Remove(Client client)
    {
        clients.Remove(client.Peer.Id); EndPair(client.Peer.Id);
        if (active == client.Peer.Id)
        {
            // 只撤销当前活动连接，旧连接的迟到关闭不能影响新的会话。
            generation++; inbox.Post("DISCONNECT", generation); active = null; watching = false;
            intent = WatchingIntent.UNKNOWN; media = "手机已断开，恢复资格取消";
        }
        if (active == null) connection = "等待手机连接";
        Publish();
    }
    private void Send(Client client, WireFrame frame)
    {
        if (!client.Peer.Send(frame)) { Remove(client); client.Peer.Close(); }
    }
    private void Reject(Client client, string reason, long? sequence = null)
    {
        if (!client.Peer.Send(new WireFrame { kind = "error", session = client.Session, seq = sequence, error = reason }, true)) client.Peer.Close();
        Remove(client);
    }
    public void MediaChanged(long sourceGeneration, string text)
    {
        lock (sync)
        {
            if (sourceGeneration != generation || active == null || !watching) return;
            media = text; Publish();
            if (clients.TryGetValue(active.Value, out var client)) Send(client, new WireFrame { kind = "status", session = client.Session, status = text });
        }
    }
    public void Stop()
    {
        lock (sync)
        {
            stopped = true; generation++; inbox.Post("DISCONNECT", generation);
            foreach (var client in clients.Values.ToArray()) { Remove(client); client.Peer.Close(); }
        }
    }
    private ReceiverSnapshot SnapshotLocked() => new(revision, connection, media,
        active == null ? DesktopStatus.disconnected : !watching ? DesktopStatus.waiting : intent switch
        { WatchingIntent.ATTENDING => DesktopStatus.attending, WatchingIntent.AWAY or WatchingIntent.NO_FACE => DesktopStatus.away, _ => DesktopStatus.waiting });
    private void Publish()
    {
        revision++; var snapshot = SnapshotLocked();
        ThreadPool.QueueUserWorkItem(_ => Changed?.Invoke(snapshot));
    }
}
