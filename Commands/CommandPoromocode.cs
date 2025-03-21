using Forge.SimplePromocode.Database;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Forge.SimplePromocode.Commands
{
    public class CommandPromocode : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "promocode";
        public string Help => "Система промокодов";
        public string Syntax => "/promocode <код> | /promocode <list|info|temp|stats|reload>";
        public List<string> Aliases => new List<string> { "promo", "код" };
        public List<string> Permissions => new List<string> { "promocode.use" };

        private readonly Dictionary<string, string> _subCommandPermissions = new Dictionary<string, string>
        {
            { "list", "promocode.list" },
            { "info", "promocode.info" },
            { "temp", "promocode.temp" },
            { "stats", "promocode.stats" },
            { "reload", "promocode.reload" }
        };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
            {
                return;
            }

            if (command.Length == 0)
            {
                ShowHelp(player);
                return;
            }

            string subCommand = command[0].ToLowerInvariant();

            if (_subCommandPermissions.TryGetValue(subCommand, out string permission))
            {
                if (!player.HasPermission(permission))
                {
                    UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                        "У вас нет прав для выполнения этой команды", UnityEngine.Color.red);
                    return;
                }

                switch (subCommand)
                {
                    case "list":
                        ListActivePromocodes(player);
                        break;

                    case "info":
                        if (command.Length > 1)
                        {
                            ShowPromocodeInfo(player, command[1]);
                        }
                        else
                        {
                            UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                                "Использование: /promocode info <код>", UnityEngine.Color.yellow);
                        }
                        break;

                    case "temp":
                        if (command.Length > 1 && player.HasPermission("promocode.tempothers"))
                        {
                            ShowActiveTemporaryItems(player, command[1]);
                        }
                        else
                        {
                            ShowPlayerTemporaryItems(player);
                        }
                        break;

                    case "stats":
                        ShowStats(player, command.Length > 1 ? command[1] : null);
                        break;

                    case "reload":
                        ReloadConfig(player);
                        break;
                }
            }
            else
            {
                ActivatePromocode(player, command[0]);
            }
        }

        private void ShowHelp(UnturnedPlayer player)
        {
            StringBuilder help = new StringBuilder();
            help.AppendLine(Plugin.Instance.Translations.Instance.Translate("prefix") + "Доступные команды:");

            help.AppendLine("/promocode <код> - Активировать промокод");

            foreach (var subCmd in _subCommandPermissions)
            {
                if (player.HasPermission(subCmd.Value))
                {
                    switch (subCmd.Key)
                    {
                        case "list":
                            help.AppendLine("/promocode list - Показать доступные промокоды");
                            break;
                        case "info":
                            help.AppendLine("/promocode info <код> - Информация о промокоде");
                            break;
                        case "temp":
                            if (player.HasPermission("promocode.tempothers"))
                                help.AppendLine("/promocode temp [игрок] - Показать временные товары");
                            else
                                help.AppendLine("/promocode temp - Показать ваши временные товары");
                            break;
                        case "stats":
                            help.AppendLine("/promocode stats [код] - Показать статистику активаций");
                            break;
                        case "reload":
                            help.AppendLine("/promocode reload - Перезагрузить конфигурацию");
                            break;
                    }
                }
            }

            UnturnedChat.Say(player, help.ToString(), UnityEngine.Color.yellow);
        }

        private void ListActivePromocodes(UnturnedPlayer player)
        {
            List<Promocode> promocodes = Plugin.Instance.Configuration.Instance.Promocodes
                .Where(p => !p.IsExpired)
                .Where(p => Plugin.Instance.GetPromocodeActivationCount(p.Name) < p.MaxActivations)
                .Where(p => !Plugin.Instance.HasPlayerActivatedPromo(player.CSteamID.m_SteamID, p.Name))
                .Where(p => p.Permissions.Count == 0 || p.Permissions.Any(perm => player.HasPermission(perm)))
                .ToList();

            if (promocodes.Count == 0)
            {
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                    "Нет доступных промокодов для активации", UnityEngine.Color.yellow);
                return;
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine(Plugin.Instance.Translations.Instance.Translate("prefix") + "Доступные промокоды:");

            foreach (Promocode promo in promocodes)
            {
                string type = promo.IsTemporary ? "(Временный)" : "";
                message.AppendLine($"- {promo.Name} {type} (Истекает: {promo.ExpirationDate:dd.MM.yyyy})");
            }

            UnturnedChat.Say(player, message.ToString(), UnityEngine.Color.cyan);
        }

        private void ShowPromocodeInfo(UnturnedPlayer player, string promoName)
        {
            Promocode promo = Plugin.Instance.GetPromocode(promoName);

            if (promo == null)
            {
                string message = Plugin.Instance.Translations.Instance.Translate("promocode_not_found", promoName);
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                return;
            }

            int currentActivations = Plugin.Instance.GetPromocodeActivationCount(promo.Name);
            StringBuilder info = new StringBuilder();
            info.AppendLine(Plugin.Instance.Translations.Instance.Translate("prefix") + $"Информация о промокоде '{promo.Name}':");
            info.AppendLine($"- Статус: {(promo.IsExpired ? "Истек" : "Активен")}");
            info.AppendLine($"- Тип: {(promo.IsTemporary ? "Временный" : "Постоянный")}");
            info.AppendLine($"- Активации: {currentActivations}/{promo.MaxActivations}");
            info.AppendLine($"- Срок действия промокода: {promo.ExpirationDays} дней (до {promo.ExpirationDate:dd.MM.yyyy HH:mm})");

            if (promo.IsTemporary)
            {
                info.AppendLine($"- Длительность временного товара: {promo.TemporaryHours} ч");
            }

            if (promo.Permissions.Count > 0)
            {
                info.AppendLine($"- Требуемые права: {string.Join(", ", promo.Permissions)}");
            }

            UnturnedChat.Say(player, info.ToString(), UnityEngine.Color.cyan);
        }

        private void ReloadConfig(UnturnedPlayer player)
        {
            Plugin.Instance.Configuration.Load();
            Plugin.Instance.RefreshPromocodeCache();
            Plugin.Instance.Database.Reload();
            Plugin.Instance.TemporaryItems.Reload();

            UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                "Конфигурация и база данных успешно перезагружены!", UnityEngine.Color.green);
        }

        private void ShowStats(UnturnedPlayer player, string promoName = null)
        {
            StringBuilder stats = new StringBuilder();
            stats.AppendLine(Plugin.Instance.Translations.Instance.Translate("prefix") + "Статистика промокодов:");

            if (string.IsNullOrEmpty(promoName))
            {
                List<PromoActivation> allActivations = Plugin.Instance.Database.GetAllActivations();
                int totalActivations = allActivations.Count;
                int uniquePlayers = allActivations.Select(a => a.SteamId).Distinct().Count();
                int expiredPromos = Plugin.Instance.Configuration.Instance.Promocodes.Count(p => p.IsExpired);
                int activePromos = Plugin.Instance.Configuration.Instance.Promocodes.Count(p => !p.IsExpired);
                int temporaryPromos = Plugin.Instance.Configuration.Instance.Promocodes.Count(p => p.IsTemporary);

                stats.AppendLine($"- Всего активаций: {totalActivations}");
                stats.AppendLine($"- Уникальных игроков: {uniquePlayers}");
                stats.AppendLine($"- Активных промокодов: {activePromos}");
                stats.AppendLine($"- Истекших промокодов: {expiredPromos}");
                stats.AppendLine($"- Временных промокодов: {temporaryPromos}");

                List<TemporaryActivation> tempActivations = Plugin.Instance.TemporaryItems.GetAllTemporaryActivations();
                int activeTemp = tempActivations.Count(a => !a.IsRevoked && a.ExpiryDate > DateTime.Now);
                int expiredTemp = tempActivations.Count(a => a.IsRevoked || a.ExpiryDate <= DateTime.Now);

                stats.AppendLine($"- Активных временных товаров: {activeTemp}");
                stats.AppendLine($"- Истекших временных товаров: {expiredTemp}");

                var topPromos = allActivations
                    .GroupBy(a => a.PromoName)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToList();

                if (topPromos.Count > 0)
                {
                    stats.AppendLine("\nСамые используемые промокоды:");
                    foreach (var group in topPromos)
                    {
                        stats.AppendLine($"- {group.Key}: {group.Count()} активаций");
                    }
                }
            }
            else
            {
                Promocode promo = Plugin.Instance.GetPromocode(promoName);
                if (promo == null)
                {
                    string message = Plugin.Instance.Translations.Instance.Translate("promocode_not_found", promoName);
                    UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                    return;
                }

                List<PromoActivation> activations = Plugin.Instance.Database.GetActivationsForPromocode(promoName);
                int uniquePlayers = activations.Select(a => a.SteamId).Distinct().Count();
                List<PromoActivation> recentActivations = activations.OrderByDescending(a => a.ActivationDate).Take(5).ToList();

                stats.AppendLine($"Статистика промокода '{promoName}':");
                stats.AppendLine($"- Всего активаций: {activations.Count}/{promo.MaxActivations}");
                stats.AppendLine($"- Уникальных игроков: {uniquePlayers}");
                stats.AppendLine($"- Тип: {(promo.IsTemporary ? "Временный" : "Постоянный")}");
                stats.AppendLine($"- Статус: {(promo.IsExpired ? "Истек" : "Активен")}");
                stats.AppendLine($"- Истекает: {promo.ExpirationDate:dd.MM.yyyy HH:mm}");

                if (promo.IsTemporary)
                {
                    List<TemporaryActivation> tempActivations = Plugin.Instance.TemporaryItems.GetAllTemporaryActivations()
                        .Where(a => a.PromoName.Equals(promoName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    int activeTemp = tempActivations.Count(a => !a.IsRevoked && a.ExpiryDate > DateTime.Now);
                    int expiredTemp = tempActivations.Count(a => a.IsRevoked || a.ExpiryDate <= DateTime.Now);

                    stats.AppendLine($"- Активных временных товаров: {activeTemp}");
                    stats.AppendLine($"- Истекших временных товаров: {expiredTemp}");
                    stats.AppendLine($"- Продолжительность действия: {promo.TemporaryHours} ч");
                }

                if (recentActivations.Count > 0)
                {
                    stats.AppendLine("\nПоследние активации:");
                    foreach (PromoActivation activation in recentActivations)
                    {
                        UnturnedPlayer p = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(activation.SteamId));
                        string playerName = p != null ? p.CharacterName : activation.SteamId.ToString();
                        stats.AppendLine($"- {playerName}: {activation.ActivationDate:dd.MM.yyyy HH:mm}");
                    }
                }
            }

            UnturnedChat.Say(player, stats.ToString(), UnityEngine.Color.cyan);
        }

        private void ShowPlayerTemporaryItems(UnturnedPlayer player)
        {
            List<TemporaryActivation> activeItems = Plugin.Instance.GetPlayerActiveTemporaryItems(player.CSteamID.m_SteamID);

            if (activeItems.Count == 0)
            {
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                    Plugin.Instance.Translations.Instance.Translate("no_temporary_items"), UnityEngine.Color.yellow);
                return;
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine(Plugin.Instance.Translations.Instance.Translate("prefix") +
                Plugin.Instance.Translations.Instance.Translate("temp_items_header"));

            foreach (TemporaryActivation item in activeItems)
            {
                Promocode promo = Plugin.Instance.GetPromocode(item.PromoName);
                string promoName = promo != null ? promo.Name : item.PromoName;
                TimeSpan remaining = item.ExpiryDate - DateTime.Now;

                message.AppendLine(Plugin.Instance.Translations.Instance.Translate("temp_items_list_item",
                    promoName,
                    item.ExpiryDate.ToString("dd.MM HH:mm"),
                    (int)remaining.TotalHours,
                    remaining.Minutes));
            }

            UnturnedChat.Say(player, message.ToString(), UnityEngine.Color.cyan);
        }

        private void ShowActiveTemporaryItems(UnturnedPlayer player, string steamIdOrName)
        {
            ulong targetSteamId = 0;
            string targetName = "всех игроков";

            if (!string.IsNullOrEmpty(steamIdOrName))
            {
                UnturnedPlayer target = UnturnedPlayer.FromName(steamIdOrName);
                if (target != null)
                {
                    targetSteamId = target.CSteamID.m_SteamID;
                    targetName = target.CharacterName;
                }
                else if (ulong.TryParse(steamIdOrName, out ulong sid))
                {
                    targetSteamId = sid;
                    targetName = sid.ToString();
                }
                else
                {
                    UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                        "Игрок не найден", UnityEngine.Color.red);
                    return;
                }
            }

            List<TemporaryActivation> activeItems;
            if (targetSteamId > 0)
            {
                activeItems = Plugin.Instance.GetPlayerActiveTemporaryItems(targetSteamId);
            }
            else
            {
                List<TemporaryActivation> allItems = Plugin.Instance.TemporaryItems.GetAllTemporaryActivations();
                activeItems = allItems.Where(a => !a.IsRevoked && a.ExpiryDate > DateTime.Now).ToList();
            }

            if (activeItems.Count == 0)
            {
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") +
                    $"У {targetName} нет активных временных товаров", UnityEngine.Color.yellow);
                return;
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine(Plugin.Instance.Translations.Instance.Translate("prefix") + $"Активные временные товары для {targetName}:");

            var groupedItems = activeItems.GroupBy(a => a.PromoName);

            foreach (var group in groupedItems)
            {
                Promocode promo = Plugin.Instance.GetPromocode(group.Key);
                string promoName = promo != null ? promo.Name : group.Key;

                message.AppendLine($"- {promoName}: {group.Count()} активаций");

                foreach (var item in group.Take(5))
                {
                    UnturnedPlayer itemPlayer = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(item.SteamId));
                    string playerName = itemPlayer != null ? itemPlayer.CharacterName : item.SteamId.ToString();
                    TimeSpan remaining = item.ExpiryDate - DateTime.Now;

                    message.AppendLine($"  * {playerName}: истекает {item.ExpiryDate:dd.MM HH:mm} " +
                        $"(осталось {(int)remaining.TotalHours} ч {remaining.Minutes} мин)");
                }

                if (group.Count() > 5)
                {
                    message.AppendLine($"  * ... и еще {group.Count() - 5} активаций");
                }
            }

            UnturnedChat.Say(player, message.ToString(), UnityEngine.Color.cyan);
        }

        private void ActivatePromocode(UnturnedPlayer player, string promoName)
        {
            Promocode promo = Plugin.Instance.GetPromocode(promoName);

            if (promo == null)
            {
                string message = Plugin.Instance.Translations.Instance.Translate("promocode_not_found", promoName);
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                return;
            }

            if (promo.IsExpired)
            {
                string message = Plugin.Instance.Translations.Instance.Translate(
                    "promocode_expired",
                    promoName,
                    promo.ExpirationDate.ToString("dd.MM.yyyy HH:mm"));
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                return;
            }

            if (promo.Permissions != null && promo.Permissions.Count > 0)
            {
                bool hasPermission = false;
                foreach (string permission in promo.Permissions)
                {
                    if (player.HasPermission(permission))
                    {
                        hasPermission = true;
                        break;
                    }
                }

                if (!hasPermission)
                {
                    string message = Plugin.Instance.Translations.Instance.Translate("promocode_no_permission");
                    UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                    return;
                }
            }

            if (Plugin.Instance.HasPlayerActivatedPromo(player.CSteamID.m_SteamID, promoName))
            {
                string message = Plugin.Instance.Translations.Instance.Translate("promocode_already_activated", promoName);
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                return;
            }

            int currentActivations = Plugin.Instance.GetPromocodeActivationCount(promoName);
            if (currentActivations >= promo.MaxActivations)
            {
                string message = Plugin.Instance.Translations.Instance.Translate("promocode_max_activations", promoName);
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                return;
            }

            try
            {
                bool success = Plugin.Instance.ActivatePromocode(player.CSteamID.m_SteamID, promoName);
                if (success)
                {
                    Plugin.Instance.ExecutePromocodeCommands(player, promo);

                    if (!promo.IsTemporary)
                    {
                        string message = Plugin.Instance.Translations.Instance.Translate("promocode_activated", promoName);
                        UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.green);
                    }

                    Logger.Log($"Игрок {player.CharacterName} ({player.CSteamID}) активировал промокод '{promoName}'");
                }
                else
                {
                    string message = Plugin.Instance.Translations.Instance.Translate("command_error", "Не удалось активировать промокод");
                    UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                }
            }
            catch (Exception ex)
            {
                string message = Plugin.Instance.Translations.Instance.Translate("command_error", ex.Message);
                UnturnedChat.Say(player, Plugin.Instance.Translations.Instance.Translate("prefix") + message, UnityEngine.Color.red);
                Logger.LogException(ex);
            }
        }
    }
}