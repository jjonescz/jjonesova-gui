# jjonesova.cz Admin GUI

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz/jjonesova)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. Clear `src/JonesovaGui/bin/publish/` folder.
2. Bump version (otherwise updating existing installations will fail).
3. In Visual Studio, click Publish and use already-created ClickOnce profile.
4. Commit updated ClickOnce profile (it is keeping track of total build count)
   except `<History>` tag.
5. Zip contents of `src/JonesovaGui/bin/publish/` and also release it on GitHub.
   Only these files need to be included:

   ```txt
   Application Files/
   JonesovaGui.application
   setup.exe
   ```
