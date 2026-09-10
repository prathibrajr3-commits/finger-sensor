using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Utilities;

namespace AirGestureAI.ModelZoo
{
    /// <summary>
    /// Simulates asynchronous downloading of ONNX model files to the local model directory.
    /// </summary>
    public class ModelDownloader
    {
        /// <summary>Raised as download progress changes (0–100).</summary>
        public event Action<int>? ProgressChanged;

        /// <summary>
        /// Simulates downloading a model to the specified local path.
        /// </summary>
        public async Task<bool> DownloadAsync(ModelMetadata model, string localDirectory,
            CancellationToken cancellationToken)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            Logger.Info($"ModelDownloader: Starting download of '{model.DisplayName}'...");
            Directory.CreateDirectory(localDirectory);

            // Simulate progressive download in 10 steps
            for (int progress = 10; progress <= 100; progress += 10)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(80, cancellationToken);
                ProgressChanged?.Invoke(progress);
            }

            // Write a placeholder file for testing purposes
            var outputPath = Path.Combine(localDirectory, Path.GetFileName(model.FilePath));
            await File.WriteAllTextAsync(outputPath,
                $"// Mock ONNX model: {model.DisplayName} v{model.Version}", cancellationToken);

            Logger.Info($"ModelDownloader: '{model.DisplayName}' saved to '{outputPath}'.");
            return true;
        }
    }
}
