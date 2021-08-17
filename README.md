# jjonesova.cz Admin GUI

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz/jjonesova)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. Clear `src/JonesovaGui/bin/publish/` folder.
2. Bump version.
3. In Visual Studio, click Publish and use already-created ClickOnce profile.
4. Commit updated ClickOnce profile (it is keeping track of total build count)
   except `<History>` tag.
5. Remove all files from `src/JonesovaGui/bin/publish/` except the following:

   ```txt
   Application Files/
   JonesovaGui.application
   ```

6. Upload the directory to branch `gh-pages`.
7. Publish new release on GitHub (look at the older ones for guidelines).
8. Point people to
   <https://jjonescz.github.io/jjonesova-gui/JonesovaGui.application>.
