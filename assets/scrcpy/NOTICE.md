# scrcpy text service

This project embeds the unmodified scrcpy 4.0 server by Genymobile, licensed
under the Apache License, Version 2.0 (see LICENSE in this directory).

- Upstream: https://github.com/Genymobile/scrcpy
- Release/source: https://github.com/Genymobile/scrcpy/tree/v4.0
- File: scrcpy-server-v4.0
- SHA-256: 84924bd564a1eb6089c872c7521f968058977f91f5ff02514a8c74aff3210f3a

The C# client implements only the clipboard portion of the v4.0 control
protocol. The server is embedded as AdbTool.scrcpy-server-v4.0 at build time.
Do not replace this file without updating and testing the matching protocol,
version handshake, integrity hash, and license information.
