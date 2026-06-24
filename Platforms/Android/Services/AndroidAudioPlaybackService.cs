using Android.Media;
using PROXIMAMOP.Services;

namespace PROXIMAMOP.Platforms.Android.Services;

public sealed class AndroidAudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private MediaPlayer? _mediaPlayer;
    private bool _isPreparing;

    public async Task PlayAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Audio URL is required.", nameof(url));

        await StopInternalAsync();

        _mediaPlayer = new MediaPlayer();
        _isPreparing = true;

        var preparedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        _mediaPlayer.Prepared += OnPrepared;
        _mediaPlayer.Error += OnError;
        _mediaPlayer.Completion += OnCompletion;

        try
        {
            _mediaPlayer.SetAudioStreamType(global::Android.Media.Stream.Music);
            _mediaPlayer.SetDataSource(url);
            _mediaPlayer.PrepareAsync();

            using var registration = cancellationToken.Register(() =>
            {
                errorTcs.TrySetResult(new OperationCanceledException(cancellationToken));
            });

            var completed = await Task.WhenAny(preparedTcs.Task, errorTcs.Task);

            if (completed == errorTcs.Task)
                throw await errorTcs.Task;

            cancellationToken.ThrowIfCancellationRequested();

            _mediaPlayer.Start();
            _isPreparing = false;
        }
        catch
        {
            await StopInternalAsync();
            throw;
        }

        void OnPrepared(object? sender, EventArgs e)
        {
            preparedTcs.TrySetResult(true);
        }

        void OnError(object? sender, MediaPlayer.ErrorEventArgs e)
        {
            var message = $"Android MediaPlayer error. What={e.What}, Extra={e.Extra}";
            errorTcs.TrySetResult(new InvalidOperationException(message));
        }

        void OnCompletion(object? sender, EventArgs e)
        {
            _isPreparing = false;
        }
    }

    public async Task StopAsync()
    {
        await StopInternalAsync();
    }

    private Task StopInternalAsync()
    {
        try
        {
            if (_mediaPlayer is not null)
            {
                if (_isPreparing || _mediaPlayer.IsPlaying)
                {
                    try
                    {
                        _mediaPlayer.Stop();
                    }
                    catch
                    {
                    }
                }

                _mediaPlayer.Reset();
                _mediaPlayer.Release();
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }
        }
        finally
        {
            _isPreparing = false;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            StopInternalAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }
}