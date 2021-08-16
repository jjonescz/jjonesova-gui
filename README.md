# jjonesova.cz Admin GUI

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. In Visual Studio, click Publish and use already-created ClickOnce profile.
2. Deploy contents of `src/JonesovaGui/bin/publish/` to some website, e.g.,
   `https://jjonesova-admin.netlify.app/`.
3. Look at <https://jjonesova-admin.netlify.app/publish.html>.
4. Commit updated ClickOnce profile (it is keeping track of app version).
