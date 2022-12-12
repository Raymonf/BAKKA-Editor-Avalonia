# BAKKA Editor Avalonia Port

A Marvelously simple, scuffed, and terrible editor. But hey, it's cross-platform (tested on Windows x64 and macOS)!

**You should use this only if you have no sense of sanity or hate yourself.** For the most part, this is a close-to-direct port of the Windows Forms code, with "MVVM" in place to connect the dots into the old Windows Forms parts. It's _horrible_. You have been warned.

This depends on ManagedBass. Since there is no WASM support in BASS, the `Web` project is just the default Avalonia template. Android would work with some slight modifications, but since I don't have an Android tablet, it's also the default template.

iOS support is also insanely scuffed. I couldn't get BASS FX to do anything on iOS, so the speed slider doesn't do anything there. There are no touch gestures bound to the circle either. Oh, and saving probably doesn't work?

### Notes

* FluentAvalonia is used for the ContentDialog alone. The version in the source tree is a fork because there hasn't been [a release with this fix yet](https://github.com/amwx/FluentAvalonia/commit/71df3cb9373bfc5635db9e744e95f3527fccb75f), which is required for mobile support.
