using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirGestureAI.Services;
using AirGestureAI.Utilities;
using Xunit;

namespace AirGestureAI.Tests
{
    public sealed class LoggingServiceTests : IDisposable
    {
        private readonly string _tempTestDir;

        public LoggingServiceTests()
        {
            _tempTestDir = Path.Combine(Path.GetTempPath(), "AirGestureAI_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempTestDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempTestDir))
                {
                    Directory.Delete(_tempTestDir, true);
                }
            }
            catch
            {
                // Silence cleanup errors in test environment
            }
        }

        [Fact]
        public async Task TestLoggingServiceWritesEntryToFile()
        {
            // Arrange
            await using var loggingService = new LoggingService(_tempTestDir);
            string testMessage = "Test log message for file write check.";

            // Act
            loggingService.Information(testMessage, "UnitTest");

            // Wait brief moment for async channel to write to file
            await Task.Delay(200);

            // Assert
            var logFiles = Directory.GetFiles(Path.Combine(_tempTestDir, "Logs"), "airgesture_*.log");
            Assert.NotEmpty(logFiles);

            string logFileContent;
            using (var fs = new FileStream(logFiles.First(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                logFileContent = reader.ReadToEnd();
            }
            Assert.Contains(testMessage, logFileContent);
            Assert.Contains("Information", logFileContent);
            Assert.Contains("UnitTest", logFileContent);
        }

        [Fact]
        public async Task TestLoggingServiceSearchFiltersSuccessfully()
        {
            // Arrange
            await using var loggingService = new LoggingService(_tempTestDir);
            loggingService.Information("First entry message to match", "SearchTest", "Corr-001");
            loggingService.Warning("Second entry warning", "SearchTest", "Corr-002");
            loggingService.Error("Third error occurred", new InvalidOperationException("Test exception details"), "SearchTest", "Corr-003");

            await Task.Delay(200);

            // Act
            var allResults = await loggingService.SearchLogsAsync(null, null, null, null);
            var searchKeyword = await loggingService.SearchLogsAsync("warning", null, null, null);
            var searchLevel = await loggingService.SearchLogsAsync(null, LogLevel.Warning, null, null);
            var searchCorrelation = await loggingService.SearchLogsAsync("Corr-003", null, null, null);

            // Assert
            Assert.Equal(3, allResults.Count);
            Assert.Single(searchKeyword);
            Assert.Equal("Second entry warning", searchKeyword[0].Message);
            Assert.Equal(2, searchLevel.Count); // Warning and Error
            Assert.Single(searchCorrelation);
            Assert.Contains("Test exception details", searchCorrelation[0].ExceptionInfo ?? string.Empty);
        }

        [Fact]
        public async Task TestLoggingServiceSubscriberFiltering()
        {
            // Arrange
            await using var loggingService = new LoggingService(_tempTestDir);
            var mockSubscriber = new TestSubscriber(LogLevel.Warning);
            loggingService.AddSubscriber(mockSubscriber);

            // Act
            loggingService.Information("Info level entry - should be skipped by sub");
            loggingService.Warning("Warning level entry - should be received");
            loggingService.Critical("Critical level entry - should be received");

            await Task.Delay(100);

            // Assert
            Assert.Equal(2, mockSubscriber.ReceivedEntries.Count);
            Assert.Equal("Warning level entry - should be received", mockSubscriber.ReceivedEntries[0].Message);
            Assert.Equal("Critical level entry - should be received", mockSubscriber.ReceivedEntries[1].Message);
        }

        [Fact]
        public async Task TestLoggingServiceRotationByFileSize()
        {
            // Arrange
            await using var loggingService = new LoggingService(_tempTestDir)
            {
                MaxFileSizeBytes = 100 // Minimal file size threshold to force quick rotation
            };

            // Act - write several large entries
            for (int i = 0; i < 10; i++)
            {
                loggingService.Information(new string('X', 120), "RotationTest");
            }

            await Task.Delay(200);

            // Assert
            var logFiles = Directory.GetFiles(Path.Combine(_tempTestDir, "Logs"), "airgesture_*.log");
            // Multiple rotated files should exist due to very small size threshold
            Assert.True(logFiles.Length > 1);
        }

        [Fact]
        public async Task TestLoggingServiceExportCsvAndJson()
        {
            // Arrange
            await using var loggingService = new LoggingService(_tempTestDir);
            loggingService.Information("Row 1 content", "ExportTest");
            loggingService.Warning("Row 2 alert", "ExportTest");

            await Task.Delay(200);

            var csvPath = Path.Combine(_tempTestDir, "export.csv");
            var jsonPath = Path.Combine(_tempTestDir, "export.json");

            // Act
            await loggingService.ExportToCsvAsync(csvPath, null, null, null, null);
            await loggingService.ExportToJsonAsync(jsonPath, null, null, null, null);

            // Assert
            Assert.True(File.Exists(csvPath));
            Assert.True(File.Exists(jsonPath));

            var csvLines = File.ReadAllLines(csvPath);
            Assert.True(csvLines.Length >= 3); // Header + 2 data rows
            Assert.Contains("TimestampUtc,LogLevel,Subsystem,CorrelationId,Message,ExceptionInfo", csvLines[0]);
            Assert.Contains("Row 1 content", csvLines[1]);

            var jsonContent = File.ReadAllText(jsonPath);
            Assert.Contains("Row 2 alert", jsonContent);
        }

        [Fact]
        public async Task TestStaticLoggerBridgeCompatibility()
        {
            // Arrange
            await using var loggingService = new LoggingService(_tempTestDir);
            Logger.Initialize(loggingService);

            string legacyMessage = "Legacy log message routed through static bridge.";

            // Act
            Logger.Info(legacyMessage);

            await Task.Delay(200);

            // Assert
            var searchResults = await loggingService.SearchLogsAsync(legacyMessage, null, null, null);
            Assert.Single(searchResults);
            Assert.Equal("LegacyLogger", searchResults[0].Subsystem);
            Assert.Equal(legacyMessage, searchResults[0].Message);
        }

        private sealed class TestSubscriber : ILogSubscriber
        {
            public LogLevel MinimumLevel { get; }
            public System.Collections.Generic.List<LogEntry> ReceivedEntries { get; } = new();

            public TestSubscriber(LogLevel minimumLevel)
            {
                MinimumLevel = minimumLevel;
            }

            public void OnLogEntry(LogEntry entry)
            {
                ReceivedEntries.Add(entry);
            }
        }
    }
}
