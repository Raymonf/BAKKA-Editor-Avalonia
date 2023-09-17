# BAKKA Editor Avalonia Port

A Marvelously simple and scuffed editor, but this time it's cross-platform (tested on Windows, Linux, and macOS)! Currently mostly up-to-date with upstream v2.1.8.

**You should use this only if you have no sense of sanity or hate yourself.** (In other words, you should use this if you want to use BAKKA but are a Linux or macOS user.) For the most part, this is a port of the Windows Forms code, with "MVVM" in place to connect the dots into the old Windows Forms parts. It's _horrible_. If you are a developer, consider this a warning.

This depends on ManagedBass. Since BASS doesn't have WASM support, the `Web` project is just the default Avalonia template. It might be as simple as adding another sound engine (`IBakkaSoundEngine`).

Neither the `iOS` nor `Android` projects function properly at this time. iOS support was worked on long ago, but is likely nonfunctional in major ways.

### Non-Upstream Features
* Hardware-accelerated rendering (note: CPU/single-threaded performance is generally the bottleneck now)
* Built-in FLAC support
* Badly-implemented and very, very approximate hitsounds (edit `settings.toml`)
* Dark mode
