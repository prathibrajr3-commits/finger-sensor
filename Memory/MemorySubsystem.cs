using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.Memory
{
    // ── Memory Models ─────────────────────────────────────────────────────────

    /// <summary>A serialized memory entry with key, value, and metadata.</summary>
    public sealed class MemoryDocument
    {
        /// <summary>Gets the unique identifier for this memory entry.</summary>
        public string Id { get; init; } = Guid.NewGuid().ToString()[..8];

        /// <summary>Gets or sets the memory key/topic.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Gets or sets the stored value or description.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Gets or sets the timestamp when this memory was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Gets or sets tags for categorization.</summary>
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>Connects two memory nodes with a relationship label.</summary>
    public sealed class MemoryRelation
    {
        /// <summary>Gets or sets the source memory entry ID.</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Gets or sets the target memory entry ID.</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Gets or sets the relationship type (e.g. "Related", "Causes").</summary>
        public string RelationType { get; set; } = "Related";
    }

    // ── TF-IDF Vector Search ──────────────────────────────────────────────────

    /// <summary>
    /// Real TF-IDF ranked document search.
    /// Computes per-term TF (term frequency within a doc) and IDF (inverse document
    /// frequency across the corpus) and ranks all documents by their cosine-normalised
    /// TF-IDF score against the query.
    /// </summary>
    public sealed class MemorySearch
    {
        private static readonly char[] Delimiters =
            " \t\r\n.,;:!?\"'()[]{}/<>\\|@#$%^&*+-=~`".ToCharArray();

        private static string[] Tokenize(string text)
            => text.ToLowerInvariant()
                   .Split(Delimiters, StringSplitOptions.RemoveEmptyEntries);

        private static Dictionary<string, double> BuildIdf(IReadOnlyList<string[]> tokenizedDocs)
        {
            int N = tokenizedDocs.Count;
            var df = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var doc in tokenizedDocs)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tok in doc)
                {
                    if (seen.Add(tok))
                        df[tok] = df.TryGetValue(tok, out var c) ? c + 1 : 1;
                }
            }

            var idf = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (term, count) in df)
                idf[term] = Math.Log((N + 1.0) / (count + 1.0)) + 1.0; // smoothed IDF

            return idf;
        }

        private static Dictionary<string, double> BuildTf(string[] tokens)
        {
            if (tokens.Length == 0) return new Dictionary<string, double>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var tok in tokens)
                counts[tok] = counts.TryGetValue(tok, out var c) ? c + 1 : 1;

            return counts.ToDictionary(
                kv => kv.Key,
                kv => (double)kv.Value / tokens.Length,
                StringComparer.Ordinal);
        }

        private static double CosineSimilarity(
            Dictionary<string, double> vecA,
            Dictionary<string, double> vecB)
        {
            double dot = 0, normA = 0, normB = 0;
            foreach (var (term, a) in vecA)
            {
                normA += a * a;
                if (vecB.TryGetValue(term, out var b)) dot += a * b;
            }
            foreach (var (_, b) in vecB) normB += b * b;

            double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denom < 1e-10 ? 0.0 : dot / denom;
        }

        /// <summary>
        /// Ranks documents by TF-IDF cosine similarity to the query.
        /// Returns the top-<paramref name="topK"/> most relevant results ordered by descending score.
        /// </summary>
        public List<MemoryDocument> Search(
            IEnumerable<MemoryDocument> documents,
            string query,
            int topK = 20)
        {
            var docs = documents.ToList();
            if (docs.Count == 0 || string.IsNullOrWhiteSpace(query))
                return new List<MemoryDocument>();

            var corpus = docs
                .Select(d => Tokenize(d.Key + " " + d.Value))
                .ToList();

            var idf = BuildIdf(corpus);

            var docVectors = corpus
                .Select(tokens =>
                {
                    var tf = BuildTf(tokens);
                    return tf.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value * (idf.TryGetValue(kv.Key, out var w) ? w : 1.0),
                        StringComparer.Ordinal);
                })
                .ToList();

            var qTokens = Tokenize(query);
            var qTf     = BuildTf(qTokens);
            var qVec    = qTf.ToDictionary(
                kv => kv.Key,
                kv => kv.Value * (idf.TryGetValue(kv.Key, out var w) ? w : 1.0),
                StringComparer.Ordinal);

            var ranked = docs
                .Select((doc, i) => (doc, score: CosineSimilarity(qVec, docVectors[i])))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x => x.doc)
                .ToList();

            Logger.Info($"MemorySearch: TF-IDF ranked {ranked.Count}/{docs.Count} results for '{query}'.");
            return ranked;
        }
    }

    // ── Timeline Compression ──────────────────────────────────────────────────

    /// <summary>
    /// Compresses old memory documents by storing only unique, delta-encoded
    /// timestamps and deduplicating near-identical summaries.
    /// </summary>
    public sealed class MemoryCompression
    {
        private const int MaxKeyLength = 40;
        private const int MaxValLength = 80;

        /// <summary>
        /// Returns a compact, delta-encoded, deduplicated summary of the provided documents
        /// sorted by creation time.
        /// </summary>
        public string Compress(IEnumerable<MemoryDocument> documents)
        {
            var sorted = documents.OrderBy(d => d.CreatedAt).ToList();
            if (sorted.Count == 0) return "[Compressed:empty]";

            var sb = new StringBuilder();
            sb.Append("[Compressed:").Append(sorted.Count).Append(" entries");

            DateTime? prev = null;
            var seenSummaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var doc in sorted)
            {
                string deltaStr;
                if (prev is null)
                {
                    deltaStr = doc.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
                }
                else
                {
                    var delta = doc.CreatedAt - prev.Value;
                    deltaStr = delta.TotalSeconds < 1  ? "+<1s"
                             : delta.TotalMinutes < 1  ? $"+{(int)delta.TotalSeconds}s"
                             : delta.TotalHours   < 1  ? $"+{(int)delta.TotalMinutes}m"
                                                        : $"+{delta.TotalHours:F1}h";
                }
                prev = doc.CreatedAt;

                var key = doc.Key.Length > MaxKeyLength ? doc.Key[..MaxKeyLength] + "…" : doc.Key;
                var val = doc.Value.Length > MaxValLength ? doc.Value[..MaxValLength] + "…" : doc.Value;
                var summary = $"{key}={val}";

                if (!seenSummaries.Add(summary.ToLowerInvariant())) continue;
                sb.Append($"|{deltaStr}:{summary}");
            }

            sb.Append(']');
            return sb.ToString();
        }
    }

    // ── Persistent JSON Archive ───────────────────────────────────────────────

    /// <summary>Manages file persistence of the memory database using full JSON serialization.</summary>
    public sealed class MemoryArchive
    {
        private readonly string _path;
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

        /// <summary>Initializes a new instance of <see cref="MemoryArchive"/>.</summary>
        public MemoryArchive(string path = "MemoryDatabase")
        {
            _path = path;
            Directory.CreateDirectory(_path);
        }

        /// <summary>Persists a memory document to the archive directory using full JSON.</summary>
        public void Save(MemoryDocument doc)
        {
            try
            {
                var file = Path.Combine(_path, $"{doc.Id}.json");
                var payload = JsonSerializer.Serialize(new
                {
                    doc.Id,
                    doc.Key,
                    doc.Value,
                    CreatedAt = doc.CreatedAt.ToString("O"),
                    doc.Tags
                }, JsonOpts);
                File.WriteAllText(file, payload, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error($"MemoryArchive: Failed to persist '{doc.Id}': {ex.Message}");
            }
        }

        /// <summary>Loads all previously persisted documents from disk on startup.</summary>
        public List<MemoryDocument> LoadAll()
        {
            var result = new List<MemoryDocument>();
            if (!Directory.Exists(_path)) return result;

            foreach (var file in Directory.EnumerateFiles(_path, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file, Encoding.UTF8);
                    using var jdoc = JsonDocument.Parse(json);
                    var root = jdoc.RootElement;

                    result.Add(new MemoryDocument
                    {
                        Id        = root.TryGetProperty("Id",        out var id) ? id.GetString()  ?? "" : "",
                        Key       = root.TryGetProperty("Key",       out var k)  ? k.GetString()   ?? "" : "",
                        Value     = root.TryGetProperty("Value",     out var v)  ? v.GetString()   ?? "" : "",
                        CreatedAt = root.TryGetProperty("CreatedAt", out var ts)
                                    && DateTime.TryParse(ts.GetString(), out var dt) ? dt : DateTime.UtcNow,
                        Tags = root.TryGetProperty("Tags", out var tags)
                               ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToList()
                               : new List<string>()
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error($"MemoryArchive: Failed to load '{file}': {ex.Message}");
                }
            }

            return result;
        }
    }

    // ── Timeline ──────────────────────────────────────────────────────────────

    /// <summary>Chronological timeline of user goal history entries with bounded capacity.</summary>
    public sealed class MemoryTimeline
    {
        private const int MaxEvents = 1000;
        private readonly List<(DateTime Timestamp, string Description)> _events = new();

        /// <summary>Gets all timeline events.</summary>
        public IReadOnlyList<(DateTime Timestamp, string Description)> Events => _events;

        /// <summary>Adds an event to the timeline, evicting the oldest when capacity is exceeded.</summary>
        public void Add(string description)
        {
            if (_events.Count >= MaxEvents) _events.RemoveAt(0);
            _events.Add((DateTime.UtcNow, description));
        }

        /// <summary>Returns all events as a compact delta-encoded string.</summary>
        public string ToDeltaString()
        {
            if (_events.Count == 0) return "[timeline:empty]";
            var sb = new StringBuilder("[timeline:");
            DateTime? prev = null;
            foreach (var (ts, desc) in _events)
            {
                string delta = prev is null
                    ? ts.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    : $"+{(int)(ts - prev.Value).TotalSeconds}s";
                prev = ts;
                sb.Append($"|{delta}:{desc}");
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    // ── Working and Conversation Memory ───────────────────────────────────────

    /// <summary>Active short-term working memory for the current session.</summary>
    public sealed class WorkingMemory
    {
        private readonly Dictionary<string, string> _store = new();

        /// <summary>Stores a value.</summary>
        public void Set(string key, string value) => _store[key] = value;

        /// <summary>Retrieves a stored value.</summary>
        public string? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;

        /// <summary>Clears all entries from working memory.</summary>
        public void Clear() => _store.Clear();
    }

    /// <summary>Conversation history for the current chat session.</summary>
    public sealed class ConversationMemory
    {
        private readonly List<(string Role, string Text)> _messages = new();

        /// <summary>Gets all conversation messages.</summary>
        public IReadOnlyList<(string Role, string Text)> Messages => _messages;

        /// <summary>Appends a message to the conversation.</summary>
        public void Append(string role, string text) => _messages.Add((role, text));

        /// <summary>Trims the oldest messages when the history exceeds <paramref name="maxMessages"/>.</summary>
        public void Trim(int maxMessages = 200)
        {
            if (_messages.Count > maxMessages)
                _messages.RemoveRange(0, _messages.Count - maxMessages);
        }
    }

    // ── Central Memory Manager ────────────────────────────────────────────────

    /// <summary>
    /// Coordinates all memory databases including working, conversation, and long-term stores.
    /// Uses TF-IDF for ranked retrieval and delta-encoded timeline compression for history storage.
    /// </summary>
    public sealed class MemoryManager
    {
        private readonly List<MemoryDocument> _documents = new();
        private readonly List<MemoryRelation> _relations = new();
        private readonly MemorySearch        _search      = new();
        private readonly MemoryCompression   _compression = new();
        private readonly MemoryArchive       _archive;

        /// <summary>Gets the working memory instance.</summary>
        public WorkingMemory Working { get; } = new();

        /// <summary>Gets the conversation memory instance.</summary>
        public ConversationMemory Conversation { get; } = new();

        /// <summary>Gets the activity timeline.</summary>
        public MemoryTimeline Timeline { get; } = new();

        /// <summary>Gets all stored documents.</summary>
        public IReadOnlyList<MemoryDocument> Documents => _documents;

        /// <summary>Initializes a new instance of <see cref="MemoryManager"/>.</summary>
        public MemoryManager(string archivePath = "MemoryDatabase")
        {
            _archive = new MemoryArchive(archivePath);
            var loaded = _archive.LoadAll();
            _documents.AddRange(loaded);
            Logger.Info($"MemoryManager: Restored {loaded.Count} documents from archive '{archivePath}'.");
        }

        /// <summary>Stores a new memory document and persists it.</summary>
        public MemoryDocument Store(string key, string value, params string[] tags)
        {
            var doc = new MemoryDocument { Key = key, Value = value, Tags = new List<string>(tags) };
            _documents.Add(doc);
            _archive.Save(doc);
            Timeline.Add($"Stored: {key}");
            Logger.Info($"MemoryManager: Stored '{key}' (id={doc.Id}).");
            return doc;
        }

        /// <summary>Searches memory documents by TF-IDF ranked relevance.</summary>
        public List<MemoryDocument> Search(string query, int topK = 20)
            => _search.Search(_documents, query, topK);

        /// <summary>Adds a relation between two memory documents.</summary>
        public void AddRelation(string sourceId, string targetId, string type = "Related")
            => _relations.Add(new MemoryRelation
            {
                SourceId     = sourceId,
                TargetId     = targetId,
                RelationType = type
            });

        /// <summary>
        /// Compresses older documents into a single summary entry, retaining the newest
        /// <paramref name="keepNewest"/> documents verbatim.
        /// </summary>
        public void CompressOldDocuments(int keepNewest = 100)
        {
            if (_documents.Count <= keepNewest) return;
            var toCompress = _documents.OrderBy(d => d.CreatedAt).Take(_documents.Count - keepNewest).ToList();
            var summary = _compression.Compress(toCompress);
            var compressed = new MemoryDocument
            {
                Key   = "_compressed_",
                Value = summary,
                Tags  = new List<string> { "compressed" }
            };
            foreach (var d in toCompress) _documents.Remove(d);
            _documents.Insert(0, compressed);
            _archive.Save(compressed);
            Logger.Info($"MemoryManager: Compressed {toCompress.Count} old documents into 1 summary entry.");
        }
    }
}
