# jjonesova.cz Admin GUI

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz/jjonesova)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. Run `.\release.ps1` from Developer PowerShell for Visual Studio.
2. Remove all files from `src/JonesovaGui/bin/publish/` except the following:

   ```txt
   Application Files/
   JonesovaGui.application
   ```

3. Upload the directory to branch `gh-pages`.
4. Publish new release on GitHub (look at the older ones for guidelines).
5. Point people to
   <https://jjonescz.github.io/jjonesova-gui/JonesovaGui.application>.
