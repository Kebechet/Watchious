using Plugin.Maui.WearableMessaging;

namespace Watchious.Samples.Phone;

// Everything Watchious.Messaging does, from the phone: reachability, a message, a message that waits for the watch's
// answer, the application context, and answers to the watch's own questions. The Counter sample's Phone page is the
// other side.
public sealed class MainPage : ContentPage
{
    private readonly IWearableMessaging _messaging;
    private readonly Label _status = new() { Text = "Not checked yet" };
    private readonly VerticalStackLayout _log = new() { Spacing = 4 };
    private int _contextUpdates;
    private bool _hasRunLaunchActions;

    public MainPage(IWearableMessaging messaging)
    {
        _messaging = messaging;
        Title = "Watchious Phone";
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 8,
                Children =
                {
                    _status,
                    CreateButton("Check the watch", CheckAsync),
                    CreateButton("Send hello", SendAsync),
                    CreateButton("Ask the watch", AskAsync),
                    CreateButton("Update the context", UpdateContextAsync),
                    new Label { Text = "Log", FontAttributes = FontAttributes.Bold },
                    _log,
                },
            },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _messaging.MessageReceived += OnMessageReceived;
        _messaging.ApplicationContextChanged += OnApplicationContextChanged;
        // The plugin starts listening on Android with its first call.
        await RunAsync(CheckAsync);
        if (!_hasRunLaunchActions)
        {
            _hasRunLaunchActions = true;
            await RunLaunchActionsAsync();
        }
    }

    protected override void OnDisappearing()
    {
        _messaging.MessageReceived -= OnMessageReceived;
        _messaging.ApplicationContextChanged -= OnApplicationContextChanged;
        base.OnDisappearing();
    }

    private async Task CheckAsync()
    {
        var isReachable = await _messaging.IsWearableReachable();
        var isInstalled = await _messaging.IsWearableAppInstalled();
        _status.Text = $"Watch reachable: {isReachable}, watch app installed: {isInstalled}";
        Log(_status.Text);
    }

    // For tests on real devices with no one at the phone: --watchious-phone=check,hello,ask,context presses those buttons
    // at start, and the log also goes to the console (xcrun devicectl device process launch --console).
    private async Task RunLaunchActionsAsync()
    {
        const string prefix = "--watchious-phone=";
        var argument = Environment.GetCommandLineArgs().FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        if (argument is null)
        {
            return;
        }

        foreach (var action in argument[prefix.Length..].Split(','))
        {
            Func<Task>? step = action switch
            {
                "check" => CheckAsync,
                "hello" => SendAsync,
                "ask" => AskAsync,
                "context" => UpdateContextAsync,
                _ => null,
            };
            if (step is null)
            {
                Log($"Unknown action {action}");
                continue;
            }

            await RunAsync(step);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    private async Task SendAsync()
    {
        await _messaging.SendMessageAsync(new Dictionary<string, string> { ["text"] = "Hello from the phone" });
        Log("Sent hello");
    }

    private async Task AskAsync()
    {
        var answer = await _messaging.SendMessageWithReplyAsync(
            new Dictionary<string, string> { ["question"] = "How are you?" },
            TimeSpan.FromSeconds(10));
        Log($"Watch answered: {Describe(answer)}");
    }

    private async Task UpdateContextAsync()
    {
        _contextUpdates++;
        await _messaging.UpdateApplicationContextAsync(new Dictionary<string, object>
        {
            ["from"] = "phone",
            ["update"] = _contextUpdates.ToString(),
        });
        Log($"Context update {_contextUpdates} sent");
    }

    // An iPhone answers through WatchConnectivity's reply handler. The plugin has none on Android, where the answer is a
    // message of its own whose "_replyToId" is the question's "_requestId".
    private void OnMessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        Log($"Watch says: {Describe(args.Data)}");
        if (args.ReplyHandler is not null)
        {
            args.ReplyHandler(new Dictionary<string, string> { ["answer"] = "Fine, says the iPhone" });
            return;
        }

        if (args.Data.TryGetValue("_requestId", out var requestId))
        {
            _ = RunAsync(() => _messaging.SendMessageAsync(new Dictionary<string, string>
            {
                ["_replyToId"] = requestId,
                ["answer"] = "Fine, says the Android phone",
            }));
        }
    }

    private void OnApplicationContextChanged(object? sender, ApplicationContextChangedEventArgs args)
    {
        Log($"Watch context: {string.Join(", ", args.Context.Select(pair => $"{pair.Key}={pair.Value}"))}");
    }

    private Button CreateButton(string text, Func<Task> action)
    {
        return new Button { Text = text, Command = new Command(() => _ = RunAsync(action)) };
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            Log($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void Log(string text)
    {
        Console.WriteLine($"[Watchious Phone] {text}");
        MainThread.BeginInvokeOnMainThread(() =>
            _log.Insert(0, new Label { Text = $"{DateTime.Now:HH:mm:ss} {text}" }));
    }

    private static string Describe(IReadOnlyDictionary<string, string> message)
    {
        return string.Join(", ", message.Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
