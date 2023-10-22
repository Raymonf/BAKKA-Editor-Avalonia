# BAKKA Editor Avalonia ~~Port~~ Fork

#### [**Download Latest Nightly Build**](https://nightly.link/Raymonf/BAKKA-Editor-Avalonia/workflows/build/main?preview) <-- click this link if you're a normal person that just wants to run this thing

A Marvelously simple and scuffed editor, but this time it's cross-platform (tested on Windows, Linux, and macOS)! Currently mostly up-to-date with upstream v2.1.8.

~~For the most part, this is a port of the Windows Forms code, with "MVVM" in place to connect the dots into the old Windows Forms parts. It's _horrible_.~~ There is some work to slowly start moving things to MVVM. If you are a developer, consider this caution tape in text form. Contributions/PRs are welcome and they will be reviewed (and merged) ASAP.

### Non-Upstream Features
* Hardware-accelerated rendering and uncapped FPS (with some optimizations for performance)
* Built-in FLAC support
* Badly-implemented and very, very approximate hitsounds (edit `settings.toml`)
* Dark mode, thanks to FluentAvalonia
* You can actually type in the number boxes (is this a feature?)
* Checks and prompts for a "End of Chart" note before saving

### Cross Platform Code Warnings

This depends on ManagedBass. Since BASS doesn't have WASM support, the `Web` project is just the default Avalonia template. It might be as simple as adding another sound engine (`IBakkaSoundEngine`).

Neither the `iOS` nor `Android` projects function properly at this time. iOS support was worked on long ago, but is mostly nonfunctional today.

### Credits / Attribution
* Goatgarien - Wrote the original editor and gave lots of support
* Original editor contributors
* kevqiu - ported code from upstream PR #17 (Notes on Beat list view)
* yellowberryHN - ported code from upstream PR #18 (Place Note on Drag)
* All the people who reported bugs and feedback