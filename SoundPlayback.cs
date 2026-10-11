using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace ses_uyg
{
    // Decode on a worker and keep at most two seconds of mono audio in memory.
    // The audio callback only consumes ready samples; it never reads a file or waits.
    internal sealed class SoundPlayback : IDisposable
    {
        private readonly object sync = new object();
        private readonly float[] samples = new float[44100 * 2];
        private readonly TaskCompletionSource<bool> ready =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int readIndex;
        private int writeIndex;
        private int count;
        private bool disposed;
        private bool completed;
        private Exception error;

        internal Task Ready { get { return ready.Task; } }
        internal Task Completion { get; private set; }
        internal bool Finished { get { lock (sync) return completed && count == 0; } }
        internal Exception Error { get { lock (sync) return error; } }

        internal SoundPlayback(string path)
        {
            Completion = Task.Run(() => Decode(path));
        }

        private void Decode(string path)
        {
            try
            {
                lock (sync) { if (disposed) return; }
                using (var reader = new AudioFileReader(path))
                using (var resampler = new MediaFoundationResampler(reader, new WaveFormat(44100, 1)))
                {
                    resampler.ResamplerQuality = 60;
                    var source = resampler.ToSampleProvider();
                    var chunk = new float[4410];
                    while (true)
                    {
                        lock (sync) { if (disposed) return; }
                        int read = source.Read(chunk, 0, chunk.Length);
                        if (read == 0) break;
                        int offset = 0;
                        lock (sync)
                        {
                            while (offset < read)
                            {
                                while (!disposed && count == samples.Length) Monitor.Wait(sync);
                                if (disposed) return;
                                int length = Math.Min(read - offset, Math.Min(samples.Length - count, samples.Length - writeIndex));
                                Array.Copy(chunk, offset, samples, writeIndex, length);
                                writeIndex = (writeIndex + length) % samples.Length;
                                count += length;
                                offset += length;
                            }
                        }
                        ready.TrySetResult(true);
                    }
                }
                ready.TrySetResult(true); // Empty files finish without hanging the caller.
            }
            catch (Exception ex)
            {
                lock (sync) { if (!disposed) error = ex; }
                ready.TrySetException(ex);
            }
            finally
            {
                lock (sync)
                {
                    completed = true;
                    Monitor.PulseAll(sync);
                }
            }
        }

        internal void MixInto(float[] destination, int sampleCount)
        {
            lock (sync)
            {
                int available = Math.Min(sampleCount, count);
                for (int i = 0; i < available; i++)
                {
                    destination[i] += samples[readIndex] * 0.6f;
                    readIndex = (readIndex + 1) % samples.Length;
                }
                count -= available;
                Monitor.PulseAll(sync);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                count = 0;
                ready.TrySetCanceled();
                Monitor.PulseAll(sync);
            }
            // The worker owns and disposes the decoder. Never wait for file I/O on the UI thread.
        }
    }
}
