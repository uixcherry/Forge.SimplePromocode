using Newtonsoft.Json;
using Rocket.API;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Forge.SimplePromocode
{
    public class Configuration : IRocketPluginConfiguration
    {
        public List<Promocode> Promocodes { get; set; }
        public int TemporaryItemsCheckInterval { get; set; }

        [XmlElement("PlaceholdersInfo")]
        public string PlaceholdersInfo { get; set; }

        public void LoadDefaults()
        {
            TemporaryItemsCheckInterval = 60;
            PlaceholdersInfo = "@p - имя персонажа игрока, @pid - SteamID игрока, @s - server (для команд от имени сервера)";

            Promocodes = new List<Promocode>
            {
                new Promocode
                {
                    Name = "welcome",
                    MaxActivations = 100,
                    Commands = new List<string> { "give @p 363 1" },
                    Permissions = new List<string> { "promocode.use" },
                    ExpirationDays = 30,
                    IsTemporary = false
                },
                new Promocode
                {
                    Name = "vip1day",
                    MaxActivations = 50,
                    Commands = new List<string> { "addrole @pid VIP" },
                    RemoveCommands = new List<string> { "removerole @pid VIP" },
                    Permissions = new List<string> { "promocode.vip" },
                    ExpirationDays = 30,
                    IsTemporary = true,
                    TemporaryHours = 24
                }
            };
        }
    }

    public class Promocode
    {
        public string Name { get; set; }
        public int MaxActivations { get; set; }
        public List<string> Commands { get; set; }
        public List<string> RemoveCommands { get; set; }
        public List<string> Permissions { get; set; }

        [XmlElement("ExpirationDays")]
        public int ExpirationDays { get; set; }

        [XmlIgnore]
        public DateTime ExpirationDate
        {
            get
            {
                return _expirationDate == DateTime.MinValue ?
                    DateTime.Now.AddDays(ExpirationDays) :
                    _expirationDate;
            }
            set
            {
                _expirationDate = value;
                if (value > DateTime.Now)
                {
                    ExpirationDays = (int)Math.Ceiling((value - DateTime.Now).TotalDays);
                }
            }
        }
        private DateTime _expirationDate = DateTime.MinValue;

        public bool IsTemporary { get; set; }

        [XmlElement("TemporaryHours")]
        public int TemporaryHours { get; set; }

        [XmlIgnore]
        public TimeSpan TemporaryDuration
        {
            get
            {
                return TimeSpan.FromHours(TemporaryHours);
            }
            set
            {
                TemporaryHours = (int)Math.Ceiling(value.TotalHours);
            }
        }

        [XmlIgnore]
        [JsonIgnore]
        public bool IsExpired => DateTime.Now > ExpirationDate;

        public Promocode()
        {
            Commands = new List<string>();
            RemoveCommands = new List<string>();
            Permissions = new List<string>();
            IsTemporary = false;
            TemporaryHours = 0;
            ExpirationDays = 30;
        }
    }
}