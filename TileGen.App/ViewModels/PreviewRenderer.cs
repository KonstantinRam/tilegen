using TileGen.Core;

namespace TileGen.App.ViewModels;

/// <summary>What one preview render needs: the state at the moment it was scheduled.</summary>
public sealed record RenderJob(Preset Preset, IReadOnlyList<Variant> Variants, ulong SetSeed, int Size, bool Floor, bool Wall);

/// <summary>
/// Renders previews through Generator.Render on a background thread. A change schedules a render after a short delay,
/// so a dragged slider renders at most 20 times per second, and a render that is still running when the next one
/// starts is cancelled. Results come back on the thread that created the renderer.
/// </summary>
public sealed class PreviewRenderer
{
    public const int DebounceMilliseconds = 50;

    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly object _gate = new();
    private RenderJob? _pending;
    private CancellationTokenSource? _debounce;
    private CancellationTokenSource? _running;
    private int _active;

    public event Action<TileKind, IReadOnlyList<TileImage>>? Rendered;
    public event Action<string>? Failed;
    public event Action<bool>? BusyChanged;

    public bool IsBusy => Volatile.Read(ref _active) > 0;

    public void Schedule(RenderJob job)
    {
        lock (_gate)
        {
            // Two edits inside the delay merge into one job that renders both tile types if either asked for it.
            _pending = _pending is null ? job : job with { Floor = job.Floor || _pending.Floor, Wall = job.Wall || _pending.Wall };
            _debounce?.Cancel();
            _debounce = new CancellationTokenSource();
            var token = _debounce.Token;
            if (Interlocked.Increment(ref _active) == 1)
            {
                Post(() => BusyChanged?.Invoke(true));
            }
            _ = Task.Delay(DebounceMilliseconds, token).ContinueWith(t =>
            {
                if (t.IsCanceled)
                {
                    Finish();
                    return;
                }
                Start();
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Waits until nothing is pending or running. The tests use it before capturing a screenshot.</summary>
    public async Task WaitForIdleAsync()
    {
        while (IsBusy)
        {
            await Task.Delay(10);
        }
    }

    private void Start()
    {
        RenderJob job;
        CancellationToken token;
        lock (_gate)
        {
            if (_pending is null)
            {
                Finish();
                return;
            }
            job = _pending;
            _pending = null;
            _running?.Cancel();
            _running = new CancellationTokenSource();
            token = _running.Token;
        }
        Task.Run(() =>
        {
            try
            {
                if (job.Floor)
                {
                    RenderKind(job, TileKind.Floor, token);
                }
                if (job.Wall)
                {
                    RenderKind(job, TileKind.Wall, token);
                }
            }
            catch (OperationCanceledException)
            {
                // A newer job replaced this one, and its result would be stale.
            }
            catch (Exception e)
            {
                Post(() => Failed?.Invoke(e.Message));
            }
            finally
            {
                Finish();
            }
        }, CancellationToken.None);
    }

    private void RenderKind(RenderJob job, TileKind kind, CancellationToken token)
    {
        var tiles = new TileImage[job.Variants.Count];
        for (int i = 0; i < tiles.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var variant = job.Variants[i];
            var type = job.Preset.Get(kind);
            var request = new TileRequest(job.Preset, kind, kind == TileKind.Floor ? variant.Floor : variant.Wall, type.GarbageCoverage, variant.Seed, job.Size, job.SetSeed);
            tiles[i] = new TileImage(Generator.Render(request, token), job.Size);
        }
        token.ThrowIfCancellationRequested();
        Post(() => Rendered?.Invoke(kind, tiles));
    }

    private void Finish()
    {
        if (Interlocked.Decrement(ref _active) == 0)
        {
            Post(() => BusyChanged?.Invoke(false));
        }
    }

    private void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }
}
