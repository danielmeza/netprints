# 0042: Run in external terminal

## Status

Accepted, 2026-10-19 (P3 sub-phase E, T123; `specs/006-editor-extension-host/` research R23, FR-056, FR-064). Decided
by the coordinating session under the owner's delegation, after a research report and its adversarial audit
(`.agent-archive/2026-10-19-p3-terminal-default/`). It replaces R23's draft detection order.

## Context

- "Run in external terminal" (FR-064) starts a run profile's program in a terminal window of the OS. The terminal
  command is configurable on Linux (`run.linuxTerminal`). Arguments must reach the program exactly (FR-056), so no
  shell may re-parse them.
- On Linux and macOS NetPrints writes a POSIX `sh` script (single-quoted arguments, `cd`, `env -u … NAME=value`, a
  final "Press Enter to close" read) and hands it to a terminal.
- Desktops no longer agree on one terminal:
  - Fedora 41+ Workstation ships only Ptyxis ([fedora-workstation#417](https://pagure.io/fedora-workstation/issue/417),
    [comps-f44](https://forge.fedoraproject.org/releng/fedora-comps/raw/branch/main/comps-f44.xml.in)).
  - Ubuntu 24.04 ships GNOME Terminal; 25.10 and 26.04 ship Ptyxis with `xdg-terminal-exec`
    ([seeds](https://git.launchpad.net/~ubuntu-core-dev/ubuntu-seeds/+git/ubuntu/plain/desktop-minimal?h=resolute)).
  - Debian 13's GNOME pulls in GNOME Terminal ([gnome-core](https://packages.debian.org/trixie/gnome-core)).
  - GNOME OS and Arch GNOME ship Console, `kgx` ([Arch gnome group](https://archlinux.org/groups/x86_64/gnome/)).
  - KDE ships Konsole, and Xfce ships xfce4-terminal.
  - The R23 draft (`x-terminal-emulator`, `gnome-terminal`, `konsole`, `xfce4-terminal`, `xterm`) finds nothing on
    Fedora Workstation or Arch GNOME.
- Ubuntu 25.04+ sets the user's default terminal through `xdg-terminal-exec`
  ([Ubuntu docs](https://ubuntu.com/desktop/docs/en/latest/how-to/change-the-default-terminal/)). That tool implements
  a proposed specification that passes the command and its arguments as given and execs the terminal without forking
  ([README](https://github.com/Vladimir-csp/xdg-terminal-exec)).
- Launchers take a single argument after their exec flag differently from two or more:
  - Debian's `xfce4-terminal.wrapper` turns one `-e` argument into a re-parsed `xfce4-terminal -e <string>`, and two or
    more into `-x` ([wrapper](https://sources.debian.org/data/main/x/xfce4-terminal/1.2.0-1/debian/xfce4-terminal.wrapper)).
  - GNOME Terminal's wrapper and Console exec a single executable path directly
    ([gnome-terminal.wrapper](https://git.launchpad.net/ubuntu/+source/gnome-terminal/plain/debian/gnome-terminal.wrapper?h=ubuntu/noble),
    [kgx-utils.c](https://gitlab.gnome.org/GNOME/console/-/raw/main/src/kgx-utils.c)).
- The terminal process's lifetime is not the program's:
  - `gnome-terminal` returns at once.
  - Debian's `x-terminal-emulator` wrapper adds `--wait`.
  - `ptyxis --` runs standalone ([main.c](https://gitlab.gnome.org/chergert/ptyxis/-/raw/main/src/main.c)).
  - Konsole opens its own process by default ([konsole.kcfg](https://raw.githubusercontent.com/KDE/konsole/master/src/settings/konsole.kcfg)).
  - Console depends on whether an instance is already running.
  - `open` on macOS returns at once.
  - None of them reports the program's exit code.
- On Windows a console program started by the editor (a GUI process) with `UseShellExecute = false` and
  `CreateNoWindow = false` gets a new console. That console is hosted by the default terminal application: Windows
  Terminal on Windows 11 22H2+ ([announcement](https://devblogs.microsoft.com/commandline/windows-terminal-is-now-the-default-in-windows-11/)),
  else conhost. Both close it when the program exits: Windows Terminal's default `closeOnExit` behaves as `always` for
  a handed-off process ([docs](https://learn.microsoft.com/windows/terminal/customize-settings/profile-advanced)).
  Keeping it open without re-parsing the arguments needs a console helper. Visual Studio uses one and keeps its console
  open by default ([dotnet/project-system#4388](https://github.com/dotnet/project-system/pull/4388)). NetPrints ships
  no console executable with the editor: `netprints` is a separate .NET tool, and CI runs no editor test on Windows.

## Decision

1. **Linux command.** When `run.linuxTerminal` is set, it is split with the CLI's splitter (ADR-0037). Otherwise
   NetPrints uses the first of these found on `PATH`: `xdg-terminal-exec --`, `x-terminal-emulator -e`, `ptyxis --`,
   `gnome-terminal --`, `kgx --`, `konsole -e`, `xfce4-terminal -x`, `xterm -e`. NetPrints appends `/bin/sh` and the
   script path as two arguments, so every launcher takes its rest-of-argv path. The setting must therefore end with
   such an option (`ptyxis --`, `konsole -e`, `alacritty -e`, …). The script is
   `<state>/terminal/<start id>-<8 hex>.sh`, for example `<state>/terminal/3-0f1e2d3c.sh`, mode 0700.
2. **macOS command.** `open -a Terminal <script>`. The script is an executable `.command` file in the per-user temp
   folder (no space in its path).
3. **Windows command.** The program itself: `ArgumentList`, its working directory and environment, no redirection,
   `CreateNoWindow = false`.
4. **Hand-off on Linux and macOS.** NetPrints does not follow the started process. Output shows "Started in an external
   terminal.", and the run ends for NetPrints at once: Run is enabled again, and Stop, the unload prompt and the exit
   prompt do not apply. The script waits for Enter after printing the program's exit code, so the window stays open
   whatever the terminal's own settings.
5. **Windows follows the program.** The started process is the program, so Stop, the unload prompt and its exit code
   apply as for any run. Its console window closes when the program exits. This difference from Linux and macOS is
   deliberate.
6. **Messages.** No terminal found: "No terminal was found on PATH (looked for …); set run.linuxTerminal in Settings ›
   Run." A configured terminal that cannot start: "The terminal command '<program>' could not be started: <reason>.
   Check run.linuxTerminal in Settings › Run."

## Alternatives rejected

- **The R23 order** (`x-terminal-emulator` first, no Ptyxis or Console). It finds no terminal on Fedora Workstation 41+
  or Arch GNOME.
- **Choosing the terminal from `XDG_CURRENT_DESKTOP` or `$TERMINAL`.** `xdg-terminal-exec` and `x-terminal-emulator`
  already carry the user's and the system's choice where they exist. `$TERMINAL` has no specification and does not say
  which flag runs a command.
- **Following the terminal process on Linux and macOS.** Its lifetime differs per emulator, its exit code is not the
  program's, and Stop would kill a terminal window rather than the program.
- **Appending only the script path.** Debian's Xfce wrapper re-parses a single `-e` argument.
- **`cmd /k`, a `.cmd` wrapper, PowerShell `-NoExit`.** Each re-parses the arguments or adds a second parser
  (FR-056).
- **Launching `wt.exe` directly.** It treats `;` as a sub-command delimiter and is missing on Windows 10 without the
  Store app.
- **Windows Terminal `closeOnExit: "never"`.** A per-user global setting that NetPrints cannot set per run, and only
  for Windows Terminal.

## Consequences

- Linux and macOS external runs are never stopped by NetPrints, including on project unload and application exit.
  Each Run opens a new window.
- On Windows, a console program's last output disappears with its window. The exit code stays in the status bar, and
  running the same profile with capture on (and the Output input line) shows the output.
- On macOS the program starts from the environment of Terminal's login shell plus the profile's changes, not from
  NetPrints' environment. On Linux the environment depends on the terminal (a server-based terminal may use its own).
- Users of other Linux terminals (alacritty, kitty, foot, WezTerm) set `run.linuxTerminal`. A terminal command that
  starts and fails at once is still reported as started.
- Unit tests pin the detection order, the command lines, the messages and the hand-off with a fake `PATH` probe. No CI
  job starts a real terminal, so the Windows and macOS windows and a Linux spot check stay on the owner's manual list.
- Later, outside P3 sub-phase E (recorded in the roadmap, T-28):
  - a console helper inside the editor archive with `run.keepTerminalOpen` (default on), once a Windows editor CI leg
    exists, which would keep Windows windows open too;
  - `run.macTerminal` for other macOS terminals;
  - more Linux candidates.
