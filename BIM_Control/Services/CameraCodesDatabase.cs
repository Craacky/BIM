using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace BIM_Control.Services
{
    public class CameraCodesDatabase
    {
        private readonly string _connectionString;
        private readonly object _dbLock = new object();
        private SQLiteConnection _connection;
        private SQLiteTransaction _currentTransaction;
        private SQLiteCommand _insertCommand;
        private int _pendingCount = 0;
        private const int BatchSize = 500;
        private const int WriterPollMs = 50;
        private readonly BlockingCollection<CodeWriteItem> _writeQueue = new BlockingCollection<CodeWriteItem>(new ConcurrentQueue<CodeWriteItem>());
        private readonly CancellationTokenSource _writerCts = new CancellationTokenSource();
        private readonly Task _writerTask;
        private long _enqueuedItems = 0;
        private long _persistedItems = 0;
        private volatile bool _writerBusy = false;

        private sealed class CodeWriteItem
        {
            public string Code { get; init; } = string.Empty;
            public int SequenceNumber { get; init; }
        }

        public CameraCodesDatabase(string dbPath)
        {
            _connectionString = $"Data Source={dbPath};Version=3;";
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            _connection = new SQLiteConnection(_connectionString);
            _connection.Open();
            ApplyPerformancePragmas();
            InitializeDatabase();
            _writerTask = Task.Run(() => WriterLoop(_writerCts.Token));
        }

        private void ApplyPerformancePragmas()
        {
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                ExecutePragma("PRAGMA journal_mode=WAL;");
                ExecutePragma("PRAGMA synchronous=NORMAL;");
                ExecutePragma("PRAGMA temp_store=MEMORY;");
                ExecutePragma("PRAGMA cache_size=-20000;");
            }
        }

        private void ExecutePragma(string pragmaSql)
        {
            using var pragmaCommand = new SQLiteCommand(pragmaSql, _connection);
            pragmaCommand.ExecuteNonQuery();
        }

        private void InitializeDatabase()
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS CameraCodes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL,
                    SequenceNumber INTEGER,
                    ReceivedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS IX_CameraCodes_Code ON CameraCodes(Code);
                CREATE INDEX IF NOT EXISTS IX_CameraCodes_SequenceNumber ON CameraCodes(SequenceNumber);";

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                using var command = new SQLiteCommand(createTableQuery, _connection);
                command.ExecuteNonQuery();
            }
        }

        public void AddCode(string code, int sequenceNumber)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            _writeQueue.Add(new CodeWriteItem
            {
                Code = code,
                SequenceNumber = sequenceNumber
            });

            Interlocked.Increment(ref _enqueuedItems);
        }

        private void WriterLoop(CancellationToken token)
        {
            var batch = new List<CodeWriteItem>(BatchSize);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_writeQueue.TryTake(out var firstItem, WriterPollMs, token))
                    {
                        continue;
                    }

                    batch.Add(firstItem);
                    while (batch.Count < BatchSize && _writeQueue.TryTake(out var nextItem))
                    {
                        batch.Add(nextItem);
                    }

                    PersistBatch(batch);
                    batch.Clear();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            while (_writeQueue.TryTake(out var item))
            {
                batch.Add(item);
                if (batch.Count >= BatchSize)
                {
                    PersistBatch(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                PersistBatch(batch);
                batch.Clear();
            }

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
            }
        }

        private void PersistBatch(List<CodeWriteItem> batch)
        {
            if (batch == null || batch.Count == 0)
            {
                return;
            }

            _writerBusy = true;
            try
            {
                lock (_dbLock)
                {
                    EnsureConnectionOpen();
                    EnsureWriteTransaction();

                    for (int i = 0; i < batch.Count; i++)
                    {
                        _insertCommand.Parameters["@code"].Value = batch[i].Code;
                        _insertCommand.Parameters["@sequence"].Value = batch[i].SequenceNumber;
                        _insertCommand.ExecuteNonQuery();

                        _pendingCount++;
                        if (_pendingCount >= BatchSize)
                        {
                            CommitWriteTransaction();
                        }
                    }
                }

                Interlocked.Add(ref _persistedItems, batch.Count);
            }
            finally
            {
                _writerBusy = false;
            }
        }

        public List<string> GetAllCodes()
        {
            var codes = new List<string>();
            SyncQueueBeforeDbOperation();

            string selectQuery = "SELECT Code FROM CameraCodes ORDER BY SequenceNumber";
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(selectQuery, _connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    codes.Add(reader["Code"].ToString());
                }
            }

            return codes;
        }

        public int ExportAllCodesToFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Путь к файлу экспорта не задан.", nameof(filePath));
            }

            SyncQueueBeforeDbOperation();

            string? targetDirectory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new InvalidOperationException("Не удалось определить директорию для файла экспорта.");
            }

            Directory.CreateDirectory(targetDirectory);

            string tempFilePath = filePath + ".tmp";
            int exportedCount = 0;

            try
            {
                lock (_dbLock)
                {
                    EnsureConnectionOpen();
                    FlushPendingWrites();

                    using var readerCommand = new SQLiteCommand("SELECT Code FROM CameraCodes ORDER BY SequenceNumber, Id", _connection);
                    using var reader = readerCommand.ExecuteReader();
                    using var writer = new StreamWriter(tempFilePath, false);

                    while (reader.Read())
                    {
                        string code = reader["Code"]?.ToString() ?? string.Empty;
                        writer.WriteLine(code);
                        exportedCount++;
                    }
                }

                File.Move(tempFilePath, filePath, true);
                return exportedCount;
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // ignore cleanup errors
                    }
                }
                throw;
            }
        }

        public int GetCodesCount()
        {
            const string countQuery = "SELECT COUNT(*) FROM CameraCodes";
            SyncQueueBeforeDbOperation();
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(countQuery, _connection);
                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        public List<string> GetDuplicateCodes()
        {
            var duplicates = new List<string>();
            SyncQueueBeforeDbOperation();

            string duplicateQuery = @"
                SELECT Code, COUNT(*) as Count
                FROM CameraCodes
                GROUP BY Code
                HAVING COUNT(*) > 1";

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(duplicateQuery, _connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    duplicates.Add(reader["Code"].ToString());
                }
            }

            return duplicates;
        }

        public Dictionary<string, List<int>> GetDuplicateCodesWithSequenceNumbers()
        {
            var duplicates = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            SyncQueueBeforeDbOperation();

            string duplicateQuery = @"
                SELECT Code, SequenceNumber
                FROM CameraCodes
                WHERE Code IN (
                    SELECT Code
                    FROM CameraCodes
                    GROUP BY Code
                    HAVING COUNT(*) > 1
                )
                ORDER BY SequenceNumber";

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(duplicateQuery, _connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string code = reader["Code"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    if (!duplicates.TryGetValue(code, out var lineNumbers))
                    {
                        lineNumbers = new List<int>();
                        duplicates[code] = lineNumbers;
                    }

                    if (reader["SequenceNumber"] != DBNull.Value)
                    {
                        lineNumbers.Add(Convert.ToInt32(reader["SequenceNumber"]));
                    }
                }
            }

            return duplicates;
        }

        /// <summary>
        /// Проверяет дубликаты ТОЛЬКО в переданном списке кодов (не во всей БД)
        /// Используется для проверки 4 сканируемых кодов при открытой голове принтера
        /// </summary>
        public List<string> CheckDuplicatesInList(List<string> codesToCheck)
        {
            var duplicatesInList = new List<string>();
            var codeGroups = codesToCheck.GroupBy(c => c, StringComparer.OrdinalIgnoreCase);
            
            foreach (var group in codeGroups)
            {
                if (group.Count() > 1)
                {
                    duplicatesInList.Add(group.Key);
                }
            }

            return duplicatesInList;
        }

        /// <summary>
        /// Получает максимальный Id для указанных кодов
        /// Используется для определения якорной точки перед удалением кодов
        /// </summary>
        public int GetMaxIdForCodes(List<string> anchorCodes)
        {
            var codeParams = anchorCodes.Select((code, i) => $"@code{i}").ToArray();
            string query = $"SELECT MAX(Id) FROM CameraCodes WHERE Code IN ({string.Join(", ", codeParams)})";
            SyncQueueBeforeDbOperation();

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(query, _connection);
                for (int i = 0; i < anchorCodes.Count; i++)
                {
                    command.Parameters.AddWithValue($"@code{i}", anchorCodes[i]);
                }

                var result = command.ExecuteScalar();
                if (result != null && result != System.DBNull.Value)
                {
                    return (int)(long)result;
                }

                return -1;
            }
        }

        /// <summary>
        /// Получает все коды ПОСЛЕ указанного Id (для информирования оператора)
        /// Индексы (Id) остаются неизменными - это АВТОИНКРЕМЕНТ
        /// </summary>
        public List<string> GetCodesAfterMaxId(int maxId)
        {
            var codes = new List<string>();
            SyncQueueBeforeDbOperation();

            string query = "SELECT Code FROM CameraCodes WHERE Id > @maxId ORDER BY Id";
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(query, _connection);
                command.Parameters.AddWithValue("@maxId", maxId);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    codes.Add(reader["Code"].ToString());
                }
            }

            return codes;
        }

        public void ClearAllCodes()
        {
            string clearQuery = "DELETE FROM CameraCodes";
            SyncQueueBeforeDbOperation();
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(clearQuery, _connection);
                int deletedRows = command.ExecuteNonQuery();
            }
        }
        
        public int GetTotalDuplicateCount()
        {
            string countQuery = @"
                SELECT SUM(count_diff) as TotalDuplicates
                FROM (
                    SELECT (COUNT(*) - 1) as count_diff
                    FROM CameraCodes
                    GROUP BY Code
                    HAVING COUNT(*) > 1
                )";
            SyncQueueBeforeDbOperation();

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(countQuery, _connection);
                var result = command.ExecuteScalar();

                if (result != null && result != System.DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }

                return 0;
            }
        }

        public void DeleteCodesAfter(List<string> anchorCodes)
        {
            SyncQueueBeforeDbOperation();
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                // Начинаем транзакцию для атомарности операции
                using (var transaction = _connection.BeginTransaction())
                {

                    // 1. Найти ID для отсечки
                    long? anchorId = null;
                    var findIdCommand = _connection.CreateCommand();

                    // Формируем плейсхолдеры для безопасного запроса
                    var codeParams = anchorCodes.Select((code, i) => $"@code{i}").ToArray();
                    findIdCommand.CommandText = $"SELECT MAX(Id) FROM CameraCodes WHERE Code IN ({string.Join(", ", codeParams)})";

                    for(int i = 0; i < anchorCodes.Count; i++)
                    {
                        findIdCommand.Parameters.AddWithValue(codeParams[i], anchorCodes[i]);
                    }

                    var result = findIdCommand.ExecuteScalar();
                    if (result != null && result != System.DBNull.Value)
                    {
                        anchorId = (long)result;
                    }

                    // Если ни один из кодов не найден в базе, это ошибка. Откатываем.
                    if (anchorId == null)
                    {
                        transaction.Rollback();
                        throw new System.Exception("Ни один из 4-х отсканированных кодов не найден в базе данных камеры.");
                    }

                    // 2. Удалить все записи после этого ID
                    var deleteCommand = _connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM CameraCodes WHERE Id > @anchorId";
                    deleteCommand.Parameters.AddWithValue("@anchorId", anchorId.Value);
                    int deletedRows = deleteCommand.ExecuteNonQuery();


                    // 3. Если все успешно, фиксируем транзакцию
                    transaction.Commit();
                }
            }
        }

        public int DeleteCodesAfterSequence(int anchorSequence)
        {
            const string deleteQuery = "DELETE FROM CameraCodes WHERE SequenceNumber > @anchorSequence";
            SyncQueueBeforeDbOperation();
            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
                using var command = new SQLiteCommand(deleteQuery, _connection);
                command.Parameters.AddWithValue("@anchorSequence", anchorSequence);
                return command.ExecuteNonQuery();
            }
        }

        private void EnsureConnectionOpen()
        {
            if (_connection == null)
            {
                _connection = new SQLiteConnection(_connectionString);
                _connection.Open();
                return;
            }

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        private void EnsureWriteTransaction()
        {
            if (_currentTransaction != null && _currentTransaction.Connection == _connection)
            {
                return;
            }

            _currentTransaction = _connection.BeginTransaction();
            _insertCommand?.Dispose();
            _insertCommand = new SQLiteCommand("INSERT INTO CameraCodes (Code, SequenceNumber) VALUES (@code, @sequence)", _connection, _currentTransaction);
            _insertCommand.Parameters.Add(new SQLiteParameter("@code", DbType.String));
            _insertCommand.Parameters.Add(new SQLiteParameter("@sequence", DbType.Int32));
            _pendingCount = 0;
        }

        private void CommitWriteTransaction()
        {
            if (_currentTransaction == null) return;

            _currentTransaction.Commit();
            _currentTransaction.Dispose();
            _currentTransaction = null;
            _pendingCount = 0;
        }

        private void FlushPendingWrites()
        {
            if (_pendingCount <= 0)
            {
                return;
            }

            CommitWriteTransaction();
        }

        public void FlushPendingWritesPublic()
        {
            WaitForWritesToDrain(Timeout.InfiniteTimeSpan);
        }

        private void SyncQueueBeforeDbOperation()
        {
            WaitForWritesToDrain(TimeSpan.FromSeconds(30));
        }

        public int GetPendingWriteCount()
        {
            long enqueued = Interlocked.Read(ref _enqueuedItems);
            long persisted = Interlocked.Read(ref _persistedItems);
            long pending = enqueued - persisted;
            if (pending < 0) return 0;
            if (pending > int.MaxValue) return int.MaxValue;
            return (int)pending;
        }

        public void WaitForWritesToDrain(TimeSpan timeout)
        {
            long targetPersisted = Interlocked.Read(ref _enqueuedItems);
            DateTime startedAt = DateTime.UtcNow;

            while (Interlocked.Read(ref _persistedItems) < targetPersisted || _writerBusy)
            {
                if (timeout != Timeout.InfiniteTimeSpan && DateTime.UtcNow - startedAt > timeout)
                {
                    throw new TimeoutException($"Timeout waiting for camera DB writes to drain. Pending={GetPendingWriteCount()}");
                }
                Thread.Sleep(15);
            }

            lock (_dbLock)
            {
                EnsureConnectionOpen();
                FlushPendingWrites();
            }
        }
    }
}
