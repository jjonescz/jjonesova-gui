# jjonesova.cz Admin GUI

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz/jjonesova)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. Clear `src/JonesovaGui/bin/publish/` folder.
2. In Visual Studio, click Publish and use already-created ClickOnce profile.
3. Deploy contents of `src/JonesovaGui/bin/publish/` to branch `gh-pages` and
   push it.
4. Commit updated ClickOnce profile (it is keeping track of app version).
5. Look at <https://jjonescz.github.io/jjonesova-gui/Publish.html>.
