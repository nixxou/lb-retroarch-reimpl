# Vendored LaunchBox plugin SDK

`Unbroken.LaunchBox.Plugins.dll` - assembly version **14.0.1.2** (LaunchBox 14.0.1), 122,168 bytes,
MD5 `bf7602fbbe2830582e0f82195be8019b`. Copied from a LaunchBox 14.0.1 install (`Core\`).

It is committed so the project compiles on a machine with no LaunchBox on it. The other LaunchBox
assemblies the plugin calls (`Unbroken`, `Unbroken.LaunchBox`, `Unbroken.LaunchBox.LocalDb`,
`Unbroken.LaunchBox.Windows`, `Newtonsoft.Json`) are *not* vendored: they are referenced from a
LaunchBox install at build time (see the README).

**Never ship this file.** The project references it with `<Private>false</Private>`: the host
provides the SDK at runtime, and a second copy loaded from the plugin's own folder would give
the plugin *different* `EmulatorPlugin` / `IEmulator` types than the host's, so the plugin
would never register.
