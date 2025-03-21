using Newtonsoft.Json;
using Rocket.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Forge.SimplePromocode.Database
{
    public class DatabaseManager : IDisposable
    {
        private readonly string _databasePath;
        private readonly object _lockObject = new object();
        private List<PromoActivation> _activations;
        private Timer _autoSaveTimer;
        private bool _hasChanges;

        private Dictionary<ulong, List<PromoActivation>> _playerActivationsIndex;
        private Dictionary<string, List<PromoActivation>> _promocodeActivationsIndex;

        public DatabaseManager(string pluginDirectory)
        {
            _databasePath = Path.Combine(pluginDirectory, "activations.json");
            InitializeCache();
            _autoSaveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private void InitializeCache()
        {
            LoadDatabase();
            RebuildIndices();
        }

        private void RebuildIndices()
        {
            _playerActivationsIndex = new Dictionary<ulong, List<PromoActivation>>();
            _promocodeActivationsIndex = new Dictionary<string, List<PromoActivation>>(StringComparer.OrdinalIgnoreCase);

            foreach (PromoActivation activation in _activations)
            {
                if (!_playerActivationsIndex.TryGetValue(activation.SteamId, out List<PromoActivation> playerActivations))
                {
                    playerActivations = new List<PromoActivation>();
                    _playerActivationsIndex[activation.SteamId] = playerActivations;
                }
                playerActivations.Add(activation);

                string lowerPromoName = activation.PromoName.ToLowerInvariant();
                if (!_promocodeActivationsIndex.TryGetValue(lowerPromoName, out List<PromoActivation> promoActivations))
                {
                    promoActivations = new List<PromoActivation>();
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
                    _activations = new List<PromoActivation>();
                    SaveDatabase();
                    Logger.Log($"Создана новая база данных активаций промокодов: {_databasePath}");
                    return;
                }

                try
                {
                    string json = File.ReadAllText(_databasePath);
                    _activations = JsonConvert.DeserializeObject<List<PromoActivation>>(json) ?? new List<PromoActivation>();
                    Logger.Log($"Загружена база данных активаций из {_databasePath}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка загрузки базы данных активаций: {ex.Message}");
                    _activations = new List<PromoActivation>();

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

                    string json = JsonConvert.SerializeObject(_activations, Formatting.Indented);

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
                    Logger.LogError($"Ошибка сохранения базы данных активаций: {ex.Message}");
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

        public List<PromoActivation> GetAllActivations()
        {
            lock (_lockObject)
            {
                return _activations.ToList();
            }
        }

        public List<PromoActivation> GetActivationsForPlayer(ulong steamId)
        {
            lock (_lockObject)
            {
                if (_playerActivationsIndex.TryGetValue(steamId, out List<PromoActivation> activations))
                {
                    return activations.ToList();
                }
                return new List<PromoActivation>();
            }
        }

        public List<PromoActivation> GetActivationsForPromocode(string promoName)
        {
            if (string.IsNullOrEmpty(promoName))
            {
                return new List<PromoActivation>();
            }

            lock (_lockObject)
            {
                if (_promocodeActivationsIndex.TryGetValue(promoName.ToLowerInvariant(), out List<PromoActivation> activations))
                {
                    return activations.ToList();
                }
                return new List<PromoActivation>();
            }
        }

        public bool HasPlayerActivatedPromo(ulong steamId, string promoName)
        {
            if (string.IsNullOrEmpty(promoName))
            {
                return false;
            }

            lock (_lockObject)
            {
                if (_playerActivationsIndex.TryGetValue(steamId, out List<PromoActivation> activations))
                {
                    return activations.Any(a => a.PromoName.Equals(promoName, StringComparison.OrdinalIgnoreCase));
                }
                return false;
            }
        }

        public int GetPromocodeActivationCount(string promoName)
        {
            if (string.IsNullOrEmpty(promoName))
            {
                return 0;
            }

            lock (_lockObject)
            {
                if (_promocodeActivationsIndex.TryGetValue(promoName.ToLowerInvariant(), out List<PromoActivation> activations))
                {
                    return activations.Count;
                }
                return 0;
            }
        }

        public bool AddActivation(PromoActivation activation)
        {
            if (activation == null || string.IsNullOrEmpty(activation.PromoName))
            {
                return false;
            }

            lock (_lockObject)
            {
                if (HasPlayerActivatedPromo(activation.SteamId, activation.PromoName))
                {
                    return false;
                }

                _activations.Add(activation);

                if (!_playerActivationsIndex.TryGetValue(activation.SteamId, out List<PromoActivation> playerActivations))
                {
                    playerActivations = new List<PromoActivation>();
                    _playerActivationsIndex[activation.SteamId] = playerActivations;
                }
                playerActivations.Add(activation);

                string lowerPromoName = activation.PromoName.ToLowerInvariant();
                if (!_promocodeActivationsIndex.TryGetValue(lowerPromoName, out List<PromoActivation> promoActivations))
                {
                    promoActivations = new List<PromoActivation>();
                    _promocodeActivationsIndex[lowerPromoName] = promoActivations;
                }
                promoActivations.Add(activation);

                _hasChanges = true;
                return true;
            }
        }

        public void RemoveAllActivationsForPromocode(string promoName)
        {
            if (string.IsNullOrEmpty(promoName))
            {
                return;
            }

            lock (_lockObject)
            {
                string lowerPromoName = promoName.ToLowerInvariant();
                if (!_promocodeActivationsIndex.TryGetValue(lowerPromoName, out List<PromoActivation> activations))
                {
                    return;
                }

                List<PromoActivation> activationsCopy = activations.ToList();

                foreach (PromoActivation activation in activationsCopy)
                {
                    _activations.Remove(activation);

                    if (_playerActivationsIndex.TryGetValue(activation.SteamId, out List<PromoActivation> playerActivations))
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

        public void Dispose()
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;

            if (_hasChanges)
            {
                SaveDatabase();
            }
        }
    }
}