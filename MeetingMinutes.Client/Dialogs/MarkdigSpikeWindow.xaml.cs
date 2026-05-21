using System.Diagnostics;
using System.Windows;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Dialogs;

public partial class MarkdigSpikeWindow : Window
{
    public MarkdigSpikeWindow()
    {
        InitializeComponent();
        Loaded += MarkdigSpikeWindow_Loaded;
    }

    private void MarkdigSpikeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Viewer.Markdown = "# Nadpis H1\n\n- bod jedna\n- bod dva\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n**tučně** a *kurzíva*.";
            Debug.WriteLine("MarkdigSpikeWindow: render SUCCESS");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MarkdigSpikeWindow: render FAILED — {ex}");
        }
    }

    // X.0 Cancellation spike — run on Windows to verify OllamaSharp honours ct mid-stream.
    // Pass: sw.ElapsedMilliseconds <= 1500 AND (DateTime.UtcNow - lastTokenAt).TotalMilliseconds <= 500
    // after cts.Cancel(). Fail -> proceed to X.0a raw-HttpClient fallback.
    private async Task RunCancellationSpike()
    {
        var client = new OllamaApiClient(new Uri("http://localhost:11434"));
        var cts = new CancellationTokenSource();
        var sw = new Stopwatch();
        var lastTokenAt = DateTime.MinValue;

        var streamTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var c in client.ChatAsync(new ChatRequest
                {
                    Model = "gemma3:12b",
                    Stream = true,
                    Messages = new List<Message>
                    {
                        new() { Role = "user", Content = "Napiš podrobný esej o dějinách Prahy v cca 2000 slovech." }
                    }
                }, cts.Token))
                {
                    lastTokenAt = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { }
        });

        await Task.Delay(1000);
        sw.Start();
        cts.Cancel();
        try { await streamTask; } catch (OperationCanceledException) { }
        sw.Stop();

        var msSinceLastToken = lastTokenAt == DateTime.MinValue
            ? 0
            : (DateTime.UtcNow - lastTokenAt).TotalMilliseconds;

        Debug.WriteLine($"[CancellationSpike] Cancel latency: {sw.ElapsedMilliseconds}ms, ms since last token: {msSinceLastToken:F0}ms");
        Debug.WriteLine($"[CancellationSpike] PASS={sw.ElapsedMilliseconds <= 1500 && msSinceLastToken <= 500}");
    }
}
