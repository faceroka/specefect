# ⚡ Specefect v1

> Консольный монитор сетевого трафика в реальном времени с автоматическим определением приложений через `ss` и парсингом пакетов на C# (.NET / SharpPcap).

---

## 📋 Требования перед установкой

Для захвата сетевых пакетов программе необходимы драйверы захвата трафика (**Npcap** под Windows или **libpcap** под Linux) и установленный **.NET 8.0 SDK / Runtime**.

---

## 🐧 Установка и запуск на Linux

### 1. Установка зависимостей

Установите библиотеку `libpcap`, утилиту сокетов `iproute2` (для команды `ss`) и .NET SDK.

* **Ubuntu / Debian / Kali Linux:**
  ```bash
  sudo apt update
  sudo apt install -y libpcap-dev iproute2 dotnet-sdk-8.0 git

    Fedora:
    Bash

sudo dnf install -y libpcap-devel iproute dotnet-sdk-8.0 git

Arch Linux:
Bash

    sudo pacman -Syu --needed libpcap iproute2 dotnet-sdk git

2. Клонирование и сборка
Bash

git clone [https://github.com/ваш-юзернейм/Specefect.git](https://github.com/ваш-юзернейм/Specefect.git)
cd Specefect
dotnet build -c Release

3. Запуск

    ⚠️ Важно: Захват сетевого интерфейса в режиме Promiscuous и получение списка открытых процессов через ss -tunp требуют привилегий суперпользователя.

Bash

sudo dotnet run -c Release

Либо запуск готового бинарника после сборки:
Bash

sudo ./bin/Release/net8.0/LinuxTrafficMonitor

🪟 Установка и запуск на Windows
1. Установка драйвера Npcap (Обязательно)

    Скачайте инсталлятор Npcap с официального сайта: npcap.com/#download

    При установке обязательно поставьте галочку:

        ☑ Install Npcap in WinPcap API-compatible Mode (Установка в режиме совместимости с WinPcap).

2. Установка .NET SDK

Скачайте и установите .NET 8.0 SDK с официального сайта Microsoft.
3. Клонирование и сборка

Откройте PowerShell или Терминал Windows от имени администратора:
PowerShell

git clone [https://github.com/ваш-юзернейм/Specefect.git](https://github.com/ваш-юзернейм/Specefect.git)
cd Specefect
dotnet build -c Release

4. Запуск

Запустите программу от имени Администратора (Run as Administrator):
PowerShell

dotnet run -c Release

    ℹ️ Примечание для Windows: Парсинг процессов в коде использует системную Linux-утилиту ss. Под Windows захват пакетов, парсинг IP, портов и DNS/mDNS/QUIC будет работать в штатном режиме, а имена локальных процессов будут помечаться как Unknown (если не запущены внутри WSL2).
