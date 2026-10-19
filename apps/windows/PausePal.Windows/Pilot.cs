namespace PausePal.Windows;

internal static class Pilot
{
    public static void Run()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var dispatch = new System.Windows.Forms.Control();
        dispatch.CreateControl();
        using var context = new System.Windows.Forms.ApplicationContext();
        using var input = new InputWatch();
        var commands = new IntentInbox(input);
        _ = Task.Run(async () =>
        {
            while (await Console.In.ReadLineAsync() is { } line)
            {
                if (!commands.Post(line.Trim())) break;
            }
            commands.Complete();
        });
        _ = Task.Run(async () =>
        {
            var session = new MediaSession(new WindowsMediaEnvironment(input));
            try
            {
                Console.WriteLine("{\"kind\":\"pilot-ready\"}");
                var exit = false;
                while (!exit && !commands.Closed)
                {
                    while (commands.TryRead(out var pending))
                    {
                        var command = pending.Value;
                        switch (command)
                        {
                            case "START": session.Start(); break;
                            case "STOP": session.Stop(); break;
                            case "DISCONNECT": session.Stop("连接已断开，观影停止"); break;
                            case "EXIT": exit = true; break;
                            default:
                                if (Enum.TryParse<WatchingIntent>(command, out var intent) && Enum.IsDefined(intent) && command == intent.ToString()) session.Receive(intent, pending.Epoch);
                                else Console.WriteLine("{\"kind\":\"invalid-pilot-intent\"}");
                                break;
                        }
                    }
                    if (commands.Closed) exit = true;
                    if (!exit) { session.Tick(); await Task.Delay(200); }
                }
                session.Stop();
            }
            finally { dispatch.BeginInvoke(context.ExitThread); }
        });
        System.Windows.Forms.Application.Run(context);
    }
}
