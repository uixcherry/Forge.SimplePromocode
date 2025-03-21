# Forge.SimplePromocode

## Описание
**Forge.SimplePromocode** - это мощный плагин для серверов Unturned с RocketMod, который позволяет владельцам серверов создавать и управлять системой промокодов. Поддерживаются как обычные, так и временные промокоды с автоматическим отзывом предметов или привилегий по истечении срока.

## Особенности
- 🎁 **Простая система промокодов** - игроки могут активировать промокоды через чат
- ⏱️ **Временные промокоды** - выдача временных привилегий или предметов с автоматическим отзывом
- 🔒 **Гибкая система прав** - настройка доступа к промокодам для разных групп игроков
- 📊 **Статистика использования** - отслеживание активаций каждого промокода
- 📅 **Управление сроками** - настройка срока действия для каждого промокода
- 🧩 **Плейсхолдеры** - использование @p, @pid и @s в командах для гибкости

## Установка
1. Скачайте последнюю версию плагина из [релизов](https://github.com/YourUsername/Forge.SimplePromocode/releases)
2. Поместите файл **Forge.SimplePromocode.dll** в папку `Rocket/Plugins` вашего сервера
3. Перезапустите сервер или используйте команду `/rocket reload`
4. Настройте файл конфигурации в `Rocket/Plugins/Forge.SimplePromocode`

## Конфигурация
После первого запуска плагина создается файл конфигурации `Forge.SimplePromocode.configuration.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Promocodes>
    <Promocode>
      <Name>welcome</Name>
      <MaxActivations>100</MaxActivations>
      <Commands>
        <string>give @p 363 1</string>
      </Commands>
      <RemoveCommands />
      <Permissions>
        <string>promocode.use</string>
      </Permissions>
      <ExpirationDays>30</ExpirationDays>
      <IsTemporary>false</IsTemporary>
      <TemporaryHours>0</TemporaryHours>
    </Promocode>
    <Promocode>
      <Name>vip1day</Name>
      <MaxActivations>50</MaxActivations>
      <Commands>
        <string>addrole @pid VIP</string>
      </Commands>
      <RemoveCommands>
        <string>removerole @pid VIP</string>
      </RemoveCommands>
      <Permissions>
        <string>promocode.vip</string>
      </Permissions>
      <ExpirationDays>30</ExpirationDays>
      <IsTemporary>true</IsTemporary>
      <TemporaryHours>24</TemporaryHours>
    </Promocode>
  </Promocodes>
  <TemporaryItemsCheckInterval>60</TemporaryItemsCheckInterval>
  <PlaceholdersInfo>@p - имя персонажа игрока, @pid - SteamID игрока, @s - server (для команд от имени сервера)</PlaceholdersInfo>
</Configuration>
```

### Параметры конфигурации

#### Глобальные настройки
- **TemporaryItemsCheckInterval** - интервал проверки истечения временных товаров в секундах
- **PlaceholdersInfo** - справочная информация о доступных плейсхолдерах

#### Настройки промокода
- **Name** - название промокода (то, что вводят игроки)
- **MaxActivations** - максимальное количество активаций промокода
- **Commands** - список команд, выполняемых при активации промокода
- **RemoveCommands** - команды, выполняемые при истечении срока временного промокода
- **Permissions** - список разрешений, необходимых для активации промокода
- **ExpirationDays** - срок действия промокода в днях
- **IsTemporary** - является ли промокод временным (`true` или `false`)
- **TemporaryHours** - продолжительность действия временного товара в часах

### Плейсхолдеры
В командах можно использовать следующие плейсхолдеры:
- **@p** - имя персонажа игрока (например, "Player123")
- **@pid** - SteamID игрока (например, "76561198012345678")
- **@s** - server (для выполнения команд от имени сервера)

## Команды и разрешения

### Основные команды
| Команда | Описание | Разрешение |
|---------|----------|------------|
| `/promocode <код>` | Активировать промокод | promocode.use |
| `/promocode list` | Показать доступные промокоды | promocode.list |
| `/promocode info <код>` | Показать информацию о промокоде | promocode.info |
| `/promocode temp` | Показать свои активные временные товары | promocode.temp |
| `/promocode temp <игрок>` | Показать временные товары игрока | promocode.tempothers |
| `/promocode stats [код]` | Показать статистику активаций | promocode.stats |
| `/promocode reload` | Перезагрузить конфигурацию | promocode.reload |

### Алиасы
- `/promo` - альтернатива для `/promocode`
- `/код` - русскоязычная альтернатива

## Примеры использования

### Создание базового промокода
```xml
<Promocode>
  <Name>start2024</Name>
  <MaxActivations>1000</MaxActivations>
  <Commands>
    <string>give @p 16 1</string>
    <string>give @p 13 2</string>
    <string>give @p 363 1</string>
  </Commands>
  <RemoveCommands />
  <Permissions />
  <ExpirationDays>60</ExpirationDays>
  <IsTemporary>false</IsTemporary>
  <TemporaryHours>0</TemporaryHours>
</Promocode>
```
Этот промокод даст игроку лук, две стрелы и бинт. Промокод может быть активирован 1000 раз и действует 60 дней.

### Создание временного VIP-промокода
```xml
<Promocode>
  <Name>vip3days</Name>
  <MaxActivations>50</MaxActivations>
  <Commands>
    <string>addrole @pid VIP</string>
    <string>tell @p Вы получили VIP на 3 дня!</string>
  </Commands>
  <RemoveCommands>
    <string>removerole @pid VIP</string>
    <string>tell @p Ваш VIP-статус закончился.</string>
  </RemoveCommands>
  <Permissions />
  <ExpirationDays>30</ExpirationDays>
  <IsTemporary>true</IsTemporary>
  <TemporaryHours>72</TemporaryHours>
</Promocode>
```
Этот промокод даёт игроку VIP-статус на 72 часа (3 дня). После истечения срока, VIP-статус автоматически отзывается.

### Создание промокода с ограничением доступа
```xml
<Promocode>
  <Name>premium2024</Name>
  <MaxActivations>100</MaxActivations>
  <Commands>
    <string>give @p 113 1</string>
    <string>give @p 253 3</string>
  </Commands>
  <RemoveCommands />
  <Permissions>
    <string>vip.use</string>
    <string>premium.use</string>
  </Permissions>
  <ExpirationDays>30</ExpirationDays>
  <IsTemporary>false</IsTemporary>
  <TemporaryHours>0</TemporaryHours>
</Promocode>
```
Этот промокод может быть активирован только игроками с разрешениями `vip.use` или `premium.use`.

## Часто задаваемые вопросы

### Как создать новый промокод?
Откройте файл конфигурации `Forge.SimplePromocode.configuration.xml` и добавьте новый элемент `<Promocode>` внутри секции `<Promocodes>`. После сохранения изменений, используйте команду `/promocode reload` для применения изменений.

### Как сделать промокод для определенной группы игроков?
Добавьте нужные разрешения в секцию `<Permissions>` промокода. Игрок должен иметь хотя бы одно из перечисленных разрешений, чтобы активировать промокод.

### Что делают временные промокоды?
Временные промокоды позволяют дать игрокам преимущества на определенный срок. Когда срок истекает, выполняются команды отзыва, указанные в `<RemoveCommands>`.

### Как работает система отзыва временных товаров?
Плагин периодически проверяет все активные временные товары. Когда срок действия товара истекает, выполняются команды отзыва для этого товара.

### Можно ли ограничить количество активаций промокода одним игроком?
Да, система автоматически отслеживает, кто активировал промокод. Каждый игрок может активировать один и тот же промокод только один раз.

### Что будет с промокодами после перезапуска сервера?
Все данные сохраняются в файлах конфигурации и базе данных. После перезапуска сервера плагин загрузит все промокоды и информацию об активациях.

## Контакты и поддержка
Если у вас возникли проблемы или есть предложения:
- Присоединяйтесь к нашему [Discord-серверу](https://discord.gg/HB9G962FRY)

Плагин разработан с ❤️ командой Forge
