# jjonesova.cz Admin GUI

[![Netlify Status](https://api.netlify.com/api/v1/badges/e2783b12-7f6b-43b8-b6d5-40263e593941/deploy-status)](https://app.netlify.com/sites/jjonesova-admin/deploys)

This program is a simple admin desktop GUI for website
[jjonesova.cz](https://jjonesova.cz) ([hosted
separately](https://github.com/jjonescz/jjonesova)).

## Documentation

Tooltips in the UI itself document some functionality.

## Release process

1. Clear `src/JonesovaGui/bin/publish/` folder.
2. In Visual Studio, click Publish and use already-created ClickOnce profile.
3. Deploy contents of `src/JonesovaGui/bin/publish/` to
   `https://jjonesova-admin.netlify.app/`.
4. Commit updated ClickOnce profile (it is keeping track of app version).
5. Look at <https://jjonesova-admin.netlify.app/publish.html>.
