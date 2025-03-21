using Forge.SimplePromocode.Database;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Forge.SimplePromocode
{
    public class Plugin : RocketPlugin<Configuration>
    {
        public static Plugin Instance { get; private set; }
        public DatabaseManager Database { get; private set; }
        public TemporaryItemsManager TemporaryItems { get; private set; }

        private Dictionary<string, Promocode> _promocodeCache;
        private Dictionary<string, int> _activationCountCache;
        private Timer _cleanupTimer;

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "prefix", "[Промокоды] " },
            { "promocode_not_found", "Промокод \"{0}\" не найден." },
            { "promocode_expired", "Промокод \"{0}\" истек. Истекает: {1}" },
            { "promocode_no_permission", "У вас нет разрешения использовать этот промокод." },
            { "promocode_already_activated", "Вы уже активировали промокод \"{0}\"." },
            { "promocode_max_activations", "Промокод \"{0}\" достиг максимального количества активаций." },
            { "promocode_activated", "Промокод \"{0}\" успешно активирован!" },
            { "promocode_activated_temporary", "Промокод \"{0}\" успешно активирован! Действует до: {1}" },
            { "command_success", "Команда выполнена успешно." },
            { "command_error", "Ошибка при выполнении команды: {0}" },
            { "promocode_created", "Промокод \"{0}\" успешно создан." },
            { "promocode_deleted", "Промокод \"{0}\" успешно удален." },
            { "promocode_edited", "Промокод \"{0}\" успешно отредактирован." },
            { "config_reloaded", "Конфигурация успешно перезагружена." },
            { "temporary_item_revoked", "Срок действия временного товара \"{0}\" истек." },
            { "no_temporary_items", "У вас нет активных временных товаров." },
            { "temp_items_header", "Ваши активные временные товары:" },
            { "temp_items_list_item", "- {0}: истекает {1} (осталось {2} ч {3} мин)" }
        };

        protected override void Load()
        {
            Instance = this;

            try
            {
                Database = new DatabaseManager(Directory);
                Logger.Log("База данных активаций промокодов загружена.");

                TemporaryItems = new TemporaryItemsManager(Directory, Configuration.Instance.TemporaryItemsCheckInterval);
                Logger.Log("Менеджер временных товаров инициализирован.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка при загрузке базы данных: {ex.Message}");
                Database = null;
            }

            RefreshPromocodeCache();

            _cleanupTimer = new Timer(CleanupExpiredPromocodes, null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

            Level.onLevelLoaded += OnLevelLoaded;
            Logger.Log($"SimplePromocode v{Assembly.GetName().Version} загружен!");
        }

        protected override void Unload()
        {
            Level.onLevelLoaded -= OnLevelLoaded;

            _cleanupTimer?.Dispose();
            _cleanupTimer = null;

            TemporaryItems?.Dispose();
            TemporaryItems = null;

            Database?.Dispose();
            Database = null;

            _promocodeCache = null;
            _activationCountCache = null;

            Instance = null;
            Logger.Log("SimplePromocode выгружен.");
        }

        private void OnLevelLoaded(int level)
        {
            try
            {
                if (Database != null)
                {
                    List<string> existingPromoNames = Configuration.Instance.Promocodes.Select(p => p.Name.ToLowerInvariant()).ToList();
                    List<PromoActivation> allActivations = Database.GetAllActivations();

                    List<string> unusedActivations = allActivations
                        .Where(a => !existingPromoNames.Contains(a.PromoName.ToLowerInvariant()))
                        .Select(a => a.PromoName)
                        .Distinct()
                        .ToList();

                    foreach (string promoName in unusedActivations)
                    {
                        Database.RemoveAllActivationsForPromocode(promoName);
                        Logger.Log($"Удалены активации для несуществующего промокода: {promoName}");
                    }

                    RefreshActivationCountCache();

                    Logger.Log($"Проверка базы данных завершена. {unusedActivations.Count} неиспользуемых промокодов очищено.");

                    TemporaryItems.CleanupRevokedActivations(30);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка при проверке базы данных: {ex.Message}");
            }
        }

        private void CleanupExpiredPromocodes(object state)
        {
            try
            {
                List<Promocode> expiredPromocodes = Configuration.Instance.Promocodes
                    .Where(p => p.IsExpired)
                    .ToList();

                if (expiredPromocodes.Count > 0)
                {
                    foreach (Promocode promo in expiredPromocodes)
                    {
                        Configuration.Instance.Promocodes.Remove(promo);
                        Database.RemoveAllActivationsForPromocode(promo.Name);

                        TemporaryItems.RemoveAllForPromocode(promo.Name);

                        Logger.Log($"Автоматически удален истекший промокод: {promo.Name}");
                    }

                    Configuration.Save();
                    RefreshPromocodeCache();

                    Logger.Log($"Очистка истекших промокодов завершена. Удалено {expiredPromocodes.Count} промокодов.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка при очистке истекших промокодов: {ex.Message}");
            }
        }

        public bool ActivatePromocode(ulong steamId, string promoName)
        {
            if (Database == null || string.IsNullOrEmpty(promoName))
            {
                return false;
            }

            Promocode promo = GetPromocode(promoName);
            if (promo == null)
            {
                return false;
            }

            if (HasPlayerActivatedPromo(steamId, promoName))
            {
                return false;
            }

            int currentActivations = GetPromocodeActivationCount(promoName);
            if (currentActivations >= promo.MaxActivations)
            {
                return false;
            }

            PromoActivation activation = new PromoActivation
            {
                SteamId = steamId,
                PromoName = promoName,
                ActivationDate = DateTime.Now
            };

            bool success = Database.AddActivation(activation);

            if (success)
            {
                if (_activationCountCache != null && _activationCountCache.ContainsKey(promoName))
                {
                    _activationCountCache[promoName]++;
                }

                if (promo.IsTemporary && promo.TemporaryDuration > TimeSpan.Zero)
                {
                    TemporaryItems.AddTemporaryActivation(steamId, promoName, promo.TemporaryDuration);
                }
            }

            return success;
        }

        public void ExecutePromocodeCommands(UnturnedPlayer player, Promocode promo)
        {
            if (player == null || promo == null || promo.Commands == null || promo.Commands.Count == 0)
            {
                return;
            }

            try
            {
                foreach (string command in promo.Commands)
                {
                    string parsedCommand = command
                        .Replace("@p", player.CharacterName)
                        .Replace("@pid", player.CSteamID.ToString())
                        .Replace("@s", "server");

                    R.Commands.Execute(new ConsolePlayer(), parsedCommand);
                }

                if (promo.IsTemporary && promo.TemporaryDuration > TimeSpan.Zero)
                {
                    DateTime expiryDate = DateTime.Now.Add(promo.TemporaryDuration);

                    string message = Translations.Instance.Translate(
                        "promocode_activated_temporary",
                        promo.Name,
                        expiryDate.ToString("dd.MM.yyyy HH:mm")
                    );

                    UnturnedChat.Say(
                        player,
                        Translations.Instance.Translate("prefix") + message,
                        UnityEngine.Color.green
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка при выполнении команд промокода: {ex.Message}");
            }
        }

        public bool RevokeTemporaryItem(TemporaryActivation activation)
        {
            if (activation == null)
            {
                return false;
            }

            try
            {
                Promocode promo = GetPromocode(activation.PromoName);
                if (promo == null || !promo.IsTemporary || promo.RemoveCommands == null || promo.RemoveCommands.Count == 0)
                {
                    return false;
                }

                UnturnedPlayer player = null;
                Steamworks.CSteamID steamId = new Steamworks.CSteamID(activation.SteamId);
                try
                {
                    player = UnturnedPlayer.FromCSteamID(steamId);
                }
                catch { }

                foreach (string command in promo.RemoveCommands)
                {
                    string parsedCommand = command
                        .Replace("@pid", activation.SteamId.ToString())
                        .Replace("@p", player?.CharacterName ?? activation.SteamId.ToString())
                        .Replace("@s", "server");

                    R.Commands.Execute(new ConsolePlayer(), parsedCommand);
                }

                if (player != null)
                {
                    string message = Translations.Instance.Translate("temporary_item_revoked", activation.PromoName);
                    UnturnedChat.Say(
                        player,
                        Translations.Instance.Translate("prefix") + message,
                        UnityEngine.Color.yellow
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка при отзыве временного товара: {ex.Message}");
                return false;
            }
        }

        public void RefreshPromocodeCache()
        {
            _promocodeCache = new Dictionary<string, Promocode>(StringComparer.OrdinalIgnoreCase);
            foreach (Promocode promo in Configuration.Instance.Promocodes)
            {
                _promocodeCache[promo.Name] = promo;
            }

            RefreshActivationCountCache();
        }

        public void RefreshActivationCountCache()
        {
            if (Database == null)
            {
                _activationCountCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            _activationCountCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Promocode promo in Configuration.Instance.Promocodes)
            {
                _activationCountCache[promo.Name] = Database.GetPromocodeActivationCount(promo.Name);
            }
        }

        public Promocode GetPromocode(string name)
        {
            if (_promocodeCache == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            _promocodeCache.TryGetValue(name, out Promocode promo);
            return promo;
        }

        public int GetPromocodeActivationCount(string name)
        {
            if (_activationCountCache == null || string.IsNullOrEmpty(name))
            {
                return 0;
            }

            _activationCountCache.TryGetValue(name, out int count);
            return count;
        }

        public bool HasPlayerActivatedPromo(ulong steamId, string promoName)
        {
            return Database?.HasPlayerActivatedPromo(steamId, promoName) ?? false;
        }

        public List<TemporaryActivation> GetPlayerActiveTemporaryItems(ulong steamId)
        {
            return TemporaryItems?.GetActiveTemporaryActivations(steamId) ?? new List<TemporaryActivation>();
        }

        public Promocode CreateTemporaryPromocode(string name, int maxActivations, DateTime expirationDate,
            TimeSpan temporaryDuration, List<string> giveCommands, List<string> removeCommands,
            List<string> permissions = null)
        {
            if (string.IsNullOrEmpty(name) || GetPromocode(name) != null || temporaryDuration <= TimeSpan.Zero)
            {
                return null;
            }

            Promocode promo = new Promocode
            {
                Name = name,
                MaxActivations = maxActivations,
                ExpirationDate = expirationDate,
                Commands = giveCommands ?? new List<string>(),
                RemoveCommands = removeCommands ?? new List<string>(),
                Permissions = permissions ?? new List<string>(),
                IsTemporary = true,
                TemporaryDuration = temporaryDuration
            };

            Configuration.Instance.Promocodes.Add(promo);
            Configuration.Save();
            RefreshPromocodeCache();

            return promo;
        }
    }
}