# jjonesova.cz Admin GUI

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz/jjonesova)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. Clear `src/JonesovaGui/bin/publish/` folder.
2. In Visual Studio, click Publish and use already-created ClickOnce profile.
3. Commit updated ClickOnce profile (it is keeping track of total build count)
   except `<History>` tag.
4. Remove all files from `src/JonesovaGui/bin/publish/` except the following:

   ```txt
   Application Files/
   JonesovaGui.application
   ```

5. Upload the directory to branch `gh-pages`.
6. Point people to
   <https://jjonescz.gihub.io/jjonesova-gui/JonesovaGui.application>.

## Installing

When upgrading, ensure you are installing from the same folder, otherwise
ClickOnce will complain.
