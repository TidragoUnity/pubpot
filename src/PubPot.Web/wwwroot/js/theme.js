// Hell-/Dunkelmodus. Ohne Auswahl gilt die Systemeinstellung (prefers-color-scheme).
// Die Auswahl liegt nur im Browser (localStorage) und wird nie an den Server geschickt.
// Ohne JavaScript bleibt der Schalter versteckt, und die Seite folgt der Systemeinstellung.
(function () {
  'use strict';
  var key = 'pubpot-theme';
  var root = document.documentElement;

  function read() {
    try { return localStorage.getItem(key); } catch (e) { return null; }
  }

  function write(value) {
    try { localStorage.setItem(key, value); } catch (e) { /* z. B. privater Modus */ }
  }

  var stored = read();
  if (stored === 'light' || stored === 'dark') {
    root.setAttribute('data-theme', stored);
  }

  function current() {
    var explicit = root.getAttribute('data-theme');
    if (explicit) return explicit;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  document.addEventListener('DOMContentLoaded', function () {
    var button = document.getElementById('theme-toggle');
    if (!button) return;

    function sync() {
      button.setAttribute('aria-pressed', current() === 'dark' ? 'true' : 'false');
    }

    button.hidden = false;
    sync();
    // Wechselt das System während des Besuchs (z. B. abends automatisch), Schalterzustand nachziehen.
    var media = window.matchMedia('(prefers-color-scheme: dark)');
    if (media.addEventListener) media.addEventListener('change', sync);
    button.addEventListener('click', function () {
      var next = current() === 'dark' ? 'light' : 'dark';
      root.setAttribute('data-theme', next);
      write(next);
      sync();
    });
  });
})();
