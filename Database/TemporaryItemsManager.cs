using Newtonsoft.Json;
using Rocket.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Forge.SimplePromocode.Database
{
    public class TemporaryItemsManager : IDisposable
    {
        private readonly string _databasePath;
        private readonly object _lockObject = new object();
        private List<TemporaryActivation> _temporaryActivations;
        private Timer _checkTimer;
        private Timer _autoSaveTimer;
        private bool _hasChanges;

        private Dictionary<ulong, List<TemporaryActivation>> _playerActivationsIndex;
        private Dictionary<string, List<TemporaryActivation>> _promocodeActivationsIndex;

        public TemporaryItemsManager(string pluginDirectory, int checkIntervalSeconds)
        {
            _databasePath = Path.Combine(pluginDirectory, "temporary_items.json");
            InitializeCache();

            _checkTimer = new Timer(CheckExpiredItems, null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(checkIntervalSeconds));

            _autoSaveTimer = new Timer(AutoSaveCallback, null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
        }

        private void InitializeCache()
        {
            LoadDatabase();
            RebuildIndices();
        }

        private void RebuildIndices()
        {
            _playerActivationsIndex = new Dictionary<ulong, List<TemporaryActivation>>();
            _promocodeActivationsIndex = new Dictionary<string, List<TemporaryActivation>>(StringComparer.OrdinalIgnoreCase);

            foreach (TemporaryActivation activation in _temporaryActivations)
            {
                if (!_playerActivationsIndex.TryGetValue(activation.SteamId, out List<TemporaryActivation> playerActivations))
                {
                    playerActivations = new List<TemporaryActivation>();
                    _playerActivationsIndex[activation.SteamId] = playerActivations;
                }
                playerActivations.Add(activation);

                string lowerPromoName = activation.PromoName.ToLowerInvariant();
                if (!_promocodeActivationsIndex.TryGetValue(lowerPromoName, out List<TemporaryActivation> promoActivations))
                {
                    promoActivations = new List<TemporaryActivation>();
                    _promocodeActivationsIndex[lowerPromoName] = promoActivations;
                }
                promoActivations.Add(activation);
            }
        }

        private void LoadDatabase()
        {
            lock (_lockObject)
            {
                if (!File.Exists(_databasePath))
                {
                    _temporaryActivations = new List<TemporaryActivation>();
                    SaveDatabase();
                    Logger.Log($"Создана новая база данных временных активаций: {_databasePath}");
                    return;
                }

                try
                {
                    string json = File.ReadAllText(_databasePath);
                    _temporaryActivations = JsonConvert.DeserializeObject<List<TemporaryActivation>>(json) ?? new List<TemporaryActivation>();
                    Logger.Log($"Загружена база данных временных активаций из {_databasePath}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка загрузки базы данных временных активаций: {ex.Message}");
                    _temporaryActivations = new List<TemporaryActivation>();

                    if (File.Exists(_databasePath))
                    {
                        string backupPath = _databasePath + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(_databasePath, backupPath);
                        Logger.Log($"Создана резервная копия поврежденной базы данных: {backupPath}");
                    }
                    SaveDatabase();
                }
            }
        }

        public void SaveDatabase()
        {
            lock (_lockObject)
            {
                try
                {
                    string directoryPath = Path.GetDirectoryName(_databasePath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    string json = JsonConvert.SerializeObject(_temporaryActivations, Formatting.Indented);

                    string tempPath = _databasePath + ".temp";
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(_databasePath))
                    {
                        File.Delete(_databasePath);
                    }

                    File.Move(tempPath, _databasePath);
                    _hasChanges = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка сохранения базы данных временных активаций: {ex.Message}");
                }
            }
        }

        private void AutoSaveCallback(object state)
        {
            if (_hasChanges)
            {
                SaveDatabase();
            }
        }

        public TemporaryActivation AddTemporaryActivation(ulong steamId, string promoName, TimeSpan duration)
        {
            if (string.IsNullOrEmpty(promoName))
            {
                return null;
            }

            lock (_lockObject)
            {
                TemporaryActivation activation = new TemporaryActivation
                {
                    SteamId = steamId,
                    PromoName = promoName,
                    ActivationDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.Add(duration)
                };

                _temporaryActivations.Add(activation);

                if (!_playerActivationsIndex.TryGetValue(steamId, out List<TemporaryActivation> playerActivations))
                {
                    playerActivations = new List<TemporaryActivation>();
                    _playerActivationsIndex[steamId] = playerActivations;
                }
                playerActivations.Add(activation);

                string lowerPromoName = promoName.ToLowerInvariant();
                if (!_promocodeActivationsIndex.TryGetValue(lowerPromoName, out List<TemporaryActivation> promoActivations))
                {
                    promoActivations = new List<TemporaryActivation>();
                    _promocodeActivationsIndex[lowerPromoName] = promoActivations;
                }
                promoActivations.Add(activation);

                _hasChanges = true;
                return activation;
            }
        }

        public List<TemporaryActivation> GetActiveTemporaryActivations(ulong steamId)
        {
            lock (_lockObject)
            {
                if (_playerActivationsIndex.TryGetValue(steamId, out List<TemporaryActivation> activations))
                {
                    return activations
                        .Where(a => !a.IsRevoked && a.ExpiryDate > DateTime.Now)
                        .ToList();
                }
                return new List<TemporaryActivation>();
            }
        }

        public List<TemporaryActivation> GetAllTemporaryActivations()
        {
            lock (_lockObject)
            {
                return _temporaryActivations.ToList();
            }
        }

        public List<TemporaryActivation> GetExpiredActivations()
        {
            lock (_lockObject)
            {
                return _temporaryActivations
                    .Where(a => !a.IsRevoked && a.ExpiryDate <= DateTime.Now)
                    .ToList();
            }
        }

        public void MarkAsRevoked(string activationId)
        {
            lock (_lockObject)
            {
                TemporaryActivation activation = _temporaryActivations.FirstOrDefault(a => a.Id == activationId);
                if (activation != null && !activation.IsRevoked)
                {
                    activation.IsRevoked = true;
                    _hasChanges = true;
                }
            }
        }

        public void RemoveAllForPromocode(string promoName)
        {
            if (string.IsNullOrEmpty(promoName))
            {
                return;
            }

            lock (_lockObject)
            {
                string lowerPromoName = promoName.ToLowerInvariant();
                if (!_promocodeActivationsIndex.TryGetValue(lowerPromoName, out List<TemporaryActivation> activations))
                {
                    return;
                }

                List<TemporaryActivation> activationsCopy = activations.ToList();

                foreach (TemporaryActivation activation in activationsCopy)
                {
                    _temporaryActivations.Remove(activation);

                    if (_playerActivationsIndex.TryGetValue(activation.SteamId, out List<TemporaryActivation> playerActivations))
                    {
                        playerActivations.Remove(activation);
                        if (playerActivations.Count == 0)
                        {
                            _playerActivationsIndex.Remove(activation.SteamId);
                        }
                    }
                }

                _promocodeActivationsIndex.Remove(lowerPromoName);
                _hasChanges = true;
            }
        }

        private void CheckExpiredItems(object state)
        {
            List<TemporaryActivation> expiredActivations = GetExpiredActivations();

            if (expiredActivations.Count > 0)
            {
                Logger.Log($"Найдено {expiredActivations.Count} истекших временных товаров. Начинаем отзыв...");

                foreach (TemporaryActivation activation in expiredActivations)
                {
                    if (Plugin.Instance.RevokeTemporaryItem(activation))
                    {
                        Logger.Log($"Отозван временный товар {activation.PromoName} у игрока {activation.SteamId}");
                        MarkAsRevoked(activation.Id);
                    }
                }

                SaveDatabase();
            }
        }

        public void Reload()
        {
            lock (_lockObject)
            {
                if (_hasChanges)
                {
                    SaveDatabase();
                }

                LoadDatabase();
                RebuildIndices();
            }
        }

        public void CleanupRevokedActivations(int daysToKeep)
        {
            lock (_lockObject)
            {
                DateTime cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                int removed = _temporaryActivations.RemoveAll(a =>
                    a.IsRevoked && a.ExpiryDate < cutoffDate);

                if (removed > 0)
                {
                    Logger.Log($"Удалено {removed} старых отозванных временных активаций");
                    RebuildIndices();
                    _hasChanges = true;
                    SaveDatabase();
                }
            }
        }

        public void Dispose()
        {
            _checkTimer?.Dispose();
            _checkTimer = null;

            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;

            if (_hasChanges)
            {
                SaveDatabase();
            }
        }
    }
}